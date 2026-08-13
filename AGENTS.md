<!-- AGENTS.md — ISO8583Net
This file is intended for AI coding agents. It describes the project structure,
build/test commands, architecture, conventions, and security considerations.
-->

# ISO8583Net — Agent Guide

## Project Overview

ISO8583Net is a .NET 10.0 solution for building, parsing, and serving **ISO 8583** financial transaction messages. It consists of:

| Area | Project | Purpose |
|------|---------|---------|
| Core library | `src/ISO8583Net/` | Message pack/unpack, JSON dialect engine, bitmaps, BER-TLV, encodings, headers. Published as NuGet package `ISO8583Net`. |
| Server library | `src/ISO8583Server/` | Async TCP server and a 5-stage SEDA pipeline per connection. |
| Hosted service | `tools/ISO8583Service/` | ASP.NET Core host that wires the TCP server, handlers, REST API, health checks, and message tracing together. |
| Simulator | `tools/ISO8583Simulator/` | Standalone client simulator for ISO 8583 scenarios. |
| Samples | `samples/` | Console and WinForms demo/test clients. |
| Benchmarks | `benchmarks/ISO8583Net.Benchmarks/` | BenchmarkDotNet suite. |

Version: **2.0.0** (set in `src/ISO8583Net/ISO8583Net.csproj`).

The natural language used in comments and documentation is **English**.

---

## Build & Test Commands

All commands run from the repository root (`/home/ecommbx/iso8583net`).

```bash
# Build the entire solution
dotnet build iso8583net.sln

# Run all xUnit tests (23 tests as of this writing)
dotnet test tests/ISO8583Net.Tests/ISO8583Tests.csproj

# Run a filtered subset
dotnet test tests/ISO8583Net.Tests/ISO8583Tests.csproj --filter "FullyQualifiedName~PipelineTests" --verbosity normal
dotnet test tests/ISO8583Net.Tests/ISO8583Tests.csproj --filter "FullyQualifiedName~IntegrationTests.FiveConnections"

# Run the hosted service (REST API on :5000, ISO 8583 TCP on :9443 by default)
dotnet run --project tools/ISO8583Service/ISO8583Service.csproj

# Run benchmarks
dotnet run --project benchmarks/ISO8583Net.Benchmarks/ISO8583NetBenchmark.csproj -c Release

# Publish the service for Linux deployment (self-contained)
dotnet publish tools/ISO8583Service/ISO8583Service.csproj \
    --runtime linux-x64 \
    --self-contained true \
    --configuration Release \
    --output tools/ISO8583Service/publish/linux-x64
```

CI (`.github/workflows/build.yml`) runs on `windows-latest` with .NET 10.0.x:
`dotnet restore` → `dotnet build --configuration Release --no-restore` → `dotnet test --configuration Release --no-build`.

---

## Technology Stack

- **Runtime / SDK:** .NET 10.0
- **Test framework:** xUnit (`Microsoft.NET.Test.Sdk`, `xunit`, `xunit.runner.visualstudio`, `coverlet.collector`)
- **Web framework:** ASP.NET Core (`tools/ISO8583Service/`, `tools/ISO8583Simulator/`)
- **OpenAPI / docs:** `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore`
- **Logging:** Serilog (`Serilog.AspNetCore`)
- **Database tracing (optional):** Entity Framework Core + Npgsql (`Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL`)
- **Benchmarks:** BenchmarkDotNet
- **Deployment target:** Linux x64 self-contained executable managed by systemd

---

## Solution Structure

