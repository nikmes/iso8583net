# ISO8583Simulator — Implementation Plan

> Derived from `ISO8583Simulator.md` | Target framework: `net10.0` | ASP.NET Core Web SDK  
> References: `src/ISO8583Net`, `src/ISO8583Server` | Pattern: mirrors existing `tools/ISO8583Service`

## Progress: 52/52 tasks done (100%) ✅ COMPLETE

| Sprint | Done | Total | Status |
|--------|------|-------|--------|
| Sprint 1 — Foundation | 10 | 10 | ✅ Complete |
| Sprint 2 — Message Builders | 13 | 13 | ✅ Complete |
| Sprint 3 — Scenarios | 12 | 12 | ✅ Complete |
| Sprint 4 — REST API | 9 | 9 | ✅ Complete |
| Sprint 5 — SignalR | 6 | 6 | ✅ Complete |
| Sprint 6 — Polish | 7 | 7 | ✅ Complete |

> **Note:** 10 cross-sprint zero-dependency tasks were implemented early alongside Sprint 1.  
> Code-review audit completed; 3 issues found and fixed (IOException catch, 2× CTS leak).  
> All 52 tasks across 6 sprints are now implemented. Build: 0 errors.

---

## Sprint 1 — Foundation: Project scaffold, connection, framing

**Goal:** Standalone ASP.NET Core project that can connect to an ISO8583Server, send a hand-crafted frame, and read responses.

| # | Task | Details |
|---|------|---------|
| 1.1 ✅ | Create project and register in solution | `tools/ISO8583Simulator/ISO8583Simulator.csproj` (Microsoft.NET.Sdk.Web, net10.0, OutputType=Exe). Add to `iso8583net.sln`. |
| 1.2 ✅ | `SimulatorOptions` + `appsettings.json` | POCO class `SimulatorOptions` with `SectionName = "Simulator"`. Bind Host, Port, TlsEnabled, TlsCertPath, TlsAllowUntrusted, ConnectTimeoutSeconds, ResponseTimeoutSeconds, DialectPath, Scenarios list. |
| 1.3 ✅ | `SimulatorState` enum | Values: `Disconnected`, `Connecting`, `Connected`, `Running`. |
| 1.4 ✅ | `SimulatorStats` | Thread-safe counters: `MessagesSent`, `ResponsesReceived`, `Errors`, and a latency histogram (50th/99th percentile + average). Use `ConcurrentDictionary` + lock-free where possible. |
| 1.5 ✅ | `FrameReader` | Reads 2-byte big-endian length prefix (`ReadExactlyAsync`), then body, returns `byte[]`. Runs as a background `Task` loop. |
| 1.6 ✅ | `FrameWriter` | Takes `ISOMessage`, calls `Pack()`, prepends 2-byte big-endian length prefix, writes to stream via `WriteAsync`. |
| 1.7 ✅ | `ResponseMatcher` | `ConcurrentDictionary<string, TaskCompletionSource<ISOMessage>>` keyed by STAN. Provides `RegisterAsync(stan, ct)` and `TryComplete(stan, response)`. Handles cancellation/timeout cleanup. |
| 1.8 ✅ | `SimulatorSession` | Core class: opens `TcpClient`, optionally wraps with `SslStream` (respecting `TlsAllowUntrusted`). Starts background `FrameReader` loop. Provides `ConnectAsync`, `DisconnectAsync`, `SendAsync(mti)`. Wires `ResponseMatcher`. |
| 1.9 ✅ | `SimulatorHostedService` | `BackgroundService` that manages `SimulatorSession` lifecycle. Handles graceful shutdown via `ExecuteAsync`. |
| 1.10 ✅ | `Program.cs` — basic DI wiring | Register `SimulatorOptions`, `ISOMessagePackager`, `SimulatorSession`, `SimulatorStats`, `ResponseMatcher`, `SimulatorHostedService`. Configure Serilog. |

