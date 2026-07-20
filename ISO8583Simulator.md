# ISO8583Simulator — Design Document

A .NET console application that connects to an ISO8583Server instance and
simulates client-initiated ISO 8583 message flows. The simulator can
exercise every message type defined in a dialect, validate responses, and
report timing/error statistics — useful for integration testing, load
testing, and CI/CD smoke tests.

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
6. [Configuration](#configuration)
7. [Program Entry Point](#program-entry-point)
8. [Mirroring the Server Pattern](#mirroring-the-server-pattern)
9. [Implementation Plan](#implementation-plan)

---

## Architecture Overview

```
┌──────────────────────────────────────────────────────────────────┐
│                        ISO8583Simulator                           │
│                                                                    │
│  ┌──────────────┐    ┌────────────────────┐    ┌───────────────┐  │
│  │  ScenarioRunner │──▶│  SimulatorSession  │──▶│ ISO8583Server │  │
│  │  (orchestrator) │   │  (per-connection)   │   │  (port 9443)  │  │
│  └──────┬─────────┘    └────────┬───────────┘    └───────────────┘  │
│         │                       │                                    │
│         ▼                       ▼                                    │
│  ┌──────────────────────────────────────────────────────────────┐  │
│  │                    SimulatorSession internals                  │  │
│  │                                                                │  │
│  │  ┌───────────────────┐          ┌─────────────────────────┐   │  │
│  │  │ MessageBuilderReg  │          │     Background Loop       │   │  │
│  │  │ MTI → IMsgBuilder  │          │                           │   │  │
│  │  └────────┬──────────┘          │  ┌─────────────────────┐  │   │  │
│  │           │                     │  │ FrameReader (Task)   │  │   │  │
│  │           ▼                     │  │ Reads all responses   │  │   │  │
│  │  ┌───────────────────┐          │  └──────────┬──────────┘  │   │  │
│  │  │ BuildRequest()     │          │             │             │   │  │
│  │  │ + FillMandatory()   │          │             ▼             │   │  │
│  │  └────────┬──────────┘          │  ┌─────────────────────┐  │   │  │
│  │           │                     │  │ ResponseMatcher      │  │   │  │
│  │           ▼                     │  │ STAN → TaskCompletion │  │   │  │
│  │  ┌───────────────────┐          │  └─────────────────────┘  │   │  │
│  │  │ FrameWriter        │          │                           │   │  │
│  │  │ Pack + 2-byte LI   │──────────│── TCP/TLS Stream ────────▶│   │  │
│  │  └───────────────────┘          └─────────────────────────┘   │  │
│  └──────────────────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────────────────┘
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
├── ISO8583Simulator.csproj       # Console app, net10.0
├── Program.cs                    # Entry point, DI setup
├── appsettings.json              # Connection + scenario config
├── Simulator/
│   ├── SimulatorSession.cs       # Per-connection session: connect, send, receive
│   ├── SimulatorOptions.cs       # Connection parameters (host, port, TLS, timeout)
│   ├── SimulatorStats.cs         # Metrics: sent, received, errors, avg latency
│   └── ResponseMatcher.cs        # Correlates responses to requests by STAN
├── Builders/
│   ├── IMessageBuilder.cs        # Interface: SupportedMTIs + BuildRequest
│   ├── BaseRequestBuilder.cs     # For request/response MTIs (0100, 0200, 0400)
│   ├── BaseAdviceBuilder.cs     # For advice MTIs (0120, 0220, 0420)
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
│   ├── SignOnScenario.cs         # 0800 → 0810
│   ├── AuthorizationScenario.cs  # 0100 → 0110
│   ├── FinancialScenario.cs      # 0200 → 0210
│   ├── ReversalScenario.cs       # 0400 → 0410
│   ├── AdviceScenarios.cs        # 0120, 0220, 0420 (no response)
│   ├── FullLifecycleScenario.cs  # All flows in sequence
│   └── LoadTestScenario.cs       # Configurable parallel throughput test
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
public static async Task<int> Main(string[] args)
{
    // 1. Parse config
    // 2. Set up Serilog
    // 3. Load dialect → ISOMessagePackager
    // 4. DI: register all IMessageBuilder implementations
    // 5. DI: register all IScenario implementations
    // 6. Create SimulatorSession
    // 7. Connect to server
    // 8. Run ScenarioRunner
    // 9. Print results
    // 10. Exit with 0 (all pass) or 1 (any fail)
}
```

**Key behavior:**
- Exit code 0 = all scenarios pass (CI-friendly)
- Exit code 1 = one or more scenarios fail
- Exit code 2 = connection/configuration error
- `--scenario Auth` flag to run a single scenario
- `--scenario LoadTest --count 10000` for load testing

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

### Phase 1 — Foundation (connection + framing)
- [ ] Create `ISO8583Simulator.csproj`
- [ ] `SimulatorOptions` + `appsettings.json`
- [ ] `FrameReader` / `FrameWriter` (mirror server's framing)
- [ ] `SimulatorSession` — connect, read loop, write, disconnect
- [ ] `ResponseMatcher` — STAN-based correlation

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

### Phase 4 — Polish
- [ ] `Program.cs` with DI, config binding, exit codes
- [ ] CLI flags: `--scenario`, `--count`, `--host`, `--port`
- [ ] Pretty-printed results table
- [ ] `README.md` for the simulator

---

## Dependencies

| Package | Purpose |
|---------|---------|
| `ISO8583Net` (project reference) | ISOMessage, ISOMessagePackager, ISOUtils |
| `ISO8583Server` (project reference) | Shared pipeline types (optional — for TlsOptions etc.) |
| `Microsoft.Extensions.Hosting` | DI, config binding, logging |
| `Serilog.AspNetCore` | Structured logging |

No new external dependencies beyond what the solution already uses.

---

## Stretch Goals (Future)

- **Multi-connection load testing** — `SimulatorPool` managing N concurrent sessions
- **Replay mode** — read a JSON trace file and replay exact message sequences
- **Fuzzing mode** — send deliberately malformed messages to test error handling
- **gRPC control plane** — remote-control the simulator from a dashboard
- **BenchmarkDotNet integration** — measure P50/P99/P999 latency with proper statistical rigor
