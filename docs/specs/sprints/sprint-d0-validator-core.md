# Sprint D0 — Dialect Validator Core

> Part of [Dialect-Enforced Validation](proposal-dialect-validation.md).
> Depends on: none. Unblocks: D1, D2, D3.

**Goal:** Expose MTI membership and per-MTI field participation as a first-class, non-throwing
API on the packager, and implement a shared validator used by both `Pack` and `UnPack`. No
behavioral enforcement yet — this sprint only builds and tests the primitives.

**Status legend:** `Not started` · `In progress` · `Done`

| ID | Task | File(s) | Status |
|----|------|---------|--------|
| D0-1 | Add `ISOMessageTypesPackager.Contains(string mti)` and `TryGet(string mti, out ISOMsgTypePackager)` non-throwing accessors over `m_msgTypes` | `src/ISO8583Net/ISOPackager/ISOMessageTypesPackager.cs` | Done |
| D0-2 | Replace the stub `ISOMsgTypePackager.ValidateBitmap(ISOFieldBitmap)` with a real implementation that returns `{ IsValid, MissingMandatory, Disallowed }` against `m_manBitmap` / `m_optBitmap` / `m_conBitmap` | `src/ISO8583Net/ISOPackager/ISOMsgTypePackager.cs` | Done |
| D0-3 | Add `DialectValidationResult` record (`IsValid`, `IsMtiKnown`, `MissingMandatoryFields`, `DisallowedFields`, `Message`) | `src/ISO8583Net/ISOPackager/DialectValidationResult.cs` (new) | Done |
| D0-4 | Add `DialectValidationException` for the outbound fail-fast path | `src/ISO8583Net/ISOPackager/DialectValidationException.cs` (new) | Done |
| D0-5 | Add `DialectValidator` static helper: `Validate(ISOMessageFieldsPackager, string mti, ISOFieldBitmap)` and `Validate(ISOMessageFieldsPackager, ISOMessage)` — shared by pack/unpack | `src/ISO8583Net/ISOPackager/DialectValidator.cs` (new) | Done |
| D0-6 | Expose `GetMessageTypesPackager()` already present on `ISOMessageFieldsPackager`; document it as the public entry point used by the validator | `src/ISO8583Net/ISOPackager/ISOMessageFieldsPackager.cs` | Done |
| D0-7 | Unit tests: known MTI → valid; unknown MTI → `IsMtiKnown=false`; missing mandatory bit → listed; disallowed bit → listed; empty bitmap against 1804 (mandatory F7/F11/F24) → missing list populated | `tests/ISO8583Net.Tests/DialectValidatorTests.cs` (new) | Done |
| D0-8 | Build + verify: solution compiles, all existing + new tests pass | — | Done |

## Acceptance criteria

- `Contains` / `TryGet` return correct membership for all 14 D8 MTIs and `false` for `1800`.
- `ISOMsgTypePackager.ValidateBitmap` is no longer a `return false` stub.
- `DialectValidator` is pure and has no side effects; it does not change any pack/unpack behavior yet.