**Exit criteria:** ✅ Project builds. Can connect to running ISO8583Server on port 9443. FrameReader/Writer round-trip works. ResponseMatcher correlates by STAN. *(Met — Sprint 1 complete)*

---

## Sprint 2 — Message Builders

**Goal:** Registry of `IMessageBuilder` classes that generate properly populated ISO 8583 messages for every MTI in the D8 G2B dialect.

| # | Task | Details |
|---|------|---------|
| 2.1 ✅ | `IMessageBuilder` interface | `IReadOnlySet<string> SupportedMTIs { get; }` + `void BuildRequest(ISOMessage message)`. |
| 2.2 ✅ | `BaseRequestBuilder` | Abstract base for request/response MTIs. Property `RequestMTI`. Virtual `BuildRequest(message)` sets F0=MTI, calls `FillMandatoryDefaults`. Includes `GenerateStan()` and `GenerateRrn()` static helpers. |
| 2.3 ✅ | `BaseAdviceBuilder` | Abstract base for advice MTIs. Property `AdviceMTI`. Same `FillMandatoryDefaults` as `BaseRequestBuilder`, plus sets `F39="400"`. |
| 2.4 ✅ | `DefaultFieldValues` static helper | Extract shared `FillMandatoryDefaults` into a static class so both `BaseRequestBuilder` and `BaseAdviceBuilder` reuse it. Default values for F2, F3, F4, F7, F11, F12, F22, F24, F26, F28, F32, F37, F41, F42, F49. |
| 2.5 ✅ | `NetworkManagementBuilder` | Supports MTI `"0800"`. Configurable `Function` enum: `SignOn` (F70="001"), `SignOff` (F70="002"), `Echo` (F70="301"). Sets F7, F11. |
| 2.6 ✅ | Concrete builders: `AuthorizationBuilder` (`0100`) | Extends `BaseRequestBuilder`. |
| 2.7 ✅ | Concrete builders: `AuthorizationAdviceBuilder` (`0120`) | Extends `BaseAdviceBuilder`. |
| 2.8 ✅ | Concrete builders: `FinancialBuilder` (`0200`) | Extends `BaseRequestBuilder`. |
| 2.9 ✅ | Concrete builders: `FinancialAdviceBuilder` (`0220`) | Extends `BaseAdviceBuilder`. |
| 2.10 ✅ | Concrete builders: `ReversalBuilder` (`0400`) | Extends `BaseRequestBuilder`. Overrides to set F90 (Original Data Elements). |
| 2.11 ✅ | Concrete builders: `ReversalAdviceBuilder` (`0420`) | Extends `BaseAdviceBuilder`. |
| 2.12 ✅ | `MessageBuilderRegistry` | `Dictionary<string, IMessageBuilder>`. Populated from DI via `IEnumerable<IMessageBuilder>`. `GetBuilder(mti)` lookup. `IsAdvice(mti)` check. |
| 2.13 ✅ | DI registration in `Program.cs` | Register all 7 builders as `IMessageBuilder` singletons. Register `MessageBuilderRegistry` as singleton. |

**Exit criteria:** ✅ Calling `session.SendAsync("0100")` builds a valid 0100 message, sends it, and awaits a 0110 response. All 7 MTIs produce valid messages. *(Met — Sprint 2 complete)*

---

## Sprint 3 — Scenarios

**Goal:** Composable, named scenarios that exercise message flows and assert responses.

