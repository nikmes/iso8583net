# Design note — Generalizing the `9xxx` format-error logic beyond D8

> Status: **Design note / parked idea.** Not a sprint and not planned for implementation yet.
> Purpose: record, in one place, *why* the current `9xxx` handling is D8-specific and *what*
> a dialect-driven generalization would look like, so the decision is explicit rather than
> implicit.

## The two layers

The `9xxx` format-error feature is actually two concerns that today have very different
reusability characteristics:

| Layer | What it does | Reusable today? |
|-------|--------------|-----------------|
| **Validation** | Decide whether an MTI/bitmap is valid, and name the offending field | Yes — fully dialect-driven |
| **Response framing** | Recognize an inbound `9xxx` and build the `9800`/`9200` wire bytes | No — D8-hardcoded |

### 1. Validation is already dialect-driven

The "is this message valid?" path has no knowledge of D8 vs. VISA:

- `DialectValidator.Validate(fieldsPackager, mti, bitmap)` — `src/ISO8583Net/ISOPackager/DialectValidator.cs:19` —
  looks the MTI up in `GetMessageTypesPackager()` and delegates to
  `ISOMsgTypePackager.ValidateBitmap(...)`. Every input (which MTIs exist, which fields are
  mandatory, which are disallowed) comes from the dialect JSON.
- It returns a structured `DialectValidationResult` (`IsMtiKnown`, `MissingMandatoryFields`,
  `DisallowedFields`) — `src/ISO8583Net/ISOPackager/DialectValidationResult.cs`.
- It is wired once, at unpack time, with no header-type check — `src/ISO8583Net/ISOMessage/ISOMessage.cs:284`.

So the "known MTI but field 004 missing" detection — and the reported field number — is
portable to any dialect that shares the same MTI/field semantics.

### 2. Response framing is D8-specific

The `9xxx` response path is coupled to the D8 header in three places:

1. **Every dispatcher branch gates on the header type** — `src/ISO8583Server/Pipeline/DispatcherStage.cs:53,79,91`
   all start with `parsed.Message.Header is ISOHeaderD8`.
2. **The frame builder is D8-only** — `ErrorResponseBuilder.BuildD8ErrorFrame`
   (`src/ISO8583Server/Pipeline/ErrorResponseBuilder.cs:69`) hardcodes
   `ISOHeaderD8.HeaderLength = 21`, the 21-byte ASCII header layout, the 2-byte packed-BCD MTI,
   and the 2-byte big-endian length prefix.
3. **`Field in Error` is a D8 header concept** — positions 17–19 in
   `src/ISO8583Net/ISOHeader/ISOHeaderD8.cs:60`. `ISOHeaderVisa` has no such field, uses a
   different 22-byte header, and has different error semantics.

The actual transformation rule ("first MTI digit → `9`") is written generically
(`IsFormatErrorMti` — `DispatcherStage.cs:232`; `TransformToFormatErrorMti` —
`ErrorResponseBuilder.cs:101`) but only ever fires under the `is ISOHeaderD8` guard, and the
bytes it emits are D8's.

## Reuse verdict

- **D8-family / ISO 8583:1993 dialects** (same 21-byte header and framing): reusable with
  near-zero change — only the MTI table and mandatory-field sets differ, and those already
  come from each dialect JSON.
- **A different header protocol (e.g. VISA)**: not reusable today. The coupling is in the
  header assumption, not in the validation logic.

## Generalization path (if we ever need it)

1. **Replace the header type-check with a capability.** Introduce a small interface — e.g.
   `IFieldInErrorHeader` or `IFormatErrorHeader` — so `DispatcherStage` asks "does this
   dialect support a format-error response?" instead of "is this a D8 header?".
2. **Move wire-format facts into the header/dialect definition.** Header length, the
   `Field in Error` offset, BCD-vs-ASCII MTI encoding, and length-prefix size should be
   read from the header/dialect rather than hardcoded as `21`/`9800`/`999` in
   `ErrorResponseBuilder`.
3. **Declare format-error parameters per-dialect in the JSON.** The format-error MTI
   (currently `9800`), the invalid-header sentinel (`999`), and whether the `9xxx`
   transform applies, so a VISA dialect could specify its own — or none.

Until a second dialect actually needs format-error responses, the D8 hardcoding is a
reasonable, deliberate trade-off; this note exists so the refactor is a known quantity when
that day comes.