```
iso8583net/
├── src/
│   ├── ISO8583Net/              # Core library
│   │   ├── ISOMessage/          # ISOMessage public API
│   │   ├── ISOPackager/         # JSON dialect loader, packagers
│   │   ├── ISOField/            # Field types (simple, bitmap, sub-fields, BER-TLV)
│   │   ├── ISOHeader/           # VISA & D8 message headers
│   │   ├── ISOInterpreter/      # Indexed-value interpreters
│   │   ├── ISOEnums/            # Encoding, padding, content-type enums
│   │   ├── ISOUtils/            # Hex/BCD/EBCDIC converters
│   │   └── ISODialects/         # Embedded dialect JSON files
│   └── ISO8583Server/           # TCP server + SEDA pipeline library
│       ├── Pipeline/            # Stage implementations + options + stats
│       │   ├── Handlers/        # IMessageHandler + base classes + HandlerRegistry
│       │   └── Messages/        # RawMessage, ParsedMessage, OutboundMessage, MessageContext, IMessageTracer
│       ├── Iso8583TcpServer.cs
│       ├── IIso8583Server.cs
│       ├── TlsOptions.cs
│       └── PeriodicSignOnService.cs
├── tests/
│   └── ISO8583Net.Tests/        # xUnit suite
├── tools/
│   ├── ISO8583Service/          # ASP.NET Core hosted service
│   │   ├── Handlers/            # Concrete handlers
│   │   ├── Tracing/             # FileMessageTracer, EfMessageTracer, DbContext
│   │   ├── HealthChecks/        # PipelineHealthCheck
│   │   ├── Controllers/         # Iso8583Controller (REST API)
│   │   ├── Program.cs
│   │   ├── Iso8583HostedService.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Local.json
│   └── ISO8583Simulator/        # Client simulator
│       ├── appsettings.json
│       └── docker-compose.simulator.yml
├── samples/
│   ├── SimpleTest/
│   ├── TestClient/              # WinForms
│   ├── TestServer/              # WinForms
│   └── HexParser/
├── benchmarks/
│   └── ISO8583Net.Benchmarks/
├── deploy/
│   ├── deploy.sh
│   └── iso8583service.service
└── docs/
    ├── handler-development-guide.md
    └── specs/
```

---

## Architecture

### Core Library (`src/ISO8583Net/`)

The core library knows nothing about sockets or pipelines. It exposes:

- `ISOMessage` — create, populate, pack, unpack.
- `ISOMessagePackager` — loads a JSON dialect.
- Built-in dialects:
  - `visa.json` — VISA BASE I, embedded resource, default, 22-byte header, supports MTIs such as `0100`/`0110`.
  - `d8-iso8583.json` — D8 G2B ISO 8583:1993, 21-byte ASCII header, MTIs such as `1100`/`1110`.
- Encodings: `BCD`, `BCDU`, `ASCII`, `EBCDIC`, `BIN`, `Z`.
- Field types via `$type`: `"simple"`, `"bitmap"`, `"bitmapSubFields"`.
- BER-TLV parser for EMV data (field 55).

Load a dialect:

```csharp
var packager = new ISOMessagePackager(logger, "path/to/dialect.json");
var msg = new ISOMessage(logger, packager);
```

Use the embedded VISA dialect:

```csharp
var packager = new ISOMessagePackager(logger);
```

### SEDA Pipeline (`src/ISO8583Server/`)

Each TCP connection gets its own independent 5-stage pipeline connected by bounded `System.Threading.Channels`:

```
Reader → [RawMessage] → Parser → [ParsedMessage] → Dispatcher → Handlers → [OutboundMessage] → Writer
```

- **ReaderStage** — reads 2-byte big-endian length-prefixed frames from the socket.
- **ParserStage** — unpacks bytes into `ISOMessage` (can run with `ParserConcurrency` > 1).
- **DispatcherStage** — looks up handlers by MTI and invokes them in parallel.
- **Handlers** — business logic; write responses back through `MessageContext.SendResponseAsync` or return an `ISOMessage`.
- **WriterStage** — packs responses and writes framed bytes to the socket.

Key classes:

- `PipelineHost` — singleton; owns all per-connection `ConnectionPipeline` instances.
- `ConnectionPipeline` — creates channels and starts/stops the five stages.
- `HandlerRegistry` — builds an MTI → handlers map from all registered `IMessageHandler` instances at startup.
- `PipelineStats` — thread-safe per-connection metrics.

Backpressure is applied via `BoundedChannelFullMode.Wait`. A parser circuit breaker can pause the reader after repeated parse errors.

### Wire Protocol (D8 G2B default)

- Framing: **2-byte big-endian length prefix**; length excludes the prefix itself. Max payload is effectively bounded by implementation.
- A length prefix of `0x0000` is a keepalive heartbeat and is silently ignored.
- D8 header: 21-byte ASCII (`G2B-ISO-1.00` + source + version + error field + reserved).

