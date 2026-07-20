# ISO8583Simulator — Design Document

An **ASP.NET Core hosted service** with a REST API and WebSocket hub that
connects to an ISO8583Server instance and simulates client-initiated
ISO 8583 message flows. The simulator can exercise every message type
defined in a dialect, validate responses, and report timing/error
statistics. A companion **Blazor WebUI** (optional) provides a
point-and-click dashboard for scenario execution, live message streaming,
and load testing.

The REST API enables CI/CD pipelines, integration tests, and any
frontend (Blazor, React, Angular, curl) to control the simulator
programmatically.

---

## Table of Contents

1. [Architecture Overview](#architecture-overview)
2. [Project Structure](#project-structure)
3. [Core Components](#core-components)
    - [SimulatorSession](#simulatorsession)
    - [MessageBuilderRegistry](#messagebuilderregistry)
    - [ResponseMatcher](#responsematcher)
    - [FrameReader / FrameWriter](#framereader--framewriter)
4. [Message Builders](#message-builders)
    - [IMessageBuilder interface](#imessagebuilder-interface)
    - [BaseRequestBuilder](#baserequestbuilder)
    - [BaseAdviceBuilder](#baseadvicebuilder)
    - [NetworkManagementBuilder](#networkmanagementbuilder)
    - [Concrete builders for D8 G2B](#concrete-builders-for-d8-g2b)
5. [Scenario Engine](#scenario-engine)
    - [IScenario interface](#iscenario-interface)
    - [Built-in scenarios](#built-in-scenarios)
    - [ScenarioRunner](#scenariorunner)
6. [REST API](#rest-api)
    - [Connection endpoints](#connection-endpoints)
    - [Scenario endpoints](#scenario-endpoints)
    - [Message endpoints](#message-endpoints)
    - [Load test endpoints](#load-test-endpoints)
7. [SignalR Hub — Real-time Streaming](#signalr-hub--real-time-streaming)
8. [Blazor WebUI Integration](#blazor-webui-integration)
9. [Configuration](#configuration)
10. [Program Entry Point](#program-entry-point)
11. [Mirroring the Server Pattern](#mirroring-the-server-pattern)
12. [Implementation Plan](#implementation-plan)

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────────────┐
│                     ISO8583Simulator (ASP.NET Core)                       │
│                                                                           │
│  ┌──────────────────────┐  ┌────────────────────────────────────────┐    │
│  │   Blazor WebUI       │  │       REST API (Controllers)            │    │
│  │   (optional SPA)     │  │                                         │    │
│  │                      │  │  POST /api/simulator/connect             │    │
│  │  ┌────────────────┐  │  │  POST /api/simulator/disconnect          │    │
│  │  │  Live Stream    │◀─┼──│  GET  /api/simulator/status             │    │
│  │  │  (SignalR)      │  │  │  POST /api/scenarios/run               │    │
│  │  └────────────────┘  │  │  GET  /api/scenarios                    │    │
│  └──────────────────────┘  │  POST /api/messages/send                │    │
│                             │  POST /api/loadtest/start               │    │
│                             └──────────────────┬─────────────────────┘    │
│                                                │                          │
│  ┌─────────────────────────────────────────────▼─────────────────────┐   │
│  │                SignalR Hub — SimulatorHub (WebSocket)              │   │
│  │  Streams: MessageSent, ResponseReceived, ErrorOccurred,            │   │
│  │           ScenarioProgress, StatsUpdate                            │   │
│  └─────────────────────────────────────────────┬─────────────────────┘   │
│                                                │                          │
│  ┌─────────────────────────────────────────────▼─────────────────────┐   │
│  │                    SimulatorHostedService                          │   │
│  │  ┌──────────────┐    ┌────────────────────┐    ┌───────────────┐  │   │
│  │  │ScenarioRunner │───▶│  SimulatorSession  │───▶│ ISO8583Server │  │   │
│  │  │ (orchestrator)│    │  (per-connection)   │    │  (port 9443)  │  │   │
│  │  └──────┬───────┘    └────────┬───────────┘    └───────────────┘  │   │
│  │         │                     │                                    │   │
│  │         ▼                     ▼                                    │   │
│  │  ┌─────────────────────────────────────────────────────────────┐  │   │
│  │  │                  SimulatorSession internals                   │  │   │
│  │  │                                                               │  │   │
│  │  │  ┌──────────────────┐         ┌─────────────────────────┐    │  │   │
│  │  │  │MessageBuilderReg │         │    Background Loop       │    │  │   │
│  │  │  │MTI → IMsgBuilder │         │                          │    │  │   │
│  │  │  └────────┬─────────┘         │  ┌────────────────────┐  │    │  │   │
│  │  │           │                   │  │FrameReader (Task)   │  │    │  │   │
│  │  │           ▼                   │  │Reads all responses   │  │    │  │   │
│  │  │  ┌──────────────────┐         │  └──────────┬─────────┘  │    │  │   │
│  │  │  │ BuildRequest()    │         │             │            │    │  │   │
│  │  │  │ + FillMandatory() │         │             ▼            │    │  │   │
│  │  │  └────────┬─────────┘         │  ┌────────────────────┐  │    │  │   │
│  │  │           │                   │  │ ResponseMatcher     │  │    │  │   │
│  │  │           ▼                   │  │ STAN → TCS          │  │    │  │   │
│  │  │  ┌──────────────────┐         │  └────────────────────┘  │    │  │   │
│  │  │  │ FrameWriter       │         │                          │    │  │   │
│  │  │  │ Pack + 2-byte LI  │─────────│── TCP/TLS Stream ───────▶│   │  │   │
│  │  │  └──────────────────┘         └─────────────────────────┘    │  │   │
│  │  └───────────────────────────────────────────────────────────────┘  │   │
│  └─────────────────────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────────────────────┘
```

**Flow for a request/response exchange (e.g. 0100 → 0110):**

1. `ScenarioRunner` calls `session.SendAsync("0100")`
2. `SimulatorSession` looks up `IMessageBuilder` for MTI "0100"
3. Builder's `BuildRequest()` is called — returns a populated `ISOMessage`
4. A `TaskCompletionSource<ISOMessage>` is registered under the STAN
5. `FrameWriter` packs the message, prepends 2-byte length prefix, sends
6. Background `FrameReader` reads response frames, unpacks, extracts STAN
7. `ResponseMatcher` resolves the `TaskCompletionSource` by STAN
8. `SendAsync` returns the response `ISOMessage` to the scenario

**Flow for an advice (fire-and-forget, e.g. 0120):**

1. Same as above but no `TaskCompletionSource` is registered
2. Builder's `BuildRequest()` is called
3. FrameWriter sends
4. SendAsync returns immediately (no wait for response)

---

## Project Structure

```
tools/ISO8583Simulator/
├── ISO8583Simulator.csproj       # ASP.NET Core hosted service, net10.0
├── Program.cs                    # Entry point, DI setup, pipeline bootstrap
├── appsettings.json              # Connection + scenario + WebAPI config
├── Simulator/
│   ├── SimulatorSession.cs       # Per-connection session: connect, send, receive
│   ├── SimulatorOptions.cs       # Connection parameters (host, port, TLS, timeout)
│   ├── SimulatorStats.cs         # Metrics: sent, received, errors, avg latency
│   ├── SimulatorState.cs         # State enum: Disconnected, Connecting, Connected, Running
│   ├── SimulatorHostedService.cs # BackgroundService: lifetime, graceful shutdown
│   └── ResponseMatcher.cs        # Correlates responses to requests by STAN
├── Builders/
│   ├── IMessageBuilder.cs        # Interface: SupportedMTIs + BuildRequest
│   ├── BaseRequestBuilder.cs     # For request/response MTIs (0100, 0200, 0400)
│   ├── BaseAdviceBuilder.cs      # For advice MTIs (0120, 0220, 0420)
│   ├── NetworkManagementBuilder.cs  # 0800 SignOn/Echo
│   └── Concrete/                 # One class per MTI
│       ├── AuthorizationBuilder.cs       # 0100
│       ├── AuthorizationAdviceBuilder.cs # 0120
│       ├── FinancialBuilder.cs           # 0200
│       ├── FinancialAdviceBuilder.cs     # 0220
│       ├── ReversalBuilder.cs            # 0400
│       └── ReversalAdviceBuilder.cs      # 0420
├── Scenarios/
│   ├── IScenario.cs              # Scenario contract: RunAsync(session)
│   ├── ScenarioRunner.cs         # Orchestrates multiple scenarios
│   ├── ScenarioReport.cs         # Results: total, passed, failed, duration
│   ├── SignOnScenario.cs         # 0800 → 0810
│   ├── EchoScenario.cs           # 0800 (echo) → 0810
│   ├── AuthorizationScenario.cs  # 0100 → 0110
│   ├── FinancialScenario.cs      # 0200 → 0210
│   ├── ReversalScenario.cs       # 0400 → 0410
│   ├── AdviceScenarios.cs        # 0120, 0220, 0420 (no response)
│   ├── FullLifecycleScenario.cs  # All flows in sequence
│   └── LoadTestScenario.cs       # Configurable parallel throughput test
├── Controllers/
│   ├── SimulatorController.cs    # Connect/disconnect/status/state
│   ├── ScenarioController.cs     # List scenarios, run by name
│   ├── MessageController.cs      # Send individual messages, query history
│   └── LoadTestController.cs     # Start/stop load tests, get results
├── Hubs/
│   └── SimulatorHub.cs           # SignalR hub for real-time message streaming
├── Models/
│   ├── ConnectRequest.cs         # DTO: host, port, tls, dialect
│   ├── SendMessageRequest.cs     # DTO: mti, field overrides
│   ├── LoadTestRequest.cs        # DTO: mti, count, concurrency
│   ├── SimulatorStatus.cs        # DTO: state, stats, uptime
│   └── MessageTrace.cs           # DTO for message history
└── Framing/
    ├── FrameReader.cs            # Read 2-byte LI + body from stream
    └── FrameWriter.cs            # Pack + prepend 2-byte LI + write
```

> **Why a separate project (not in samples/)?** The simulator is a tool,
> not a demo. It's config-driven, DI-based, and tests the server
> end-to-end. Samples/ are for human copy-paste learning.

---

## Core Components

### SimulatorSession

The central orchestrator for a single TCP/TLS connection to the server.

```csharp
public sealed class SimulatorSession : IAsyncDisposable
{
    // ── Construction ───────────────────────────────────────────────
    public SimulatorSession(
        SimulatorOptions options,
        ISOMessagePackager packager,
        IEnumerable<IMessageBuilder> builders,
        ILogger<SimulatorSession> logger);

    // ── Lifecycle ──────────────────────────────────────────────────
    public Task ConnectAsync(CancellationToken ct = default);
    // Opens TCP socket, optionally wraps in SslStream, authenticates

    public Task DisconnectAsync();
    // Graceful close

    // ── Message exchange ───────────────────────────────────────────
    public Task<ISOMessage> SendAsync(
        string mti, CancellationToken ct = default);
    // 1. Lookup builder for MTI
    // 2. BuildRequest()
    // 3. If it's a request builder: register TCS by STAN, wait for response
    // 4. If it's an advice builder: just send, return null
    // 5. Frame + write

    public Task SendAdviceAsync(
        string mti, CancellationToken ct = default);
    // Explicit fire-and-forget — no response expected

    // ── Statistics ─────────────────────────────────────────────────
    public SimulatorStats Stats { get; }

    // ── Internals ──────────────────────────────────────────────────
    private TcpClient _tcpClient;
    private Stream _stream;          // Raw or SslStream
    private CancellationTokenSource _cts;
    private Task _readerTask;        // Background FrameReader loop
    private ResponseMatcher _matcher;
    private MessageBuilderRegistry _registry;
}
```

### MessageBuilderRegistry

Maps MTIs to `IMessageBuilder` instances, similar to `HandlerRegistry` on the server.

```csharp
internal sealed class MessageBuilderRegistry
{
    private readonly Dictionary<string, IMessageBuilder> _builders = new();

    public MessageBuilderRegistry(IEnumerable<IMessageBuilder> builders)
    {
        foreach (var b in builders)
        foreach (var mti in b.SupportedMTIs)
            _builders[mti] = b;
    }

    public IMessageBuilder? GetBuilder(string mti)
        => _builders.TryGetValue(mti, out var b) ? b : null;

    public bool IsAdvice(string mti)
        => GetBuilder(mti) is BaseAdviceBuilder;
}
```

### ResponseMatcher

Correlates incoming responses to pending requests using STAN (field 11).
Thread-safe; accessed by both the caller (register) and the background
reader (complete).

```csharp
internal sealed class ResponseMatcher
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<ISOMessage>> _pending = new();

    // ── Caller side ────────────────────────────────────────────────
    public Task<ISOMessage> RegisterAsync(string stan, CancellationToken ct)
    {
        var tcs = new TaskCompletionSource<ISOMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _pending[stan] = tcs;

        // Cancel on timeout
        ct.Register(() =>
        {
            if (_pending.TryRemove(stan, out var removed))
                removed.TrySetCanceled(ct);
        });

        return tcs.Task;
    }

    // ── Reader side ────────────────────────────────────────────────
    public bool TryComplete(string stan, ISOMessage response)
    {
        if (_pending.TryRemove(stan, out var tcs))
        {
            tcs.TrySetResult(response);
            return true;
        }
        return false; // Unsolicited response or already timed out
    }

    // ── Shutdown ───────────────────────────────────────────────────
    public void CancelAll()
    {
        foreach (var (stan, tcs) in _pending)
            tcs.TrySetCanceled();
        _pending.Clear();
    }
}
```

### FrameReader / FrameWriter

Thin wrappers that mirror the server's ReaderStage/WriterStage framing
protocol (2-byte big-endian length prefix, 4KB max message size).

These are nearly identical to the server-side code:

```
Frame format:  [LI_hi][LI_lo][ISO 8583 body (LI bytes)]
                  2 bytes       variable
```

- **FrameReader**: background `Task` loop — `ReadExactlyAsync(2)` for LI → `ReadExactlyAsync(LI)` for body → `ISOMessage.Unpack()` → dispatch to `ResponseMatcher`
- **FrameWriter**: `message.Pack()` → allocate frame `[LI][body]` → `stream.WriteAsync`

**Reuse potential:** Since the framing protocol is identical, eventually
the server's `ReaderStage`/`WriterStage` internals could be extracted
into a shared `ISO8583Net.Framing` package. For now, the simulator
duplicates these ~40 lines to avoid coupling.

---

## Message Builders

Builders are the client-side mirror of server-side handlers. Instead of
*reacting* to messages, they *produce* them. The class hierarchy mirrors
the server's:

| Server (Handlers) | Simulator (Builders) |
|---|---|
| `IMessageHandler` | `IMessageBuilder` |
| `BaseRequestHandler` | `BaseRequestBuilder` |
| `BaseAdviceHandler` | `BaseAdviceBuilder` |
| `NetworkManagementHandler` | `NetworkManagementBuilder` |
| `DefaultHandler` | `DefaultBuilder` |

### IMessageBuilder Interface

```csharp
/// <summary>
/// Builds an ISO 8583 request message for a specific MTI.
/// Client-side analogue of IMessageHandler.
/// </summary>
public interface IMessageBuilder
{
    /// <summary>MTIs this builder can produce.</summary>
    IReadOnlySet<string> SupportedMTIs { get; }

    /// <summary>
    /// Build the request message. The caller provides a pre-constructed
    /// ISOMessage (empty, bound to the packager). The builder populates
    /// mandatory fields and any optional fields needed.
    /// </summary>
    /// <param name="message">Empty ISOMessage to populate.</param>
    void BuildRequest(ISOMessage message);
}
```

### BaseRequestBuilder

For request/response MTIs (0100, 0200, 0400). Automatically sets the
request MTI in F0 and provides sensible defaults for mandatory fields.
Derived classes override `BuildRequest` to customize field values.

```csharp
public abstract class BaseRequestBuilder : IMessageBuilder
{
    public abstract string RequestMTI { get; }
    public IReadOnlySet<string> SupportedMTIs { get; }

    protected readonly ILogger Logger;

    protected BaseRequestBuilder(ILogger? logger = null)
    {
        Logger = logger ?? NullLogger.Instance;
        SupportedMTIs = new HashSet<string> { RequestMTI };
    }

    /// <summary>
    /// Fill mandatory fields with sensible defaults. Override to
    /// customize or add optional fields.
    /// </summary>
    public virtual void BuildRequest(ISOMessage message)
    {
        message.Set(0, RequestMTI);

        // Generate default values for common mandatory fields
        FillMandatoryDefaults(message);
    }

    /// <summary>
    /// Fill mandatory fields with test values. Subclasses can
    /// override individual fields.
    /// </summary>
    protected virtual void FillMandatoryDefaults(ISOMessage message)
    {
        // F2  PAN — test card number
        message.Set(2, "4000400040004001");

        // F3  Processing Code
        message.Set(3, "300000");

        // F4  Transaction Amount
        message.Set(4, "000000001000"); // 10.00

        // F7  Transmission Date & Time (MMDDhhmmss)
        message.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));

        // F11 STAN — 6-digit sequential
        message.Set(11, GenerateStan());

        // F12 Local Transaction Time (hhmmss)
        message.Set(12, DateTime.UtcNow.ToString("HHmmss"));

        // F22 Point of Service Entry Mode
        message.Set(22, "9010");

        // F24 Function Code
        message.Set(24, "200");

        // F26 POS Card Capture Capability
        message.Set(26, "06");

        // F28 Transaction Fee Amount
        message.Set(28, "000000000000");

        // F32 Acquiring Institution ID
        message.Set(32, "000005");

        // F37 Retrieval Reference Number (12 digits)
        message.Set(37, GenerateRrn());

        // F41 Card Acceptor Terminal ID
        message.Set(41, "TERM0001");

        // F42 Card Acceptor ID
        message.Set(42, "MERCHANT001");

        // F49 Currency Code (EUR)
        message.Set(49, "978");
    }

    protected static string GenerateStan()
    {
        var stan = (uint)(DateTime.UtcNow.Ticks % 1_000_000);
        return stan.ToString("D6");
    }

    protected static string GenerateRrn()
    {
        var rrn = (ulong)(DateTime.UtcNow.Ticks % 10_000_000_000_000);
        return rrn.ToString("D12");
    }
}
```

### BaseAdviceBuilder

For advice MTIs (0120, 0220, 0420). Same as BaseRequestBuilder but the
simulator knows not to wait for a response. Optionally sets F39="400".

```csharp
public abstract class BaseAdviceBuilder : IMessageBuilder
{
    public abstract string AdviceMTI { get; }
    public IReadOnlySet<string> SupportedMTIs { get; }

    protected readonly ILogger Logger;

    protected BaseAdviceBuilder(ILogger? logger = null)
    {
        Logger = logger ?? NullLogger.Instance;
        SupportedMTIs = new HashSet<string> { AdviceMTI };
    }

    public virtual void BuildRequest(ISOMessage message)
    {
        message.Set(0, AdviceMTI);
        FillMandatoryDefaults(message);
        // Advices carry F39 from the original transaction
        message.Set(39, "400");
    }

    // Same FillMandatoryDefaults as BaseRequestBuilder
    protected virtual void FillMandatoryDefaults(ISOMessage message)
    {
        // ... (identical to BaseRequestBuilder's version)
    }
}
```

> **Refactoring opportunity:** Extract `FillMandatoryDefaults` into a
> static helper class `DefaultFieldValues` shared by both base classes.

### NetworkManagementBuilder

Handles SignOn (0800 with F70=001), Echo (0800 with F70=301), and SignOff
(0800 with F70=002).

```csharp
public class NetworkManagementBuilder : IMessageBuilder
{
    public IReadOnlySet<string> SupportedMTIs { get; } = new HashSet<string> { "0800" };

    public enum Function { SignOn, SignOff, Echo }

    public Function CurrentFunction { get; set; } = Function.Echo;

    public void BuildRequest(ISOMessage message)
    {
        message.Set(0, "0800");
        message.Set(7, DateTime.UtcNow.ToString("MMddHHmmss"));
        message.Set(11, BaseRequestBuilder.GenerateStan());
        message.Set(70, CurrentFunction switch
        {
            Function.SignOn  => "001",
            Function.SignOff => "002",
            Function.Echo    => "301",
            _                => "301"
        });
    }
}
```

### Concrete Builders for D8 G2B

These are thin — most logic is in the base classes. Each overrides only
the fields that differ from defaults.

```csharp
// tools/ISO8583Simulator/Builders/Concrete/AuthorizationBuilder.cs
public sealed class AuthorizationBuilder : BaseRequestBuilder
{
    public override string RequestMTI => "0100";

    public override void BuildRequest(ISOMessage message)
    {
        base.BuildRequest(message);
        // Authorization-specific overrides (if any)
    }
}

// tools/ISO8583Simulator/Builders/Concrete/FinancialBuilder.cs
public sealed class FinancialBuilder : BaseRequestBuilder
{
    public override string RequestMTI => "0200";
}

// tools/ISO8583Simulator/Builders/Concrete/ReversalBuilder.cs
public sealed class ReversalBuilder : BaseRequestBuilder
{
    public override string RequestMTI => "0400";

    public override void BuildRequest(ISOMessage message)
    {
        base.BuildRequest(message);
        // Reversals carry F90 (Original Data Elements)
        message.Set(90, "0100" + GenerateRrn() + DateTime.UtcNow.ToString("MMdd"));
    }
}

// Advice builders — nearly identical, just different MTIs
public sealed class AuthorizationAdviceBuilder : BaseAdviceBuilder
    { public override string AdviceMTI => "0120"; }
public sealed class FinancialAdviceBuilder : BaseAdviceBuilder
    { public override string AdviceMTI => "0220"; }
public sealed class ReversalAdviceBuilder : BaseAdviceBuilder
    { public override string AdviceMTI => "0420"; }
```

---

## Scenario Engine

Scenarios are composable message exchange sequences. The `ScenarioRunner`
executes them in order, collecting pass/fail results and timing data.

### IScenario Interface

```csharp
public interface IScenario
{
    /// <summary>Human-readable name for reporting.</summary>
    string Name { get; }

    /// <summary>
    /// Run the scenario against a connected session.
    /// Returns true if all assertions pass.
    /// </summary>
    Task<bool> RunAsync(SimulatorSession session, CancellationToken ct = default);
}
```

### Built-in Scenarios

| Scenario | Flow | Asserts |
|----------|------|---------|
| `SignOnScenario` | 0800 (F70=001) → 0810 | F39=000 |
| `EchoScenario` | 0800 (F70=301) → 0810 | Response echoes back |
| `AuthorizationScenario` | 0100 → 0110 | Rsp MTI=0110, F2=request F2, F39=000 |
| `FinancialScenario` | 0200 → 0210 | Rsp MTI=0210, F39=000 |
| `ReversalScenario` | 0400 → 0410 | Rsp MTI=0410, F39=000 |
| `AuthorizationAdviceScenario` | 0120 (no response) | No timeout |
| `FinancialAdviceScenario` | 0220 (no response) | No timeout |
| `ReversalAdviceScenario` | 0420 (no response) | No timeout |
| `FullLifecycleScenario` | SignOn → Auth → Financial → Reversal → SignOff | All pass |
| `LoadTestScenario` | Configurable N concurrent requests | P99 latency < X ms |

**Example scenario implementation:**

```csharp
public sealed class AuthorizationScenario : IScenario
{
    public string Name => "Authorization (0100 → 0110)";
    private readonly ILogger<AuthorizationScenario> _logger;

    public AuthorizationScenario(ILogger<AuthorizationScenario> logger) => _logger = logger;

    public async Task<bool> RunAsync(SimulatorSession session, CancellationToken ct)
    {
        _logger.LogInformation("Running {Scenario}", Name);

        var response = await session.SendAsync("0100", ct);

        // Assert response MTI
        var responseMti = response.GetString(0);
        if (responseMti != "0110")
        {
            _logger.LogError("Expected response MTI 0110, got {Actual}", responseMti);
            return false;
        }

        // Assert F39 = approved
        var f39 = response.GetString(39);
        if (f39 != "000")
        {
            _logger.LogError("Expected F39=000 (approved), got {Actual}", f39);
            return false;
        }

        _logger.LogInformation("{Scenario} passed: F39={F39}", Name, f39);
        return true;
    }
}
```

### ScenarioRunner

Orchestrates scenario execution, collects results, prints a summary.

```csharp
public sealed class ScenarioRunner
{
    private readonly IEnumerable<IScenario> _scenarios;
    private readonly ILogger<ScenarioRunner> _logger;

    public ScenarioRunner(
        IEnumerable<IScenario> scenarios,
        ILogger<ScenarioRunner> logger);

    public async Task<ScenarioReport> RunAllAsync(
        SimulatorSession session,
        CancellationToken ct = default);

    public async Task<ScenarioReport> RunAsync(
        string scenarioName,
        SimulatorSession session,
        CancellationToken ct = default);
}

public sealed class ScenarioReport
{
    public int Total { get; init; }
    public int Passed { get; init; }
    public int Failed { get; init; }
    public TimeSpan TotalDuration { get; init; }
    public List<ScenarioResult> Results { get; init; }
}

public sealed class ScenarioResult
{
    public string ScenarioName { get; init; }
    public bool Passed { get; init; }
    public TimeSpan Duration { get; init; }
    public string? ErrorMessage { get; init; }
}
```

---

## REST API

The simulator exposes a REST API for programmatic control — connect, send
messages, run scenarios, start load tests. All endpoints return JSON.

### SimulatorController — `/api/simulator`

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/simulator/connect` | Connect to the ISO8583Server |
| `POST` | `/api/simulator/disconnect` | Disconnect and drain pending requests |
| `GET` | `/api/simulator/status` | Current connection state + stats |
| `GET` | `/api/simulator/health` | Health check endpoint |

**POST /api/simulator/connect**

```jsonc
// Request
{
  "host": "localhost",
  "port": 9443,
  "tlsEnabled": true,
  "tlsAllowUntrusted": true,
  "dialectPath": "Dialects/d8-iso8583.json"
}

// Response 200
{
  "state": "Connected",
  "connectedAt": "2026-07-20T10:00:00Z",
  "remoteEndpoint": "localhost:9443"
}

// Response 409 — already connected
{ "error": "Already connected", "state": "Connected" }
```

**GET /api/simulator/status**

```jsonc
// Response 200
{
  "state": "Connected",         // Disconnected | Connecting | Connected | Running
  "connectedAt": "2026-07-20T10:00:00Z",
  "uptimeSeconds": 120,
  "stats": {
    "messagesSent": 1543,
    "responsesReceived": 1543,
    "errors": 0,
    "avgLatencyMs": 1.23,
    "p99LatencyMs": 5.67,
    "throughputMsgPerSec": 12.8
  }
}
```

### ScenarioController — `/api/scenarios`

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/scenarios` | List all registered scenarios |
| `POST` | `/api/scenarios/run` | Run a scenario by name |
| `POST` | `/api/scenarios/run-all` | Run all configured scenarios |

**GET /api/scenarios**

```jsonc
// Response 200
{
  "scenarios": [
    { "name": "SignOn", "description": "0800 SignOn → 0810" },
    { "name": "Echo", "description": "0800 Echo → 0810" },
    { "name": "Authorization", "description": "0100 Request → 0110 Response" },
    { "name": "Financial", "description": "0200 Request → 0210 Response" },
    { "name": "Reversal", "description": "0400 Request → 0410 Response" },
    { "name": "FullLifecycle", "description": "All flows in sequence" }
  ]
}
```

**POST /api/scenarios/run**

```jsonc
// Request
{ "name": "Authorization" }

// Response 200
{
  "scenarioName": "Authorization",
  "passed": true,
  "durationMs": 12.5,
  "errorMessage": null
}

// Response 400 — unknown scenario
{ "error": "Unknown scenario: FooBar" }

// Response 400 — not connected
{ "error": "Simulator is not connected" }
```

### MessageController — `/api/messages`

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/messages/send` | Send a single message and await response |
| `GET` | `/api/messages/recent` | Query recent message history |
| `POST` | `/api/messages/send-advice` | Fire-and-forget advice message |

**POST /api/messages/send**

```jsonc
// Request
{
  "mti": "0100",
  "fieldOverrides": {
    "2": "4000400040005000"     // Override PAN
    // If omitted, BaseRequestBuilder defaults are used
  },
  "timeoutMs": 30000
}

// Response 200
{
  "requestMti": "0100",
  "responseMti": "0110",
  "stan": "482901",
  "f39": "000",
  "elapsedMs": 1.87,
  "responseHex": "01 B0 20 00 ..."
}

// Response 408 — timeout
{ "error": "Timeout waiting for response to STAN 482901", "stan": "482901" }
```

**GET /api/messages/recent?count=50&mti=0100**

```jsonc
// Response 200
{
  "messages": [
    {
      "id": 1,
      "timestamp": "2026-07-20T10:01:00Z",
      "requestMti": "0100",
      "responseMti": "0110",
      "stan": "482901",
      "f39": "000",
      "elapsedMs": 1.87
    }
  ],
  "total": 1543
}
```

### LoadTestController — `/api/loadtest`

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/loadtest/start` | Start a load test (returns immediately) |
| `POST` | `/api/loadtest/stop` | Stop the running load test |
| `GET` | `/api/loadtest/status` | Current load test progress |

**POST /api/loadtest/start**

```jsonc
// Request
{
  "mti": "0100",
  "totalCount": 10000,
  "concurrency": 10,
  "timeoutMs": 30000
}

// Response 202
{
  "loadTestId": "a3f2c1b9",
  "state": "Running",
  "totalCount": 10000,
  "concurrency": 10
}
```

**GET /api/loadtest/status**

```jsonc
// Response 200
{
  "loadTestId": "a3f2c1b9",
  "state": "Running",          // Running | Completed | Stopped
  "sentCount": 4523,
  "receivedCount": 4520,
  "errorCount": 3,
  "avgLatencyMs": 1.34,
  "p50Ms": 1.12,
  "p99Ms": 4.89,
  "elapsedSeconds": 37
}
```

**CORS** is enabled for `http://localhost:5199` (default Blazor dev server)
so a Blazor WebUI running alongside can call these endpoints directly.

---

## SignalR Hub — Real-time Streaming

A SignalR hub pushes real-time events to connected WebSocket clients
(the Blazor WebUI, or any SignalR client).

### SimulatorHub — `/hubs/simulator`

```csharp
public class SimulatorHub : Hub
{
    // Server pushes these events; clients subscribe.
    // No client-to-server methods needed — REST API handles commands.
}
```

**Events pushed to clients:**

| Event | Payload | When |
|-------|---------|------|
| `MessageSent` | `{ mti, stan, timestamp, hex }` | Message framed and written to socket |
| `ResponseReceived` | `{ requestMti, responseMti, stan, f39, elapsedMs, hex }` | Response unpacked from socket |
| `ErrorOccurred` | `{ stan, errorType, message }` | Parse error, timeout, handler error |
| `ScenarioProgress` | `{ scenarioName, step, totalSteps, stepDescription }` | Each step in a scenario |
| `ScenarioCompleted` | `{ scenarioName, passed, durationMs }` | Scenario finishes |
| `LoadTestProgress` | `{ loadTestId, sentCount, receivedCount, errors, avgMs }` | Periodic (every 500ms) |
| `StateChanged` | `{ oldState, newState }` | Connect/disconnect/error transitions |
| `StatsUpdate` | `{ msgsSent, msgsRecv, errors, avgMs, p99Ms }` | Periodic (every 2s) |

**JavaScript client example:**

```javascript
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/hubs/simulator")
    .build();

connection.on("MessageSent", (msg) => {
    console.log(`Sent ${msg.mti} STAN=${msg.stan}`);
});

connection.on("ResponseReceived", (resp) => {
    console.log(`Got ${resp.responseMti} F39=${resp.f39} in ${resp.elapsedMs}ms`);
});

connection.on("LoadTestProgress", (progress) => {
    updateGauge(progress.sentCount, progress.avgMs);
});

await connection.start();
```

**Hub group strategy:** Single group `"all"` — every connected UI receives
all events. This keeps it simple. If multiple concurrent users need
isolated views, we can add connection-scoped filtering later.

---

## Blazor WebUI Integration

The Blazor WebUI (separate project, e.g. `tools/ISO8583Simulator.WebUI`)
connects to the simulator's REST API + SignalR hub. Here's a recommended
component layout:

### Pages

```
/connect        — Connection form (host, port, TLS, dialect)
/dashboard      — Real-time stats gauges (throughput, latency, errors)
/scenarios      — Scenario list with run/stop buttons, results table
/messages       — Send individual messages, view response hex
/message/:stan  — Single message detail with request/response hex dumps
/loadtest       — Configure and start load tests, live progress chart
/health         — Connection health and diagnostics
```

### Key Blazor Components

```
Components/
├── ConnectionPanel.razor        # Host/port form, Connect/Disconnect buttons, status badge
├── StatsGauges.razor            # Throughput, latency, error rate (live via SignalR)
├── ScenarioRunner.razor         # Dropdown to pick scenario, Run button, result badge
├── MessageBuilder.razor         # MTI picker + field overrides form → Send button
├── MessageHexViewer.razor       # Expandable hex dump with syntax highlighting
├── LoadTestConfig.razor         # MTI, count, concurrency sliders → Start/Stop
├── LoadTestChart.razor          # Live line chart (sent rate, latency p50/p99)
├── LiveMessageStream.razor      # Scrolling log of sent/received messages (SignalR)
└── SimulatorLayout.razor        # Top nav + SignalR connection state banner
```

### SignalR Integration in Blazor

```csharp
// In Program.cs or a dedicated service
builder.Services.AddSingleton<SimulatorHubClient>();

public sealed class SimulatorHubClient : IAsyncDisposable
{
    private HubConnection? _hub;

    // Events that Blazor components bind to
    public event Action<MessageTraceDto>? OnMessageSent;
    public event Action<ResponseTraceDto>? OnResponseReceived;
    public event Action<LoadTestProgressDto>? OnLoadTestProgress;

    public async Task StartAsync(string hubUrl)
    {
        _hub = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hub.On<MessageTraceDto>("MessageSent",
            m => OnMessageSent?.Invoke(m));
        _hub.On<ResponseTraceDto>("ResponseReceived",
            r => OnResponseReceived?.Invoke(r));
        _hub.On<LoadTestProgressDto>("LoadTestProgress",
            p => OnLoadTestProgress?.Invoke(p));

        await _hub.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub != null) await _hub.DisposeAsync();
    }
}
```

### Optional: Minimal Razor Component for Message Stream

```razor
@implements IDisposable
@inject SimulatorHubClient HubClient

<div class="message-stream">
    @foreach (var entry in _entries.TakeLast(200))
    {
        <div class="message-entry @entry.CssClass">
            <span class="time">@entry.Timestamp.ToString("HH:mm:ss.fff")</span>
            <span class="direction">@entry.Direction</span>
            <span class="mti">@entry.Mti</span>
            <span class="stan">STAN=@entry.Stan</span>
            @if (entry.F39 != null)
            {
                <span class="f39">F39=@entry.F39</span>
            }
            <span class="latency">@entry.ElapsedMs?.ToString("F2")ms</span>
        </div>
    }
</div>

@code {
    private readonly List<StreamEntry> _entries = new();

    protected override void OnInitialized()
    {
        HubClient.OnMessageSent += OnMsg;
        HubClient.OnResponseReceived += OnResp;
    }

    private void OnMsg(MessageTraceDto m) => _entries.Add(new StreamEntry { ... });
    private void OnResp(ResponseTraceDto r) => _entries.Add(new StreamEntry { ... });

    public void Dispose()
    {
        HubClient.OnMessageSent -= OnMsg;
        HubClient.OnResponseReceived -= OnResp;
    }
}
```

> **Note:** The WebUI project is *optional* — the REST API and SignalR hub
> allow any frontend (curl, Postman, custom Blazor/React/Angular app) to
> control the simulator. This section illustrates one possible integration.

---

## Configuration

### appsettings.json

```json
{
  "Simulator": {
    "Host": "localhost",
    "Port": 9443,
    "TlsEnabled": true,
    "TlsCertPath": "",
    "TlsAllowUntrusted": true,
    "ConnectTimeoutSeconds": 10,
    "ResponseTimeoutSeconds": 30,
    "DialectPath": "Dialects/d8-iso8583.json",
    "Scenarios": [
      "SignOn",
      "Echo",
      "Authorization",
      "Financial",
      "Reversal",
      "FullLifecycle"
    ]
  },
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/iso8583-simulator-.log",
          "rollingInterval": "Day"
        }
      }
    ]
  }
}
```

### SimulatorOptions

```csharp
public sealed class SimulatorOptions
{
    public const string SectionName = "Simulator";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 9443;
    public bool TlsEnabled { get; set; } = true;
    public string? TlsCertPath { get; set; }
    public bool TlsAllowUntrusted { get; set; } = true;
    public int ConnectTimeoutSeconds { get; set; } = 10;
    public int ResponseTimeoutSeconds { get; set; } = 30;
    public string DialectPath { get; set; } = "Dialects/d8-iso8583.json";
    public List<string> Scenarios { get; set; } = new() { "FullLifecycle" };
}
```

---

## Program Entry Point

```csharp
// tools/ISO8583Simulator/Program.cs
public static async Task Main(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    // ── Config ─────────────────────────────────────────────────────
    builder.Services.Configure<SimulatorOptions>(
        builder.Configuration.GetSection(SimulatorOptions.SectionName));

    // ── ISO 8583 packager ──────────────────────────────────────────
    builder.Services.AddSingleton(sp =>
    {
        var options = sp.GetRequiredService<IOptions<SimulatorOptions>>().Value;
        var logger = sp.GetRequiredService<ILogger<ISOMessagePackager>>();
        return new ISOMessagePackager(logger, options.DialectPath);
    });

    // ── Message builders ───────────────────────────────────────────
    builder.Services.AddSingleton<IMessageBuilder, AuthorizationBuilder>();
    builder.Services.AddSingleton<IMessageBuilder, AuthorizationAdviceBuilder>();
    builder.Services.AddSingleton<IMessageBuilder, FinancialBuilder>();
    builder.Services.AddSingleton<IMessageBuilder, FinancialAdviceBuilder>();
    builder.Services.AddSingleton<IMessageBuilder, ReversalBuilder>();
    builder.Services.AddSingleton<IMessageBuilder, ReversalAdviceBuilder>();
    builder.Services.AddSingleton<IMessageBuilder, NetworkManagementBuilder>();

    // ── Scenarios ──────────────────────────────────────────────────
    builder.Services.AddSingleton<IScenario, SignOnScenario>();
    builder.Services.AddSingleton<IScenario, EchoScenario>();
    builder.Services.AddSingleton<IScenario, AuthorizationScenario>();
    builder.Services.AddSingleton<IScenario, FinancialScenario>();
    builder.Services.AddSingleton<IScenario, ReversalScenario>();
    builder.Services.AddSingleton<IScenario, FullLifecycleScenario>();
    builder.Services.AddSingleton<IScenario, LoadTestScenario>();

    // ── Core services ──────────────────────────────────────────────
    builder.Services.AddSingleton<SimulatorSession>();
    builder.Services.AddSingleton<ScenarioRunner>();
    builder.Services.AddSingleton<SimulatorStats>();
    builder.Services.AddHostedService<SimulatorHostedService>();

    // ── SignalR ────────────────────────────────────────────────────
    builder.Services.AddSignalR();

    // ── API ────────────────────────────────────────────────────────
    builder.Services.AddControllers();
    builder.Services.AddOpenApi();

    // ── CORS (for Blazor WebUI at localhost:5199) ──────────────────
    builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:5199")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

    // ── Serilog ────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

    var app = builder.Build();

    app.UseCors();
    app.MapControllers();
    app.MapOpenApi();
    app.MapScalarApiReference();      // Scalar docs at /scalar/v1
    app.MapHub<SimulatorHub>("/hubs/simulator");

    await app.RunAsync();
}
```

**Key behavior:**
- **ASP.NET Core host** — runs as a hosted service, not a one-shot console app
- **REST API always available** — even if disconnected from the server, you can query scenario list
- **SimulatorHostedService** manages the TCP/TLS connection lifecycle in the background
- **Exit on `/api/simulator/disconnect`** only — service stays alive regardless of connection state
- **Scalar API docs** at `/scalar/v1` for interactive API exploration
- **CLI args** (optional): `--urls http://0.0.0.0:5100` to override the listening port

---

## Mirroring the Server Pattern

The simulator intentionally mirrors the server's handler architecture:

```
┌─────────────────────────────────────────────────────────────┐
│  SERVER                          SIMULATOR                  │
│                                                              │
│  IMessageHandler                 IMessageBuilder             │
│  ├─ SupportedMTIs                ├─ SupportedMTIs            │
│  └─ HandleAsync(context)         └─ BuildRequest(message)    │
│                                                              │
│  BaseRequestHandler              BaseRequestBuilder          │
│  ├─ ProcessAsync → F39           ├─ FillMandatoryDefaults     │
│  └─ BuildResponse()              └─ GenerateStan/Rrn         │
│                                                              │
│  BaseAdviceHandler               BaseAdviceBuilder           │
│  └─ F39=400 auto-ack             └─ F39=400 pre-set          │
│                                                              │
│  NetworkManagementHandler        NetworkManagementBuilder    │
│  └─ 0800 Echo/SignOn             └─ 0800 SignOn/Echo/SignOff │
│                                                              │
│  HandlerRegistry                 MessageBuilderRegistry      │
│  └─ MTI → List<IMessageHandler>  └─ MTI → IMessageBuilder   │
│                                                              │
│  DispatcherStage                 SimulatorSession.SendAsync  │
│  └─ Route + aggregate            └─ Build + send + await     │
└─────────────────────────────────────────────────────────────┘
```

This symmetry means:
1. **Learning curve is flat** — if you know the server, you know the simulator
2. **Adding a new message type** requires the same steps on both sides:
   create a builder class, override `RequestMTI`, register in DI
3. **Testing is self-documenting** — the simulator exercises exactly what the server implements

---

## Implementation Plan

### Phase 1 — Foundation (ASP.NET Core + connection + framing)
- [ ] Create `ISO8583Simulator.csproj` (ASP.NET Core Web SDK, net10.0)
- [ ] `SimulatorOptions` + `appsettings.json`
- [ ] `SimulatorState` enum
- [ ] `FrameReader` / `FrameWriter` (mirror server's framing)
- [ ] `SimulatorSession` — connect, read loop, write, disconnect
- [ ] `ResponseMatcher` — STAN-based correlation
- [ ] `SimulatorHostedService` — background lifecycle management
- [ ] `SimulatorStats` — thread-safe counters, latency histogram

### Phase 2 — Builders
- [ ] `IMessageBuilder` interface
- [ ] `BaseRequestBuilder` with `FillMandatoryDefaults`
- [ ] `BaseAdviceBuilder`
- [ ] `NetworkManagementBuilder`
- [ ] Concrete builders (6 classes for D8 G2B)
- [ ] `MessageBuilderRegistry`

### Phase 3 — Scenarios
- [ ] `IScenario` interface
- [ ] `ScenarioReport` / `ScenarioResult`
- [ ] `ScenarioRunner`
- [ ] 5 request/response scenarios + 3 advice scenarios
- [ ] `FullLifecycleScenario`
- [ ] `LoadTestScenario`

### Phase 4 — REST API
- [ ] `SimulatorController` — connect/disconnect/status
- [ ] `ScenarioController` — list, run, run-all
- [ ] `MessageController` — send, send-advice, recent history
- [ ] `LoadTestController` — start, stop, status
- [ ] CORS configuration for Blazor WebUI
- [ ] Scalar API docs at `/scalar/v1`

### Phase 5 — SignalR Hub + Blazor (optional)
- [ ] `SimulatorHub` with all 8 event streams
- [ ] `SimulatorHubClient` class for typed SignalR consumption
- [ ] WebUI project scaffold (optional)
- [ ] Core Blazor components (connection panel, message stream, load test chart)

### Phase 6 — Polish
- [ ] CLI flags: `--urls`
- [ ] CI-friendly exit codes for `curl`-based smoke tests
- [ ] `README.md` for the simulator
- [ ] Docker Compose sample: ISO8583Service + PostgreSQL + Simulator

---

## Dependencies

| Package | Purpose |
|---------|---------|
| `ISO8583Net` (project reference) | ISOMessage, ISOMessagePackager, ISOUtils |
| `ISO8583Server` (project reference) | Shared pipeline types (TlsOptions, etc.) |
| `Microsoft.AspNetCore.SignalR` | Real-time WebSocket streaming to Blazor UI |
| `Microsoft.AspNetCore.OpenApi` | OpenAPI / Swagger generation |
| `Scalar.AspNetCore` | Interactive API docs UI |
| `Microsoft.Extensions.Hosting` | DI, config binding, logging |
| `Serilog.AspNetCore` | Structured logging |

No new external dependencies beyond what the solution already uses,
plus SignalR (built into ASP.NET Core).

---

## Stretch Goals (Future)

- **Multi-connection load testing** — `SimulatorPool` managing N concurrent sessions
- **Replay mode** — read a JSON trace file and replay exact message sequences
- **Fuzzing mode** — send deliberately malformed messages to test error handling
- **gRPC control plane** — remote-control the simulator from a dashboard
- **Blazor WebUI** — full interactive dashboard as a companion project
- **Docker Compose** — ISO8583Service + PostgreSQL + Simulator + WebUI stack
- **OpenTelemetry export** — push spans/metrics to Jaeger/Prometheus for observability
- **Message diff view** — side-by-side request/response hex comparison in WebUI
