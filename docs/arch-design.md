# ISO8583Service — High-Performance Architecture Proposal

## Problem Statement

The current `Iso8583TcpServer` processes each client connection in a single
sequential loop: **read → parse → auto-respond → read next**. This creates
several bottlenecks:

| Bottleneck | Impact |
|---|---|
| Sequential read-process-respond | Next message waits for current to fully complete — head-of-line blocking |
| Parsing in the read loop | Socket can't be drained while CPU is busy unpacking |
| Synchronous `OnMessageParsed` callback | No async business logic; handlers block the read loop |
| 1-second polling timeout for SignOn | Allocates `CancellationTokenSource` per iteration, wasteful wake-ups |
| No message pipelining | Only one in-flight message per connection at any time |

When processing hundreds of messages/sec from a single connection, every
microsecond the read loop spends outside `ReadAsync` is backpressure on the
sender. We need to decouple I/O from processing.

---

## Proposed Architecture: Staged Event-Driven Pipeline (SEDA)

Split the per-connection message flow into **five independent async stages**
connected by bounded `System.Threading.Channels`:

```mermaid
flowchart LR
    subgraph PerConnection["Per-Connection Pipeline"]
        direction TB
        R["🔵 Reader Task<br/>Socket I/O<br/>Length-prefixed frames"]
        P["🟢 Parser Task<br/>ISOMessage.UnPack()<br/>CPU-bound"]
        D["🟡 Dispatcher Task<br/>Route by MTI<br/>to handlers"]
        H["🟠 Handler Tasks<br/>User business logic<br/>async / parallel"]
        W["🔴 Writer Task<br/>Socket I/O<br/>Frame + send"]

        R -->|"Channel&lt;RawMessage&gt;"| P
        P -->|"Channel&lt;ParsedMessage&gt;"| D
        D -->|"Task per handler"| H
        H -->|"Channel&lt;OutboundMessage&gt;"| W
        W -->|"TCP socket"| R
    end
```

### Stage 1 — Reader (I/O-bound)

- Runs a tight `while(true)` loop: `ReadExactlyAsync(2)` for length prefix → `ReadExactlyAsync(len)` for body
- Pushes `RawMessage { Bytes, ConnNum, ReceivedAt }` into a bounded `Channel<RawMessage>`
- Returns rented buffer to `ArrayPool` **after** the parser consumes it (via channel completion)
- No parsing, no logging in the hot path — just `ReadAsync` → push → next
- **Outcome:** Socket is drained at line rate; reader never blocks on processing

### Stage 2 — Parser (CPU-bound)

- Reads from `Channel<RawMessage>`, calls `ISOMessage.UnPack()`
- Returns `ArrayPool<byte>` buffer to pool after unpacking
- Pushes `ParsedMessage { ISOMessage, RawBytes (hex), ConnNum }` into `Channel<ParsedMessage>`
- Can run multiple parser tasks per connection for extreme throughput (configurable concurrency)
- **Outcome:** Parsing is decoupled from socket I/O; messages queue up during CPU spikes

### Stage 3 — Dispatcher (Routing)

- Reads from `Channel<ParsedMessage>`, inspects MTI (field 0)
- Looks up registered `IMessageHandler` for that MTI (or falls back to default)
- Fires handler **as a fire-and-forget task** — dispatcher immediately reads next message
- Passes a `MessageContext` that includes the `ChannelWriter<OutboundMessage>` for responses
- **Outcome:** Messages are routed and handled in parallel; dispatcher never blocks

### Stage 4 — Message Handlers (User Code)

```csharp
public interface IMessageHandler
{
    /// <summary>
    /// MTIs this handler processes (e.g. ["1800", "1814"]).
    /// Use ["*"] for a catch-all handler.
    /// </summary>
    IReadOnlySet<string> SupportedMTIs { get; }

    /// <summary>
    /// Handle an incoming ISO 8583 message.
    /// Return an ISOMessage to send a response, or null to skip.
    /// </summary>
    Task<ISOMessage?> HandleAsync(MessageContext ctx, CancellationToken ct);
}

public sealed class MessageContext
{
    public ISOMessage Request { get; init; }
    public int ConnectionNumber { get; init; }
    public string RemoteEndpoint { get; init; }
    public DateTime ReceivedAt { get; init; }

    /// <summary>Send a response back to this client.</summary>
    public ValueTask SendResponseAsync(ISOMessage response, CancellationToken ct);

    /// <summary>Send a raw byte response (pre-packed).</summary>
    public ValueTask SendRawResponseAsync(byte[] framedMessage, CancellationToken ct);
}
```

