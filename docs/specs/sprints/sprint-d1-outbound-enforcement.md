# Sprint D1 — Outbound Enforcement + 1800 Fix

> Part of [Dialect-Enforced Validation](proposal-dialect-validation.md).
> Depends on: D0. Unblocks: D3.

**Goal:** No outbound message may carry an MTI or field not defined for that MTI in the dialect.
Also fix the root cause: server-initiated SignOn/Echo/SignOff must use the dialect's
network-management MTI `1804`, not the undefined `1800`.

**Status legend:** `Not started` · `In progress` · `Done`

| ID | Task | File(s) | Status |
|----|------|---------|--------|
| D1-1 | Wire `DialectValidator` into `ISOMessageFieldsPackager.Pack`: validate MTI membership + field participation before writing bytes; throw `DialectValidationException` on failure when `EnableDialectValidation` is on | `src/ISO8583Net/ISOPackager/ISOMessageFieldsPackager.cs` | Not started |
| D1-2 | Make `m_fieldParticipationValidations` actually honored by `Pack` (read the flag; default ON) | `src/ISO8583Net/ISOPackager/ISOMessageFieldsPackager.cs` | Not started |
| D1-3 | Add early MTI-membership check in `ISOMessage.Set(0, mti)` so developers fail at the call site | `src/ISO8583Net/ISOMessage/ISOMessage.cs` | Not started |
| D1-4 | Replace hardcoded `"1800"` with `"1804"` in `PipelineHost.BuildRequest` | `src/ISO8583Server/Pipeline/PipelineHost.cs` | Done |
| D1-5 | Update server-initiated send helpers (`SendSignOnAsync`, `SendEchoAsync`, `SendSignOffAsync`, `SendSignOnOnConnect`) and `PeriodicSignOnService` to build `1804` messages and validate before enqueueing | `src/ISO8583Server/Iso8583TcpServer.cs`, `src/ISO8583Server/PeriodicSignOnService.cs`, `tools/ISO8583Service/PeriodicSignOnService.cs` | Not started |
| D1-6 | Remove legacy `1800`/`1810` handling from `DefaultHandler`; re-scope `"*"` to dialect-defined MTIs only (see D3 for final semantics) | `src/ISO8583Server/Pipeline/Handlers/DefaultHandler.cs` | Not started |
| D1-7 | Add REST API parameter validation so `/signon`, `/echo`, `/signoff` reject undefined MTI/F24 values before broadcast | `tools/ISO8583Service/Controllers/Iso8583Controller.cs` | Not started |
| D1-8 | Unit tests: `Set(0, "1800")` throws; `Pack` of `1804` + mandatory F7/F11/F24/F28 succeeds; `Pack` missing mandatory field throws; integration test that server-initiated echo now emits `1804` (not `1800`) | `tests/ISO8583Net.Tests/`, `tests/ISO8583Service.Tests/` | Not started |
| D1-9 | Build + verify: all tests pass, and an end-to-end run no longer transmits MTI `1800` | — | Not started |
| D1-10 | Populate mandatory F28 (Reconciliation Date) in `BuildRequest`, or demote F28 to optional in the dialect — otherwise D1-1 enforcement rejects every outbound network-management message | `src/ISO8583Server/Pipeline/PipelineHost.cs`, `src/ISO8583Net/ISODialects/d8-iso8583.json` | Not started |
| D1-11 | Fix SignOff F24 from `803` → `802` (Logoff) to match the dialect interpreter and inbound `NetworkManagementHandler`; update `Iso8583TcpServer` + docs/comments | `src/ISO8583Server/Iso8583TcpServer.cs`, docs | Not started |

> **Note:** D1-4 is already shipped in commit `a4ee19c`. It is a single literal in
> `BuildRequest`, which is shared by SignOn/Echo/SignOff, so all three now emit `1804`.
> `1804` is already defined in `d8-iso8583.json` with mandatory F0/F1/F7/F11/F24/F28.

## Acceptance criteria

- No code path can `Pack` an MTI absent from the dialect while validation is enabled.
- Server-initiated SignOn/Echo/SignOff wire bytes carry MTI `1804` with the correct F24
  function code (`801`/`802`/`831`).
- Sending a field not allowed for `1804` throws with a message naming the field and MTI.
- Outbound `1804` satisfies its dialect-mandatory field set (F7, F11, F24, F28) before validation ships.
