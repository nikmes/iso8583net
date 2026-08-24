# Sprint D1 — Outbound Enforcement + 1800 Fix

> Part of [Dialect-Enforced Validation](proposal-dialect-validation.md).
> Depends on: D0. Unblocks: D4.

**Goal:** No outbound message may carry an MTI or field not defined for that MTI in the dialect.
Also fix the root cause: server-initiated SignOn/Echo/SignOff must use the dialect's
network-management MTI `1804`, not the undefined `1800`.

**Status legend:** `Not started` · `In progress` · `Done`

| ID | Task | File(s) | Status |
|----|------|---------|--------|
| D1-1 | Wire `DialectValidator` into `ISOMessageFieldsPackager.Pack`: validate MTI membership + field participation before writing bytes; throw `DialectValidationException` on failure when validation is enabled | `src/ISO8583Net/ISOPackager/ISOMessageFieldsPackager.cs` | Done |
| D1-2 | Make `m_fieldParticipationValidations` actually honored by `Pack` (read the flag) | `src/ISO8583Net/ISOPackager/ISOMessageFieldsPackager.cs` | Done (revised) |
| D1-3 | Add early MTI-membership check in `ISOMessage.Set(0, mti)` so developers fail at the call site | `src/ISO8583Net/ISOMessage/ISOMessage.cs` | Done |
| D1-4 | Replace hardcoded `"1800"` with `"1804"` in `PipelineHost.BuildRequest` | `src/ISO8583Server/Pipeline/PipelineHost.cs` | Done |
| D1-5 | Update server-initiated send helpers (`SendSignOnAsync`, `SendEchoAsync`, `SendSignOffAsync`, `SendSignOnOnConnect`) and `PeriodicSignOnService` to build `1804` messages | `src/ISO8583Server/Iso8583TcpServer.cs`, `src/ISO8583Server/PeriodicSignOnService.cs` | Done |
| D1-6 | Remove legacy `1800`/`1810` handling from `DefaultHandler` | `src/ISO8583Server/Pipeline/Handlers/DefaultHandler.cs` | Done |
| D1-7 | Add REST API parameter validation so `/signon`, `/echo`, `/signoff` reject undefined MTI/F24 values before broadcast | `tools/ISO8583Service/Controllers/Iso8583Controller.cs` | Done (no-op) |
| D1-8 | Unit tests: `Set(0, "1800")` throws; `Pack` of `1804` + mandatory F7/F11/F24/F28 succeeds; `Pack` missing mandatory field throws; `Pack` disallowed field throws | `tests/ISO8583Net.Tests/DialectEnforcementTests.cs` | Done |
| D1-9 | Build + verify: all tests pass, and an end-to-end run no longer transmits MTI `1800` | — | Done |
| D1-10 | Populate mandatory F28 (Reconciliation Date) in `BuildRequest` | `src/ISO8583Server/Pipeline/PipelineHost.cs` | Done |
| D1-11 | Fix SignOff F24 from `803` → `802` (Logoff) to match the dialect interpreter and inbound `NetworkManagementHandler`; update docs/comments | `src/ISO8583Server/Iso8583TcpServer.cs`, `src/ISO8583Server/IIso8583Server.cs`, `tools/ISO8583Service/Controllers/Iso8583Controller.cs`, `tools/ISO8583Service/Program.cs` | Done |

## Notes on implementation decisions

- **Validation is gated, default OFF in core.** The enforcement flag is now a tri-state
  `DialectValidationMode` (`Off`/`Warn`/`On`) held on the shared packager. `Off` (the default)
  keeps the library permissive for existing callers; `Warn` logs a warning on violation without
  throwing; `On` throws before invalid bytes are produced. `ISOMessageFieldsPackager`
  `EnableFieldParticipationValidations(bool)` maps to `On`/`Off` and is kept for backward
  compatibility; the richer `SetFieldParticipationValidationMode(...)` is the new API.
  `Pack` and `ISOMessage.Set(0, …)` both honor the mode. The mode is runtime-toggleable
  (see D4-3): `ServerOptions.DialectValidationMode` seeds it at startup and
  `PUT /api/iso8583/config` toggles it live.
- **D1-2 revised — default ON deferred to D4.** Enabling validation unconditionally (default ON,
  or at the server entry point) breaks the financial/advice handler responses: `BaseRequestHandler`
  responses are missing mandatory F28 (and F19/F30/F56 for `1210`/`1410`), and `BaseAdviceHandler`
  acknowledgements are missing mandatory F38. Fixing those is a per-MTI field-copy refactor that
  belongs to D4 semantics, so this sprint ships the *gated* machinery and leaves the flag off in the
  running service. See the D4 file for the follow-up.
- **D1-5 "validate before enqueueing"** is satisfied by the gated `Pack`-time validation; the send
  helpers themselves now build correct `1804` messages (via `BuildRequest`, which sets F0/F7/F11/F24/F28).
- **D1-7 is a no-op for input validation.** The `/signon`, `/echo`, `/signoff` endpoints take no
  body/query parameters that could carry an arbitrary MTI or F24; they delegate to send helpers that
  already use fixed, valid F24 (`801`/`802`/`831`). Doc comments were corrected to reflect `802` for
  SignOff. No rejection logic was needed.

## Acceptance criteria

- No code path can `Pack` an MTI absent from the dialect while validation is enabled. — **satisfied via gated flag.**
- Server-initiated SignOn/Echo/SignOff wire bytes carry MTI `1804` with the correct F24
  function code (`801`/`802`/`831`). — **satisfied.**
- Sending a field not allowed for `1804` throws with a message naming the field and MTI. — **satisfied (gated).**
- Outbound `1804` satisfies its dialect-mandatory field set (F7, F11, F24, F28) before validation ships. — **satisfied (F28 added).**

## Verification

- `dotnet test tests/ISO8583Net.Tests/ISO8583Tests.csproj` → **42 passed** (37 prior + 5 new mode tests).
- `dotnet test tests/ISO8583Service.Tests/ISO8583Service.Tests.csproj` → **3 passed**.
