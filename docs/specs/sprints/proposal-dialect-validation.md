# Dialect-Enforced Validation — Proposal

> Status: **Proposal (not implemented)**. Companion sprint files:
> [`sprint-d0-validator-core.md`](sprint-d0-validator-core.md) ·
> [`sprint-d1-outbound-enforcement.md`](sprint-d1-outbound-enforcement.md) ·
> [`sprint-d2-inbound-enforcement.md`](sprint-d2-inbound-enforcement.md) ·
> [`sprint-d3-handler-guard-and-config.md`](sprint-d3-handler-guard-and-config.md)

## 1. Problem

The dialect (`src/ISO8583Net/ISODialects/d8-iso8583.json`) is currently used only as a
**field-layout / bitmap / header packing definition**. Its `messages` table — the list of
allowed message types (MTIs) and their field participation (mandatory / optional / conditional) —
is loaded into memory but never enforced. As a result:

- Outbound code can send an MTI that the dialect does not define.
  `PipelineHost.BuildRequest` hardcodes `msg.Set(0, "1800")`
  (`src/ISO8583Server/Pipeline/PipelineHost.cs:143`), but the D8 dialect only defines
  `1804` / `1814` for network management. A real D8 peer correctly rejects this with
  MTI `9800` + header `Error=999`.
- Inbound messages with an unknown MTI are unpacked and routed anyway; if no handler matches,
  `DispatcherStage` silently drops them (`src/ISO8583Server/Pipeline/DispatcherStage.cs:48-51`),
  and the `"*"` catch-all (`DefaultHandler`) can absorb them.
- A field may be set/sent even when the dialect marks it as not participating for that MTI.

The only membership check that exists is **dead code**:

- `ISOMessageTypesPackager.ValidateBitmap(string msgType)` (`ISOMessageTypesPackager.cs:92-103`)
  checks `m_msgTypes.ContainsKey(msgType)` but has **no call site** in the codebase.
- `ISOMsgTypePackager.ValidateBitmap(ISOFieldBitmap)` (`ISOMsgTypePackager.cs:99-103`)
  is a stub that returns `false`.
- `ISOMessageFieldsPackager.m_fieldParticipationValidations` +
  `EnableFieldParticipationValidations()` (`ISOMessageFieldsPackager.cs:21, 86-89`)
  is set but never read by `Pack` / `UnPack`.

## 2. Goal

Make the dialect's `messages` table the **single source of truth** for what can be packed,
unpacked, sent, and processed. Concretely:

1. **Outbound** — an `ISOMessage` whose MTI (or field set) is not defined for that MTI in the
   dialect cannot be packed/sent; it fails fast with a clear error.
2. **Inbound** — a message whose MTI is not defined in the dialect is not silently dropped or
   absorbed by the catch-all; it produces a structured diagnostic and, where the dialect
   supports it, an error response.
3. **Registration** — a handler cannot claim a `SupportedMTIs` value that is not in the dialect.

## 3. Design principles

- **Enforce at choke points, not everywhere.** The packager is the single choke point for
  byte-level pack/unpack; `HandlerRegistry` is the choke point for routing; `PipelineHost` /
  `Iso8583TcpServer` are the choke points for server-initiated sends.
- **Outbound = fail fast.** A developer error (sending `1800`, sending a field the dialect does
  not allow for that MTI) should throw/return an error, not silently emit invalid bytes.
- **Inbound = don't crash, respond.** A peer error must never take the pipeline down; it must be
  logged with context and answered with a dialect-defined error where possible.
- **Strict by default, overridable.** Validation is ON by default and can be relaxed via config
  only for explicitly acknowledged compatibility scenarios.

## 4. Validation matrix

| Direction | Layer | Current | Target |
|-----------|-------|---------|--------|
| Outbound  | MTI membership | none | reject undefined MTI at `Pack` / `Set(0, …)` |
| Outbound  | Field participation | dead flag | reject missing mandatory + disallowed fields at `Pack` |
| Inbound   | MTI membership | none | `UnPack` reports unknown MTI; `ParserStage` emits error, never silently drops |
| Inbound   | Field participation | partial (field-packager existence only) | validate mandatory fields against the MTI |
| Routing   | Handler MTI set | any string allowed, `"*"` absorbs | registration validates against dialect; `"*"` only fires for dialect-defined MTIs without a specific handler |
| Service   | Server-initiated sends | hardcoded `1800` | use dialect-valid `1804`, validate before send |

## 5. Mechanism

### 5.1 Core library (`src/ISO8583Net/`)

Introduce a small validation API on top of the existing packager objects:

- `ISOMessageTypesPackager.Contains(string mti)` / `TryGet(string mti, out ISOMsgTypePackager)`
  — non-throwing membership accessors over `m_msgTypes`.
- Implement `ISOMsgTypePackager.ValidateBitmap(ISOFieldBitmap)` for real: compare the message
  bitmap against `m_manBitmap` / `m_optBitmap` / `m_conBitmap` and return the mandatory-present
  and disallowed-field sets.
- Add `DialectValidationResult` (a plain record/struct): `IsValid`, `IsMtiKnown`,
  `MissingMandatoryFields`, `DisallowedFields`, `Message`.
