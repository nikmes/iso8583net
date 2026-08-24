# Sprint D4 — Handler Guard, Config & Docs

> Part of [Dialect-Enforced Validation](proposal-dialect-validation.md).
> Depends on: D0, D1, D2, D2R, D3.

**Goal:** Fail fast at startup for handlers that claim MTIs outside the dialect, make the
strictness configurable, and document the new constraint so developers and users are bounded by
what the dialect defines.

**Status legend:** `Not started` · `In progress` · `Done`

## Findings from D2R + D3 (review, 2026-08-24)

These clarify how D4 interacts with what the last two sprints already shipped:

- **Inbound enforcement is always-on and independent of `DialectValidationMode`.**
  `ISOMessage.UnPack` computes `ValidationResult` unconditionally (`ISOMessage.cs:280-285`);
  `DispatcherStage` then rejects unknown MTIs (`9800`) and field-level failures (`9xxx`) *before*
  handler lookup. The `Off`/`Warn`/`On` switch governs **outbound** validation only
  (`ISOMessageFieldsPackager.Pack` and the MTI guard in `ISOMessage.Set`). Do **not** re-add a
  mode gate to the inbound path — it must stay always-on so peers always get a format-error reply.
- **The D8 inbound guard already satisfies most of D4-2.** Because the unknown-MTI / field-error
  branches fire before `registry.GetHandlers(mti)`, the `"*"` catch-all never sees an undefined MTI
  for D8-header messages. The remaining gap is the **non-D8 path**: those branches are gated on
  `Header is ISOHeaderD8`, so a VISA-dialect undefined MTI would still reach the catch-all.
- **`HandlerRegistry` has no dialect access today.** Its constructor only takes
  `IEnumerable<IMessageHandler>` (`HandlerRegistry.cs:20`), so D4-1 needs the packager (or the set
  of valid MTIs from `GetMessageTypesPackager()`) injected.
- **`DefaultHandler` is already a no-op** for everything it receives, so an undefined MTI reaching
  the catch-all is benign today; D4-1's fail-fast is the real enforcement, D4-2 the general tightening.

| ID | Task | File(s) | Status |
|----|------|---------|--------|
| D4-1 | `HandlerRegistry.ValidateAgainstDialect` validates each `SupportedMTIs` value (except `"*"`) against the dialect; an undefined MTI, any `9xxx` terminal MTI, or a wildcard other than exactly `"*"` is a startup error (throws `InvalidOperationException` listing all offending MTIs). Deferred to after the packager exists via `PipelineHost.ValidateHandlers()`. | `src/ISO8583Server/Pipeline/Handlers/HandlerRegistry.cs`, `src/ISO8583Server/Pipeline/PipelineHost.cs` | Done |
| D4-2 | Make the `"*"` catch-all dialect-aware in `HandlerRegistry.GetHandlers`: consult it only for dialect-defined MTIs (plus the null/empty-MTI case), never for undefined MTIs — across **all** dialects. (`DispatcherStage` already rejects unknown/field-error MTIs for D8 from D2; no change needed there.) | `src/ISO8583Server/Pipeline/Handlers/HandlerRegistry.cs` | Done |
| D4-3 | Add runtime-toggleable `DialectValidationMode` (`Off`/`Warn`/`On`, default `Off`) to `ServerOptions` + `appsettings.json`; thread into the shared packager; expose via `PUT /api/iso8583/config` | `src/ISO8583Server/*`, `tools/ISO8583Service/*` | Done |
| D4-4 | Update docs: `AGENTS.md` (architecture + handler framework sections), `docs/handler-development-guide.md`, `tools/ISO8583Service/README.md` — document that only dialect-defined MTIs/fields may be used | `AGENTS.md`, `docs/handler-development-guide.md`, `tools/ISO8583Service/README.md` | Done |
| D4-5 | Add startup self-check: log the dialect's supported MTIs (already exists in `Iso8583TcpServer.LogMessageTypes`) plus a validation summary of registered handler MTIs | `src/ISO8583Server/Iso8583TcpServer.cs` | Done |
| D4-6 | Full regression: 58 core tests + 3 service tests pass; CI green | — | Done |

## Acceptance criteria

- Registering a handler for an undefined MTI (including any `9xxx` terminal MTI) fails at startup,
  before any connection is accepted.
- With `DialectValidationMode=Off` (the default), the previous permissive **outbound** behavior is
  restored. Inbound enforcement remains always-on (see Findings above).
- With `DialectValidationMode=Warn`, **outbound** violations are logged as warnings and do not stop
  or break any flow.
- With `DialectValidationMode=On`, **outbound** violations throw before invalid bytes are produced.
- The `"*"` catch-all is only consulted for dialect-defined MTIs (all dialects), never for undefined MTIs.
- Docs accurately state the "dialect-defined only" constraint and how to add a new MTI to the dialect.