| # | Task | Details |
|---|------|---------|
| 3.1 ✅ | `IScenario` interface | `string Name { get; }` + `Task<bool> RunAsync(SimulatorSession session, CancellationToken ct)`. |
| 3.2 ✅ | `ScenarioReport` / `ScenarioResult` | Report: Total, Passed, Failed, TotalDuration, `List<ScenarioResult>`. Result: ScenarioName, Passed, Duration, ErrorMessage. |
| 3.3 ✅ | `ScenarioRunner` | Takes `IEnumerable<IScenario>`. `RunAllAsync(session, ct)` → runs all, collects times, returns `ScenarioReport`. `RunAsync(name, session, ct)` → runs one by name. |
| 3.4 ✅ | `SignOnScenario` | Sends 0800 (SignOn) → expects 0810 with F39="000". |
| 3.5 ✅ | `EchoScenario` | Sends 0800 (Echo) → expects 0810 echo response. |
| 3.6 ✅ | `AuthorizationScenario` | Sends 0100 → expects 0110 with F39="000". |
| 3.7 ✅ | `FinancialScenario` | Sends 0200 → expects 0210 with F39="000". |
| 3.8 ✅ | `ReversalScenario` | Sends 0400 → expects 0410 with F39="000". |
| 3.9 ✅ | Advice scenarios (3) | `AuthorizationAdviceScenario` (0120), `FinancialAdviceScenario` (0220), `ReversalAdviceScenario` (0420). Fire-and-forget; assert no timeout. |
| 3.10 ✅ | `FullLifecycleScenario` | Runs in sequence: SignOn → Auth → Financial → Reversal → SignOff. All must pass. |
| 3.11 ✅ | `LoadTestScenario` | Configurable: N total requests, C concurrency, target MTI. Uses `SemaphoreSlim` or `Parallel.ForEachAsync`. Collects latency percentiles. Returns pass if P99 < configured threshold. |
| 3.12 ✅ | DI registration | Register all scenarios as `IScenario` singletons. Register `ScenarioRunner` as singleton. |

**Exit criteria:** ✅ `ScenarioRunner.RunAllAsync()` executes all scenarios, prints a summary table. FullLifecycle passes end-to-end. *(Met — Sprint 3 complete)*

---

## Sprint 4 — REST API

**Goal:** HTTP endpoints for programmatic control of the simulator — connect, send, run scenarios, load test.

| # | Task | Details |
|---|------|---------|
| 4.1 ✅ | DTO models | `ConnectRequest`, `SendMessageRequest`, `LoadTestRequest`, `SimulatorStatus`, `MessageTrace` — in `Models/` folder. |
| 4.2 ✅ | `SimulatorController` | `POST /api/simulator/connect` (accept `ConnectRequest`, return state), `POST /api/simulator/disconnect`, `GET /api/simulator/status` (state + stats), `GET /api/simulator/health` (200 OK). |
| 4.3 ✅ | `ScenarioController` | `GET /api/scenarios` (list names + descriptions), `POST /api/scenarios/run` (by name, returns result), `POST /api/scenarios/run-all` (runs all, returns report). Validate connected state. |
| 4.4 ✅ | `MessageController` | `POST /api/messages/send` (MTI + optional field overrides, timeout; returns response hex + timing), `POST /api/messages/send-advice` (fire-and-forget), `GET /api/messages/recent?count=50&mti=` (query message history from in-memory ring buffer). |
| 4.5 ✅ | `LoadTestController` | `POST /api/loadtest/start` (returns 202 with loadTestId), `POST /api/loadtest/stop`, `GET /api/loadtest/status` (progress + percentiles). |
| 4.6 ✅ | Message history ring buffer | Thread-safe in-memory store for `GET /api/messages/recent`. Fixed capacity (e.g. 10,000). |
| 4.7 ✅ | CORS configuration | Allow `http://localhost:5199` (Blazor WebUI default). |
| 4.8 ✅ | Scalar API docs | `MapScalarApiReference()` at `/scalar/v1`. |
| 4.9 ✅ | Controller DI wiring | `builder.Services.AddControllers()`, `MapControllers()` in pipeline. |

**Exit criteria:** ✅ All endpoints respond correctly via `curl`. Scenario run returns pass/fail. Load test can be started and polled for progress. *(Met — complete)*

---

## Sprint 5 — SignalR Hub + Real-time Streaming