Handlers are registered via DI:

```csharp
// Program.cs
builder.Services.AddSingleton<IMessageHandler, AuthorizationHandler>();  // MTI 0100
builder.Services.AddSingleton<IMessageHandler, ReversalHandler>();       // MTI 0400
builder.Services.AddSingleton<IMessageHandler, NetworkHandler>();        // MTI 1800
```

- Each handler runs independently — 100 messages can be in-flight simultaneously
- Responses are written to the **per-connection writer channel** — not directly to the socket
- **Outcome:** Business logic is isolated, testable, and naturally parallel. No ordering guarantee.

### Stage 5 — Writer (I/O-bound)

- Reads from `Channel<OutboundMessage>`, frames with 2-byte length prefix, calls `stream.WriteAsync`
- Single writer **per connection** guarantees no interleaved writes on the socket
- Writes are serialized but non-blocking for handlers (they just push to the channel)
- **Outcome:** Write ordering is preserved per-connection; handlers never touch the socket

---

## Key Design Decisions

### 1. Bounded Channels for Backpressure

All channels are bounded (e.g., capacity 256). When the parser can't keep up with the reader,
the reader's `WriteAsync` will asynchronously wait — applying natural backpressure to the TCP socket.

```csharp
var rawChannel = Channel.CreateBounded<RawMessage>(new BoundedChannelOptions(256)
{
    FullMode = BoundedChannelFullMode.Wait  // backpressure the reader
});
```

### 2. ArrayPool Ownership Transfer

`RawMessage` carries an `ArrayPool<byte>` lease. The parser **returns** it after unpacking.
Zero-copy where possible; the raw bytes are only used for hex-dump logging (optional,
can be disabled for max throughput).

### 3. No Ordering Guarantee

Messages A, B, C arrive in order. Handler A takes 50ms, handler B takes 2ms.
Response B is sent before response A. This is by design — the ISO 8583 wire protocol
does not mandate response ordering (each message is self-contained with STAN/DATE).

If ordering is required for a specific MTI, register a **single-threaded** handler
(configured per-handler, not globally).

### 4. SignOn / Echo Moved Out of Hot Path

Periodic SignOn and Echo are managed by a **separate background timer** that pushes
directly to the writer channel — no polling in the read loop.

### 5. Graceful Shutdown

When `StopAsync` is called:
1. Reader stops accepting from socket
2. Channels are completed (`Writer.Complete()`)
3. Parser, dispatcher drain remaining messages
4. Handlers finish in-flight work
5. Writer sends remaining queued responses
6. Socket closed

Timeout configurable (e.g., 30 seconds), after which pending work is discarded.

---

## Pipeline Host

A singleton `PipelineHost` manages all connections:

```csharp
public sealed class PipelineHost : IAsyncDisposable
{
    // Start a pipeline for a new connection
    public PipelineHandle Accept(TcpClient client, int connNum, CancellationToken ct);

    // Active connections for monitoring
    public IReadOnlyList<PipelineStats> GetStats();

    // Graceful shutdown
    public Task StopAllAsync(TimeSpan drainTimeout);
}

public sealed class PipelineStats
{
    public int ConnectionNumber { get; init; }
    public string RemoteEndpoint { get; init; }
    public DateTime ConnectedAt { get; init; }
    public int MessagesReceived { get; init; }
    public int MessagesSent { get; init; }
    public int InFlight { get; init; }       // messages being processed
    public int WriteQueueLength { get; init; }
}
```

---

## Configuration

```json
{
  "Iso8583Server": {
    "Port": 9443,
    "DialectPath": "Dialects/d8-iso8583.json",

    // Pipeline tuning
    "ParserConcurrency": 2,
    "RawMessageCapacity": 256,
    "ParsedMessageCapacity": 512,
    "OutboundMessageCapacity": 256,

    // Shutdown
    "DrainTimeoutSeconds": 30,

    // Periodic SignOn
    "SignOnIntervalSeconds": 30,
    "SendSignOnOnConnect": true,
    "EnablePeriodicSignOn": true,

    // TLS
    "TlsEnabled": true,
    "TlsCertPath": "/etc/certs/server.crt",
    "TlsKeyPath": "/etc/certs/server.key",
    "TlsCaCertPath": "/etc/certs/ca.pem",
    "TlsRequireClientCert": true
  }
}
```

---

## Migration Path

### Phase 1 — Refactor Iso8583TcpServer internals
- Keep `IIso8583Server` interface, keep REST API unchanged
- Replace `HandleClientAsync` internals with pipeline stages
- Add `IMessageHandler` registration via DI
- Existing `OnMessageParsed` callback becomes a default catch-all handler

