# Sprint D3 — Handler Guard, Config & Docs

> Part of [Dialect-Enforced Validation](proposal-dialect-validation.md).
> Depends on: D0, D1, D2.

**Goal:** Fail fast at startup for handlers that claim MTIs outside the dialect, make the
strictness configurable, and document the new constraint so developers and users are bounded by
what the dialect defines.

**Status legend:** `Not started` · `In progress` · `Done`

| ID | Task | File(s) | Status |
|----|------|---------|--------|
| D3-1 | `HandlerRegistry` validates each `SupportedMTIs` value (except `"*"`) against the dialect at construction; an undefined MTI is a startup error (throw or fail-fast log + no registration) | `src/ISO8583Server/Pipeline/Handlers/HandlerRegistry.cs` | Not started |
| D3-2 | Define and implement final `"*"` semantics: catch-all fires only for dialect-defined MTIs with no specific handler, never for undefined MTIs | `src/ISO8583Server/Pipeline/Handlers/HandlerRegistry.cs`, `DispatcherStage.cs` | Not started |
| D3-3 | Add runtime-toggleable `DialectValidationMode` (`Off`/`Warn`/`On`, default `Off`) to `ServerOptions` + `appsettings.json`; thread into the shared packager; expose via `PUT /api/iso8583/config` | `src/ISO8583Server/*`, `tools/ISO8583Service/*` | Done (config slice) |
| D3-4 | Update docs: `AGENTS.md` (architecture + handler framework sections), `docs/handler-development-guide.md`, `tools/ISO8583Service/README.md` — document that only dialect-defined MTIs/fields may be used | `AGENTS.md`, `docs/handler-development-guide.md`, `tools/ISO8583Service/README.md` | Not started |
| D3-5 | Add startup self-check: log the dialect's supported MTIs (already exists in `Iso8583TcpServer.LogMessageTypes`) plus a validation summary of registered handler MTIs | `src/ISO8583Server/Iso8583TcpServer.cs` | Not started |
| D3-6 | Full regression: existing 23 tests + all new validation tests pass; CI green | — | Not started |

## Acceptance criteria

- Registering a handler for an undefined MTI fails at startup (before any connection is accepted).
- With `DialectValidationMode=Off` (the default), the previous permissive behavior is restored.
- With `DialectValidationMode=Warn`, violations are logged as warnings and do not stop or break any flow.
- With `DialectValidationMode=On`, violations throw before invalid bytes are produced.
- Docs accurately state the "dialect-defined only" constraint and how to add a new MTI to the dialect.