### Hosted Service (`tools/ISO8583Service/`)

An ASP.NET Core application that starts `Iso8583TcpServer` as an `IHostedService` and exposes a management REST API on the Kestrel port (`http://0.0.0.0:5000` by default).

REST endpoints (`/api/iso8583`):

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/status` | Server status, connected clients, pipeline stats, current config |
| `POST` | `/signon` | Send MTI 1800/F24=801 SignOn to all connected clients |
| `POST` | `/signoff?disconnect=true` | Send MTI 1800/F24=803 SignOff; optional server stop |
| `POST` | `/echo` | Send MTI 1800/F24=831 Echo to all connected clients |
| `PUT` | `/config` | Update `SignOnIntervalSeconds` and `EnablePeriodicSignOn` at runtime |

Additional endpoints:

- `GET /health` — ASP.NET Core health check (uses `PipelineHealthCheck`).
- `GET /openapi/v1.json` — OpenAPI spec.
- `GET /scalar/v1` — Scalar API reference UI.

---

## Handler Framework

All business logic lives in handlers implementing `IMessageHandler`. Register them in `tools/ISO8583Service/Program.cs`:

```csharp
builder.Services.AddSingleton<IMessageHandler, MyHandler>();
```

The `HandlerRegistry` discovers every `IMessageHandler` from DI and routes messages by MTI. Catch-all handlers (`SupportedMTIs` contains `"*"`) receive every message.

Base classes (`namespace ISO8583Net.Server.Pipeline.Handlers`):

| Base class | Use case | Key overrides |
|------------|----------|---------------|
| `BaseRequestHandler` | Request/response pairs | `RequestMTI`, `ResponseMTI`, `ProcessAsync()` returning `ProcessResult` |
| `BaseAdviceHandler` | Fire-and-forget advice acknowledgements | `AdviceMTI`, `ResponseMTI`, `OnAcknowledgedAsync()` |
| `NetworkManagementHandler` | Network management (MTI 1804) | `HandleLogonAsync`, `HandleLogoffAsync`, `HandleKeyChangeAsync`, `HandleEchoAsync` |
| `DefaultHandler` | Catch-all (already registered) | Handles `1800` echo to `1814` and `1810` passthrough |

Built-in service handlers (`tools/ISO8583Service/Handlers/`) and their MTIs:

| Handler | Request / Advice MTI | Response MTI | Base class |
|---------|---------------------|--------------|------------|
| `AuthorizationHandler` | `1100` | `1110` | `BaseRequestHandler` |
| `AuthorizationAdviceHandler` | `1120` | `1130` | `BaseAdviceHandler` |
| `FinancialHandler` | `1200` | `1210` | `BaseRequestHandler` |
| `FinancialAdviceHandler` | `1220` | `1230` | `BaseAdviceHandler` |
| `ReversalHandler` | `1400` | `1410` | `BaseRequestHandler` |
| `ReversalAdviceHandler` | `1420` | `1430` | `BaseAdviceHandler` |
| `NetworkManagementHandler` | `1804` | `1814` | `IMessageHandler` |
| `DefaultHandler` | `*` | — | `IMessageHandler` |

`BaseRequestHandler.ProcessResult` helpers:

- `ProcessResult.Approved(approvalCode)` → F39 = `000`
- `ProcessResult.Declined(approvalCode)` → F39 = `100`
- `ProcessResult.FormatError()` → F39 = `902`

`BaseRequestHandler.BuildResponse` creates a clean response via `request.CreateCleanResponse()` and copies: F2, F3, F4, F7, F11, F12, F22, F32, F37, F41, F42, F49. Override `BuildResponse` if you need additional fields.

`BaseAdviceHandler.BuildAcknowledgement` **mutates the request in place**, setting the response MTI and F39 = `400`.

Handlers receive `MessageContext` with:

- `Request` — the incoming `ISOMessage`
- `ConnectionNumber`
- `RemoteEndpoint`
- `ReceivedAt`
- `SendResponseAsync(response)` / `SendRawResponseAsync(preFramedBytes)`

**Handlers must never touch sockets or framing.**

**No response ordering guarantee** is provided for messages on the same connection. Each ISO 8583 message is self-contained via STAN (F11) + date (F7). If ordering matters, use a single-threaded handler.

---

## Configuration

Configuration lives in `tools/ISO8583Service/appsettings.json` (copied to output).

### `Iso8583Server` → `ServerOptions`

| Setting | Default | Description |
|---------|---------|-------------|
| `Port` | `9090` | ISO 8583 TCP port |
| `DialectPath` | `null` | Path to dialect JSON; `null` or empty uses embedded VISA dialect |
| `SignOnIntervalSeconds` | `0` | Periodic SignOn interval; `0` disables |
| `SendSignOnOnConnect` | `false` | Send SignOn when a client connects |
| `EnablePeriodicSignOn` | `false` | Enable periodic `PeriodicSignOnService` loop |
| `TlsEnabled` | `false` | Enable TLS |
| `TlsCertPath` | — | Server certificate PEM path |
| `TlsKeyPath` | — | Server private key PEM path |
| `TlsCaCertPath` | — | CA certificate for client cert validation |
| `TlsRequireClientCert` | `false` | Require mTLS |

### `Iso8583Pipeline` → `PipelineOptions`

| Setting | Default | Description |
|---------|---------|-------------|
| `ParserConcurrency` | `1` | Number of parallel parser tasks per connection |
| `RawMessageCapacity` | `256` | Reader → parser channel capacity |
| `ParsedMessageCapacity` | `512` | Parser → dispatcher channel capacity |
| `OutboundMessageCapacity` | `256` | Handlers → writer channel capacity |
| `DrainTimeoutSeconds` | `30` | Graceful shutdown drain timeout |
| `MaxParseErrorsBeforePause` | `0` | Circuit-breaker threshold; `0` disables |
| `ParserCooldownSeconds` | `5` | Cooldown after circuit breaker trips |

### Message tracing

```json
{
  "MessageTrace": {
    "Enabled": false,
    "Provider": "PostgreSQL"
  },
  "ConnectionStrings": {
    "MessageTraceDb": "Host=localhost;Database=iso8583_traces;..."
  }
}
```

- `Provider: "PostgreSQL"` with `Enabled: true` registers `EfMessageTracer` and requires the `MessageTraceDb` connection string.
- Otherwise `FileMessageTracer` is registered by default.
- `NoopMessageTracer` is the fallback when nothing is registered and is JIT-eliminated.

---

## Namespaces & Code Style

### Namespace style

- **Core library** (`src/ISO8583Net/`): traditional **block-scoped** namespaces (`namespace X { }`).
- **Server library** (`src/ISO8583Server/`) and **service project** (`tools/ISO8583Service/`): **file-scoped** namespaces (`namespace X;`).

### Namespaces

Core library:

- `ISO8583Net.Packager` — `ISOMessagePackager`, dialect loader
- `ISO8583Net.Message` — `ISOMessage`
- `ISO8583Net.Field` — `ISOComponent`, `ISOFieldBitmap`, `BerTlv`, etc.
- `ISO8583Net.Header` — `ISOHeaderD8`, `ISOHeaderVisa`
- `ISO8583Net.Types` — `ISOContentTypes`, `ISOFieldPadding`, `ISOFieldType`
- `ISO8583Net.Utilities` — `ISOUtils`
- `ISO8583Net.Interpreter` — field interpreters

Server library:

- `ISO8583Net.Server` — `IIso8583Server`, `Iso8583TcpServer`, `TlsOptions`
- `ISO8583Net.Server.Pipeline` — stages, `PipelineHost`, `ConnectionPipeline`, options, stats
- `ISO8583Net.Server.Pipeline.Handlers` — `IMessageHandler`, base classes, `HandlerRegistry`
- `ISO8583Net.Server.Pipeline.Messages` — message context, tracer interface, envelope types

Service project:

- `ISO8583Service.Handlers`
- `ISO8583Service.Tracing`
- `ISO8583Service.HealthChecks`
- `ISO8583Service.Controllers`

### Coding conventions

- Fields are set/read as strings: `msg.Set(fieldNumber, stringValue)` / `msg.GetFieldValue(fieldNumber)`. Sub-fields: `msg.Set(fieldNumber, subFieldNumber, value)`.
- Handlers take `ILogger<T>?` (nullable, defaults to `NullLogger.Instance`).
- `ISOMessage.PackPooled()` uses `ArrayPool<byte>.Shared` for reduced allocations.
- Nullable reference annotations appear in some files but the project does **not** globally enable nullable (`<Nullable>enable</Nullable>` is not set in most projects), so builds currently emit `CS8632` warnings. The simulator project enables nullable globally.
- Use `ISOUtils` for hex/BCD/EBCDIC conversion rather than manual conversion.

---

## Testing Strategy

- Framework: **xUnit**.
- Test project: `tests/ISO8583Net.Tests/ISO8583Tests.csproj`.
- Test files:
  - `BitmapTests.cs`
  - `UtilTests.cs`
  - `PipelineTests.cs`
  - `IntegrationTests.cs`
- Integration tests use in-memory streams (`PassthroughStream`, `SplitStream`) to simulate bidirectional sockets without real TCP.
- Tests construct `PipelineHost` directly with a `HandlerRegistry` and `NullLoggerFactory`.
- A custom `NullTestLogger` / `NullTestLogger<T>` is defined in the test files.
- Verified: **23 tests pass** with `dotnet test tests/ISO8583Net.Tests/ISO8583Tests.csproj`.

---

## Deployment

A convenience script publishes a self-contained `linux-x64` build and installs a systemd unit:

```bash
./deploy/deploy.sh <user@server>
```

What it does:

1. Publishes `tools/ISO8583Service/ISO8583Service.csproj` to `tools/ISO8583Service/publish/linux-x64`.
2. rsyncs the publish output to `/home/ecbxuser/linux-x64` on the remote host.
3. Copies `deploy/iso8583service.service` to `/etc/systemd/system/`.
4. Runs `systemctl daemon-reload`, `enable`, and `restart`.

`deploy/iso8583service.service`:

- Runs as `ecbxuser:ecbxuser`
- Working directory `/home/ecbxuser/linux-x64`
- `ExecStart=/home/ecbxuser/linux-x64/ISO8583Service`
- `Restart=always`
- Sets `DOTNET_ENVIRONMENT=Production` and `ASPNETCORE_URLS=http://0.0.0.0:5000`
- Applies basic systemd hardening (`NoNewPrivileges=yes`, `PrivateTmp=yes`)

