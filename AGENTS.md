# AGENTS.md — ISO8583Net

## Build & Test

```bash
dotnet build iso8583net.sln
dotnet test tests/ISO8583Net.Tests/ISO8583Tests.csproj
dotnet test tests/ISO8583Net.Tests/ISO8583Tests.csproj --filter "FullyQualifiedName~PipelineTests"
dotnet run --project tools/ISO8583Service/ISO8583Service.csproj
dotnet publish tools/ISO8583Service/ISO8583Service.csproj --runtime linux-x64 --self-contained true -c Release
```

Tests use **xUnit**. All projects target **.NET 10.0**.

## Architecture

| Project | Role |
|---------|------|
| `src/ISO8583Net/` | Core library — ISO 8583 message build/pack/unpack, JSON dialects. NuGet `ISO8583Net`. |
| `src/ISO8583Server/` | Server library — 5-stage SEDA pipeline, `IMessageHandler` framework, TCP server. |
| `tools/ISO8583Service/` | ASP.NET Core hosted service — wires everything together: pipeline, handlers, REST API, tracing. |
| `tools/ISO8583Simulator/` | Standalone ISO 8583 client simulator. |

### SEDA Pipeline (per TCP connection)

```
Reader → [RawMessage] → Parser → [ParsedMessage] → Dispatcher → Handlers → [OutboundMessage] → Writer
```

Each stage is an independent async task connected by bounded `System.Threading.Channels`. Messages flow in parallel — no head-of-line blocking. Backpressure via `Wait` mode on full channels. Pipeline config is in `Iso8583Pipeline` section of `appsettings.json`.

### Handler Framework

All business logic lives in handlers implementing `IMessageHandler`. Registered as DI singletons in `tools/ISO8583Service/Program.cs`:

```csharp
builder.Services.AddSingleton<IMessageHandler, MyHandler>();
```

**Base classes** (namespace `ISO8583Net.Server.Pipeline.Handlers`):

- `BaseRequestHandler` — request/response pairs (MTIs like 1100→1110). Override `RequestMTI`, `ResponseMTI`, `ProcessAsync()`. Auto-copies F2/F3/F4/F7/F11/F12/F22/F32/F37/F41/F42/F49 and sets F39 action code.
- `BaseAdviceHandler` — fire-and-forget advices (e.g. 1120→1130). Override `AdviceMTI`, `ResponseMTI`, `OnAcknowledgedAsync()`.
- `NetworkManagementHandler` — 0800 Echo/SignOn/SignOff.
- `DefaultHandler` — catch-all (MTI `*`). Echoes 1800, passes through unknowns.

Handlers receive `MessageContext` (has `.Request` ISOMessage, `.ConnectionNumber`, `.RemoteEndpoint`, `.ReceivedAt`) and return an `ISOMessage` response or null. **Never touch sockets or framing.**

**No response ordering guarantee.** Each ISO 8583 message is self-contained via STAN (F11) + date (F7). If ordering matters for an MTI, use a single-threaded handler.

### Dialect System

Field layouts in JSON with `$type`: `"simple"`, `"bitmap"`, `"bitmapSubFields"`. Built-in: `visa.json` (default, embedded) and `d8-iso8583.json` (D8 G2B). Load custom: `new ISOMessagePackager(logger, "path/to/dialect.json")`.

## Namespaces

**Core lib** (block-scoped `namespace X { }`):
- `ISO8583Net.Packager` — `ISOMessagePackager`, dialect loader
- `ISO8583Net.Message` — `ISOMessage` (main API)
- `ISO8583Net.Field` — `ISOComponent`, `ISOFieldBitmap`, `BerTlv`
- `ISO8583Net.Header` — `ISOHeaderD8`, `ISOHeaderVisa`
- `ISO8583Net.Types` — enums (`ISOContentTypes`, `ISOFieldPadding`)
- `ISO8583Net.Utilities` — `ISOUtils` (hex/BCD/EBCDIC converters)
- `ISO8583Net.Interpreter` — field interpreters

**Server lib** (file-scoped `namespace X;`):
- `ISO8583Net.Server` — `IIso8583Server`, `Iso8583TcpServer`, `ServerOptions`
- `ISO8583Net.Server.Pipeline` — stages, `PipelineHost`, `PipelineOptions`
- `ISO8583Net.Server.Pipeline.Handlers` — `IMessageHandler`, base classes, `HandlerRegistry`
- `ISO8583Net.Server.Pipeline.Messages` — `MessageContext`, `RawMessage`, `ParsedMessage`, `OutboundMessage`, `IMessageTracer`

**Service project** (file-scoped):
- `ISO8583Service.Handlers` — concrete handlers
- `ISO8583Service.Tracing` — `FileMessageTracer`, `EfMessageTracer`

## Key Conventions

- Fields accessed via `ISOMessage.Set(fieldNumber, stringValue)` / `.GetFieldValue(fieldNumber)`. All values are strings.
- Handlers take `ILogger<T>?` (nullable, defaults to `NullLogger.Instance`).
- `ISOMessage.PackPooled()` uses `ArrayPool<byte>.Shared` for hot paths.
- Message tracing: `FileMessageTracer` (Serilog, default) or `EfMessageTracer` (PostgreSQL). Default `NoopMessageTracer` JIT-eliminates.
- Config sections: `Iso8583Pipeline` → `PipelineOptions`, `Iso8583Server` → `ServerOptions`.
- Deploy: `./deploy/deploy.sh <user@server>` publishes self-contained linux-x64, rsyncs, installs systemd unit.
