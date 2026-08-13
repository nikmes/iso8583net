# ISO8583HexParserCli (`hexparse`)

A small cross-platform command-line tool for parsing and inspecting raw ISO 8583
message bytes against a JSON dialect definition. It is the CLI successor to the
legacy WinForms sample in `samples/HexParser/`.

## Usage

```
hexparse <dialect.json> <hexdump>
```

- `dialect.json` — full path to an ISO 8583 dialect file (e.g. `src/ISO8583Net/ISODialects/d8-iso8583.json`).
- `hexdump` — hex string with a 2-byte big-endian length indicator prefix (the D8 framing).

### Example

```
hexparse src/ISO8583Net/ISODialects/d8-iso8583.json 002949534F...
```

The tool:

1. Converts the hex string to bytes and prints a hex dump.
2. Strips the 2-byte length indicator.
3. Unpacks the body with `ISOMessage` and prints the parsed fields.

## Build & Run

```bash
dotnet build tools/ISO8583HexParserCli/ISO8583HexParserCli.csproj
dotnet run --project tools/ISO8583HexParserCli/ISO8583HexParserCli.csproj -- <dialect.json> <hexdump>
```
