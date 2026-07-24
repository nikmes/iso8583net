# ISO8583Simulator

An **ASP.NET Core hosted service** with a REST API and SignalR WebSocket hub that
connects to an [ISO8583Server](../src/ISO8583Server) instance and simulates client-initiated
ISO 8583 message flows.

## Quick Start

```bash
# 1. Start the ISO8583 server (in a separate terminal)
cd tools/ISO8583Service
dotnet run

# 2. Start the simulator
cd tools/ISO8583Simulator
dotnet run

# 3. Connect to the server
curl -X POST http://localhost:5100/api/simulator/connect \
  -H "Content-Type: application/json" \
  -d '{"host":"localhost","port":9443,"tlsEnabled":true,"tlsAllowUntrusted":true}'

# 4. Check status
curl http://localhost:5100/api/simulator/status

# 5. Run a scenario
curl -X POST http://localhost:5100/api/scenarios/run \
  -H "Content-Type: application/json" \
  -d '{"name":"Authorization"}'

# 6. List available scenarios
curl http://localhost:5100/api/scenarios
```

## Configuration

Edit `appsettings.json`:

```json
{
  "Simulator": {
    "Host": "localhost",
    "Port": 9443,
    "TlsEnabled": true,
    "TlsAllowUntrusted": true,
    "ConnectTimeoutSeconds": 10,
    "ResponseTimeoutSeconds": 30,
    "DialectPath": "Dialects/d8-iso8583.json",
    "Scenarios": ["SignOn", "Authorization", "Financial", "FullLifecycle"]
  }
}
```

## REST API

| Method | Path | Description |
|--------|------|-------------|
| `POST` | `/api/simulator/connect` | Connect to ISO8583Server |
| `POST` | `/api/simulator/disconnect` | Disconnect |
| `GET` | `/api/simulator/status` | Connection state + stats |
| `GET` | `/api/simulator/health` | Health check |
| `GET` | `/api/scenarios` | List scenarios |
| `POST` | `/api/scenarios/run` | Run a scenario by name |
| `POST` | `/api/scenarios/run-all` | Run all scenarios |
| `POST` | `/api/messages/send` | Send a message and await response |
| `POST` | `/api/messages/send-advice` | Fire-and-forget advice |
| `GET` | `/api/messages/recent` | Recent message history |
| `POST` | `/api/loadtest/start` | Start a load test |
| `POST` | `/api/loadtest/stop` | Stop running load test |
| `GET` | `/api/loadtest/status` | Load test progress |

## SignalR Hub

Real-time WebSocket streaming at `/hubs/simulator`:

| Event | Description |
|-------|-------------|
| `MessageSent` | Frame written to socket |
| `ResponseReceived` | Response unpacked from socket |
| `ErrorOccurred` | Parse error, timeout, handler error |
| `ScenarioProgress` | Each step in a scenario |
| `ScenarioCompleted` | Scenario finishes |
| `LoadTestProgress` | Load test progress (every 500ms) |
| `StateChanged` | Connect/disconnect transitions |
| `StatsUpdate` | Periodic stats (every 2s) |

## API Documentation

Interactive Scalar API docs at `/scalar/v1`.

## CLI Flags

```
--urls <url>    Override listening URL (default: http://localhost:5000)
                Example: --urls http://0.0.0.0:5100
```

## Scenarios

| Name | Flow | Description |
|------|------|-------------|
| `SignOn` | 0800 → 0810 | Network sign-on |
| `Echo` | 0800 → 0810 | Echo test |
| `Authorization` | 0100 → 0110 | Authorization request |
| `Financial` | 0200 → 0210 | Financial transaction |
| `Reversal` | 0400 → 0410 | Reversal |
| `AuthorizationAdvice` | 0120 | Authorization advice |
| `FinancialAdvice` | 0220 | Financial advice |
| `ReversalAdvice` | 0420 | Reversal advice |
| `FullLifecycle` | All flows seq. | Complete transaction cycle |
| `LoadTest` | Configurable | Parallel throughput test |

## Project Structure

```
ISO8583Simulator/
├── Program.cs                   # Entry point, DI, pipeline
├── appsettings.json             # Configuration
├── Simulator/
│   ├── SimulatorSession.cs      # Per-connection session
│   ├── SimulatorOptions.cs      # Connection parameters
│   ├── SimulatorStats.cs        # Metrics collection
│   ├── SimulatorState.cs        # State enum
│   ├── SimulatorHostedService.cs # Background service
│   └── ResponseMatcher.cs       # STAN-based correlation
├── Builders/                    # Message builders (per MTI)
├── Scenarios/                   # Scenario definitions
├── Controllers/                 # REST API controllers
├── Hubs/                        # SignalR hub
├── Models/                      # Request/response DTOs
└── Framing/
    ├── FrameReader.cs           # 2-byte LI frame reader
    └── FrameWriter.cs           # 2-byte LI frame writer
```
