# ISO8583Service

ASP.NET Core hosted service that runs an **ISO 8583 TCP server** alongside a **REST management API** — a single process for both financial message handling and operational control.

## Architecture

```mermaid
flowchart TB
    subgraph Service["ISO8583Service Process"]
        subgraph REST["REST API :5000"]
            status["GET /status"]
            signon["POST /signon"]
            signoff["POST /signoff"]
            echo["POST /echo"]
            config["PUT /config"]
        end
        subgraph TCP["ISO 8583 TCP :9443"]
            tls["TLS / mTLS"]
            periodic["Periodic SignOn"]
            msgs["Message Parsing"]
        end
        hosted["Iso8583HostedService<br/>IHostedService"]
        server["IIso8583Server<br/>Iso8583TcpServer"]
        REST --> hosted
        TCP --> hosted
        hosted --> server
    end
    server --> core["ISO8583Net<br/>Dialect Engine"]
```

The REST API and TCP server share the same `IIso8583Server` instance — API calls directly control the running server.

## Quick Start

```bash
cd tools/ISO8583Service
dotnet run
```

- **REST API:** http://localhost:5000
- **Scalar API Docs:** http://localhost:5000/scalar/v1
- **OpenAPI Spec:** http://localhost:5000/openapi/v1.json
- **ISO 8583 TCP:** port 9443 (configurable)

## Configuration

All settings in `appsettings.json`:

### HTTP Endpoint

```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://0.0.0.0:5000"
      }
    }
  }
}
```

### ISO 8583 Server

```json
{
  "Iso8583Server": {
    "Port": 9443,
    "DialectPath": "Dialects/d8-iso8583.json",
    "SignOnIntervalSeconds": 30,
    "SendSignOnOnConnect": true,
    "EnablePeriodicSignOn": true,
    "TlsEnabled": true,
    "TlsCertPath": "/etc/d8dh/certs/server.crt",
    "TlsKeyPath": "/etc/d8dh/certs/server.key",
    "TlsCaCertPath": "/etc/d8dh/certs/ca.pem",
    "TlsRequireClientCert": true
  }
}
```

| Setting | Default | Description |
|---------|---------|-------------|
| `Port` | `9090` | TCP port for ISO 8583 connections |
| `DialectPath` | `null` | Path to dialect JSON. `null` = embedded VISA dialect. Set to `"Dialects/d8-iso8583.json"` for D8 G2B |
| `SignOnIntervalSeconds` | `0` | Interval between periodic SignOns. `0` = disabled |
| `SendSignOnOnConnect` | `false` | Send SignOn immediately when client connects |
| `EnablePeriodicSignOn` | `false` | Enable periodic SignOn loop |
| `TlsEnabled` | `false` | Enable TLS encryption |
| `TlsCertPath` | — | Path to server certificate (`.crt`) |
| `TlsKeyPath` | — | Path to server private key (`.key`) |
| `TlsCaCertPath` | — | Path to CA certificate for client verification |
| `TlsRequireClientCert` | `false` | Require mTLS — clients must present valid cert |

### Logging

Configured via Serilog, with console and rolling file sinks:

```json
{
  "Serilog": {
    "MinimumLevel": "Information",
    "WriteTo": [
      { "Name": "Console" },
      {
        "Name": "File",
        "Args": {
          "path": "logs/iso8583-service-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 7
        }
      }
    ]
  }
}
```

## REST API

Base URL: `http://localhost:5000/api/iso8583`

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/status` | Server status, connected clients, current config |
| `POST` | `/signon` | Send SignOn (MTI 1800, F24=801) to all clients |
| `POST` | `/signoff?disconnect=true` | Send SignOff (MTI 1800, F24=803). `disconnect=true` stops the server |
| `POST` | `/echo` | Send Echo (MTI 1800, F24=831) to all clients |
| `PUT` | `/config` | Update `SignOnIntervalSeconds` and `EnablePeriodicSignOn` at runtime |

### Example Responses

**`GET /api/iso8583/status`**
```json
{
  "isRunning": true,
  "connectionCount": 3,
  "connectedClients": [
    {
      "connectionNumber": 1,
      "remoteEndpoint": "10.1.2.3:55421",
      "connectedAt": "2026-07-17T06:00:00.0000000Z"
    }
  ],
  "config": {
    "port": 9443,
    "dialectPath": "Dialects/d8-iso8583.json",
    "signOnIntervalSeconds": 30,
    "sendSignOnOnConnect": true,
    "enablePeriodicSignOn": true,
    "tlsEnabled": true
  }
}
```

**`POST /api/iso8583/signon`**
```json
{
  "message": "SignOn request sent to 3 client(s).",
  "clientsNotified": 3
}
```

**`PUT /api/iso8583/config`**
```json
// Request body
{
  "signOnIntervalSeconds": 60,
  "enablePeriodicSignOn": true
}

// Response
{
  "message": "Configuration updated.",
  "signOnIntervalSeconds": 60,
  "enablePeriodicSignOn": true
}
```

## Publishing & Deployment

### Publish (Linux x64)

```bash
dotnet publish tools/ISO8583Service/ISO8583Service.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained true \
  --output publish/
```

### Deploy with systemd

```bash
# Copy service unit
sudo cp deploy/iso8583service.service /etc/systemd/system/

# Edit paths in the unit file, then:
sudo systemctl daemon-reload
sudo systemctl enable iso8583service
sudo systemctl start iso8583service
```

### Files bundled in publish output

| File | Source | Purpose |
|------|--------|---------|
| `appsettings.json` | Project root | Runtime configuration |
| `Dialects/*.json` | `src/ISO8583Net/ISODialects/` | Dialect definitions |
| `deploy/deploy.sh` | `deploy/` | Deployment script (Linux) |
| `deploy/iso8583service.service` | `deploy/` | systemd unit file |

## Dialects

Choose the dialect via `Iso8583Server.DialectPath` in `appsettings.json`:

| Value | Effect |
|-------|--------|
| `null` or `""` | Embedded VISA BASE I dialect (default) |
| `"Dialects/d8-iso8583.json"` | D8 G2B ISO 8583:1993 |
| `"path/to/custom.json"` | Any custom dialect file |

Dialect JSON files are copied to `Dialects/` in the publish output automatically via the `.csproj` configuration.

## Project References

```mermaid
graph TD
    service["tools/ISO8583Service<br/>ASP.NET Core Host"]
    server["src/ISO8583Server<br/>TCP Server + TLS"]
    core["src/ISO8583Net<br/>Dialect Engine"]
    service --> server --> core
```

## Key Classes

| Class | Role |
|-------|------|
| `Program` | Application entry point, DI setup, pipeline configuration |
| `Iso8583HostedService` | `IHostedService` wrapper — starts/stops the TCP server with the app lifetime |
| `Iso8583Controller` | REST API controller — exposes management endpoints |
| `ServerOptions` | Strongly-typed config binding for `Iso8583Server` section |
| `ConfigUpdate` | DTO for `PUT /config` runtime updates |
