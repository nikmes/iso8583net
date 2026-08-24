# Sprint D2R — Spec-complete `9xxx` Format-error Responses

> Part of [Dialect-Enforced Validation](proposal-dialect-validation.md).
> Depends on: D2. Unblocks: D3.
>
> **Revision of D2.** D2 shipped the `9800`/`999` bitmap-less response for the unknown-MTI /
> invalid-header case, but implemented the known-MTI field-level error case incorrectly
> (normal response MTI + `F39=902`). This sprint replaces that with the spec-defined
> `9xxx` transformation.

## Why this sprint exists

The D8 spec defines format-error responses as a **transformation**, not a fixed table of MTIs:

- **§4.2.3** (Version Number digit): `1` = `G2B-ISO-1.00`, `9` = *Format error response*.
- **§4.3.5** — *"…the receiver should return the message to the sender with a 9-series message
  (i.e. an invalid 1200 message would be returned as a 9200 message). The receiver should also
  encode the value of the first invalid field in the Field in Error field contained in the message
  header. If the message header itself is invalid, the value of the Field in Error field should be
  '999'."*
- **§6.1.2** — *"…return the message to the sender with a '9' prefix (i.e. a 9xxx message …). The
  Field in Error … will contain the value of the first offending field that caused the message
  validation failure. If the message header is invalid then the Field in Error field will contain
  '999'."*

Consequences the current code does **not** yet satisfy:

1. A known-MTI message with a field-level error must be returned as `9xxx` (first digit → `9`,
   remaining three digits unchanged), **not** as the normal response MTI with `F39=902`.
2. `Field in Error` carries the first offending field number (`000`–`128`), not an `F39` code.
3. The `9800` entry added to the dialect in D2 declares a mandatory bitmap (`f001: M`) which
   contradicts the bitmap-less wire format it actually describes.

## Goal

Replace the D2 field-error path with a single, spec-complete format-error builder that, for any
inbound D8 message, emits a bitmap-less `9xxx` response derived by transformation, with
`Field in Error` set to the first offending field number (or `999` for header/unknown-MTI errors).

**Status legend:** `Not started` · `In progress` · `Done`

| ID | Task | File(s) | Status |
|----|------|---------|--------|
| D2R-1 | Generalize `ErrorResponseBuilder` into a transformation-based builder: `BuildD8FormatErrorFrame` gains a `9xxx` mode that (a) derives the MTI by replacing the first digit with `9` and keeping class/function/origin, and (b) encodes `Field in Error` as the first offending field number (`000`–`128`, zero-padded). Keep the `9800`/`999` fallback for the header-invalid/unknown-MTI case. | `src/ISO8583Server/Pipeline/ErrorResponseBuilder.cs` | Not started |
| D2R-2 | `DispatcherStage`: replace the `SendMissingMandatoryResponseAsync` (response MTI + `F39=902`) path with the `9xxx` transformation. Compute the first offending field as `min(MissingMandatoryFields ∪ DisallowedFields)`; route header-invalid/unknown-MTI through the existing `9800`/`999` branch. | `src/ISO8583Server/Pipeline/DispatcherStage.cs` | Not started |
| D2R-3 | Remove the contradictory `9800` `messages` entry from both dialect copies; replace with a documented note that `9xxx` is a response transformation emitted as a raw bitmap-less frame, not a packed message type. | `src/ISO8583Net/ISODialects/d8-iso8583.json`, `tools/ISO8583Service/Dialects/d8-iso8583.json` | Not started |
| D2R-4 | Tests: known MTI `1200` missing F28 → `9200` + `Field in Error=028`; disallowed field → first disallowed field number; header-invalid/unknown-MTI → `9800` + `999` (regression, no longer `F39=902`). | `tests/ISO8583Net.Tests/` | Not started |
| D2R-5 | Update docs: correct the proposal + D2 text that describes `F39=902` as the field-error response; record the `9xxx` transformation rule and the D8 header `Field in Error` semantics. | `docs/specs/sprints/proposal-dialect-validation.md`, `docs/specs/sprints/sprint-d2-inbound-enforcement.md` | Not started |
| D2R-6 | Build + verify: full core + service test suites pass; replay of a `1200` missing-mandatory inbound now emits `9200` + `028`, not `1210` + `F39=902`. | — | Not started |

## Design notes

- **Transformation rule** — `mti[0] = '9'`, keep `mti[1..3]`. Examples: `1100→9100`, `1200→9200`,
  `1210→9210`, `1400→9400`, `1804→9804`, `1814→9814`. Applies only when the inbound MTI is a
  well-formed, recognized 4-digit value. If the MTI cannot be recognized (or the header is
  invalid), fall back to `9800` + `999`.
- **`Field in Error`** — 3 ASCII digits, zero-padded, value = the numerically smallest field in
  `MissingMandatoryFields ∪ DisallowedFields` (both already produced by `DialectValidator`; no new
  validator API is needed). Header-invalid / unknown-MTI → `999`.
- **Wire shape** — `[2-byte big-endian LI][21-byte D8 header, Field in Error=NNN][2-byte BCD 9xxx]`,
  **no bitmap**. This is the same shape as the observed `9800` frame, generalized to any `9xxx`.
- **Why not enumerate `9xxx` in the dialect `messages` table** — `9xxx` is not a message type with
  its own field layout; it is the output of a transformation over a rejected message. Encoding it as
  a `messages` entry (a) implies a field-participation table it does not have, and (b) risks the
  contradictory mandatory-bitmap declaration already present in the D2 `9800` entry. Format-error
  responses are composed as raw frames at the builder, never packed through the field packager.

## Acceptance criteria

- A known-MTI inbound message with a field-level error (missing mandatory or disallowed field) is
  answered with the corresponding `9xxx` MTI (first digit `9`, remaining digits unchanged).
- The D8 header `Field in Error` field carries the first offending field number (`000`–`128`).
- A header-invalid / unknown-MTI inbound message is still answered `9800` + `999` (bitmap-less).
- No code path emits `F39=902` as a substitute for a format-error response; the `9800` dialect
  `messages` entry with its mandatory-bitmap declaration is removed.
- All core + service tests pass.

## Out of scope (explicit non-goals)

- Responding to an inbound `9xxx` (a peer telling *us* our message was invalid) — that is a
  separate inbound-symmetry concern; this sprint only fixes the outbound error-response we emit.
- Semantic field validation (e.g. F2/F4 range checks) — that remains handler business logic, not
  dialect format-error enforcement.

## Verification

- `dotnet test tests/ISO8583Net.Tests/ISO8583Tests.csproj` → all pass.
- `dotnet test tests/ISO8583Service.Tests/ISO8583Service.Tests.csproj` → all pass.