Default exposed ports after deploy:

- ISO 8583 TCP: **9443** (configurable in `appsettings.json`)
- REST API: **5000** (configurable via `ASPNETCORE_URLS` / Kestrel config)

---

## Security Considerations

- **Hard-coded certificate paths** exist in `appsettings.json` (`/etc/d8dh/certs/...`). These are environment-specific and must be replaced for production.
- **TLS/mTLS** is supported via `TlsEnabled`, `TlsCertPath`, `TlsKeyPath`, `TlsCaCertPath`, and `TlsRequireClientCert`.
- **Message tracing to PostgreSQL** requires a connection string with credentials. Do not commit production credentials.
- The service runs as a non-root user (`ecbxuser`) in the provided systemd unit.
- The deploy script uses `ssh` + `sudo` on the remote host; ensure the target user has appropriate privileges.
- No authentication or authorization layer is implemented on the REST API by default; protect it with a reverse proxy or network firewall if exposed.
- **Known dependency vulnerability:** `Microsoft.OpenApi` 2.0.0 is flagged by NuGet audit with advisory GHSA-v5pm-xwqc-g5wc. Consider upgrading if the project accepts dependency changes.
- **Windows-only samples:** `samples/TestClient/`, `samples/TestServer/`, and `samples/HexParser/` target WinForms and cannot build on Linux.

---

## Useful References

- `README.md` — high-level feature list and quick-start examples.
- `docs/handler-development-guide.md` — complete handler developer guide.
- `docs/arch-design.md` — SEDA pipeline architecture proposal.
- `docs/impl-sprints.md` — implementation sprint log.
- `tools/ISO8583Service/README.md` — service-specific setup and REST API docs.
- `ISO8583Simulator.md` / `ISO8583Simulator-Plan.md` — simulator design.