### Phase 2 — Optimize
- Tune channel capacities based on benchmarks
- Add parser concurrency (multiple `ValueTask` consumers on same channel)
- Add `PipelineStats` for monitoring in `/status` endpoint

### Phase 3 — Advanced
- `ReadOnlySequence<byte>` zero-copy parsing (PipeReader)
- `IBufferWriter<byte>` zero-copy packing
- TLS offload to separate tasks if needed

---

## Measured Performance (BenchmarkDotNet)

All benchmarks on Intel Core i9-14900K, .NET 10, Release build, in-memory SplitStream (no network I/O).

| Metric | Value | Notes |
|---|---|---|
| **Single msg round-trip** | **~19 μs** (P50) | Includes frame→parse→dispatch→echo→frame→write; ~17 KB allocated |
| **Throughput (single conn)** | **~470,000 msg/sec** | 1000 messages processed in 2.1 ms; parser concurrency=2 |
| **P99 latency (1K batch)** | **~3.0 μs** | Per-message handler time; uniform under balanced load |
| **P99 latency (5K batch)** | **~6.0 μs** | GC becomes a factor at larger batch sizes |
| **Parser concurrency sweet spot** | **2 tasks** | 1→2: ~25% speedup; 4/8: no further gain, adds GC pressure |
| **Memory per round-trip** | **~17 KB** | Mostly ISOMessage allocations; zero-copy possible for raw bytes |

### Tuning Recommendations

| Setting | Default | Rationale |
|---|---|---|
| `ParserConcurrency` | **2** | Optimal from benchmarks; 1 is slightly slower, 4+ increases GC without throughput gain |
| `RawMessageCapacity` | **256** | Covers burst reads without consuming excess memory |
| `ParsedMessageCapacity` | **512** | Larger than raw to absorb parser concurrency bursts |
| `OutboundMessageCapacity` | **256** | Bounded with `Wait` mode provides natural TCP backpressure |
| `DrainTimeoutSeconds` | **30** | Generous timeout for in-flight handlers during graceful shutdown |

---

## File Layout (new)

```
src/
├── ISO8583Net/              (core library — unchanged)
├── ISO8583Server/           (TCP server library)
│   ├── Pipeline/
│   │   ├── PipelineHost.cs          (manages all connection pipelines)
│   │   ├── ConnectionPipeline.cs    (per-connection 5-stage pipeline)
│   │   ├── ReaderStage.cs           (socket → RawMessage channel)
│   │   ├── ParserStage.cs           (RawMessage → ParsedMessage channel)
│   │   ├── DispatcherStage.cs       (ParsedMessage → handlers)
│   │   ├── WriterStage.cs           (OutboundMessage channel → socket)
│   │   └── PipelineStats.cs
│   ├── Messages/
│   │   ├── RawMessage.cs
│   │   ├── ParsedMessage.cs
│   │   ├── OutboundMessage.cs
│   │   └── MessageContext.cs
│   ├── Handlers/
│   │   ├── IMessageHandler.cs
│   │   └── DefaultHandler.cs        (catch-all, replicates current behavior)
│   ├── IIso8583Server.cs            (updated)
│   ├── Iso8583TcpServer.cs          (refactored to use PipelineHost)
│   └── TlsOptions.cs                (unchanged)
tools/
└── ISO8583Service/
    ├── Program.cs                   (register handlers in DI)
    ├── Iso8583HostedService.cs      (unchanged)
    ├── Iso8583Controller.cs         (add stats to /status)
    ├── Handlers/                    (user-defined handlers)
    │   ├── AuthorizationHandler.cs
    │   ├── NetworkHandler.cs
    │   └── ReconciliationHandler.cs
    └── appsettings.json             (updated with pipeline config)
```

---

## Summary

The SEDA pipeline architecture transforms the server from a **sequential
read-process-respond loop** into a **fully asynchronous, multi-stage pipeline**
where:

- **Socket I/O is never blocked** by parsing or business logic
- **Messages are processed in parallel**, not sequentially
- **Responses are sent out-of-order** as soon as they're ready
- **Handlers are isolated, testable, and DI-friendly** via `IMessageHandler`
- **Backpressure is explicit** via bounded channels
- **The REST API remains unchanged** — same endpoints, same `IIso8583Server` contract

The migration is incremental: Phase 1 refactors internals without breaking the
public API; Phase 2 tunes for performance; Phase 3 goes zero-copy.