**Goal:** WebSocket-based real-time event stream for UI consumption.

| # | Task | Details |
|---|------|---------|
| 5.1 ✅ | `SimulatorHub` class | Empty hub class at `/hubs/simulator`. Events are pushed from `SimulatorSession` via `IHubContext<SimulatorHub>`. |
| 5.2 ✅ | Wire events in `SimulatorSession` | Inject `IHubContext<SimulatorHub>`. Push `MessageSent` on frame write, `ResponseReceived` on frame read + match, `ErrorOccurred` on exceptions, `StateChanged` on connect/disconnect, `StatsUpdate` on periodic timer (2s). |
| 5.3 ✅ | Wire events in `ScenarioRunner` | Push `ScenarioProgress` (step N of M), `ScenarioCompleted` (name, passed, duration). |
| 5.4 ✅ | Wire events in `LoadTestScenario` | Push `LoadTestProgress` periodically (every 500ms) — sentCount, receivedCount, errors, avgMs. |
| 5.5 ✅ | SignalR + CORS registration | `builder.Services.AddSignalR()`, `app.MapHub<SimulatorHub>("/hubs/simulator")`. |
| 5.6 ✅ | `SimulatorHubClient` typed wrapper | Optional C# client class for Blazor consumption — typed events, `WithAutomaticReconnect()`. |

**Exit criteria:** ✅ Connect a SignalR JS client, see `MessageSent` events when sending via REST API. StatsUpdate fires every 2 seconds. *(Met — complete)*

---

## Sprint 6 — Polish & Integration

**Goal:** Production-ready quality, CI integration, documentation.

| # | Task | Details |
|---|------|---------|
| 6.1 ✅ | CLI flags | Support `--urls http://0.0.0.0:5100` for port override. |
| 6.2 ✅ | Graceful shutdown | `SimulatorHostedService.StopAsync` calls `DisconnectAsync`, drains pending `TaskCompletionSource` items via `ResponseMatcher.CancelAll()`. |
| 6.3 ✅ | Error handling audit | Ensure every `async Task` method has try/catch logging. Timeout handling in `SendAsync`. Socket disconnection detection + automatic state transition to `Disconnected`. |
| 6.4 ✅ | Integration smoke test | Shell script or test project that: starts ISO8583Service → starts Simulator → curl connect → curl status → curl run Authorization scenario → assert 200. |
| 6.5 ✅ | `README.md` for simulator | Usage instructions: how to run, available endpoints, configuration, example curl commands. |
| 6.6 ✅ | Docker Compose sample | `docker-compose.simulator.yml`: ISO8583Service + PostgreSQL + Simulator stack. |
| 6.7 ✅ | Solution file update | Add `ISO8583Simulator.csproj` to `iso8583net.sln`. Build configuration verified (Debug/Release). |

**Exit criteria:** ✅ Full stack runs in Docker. Smoke test passes. README is clear and complete. *(Met — complete)*

---

## Dependency Graph

```
Sprint 1 ──► Sprint 2 ──► Sprint 3 ──► Sprint 4 ──► Sprint 5 ──► Sprint 6
                                    \            /
                                     └──────────┘
                                  (3 & 4 are independent;
                                   4 depends on 3's ScenarioRunner)

SignalR (Sprint 5) needs Sprint 4's hub registration pattern but can be
developed in parallel with Sprint 4 once Sprint 3 is complete.
```

## Risk Items

| Risk | Mitigation |
|------|-----------|
| TLS certificate validation during dev | `TlsAllowUntrusted = true` bypass — documented as dev-only |
| STAN collision in high-concurrency load tests | STAN generator uses tick-based 6-digit; acceptable for single-session. Future: atomic increment |
| `ISOMessagePackager` requires dialect file path | Bundled via `<Content>` in csproj, copied from `src/ISO8583Net/ISODialects/` |
| SignalR reconnect storms in Blazor | `WithAutomaticReconnect()` with exponential backoff |
