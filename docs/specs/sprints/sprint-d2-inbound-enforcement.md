# Sprint D2 — Inbound Enforcement + Error Responses

> Part of [Dialect-Enforced Validation](proposal-dialect-validation.md).
> Depends on: D0. Unblocks: D3.

**Goal:** An inbound message whose MTI is not defined in the dialect (or whose mandatory fields
are missing) is never silently dropped or absorbed by the catch-all. It produces a structured
diagnostic and, where possible, a dialect-defined error response instead of a bare `PARSE_ERR`.

> **Already shipped (commit `a4ee19c`)** — the *bitmap-less / header-error* hardening that this
> sprint's acceptance criteria call "prior hardening work" is already in place:
> - `ISOMessageFieldsPackager.UnPack` no longer throws `IndexOutOfRangeException` when fewer
>   than 8 bytes follow the MTI; it logs and leaves the bitmap empty
>   (`ISOFieldBitmap.MinimumLengthBytes`).
> - `ParserStage` logs a full D8 header breakdown at `Warning` when `FieldInError != "000"`.
> - `ISOMessage.Header` exposes the header for inspection.
> - Regression test `Unpack_BitMapLess_HeaderError_DoesNotThrow` in `PipelineTests` covers the
>   `G2B-ISO-1.00` + `Error=999` + `9800` (no bitmap) case.
>
> What remains in D2 is the *dialect-enforcement* layer: undefined-MTI / missing-mandatory
> detection, an error-response path (rather than the current log-and-continue), and a
> dialect-defined `9800` error MTI.

**Status legend:** `Not started` · `In progress` · `Done`

| ID | Task | File(s) | Status |
|----|------|---------|--------|
| D2-1 | `ISOMessage.UnPack` populates a `DialectValidationResult` for the unpacked MTI (unknown MTI, missing mandatory) without throwing | `src/ISO8583Net/ISOMessage/ISOMessage.cs` | Done |
| D2-2 | `ParserStage` reads the validation result: unknown MTI → log header + MTI breakdown (reuse the D8 header-error breakdown already added) and route to an error response path; no `PARSE_ERR`-only drop | `src/ISO8583Server/Pipeline/ParserStage.cs`, `src/ISO8583Server/Pipeline/Messages/ParsedMessage.cs` | Done |
| D2-3 | `DispatcherStage` rejects an undefined MTI before handler lookup (do not `continue`-drop); it only dispatches dialect-defined MTIs — gated to D8 headers | `src/ISO8583Server/Pipeline/DispatcherStage.cs` | Done |
| D2-4 | Add `9xxx` error message types (at minimum `9800`) to the D8 dialect `messages` table so an error response is itself dialect-valid; document the mapping of error class → MTI/F39 | `src/ISO8583Net/ISODialects/d8-iso8583.json` (+ service copy `tools/ISO8583Service/Dialects/d8-iso8583.json`) | Done |
| D2-5 | Add a raw error-frame builder for the header-error case (header `Error=999` + MTI `9800`, no bitmap) as a fallback when the error MTI is not in the dialect | `src/ISO8583Server/Pipeline/ErrorResponseBuilder.cs` | Done |
| D2-6 | Missing-mandatory-field inbound → respond with a dialect-defined response MTI + `F39` `9xxx` code (e.g. `902` format error); log the missing field list | `src/ISO8583Server/Pipeline/DispatcherStage.cs` | Done |
| D2-7 | Tests: unknown MTI inbound → no silent drop, error response emitted (or raw error frame); missing mandatory field → `F39=902`; bitmap-less header-error inbound still handled (regression) | `tests/ISO8583Net.Tests/`, `tests/ISO8583Service.Tests/` | Done |
| D2-8 | Build + verify: all tests pass; replay of the original `1800` incident now yields a structured error instead of a dropped message | — | Done |

## Acceptance criteria

- Undefined inbound MTI is logged with full context and answered (or explicitly rejected) — never silently dropped.
- The D8 error response matches observed peer behavior: MTI `9800` + header `Error=999` for the header/undefined-MTI case.
- The bitmap-less inbound guard from the prior hardening work continues to pass its regression test.
