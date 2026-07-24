# Copilot Instructions for ISO8583Net

## Build, Test, and Run Commands

```bash
# Build the entire solution
dotnet build iso8583net.sln

# Run all tests (xUnit)
dotnet test tests/ISO8583Net.Tests/ISO8583Tests.csproj

# Run a single test or filter
dotnet test tests/ISO8583Net.Tests/ISO8583Tests.csproj --filter "FullyQualifiedName~PipelineTests" --verbosity normal
dotnet test tests/ISO8583Net.Tests/ISO8583Tests.csproj --filter "FullyQualifiedName~IntegrationTests.FiveConnections"

# Run the main service (ASP.NET Core host, SEDA pipeline, REST API on :5000, ISO 8583 on :9443)
dotnet run --project tools/ISO8583Service/ISO8583Service.csproj

# Publish for Linux deployment (self-contained)
dotnet publish tools/ISO8583Service/ISO8583Service.csproj \
    --runtime linux-x64 --self-contained true --configuration Release \
    --output tools/ISO8583Service/publish/linux-x64

# Run benchmarks (BenchmarkDotNet)
dotnet run --project benchmarks/ISO8583Net.Benchmarks/ISO8583NetBenchmark.csproj -c Release
```

## Architecture Overview

This is a .NET 10.0 solution with four main areas:

| Area | Project | Purpose |
|------|---------|---------|
| **Core Library** | `src/ISO8583Net/` | ISO 8583 message building, packing, unpacking. JSON dialect configuration. Published as NuGet package `ISO8583Net`. |
| **Server Library** | `src/ISO8583Server/` | 5-stage SEDA pipeline per TCP connection. Handler framework (`IMessageHandler`). |
| **Hosted Service** | `tools/ISO8583Service/` | ASP.NET Core app that wires everything together: TCP server, handlers, REST API (`/status`, `/health`), message tracing. |
| **Simulator** | `tools/ISO8583Simulator/` | Standalone tool for simulating ISO 8583 client scenarios. |

### SEDA Pipeline (per TCP connection)

```
Reader(socket) → [RawMessage] → Parser(UnPack) → [ParsedMessage] → Dispatcher(route by MTI) → Handlers(business logic) → [OutboundMessage] → Writer(socket)
```

Each stage is an independent async task connected by bounded `System.Threading.Channels`. This decouples socket I/O from parsing from business logic — messages can be in-flight in parallel across multiple stages. Backpressure is applied via `Wait` mode on bounded channels (not drop).

### Handler Framework

The extension point for business logic. Handlers are registered in DI as `IMessageHandler` singletons:

```csharp
builder.Services.AddSingleton<IMessageHandler, MyHandler>();
```

**Base classes (in `ISO8583Net.Server.Pipeline.Handlers`):**

- **`BaseRequestHandler`** — For request/response pairs (e.g., 1100→1110). Override `RequestMTI`, `ResponseMTI`, and `ProcessAsync()`. Automatically copies standard response fields (F2, F3, F4, F7, F11, F12, F22, F32, F37, F41, F42, F49) and sets F39 action code.
- **`BaseAdviceHandler`** — For fire-and-forget advices (e.g., 1120→1130). Override `AdviceMTI`, `ResponseMTI`, and `OnAcknowledgedAsync()`.
- **`NetworkManagementHandler`** — For 0800 (Echo/SignOn/SignOff). Override `HandleLogonAsync`, `HandleEchoAsync`, etc.
- **`DefaultHandler`** — Catch-all (MTI `*`). Auto-echoes 1800 messages; passes through others.

**Handlers do NOT touch sockets or framing.** They receive an `ISOMessage` via `MessageContext` and return an `ISOMessage` response (or null to skip). The pipeline handles all I/O.

### Dialect System

Field layouts are defined in JSON files with `$type` discriminators:
- `"simple"` — standard flat field
- `"bitmap"` — bitmap field (field 1)
- `"bitmapSubFields"` — bitmap-driven sub-fields with their own nested bitmaps

Built-in dialects: `visa.json` (VISA BASE I, embedded resource, the default), `d8-iso8583.json` (D8 G2B ISO 8583:1993).

Load a custom dialect: `new ISOMessagePackager(logger, "path/to/dialect.json")`

## Key Conventions

### Namespaces

Core library uses traditional block-scoped namespaces:
- `ISO8583Net.Packager` — message packagers, dialect loader
- `ISO8583Net.Message` — `ISOMessage` (main public API)
- `ISO8583Net.Field` — field types: `ISOComponent`, `ISOFieldBitmap`, `BerTlv`
- `ISO8583Net.Header` — `ISOHeaderD8`, `ISOHeaderVisa`
- `ISO8583Net.Types` — `ISOContentTypes`, `ISOFieldPadding`, `ISOFieldType`
- `ISO8583Net.Utilities` — high-speed hex/BCD/EBCDIC converters
- `ISO8583Net.Interpreter` — indexed-value field interpreters

Server library uses file-scoped namespaces (`namespace X;`):
- `ISO8583Net.Server` — `IIso8583Server`, `Iso8583TcpServer`, `ServerOptions`
- `ISO8583Net.Server.Pipeline` — SEDA stages, `PipelineHost`, `ConnectionPipeline`, `PipelineOptions`
- `ISO8583Net.Server.Pipeline.Handlers` — `IMessageHandler` interface + base classes + `HandlerRegistry`
- `ISO8583Net.Server.Pipeline.Messages` — `MessageContext`, `RawMessage`, `ParsedMessage`, `OutboundMessage`, `IMessageTracer`

Service project:
- `ISO8583Service.Handlers` — concrete handler implementations
- `ISO8583Service.Tracing` — `FileMessageTracer`, `EfMessageTracer`

### Handler Development

- Handlers are registered in `tools/ISO8583Service/Program.cs` via `builder.Services.AddSingleton<IMessageHandler, XxxHandler>()`.
- The `HandlerRegistry` (registered as singleton) discovers all `IMessageHandler` implementations from DI and routes by MTI.
- **No ordering guarantee** between messages on the same connection — each ISO 8583 message is self-contained (identified by STAN F11 + date F7). If ordering is needed, make the handler single-threaded.
- Handlers accept `ILogger<T>?` via constructor injection (nullable, defaults to `NullLogger.Instance`).
- Use `ISOMessage.Set(fieldNumber, stringValue)` to set fields and `ISOMessage.GetFieldValue(fieldNumber)` to read them. All values are strings.

### Configuration

The service binds two config sections:
- `Iso8583Pipeline` → `PipelineOptions` (channel capacities, parser concurrency, drain timeout, circuit breaker)
- `Iso8583Server` → `ServerOptions` (port, dialect path, TLS settings, SignOn interval)

### Message Tracing

Two built-in tracers selected by config (`MessageTrace:Provider`):
- `FileMessageTracer` — Serilog-based file logging (default)
- `EfMessageTracer` — PostgreSQL via EF Core (requires `MessageTrace:Enabled=true` and a connection string)

The default is `NoopMessageTracer` which JIT-eliminates to zero overhead.

### Performance Patterns

- `ISOMessage.PackPooled()` uses `ArrayPool<byte>.Shared` for reduced allocations on hot paths.
- `ISOUtils` contains high-speed span-based converters (hex, BCD, EBCDIC) — use these, not manual conversion.
- Benchmarks use BenchmarkDotNet in `benchmarks/ISO8583Net.Benchmarks/`.