- Add `DialectValidationException` for the outbound fail-fast path.
- Add a `DialectValidator` static helper that runs the same checks for both `Pack` and `UnPack`
  so the two directions never drift.

### 5.2 Outbound enforcement

- `ISOMessageFieldsPackager.Pack` (`ISOMessageFieldsPackager.cs:97-133`) validates the MTI and
  field participation before writing bytes (reusing `DialectValidator`). When validation is
  enabled and fails, it throws `DialectValidationException`.
- `ISOMessage.Set(0, mti)` (`ISOMessage.cs:119-129`) performs an early MTI-membership check so a
  developer gets immediate feedback at the call site rather than at pack time.

### 5.3 Inbound enforcement

- `ISOMessageFieldsPackager.UnPack` (`ISOMessageFieldsPackager.cs:141-213`) records the MTI and
  returns a `DialectValidationResult` (or populates it on the packager) instead of throwing, so
  `ParserStage` can decide how to respond.
- `ParserStage` maps an unknown-MTI / missing-mandatory result to a structured log entry and,
  where the dialect supports it, a negative response (see §6), rather than a bare `PARSE_ERR`.

### 5.4 Routing / registration enforcement

- `HandlerRegistry` (`HandlerRegistry.cs:20-41`) validates each `SupportedMTIs` value (except
  `"*"`) against the dialect at construction; an undefined MTI is a startup error.
- The `"*"` catch-all is re-scoped: it fires only for **dialect-defined** MTIs that have no
  specific handler, never for undefined MTIs. Undefined MTIs are rejected at the parse/dispatch
  boundary before any handler runs.

### 5.5 Service-initiated sends

- Replace the hardcoded `"1800"` in `PipelineHost.BuildRequest`
  (`PipelineHost.cs:139-148`) with the dialect's network-management MTI `1804`.
- Update the callers of `BroadcastSignOnRequestAsync` / `SendSignOnAsync` / `SendEchoAsync` /
  `SendSignOffAsync` / `PeriodicSignOnService` to pass F24 function codes over `1804` and to
  validate before enqueueing.

## 6. Error responses (D8 semantics)

D8 peers reject an undefined MTI with a network-management format error: **MTI `9800`** and the
header `Error` field set to **`999`** (this is exactly what was observed in the incident log).
Two consequences for this work:

1. To *send* such an error response, `9800` must either be added to the D8 dialect's `messages`
   table, or the service needs a **raw error-frame builder** that emits `header + MTI 9800` with
   no bitmap (the bitmap-less inbound path already tolerates this shape).
2. For field-level inbound errors (missing mandatory field), the response is a normal
   dialect-defined response MTI with `F39` set to a `9xxx` code (e.g. `902` format error);
   this does not require new MTIs.

**Decision (recommended):** add the `9xxx` error message types to the D8 dialect for full
symmetry, and keep the raw error-frame builder as a fallback for the header-error case. This is
tracked as an explicit task in Sprint D2.

## 7. Config

Add `EnableDialectValidation` (default `true`) to `PipelineOptions`
(`src/ISO8583Server/Pipeline/PipelineOptions.cs`) and expose it in
`tools/ISO8583Service/appsettings.json`. When `false`, the behavior reverts to the current
permissive mode (for controlled compatibility windows only).

## 8. Files impacted

| File | Change |
|------|--------|
| `src/ISO8583Net/ISOPackager/ISOMessageTypesPackager.cs` | add `Contains` / `TryGet` |
| `src/ISO8583Net/ISOPackager/ISOMsgTypePackager.cs` | implement `ValidateBitmap` |
| `src/ISO8583Net/ISOPackager/ISOMessageFieldsPackager.cs` | wire validator into `Pack` / `UnPack`, honor flag |
| `src/ISO8583Net/ISOPackager/DialectValidator.cs` (new) | validation logic + result/exception types |
| `src/ISO8583Net/ISOMessage/ISOMessage.cs` | early MTI check in `Set(0, …)` |
| `src/ISO8583Server/Pipeline/ParserStage.cs` | map validation result to log + error response |
| `src/ISO8583Server/Pipeline/DispatcherStage.cs` | reject undefined MTI before handler lookup |
| `src/ISO8583Server/Pipeline/Handlers/HandlerRegistry.cs` | validate handler MTIs at startup |
| `src/ISO8583Server/Pipeline/Handlers/DefaultHandler.cs` | remove legacy `1800`/`1810`; re-scope `"*"` |
| `src/ISO8583Server/Pipeline/PipelineHost.cs` | `1800` → `1804`, validate before send |
| `src/ISO8583Server/Iso8583TcpServer.cs` | validate server-initiated sends |
| `tools/ISO8583Service/PeriodicSignOnService.cs` | validate periodic echo |
| `tools/ISO8583Service/appsettings.json` | `EnableDialectValidation` |
| `src/ISO8583Net/ISODialects/d8-iso8583.json` | add `9xxx` error MTIs (Sprint D2) |
| `tests/ISO8583Net.Tests/` | new validation tests + regression tests |

## 9. Out of scope (explicit non-goals)

- Full schema/DTO generation from the dialect (not needed for this fix).
- Message-level semantic validation (e.g. validating F2/F4 ranges) — that remains handler
  business logic, not dialect enforcement.
- Authentication/authorization on the REST API.
