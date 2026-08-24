# Sprint D3 — `9xxx` Format-error Responses: Receive Side

> Part of [Dialect-Enforced Validation](proposal-dialect-validation.md).
> Depends on: D2R. Unblocks: D4 (handler guard, config & docs).
>
> **The inbound half of D2R.** D2R made *this* service emit spec-correct `9xxx` format-error
> responses when it rejects an inbound message. This sprint closes the loop: what happens when a
> peer sends *us* a `9xxx`? Today the answer is "we treat it as an unknown MTI and bounce a
> `9800` back, which the peer treats as unknown too, and neither side ever stops."

## Why this sprint exists

D2R introduced the `9xxx` transformation (`mti[0] = '9'`, keep `mti[1..3]`; `Field in Error` =
first offending field `000`–`128`, or `999` for header/unknown-MTI). The outbound side is done and
tested. The inbound side is not, and it has two concrete defects:

1. **`9xxx` is emitted but never recognized inbound.** `d8-iso8583.json` defines only 14 MTIs
   (`1100, 1110, 1120, 1130, 1200, 1210, 1220, 1230, 1400, 1410, 1420, 1430, 1804, 1814`) —
   **no `9xxx` at all**. So when a peer sends a `9200` (e.g. telling us our `1200` was missing
   `004`), `DialectValidator` reports `IsMtiKnown = false`.

2. **The unknown-MTI path bounces our own error signal.** `DispatcherStage` checks unknown-MTI /
   field-error *before* handler lookup (see `DispatcherStage.cs` lines ~53–72 vs the handler
   dispatch at ~74). A `9200` therefore hits the unknown-MTI branch and is answered with
   `9800`/`999`. But `9800` is *also* not in the dialect, so the peer sees it as unknown too and
   replies `9800`/`999` again — **an infinite `9800`↔`9800` ping-pong loop**, because there is no
   loop guard anywhere in the pipeline. The catch-all `DefaultHandler` is never reached (the
   unknown-MTI branch short-circuits it), and even if it were, it returns a null no-op.

What is **not** broken: the parser already handles bitmap-less frames safely.
`ISOMessageFieldsPackager.UnPack` returns early with an empty bitmap when `bytesAfterMti < 8`
(lines ~205–216), so a `9200` (23-byte body, no bitmap) parses cleanly — no `IndexOutOfRange`.
`ParserStage` already logs the `9xxx` header at Warning with a full header breakdown
(lines ~92–97 and ~101–112), including the `Field in Error` value. The gap is purely *routing*:
the `9xxx` is parsed and logged, then dropped into the unknown-MTI bounce.

## Goal

Recognize the inbound `9xxx` family as a valid format-error notification, never bounce it, and
surface the peer's `Field in Error` value (and the offending original MTI) to logs/tracing —
without emitting any response.

**Status legend:** `Not started` · `In progress` · `Done`

| ID | Task | File(s) | Status |
|----|------|---------|--------|
| D3-1 | Recognize the inbound `9xxx` family. Add a dedicated inbound path (a `9xxx` `IMessageHandler` registered with `SupportedMTIs = ["9*"]`, or an explicit `9xxx` guard in `DispatcherStage` **before** the unknown-MTI branch) that consumes the message as a terminal format-error notification. | `src/ISO8583Server/Pipeline/DispatcherStage.cs`, `tools/ISO8583Service/Handlers/` | Done |
| D3-2 | Loop guard: never emit a `9xxx`/`9800` in response to a received `9xxx`. Ensure the receive-side path sends **no** outbound frame (or, if a dispatch abstraction requires a return value, returns a null/no-op that the writer skips). | `src/ISO8583Server/Pipeline/DispatcherStage.cs` | Done |
| D3-3 | Consume and log the received `Field in Error` at Warning without re-emitting. Parse `Field in Error` (3 ASCII digits) and the transformed original MTI (reconstruct it as `"1" + mti[1..3]` — the version digit is always `'1'` for G2B-ISO-1.00 — with `9800` special-cased as the header/unknown-MTI fallback), and log both alongside the existing header breakdown. Do **not** echo a reply. | `src/ISO8583Server/Pipeline/`, `tools/ISO8583Service/` | Done |
| D3-4 | Tests: (a) receive `9200` + `Field in Error=004` → parse succeeds, warning logged, `MessagesSent == 0`; (b) receive `9800` + `999` → no bounce frame emitted; (c) two-peer integration — peer A sends `1200` missing `004` to peer B, B emits `9200/004`, A receives `9200` and sends nothing back, loop terminates (assert `MessagesSent` stays at the pre-`9200` count). | `tests/ISO8583Net.Tests/` | Done |
| D3-5 | Docs: add this sprint to `docs/specs/sprints/README.md` (sprint table + dependency graph) and record the receive-side `9xxx` semantics in `proposal-dialect-validation.md`. | `docs/specs/sprints/README.md`, `docs/specs/sprints/proposal-dialect-validation.md` | Done |

## Design notes

- **Route `9xxx` before the unknown-MTI check.** The existing `DispatcherStage` order is: (1)
  unknown-MTI / field-error guard, (2) handler lookup. `9xxx` recognition must sit *before* (1),
  or be folded into it as a terminal "recognized, do not reply" case. The cleanest cut is a
  dedicated catch for `mti[0] == '9'` that logs and returns no outbound frame.
- **`9xxx` is a notification, not a request.** Unlike a normal request/response MTI, a `9xxx` is
  the peer's answer to *our* earlier mistake. We must not answer an answer — that is what creates
  the loop. The correct response is silence plus a warning log.
- **Reconstructing the original MTI** is best-effort but exact for the D8 dialect: the `9xxx`
  transformation only replaces `mti[0]` (the version digit), which is always `'1'` for
  G2B-ISO-1.00, and preserves `mti[1..3]` (class, function, originator). So `9200` → `1200`,
  `9400` → `1400`, `9804` → `1804`. The one exception is `9800`, the header/unknown-MTI fallback,
  which has no original MTI and is logged as such (not reconstructed to a misleading `1800`).
- **No dialect change required.** `9xxx` is a response transformation with no field-participation
  table, exactly as D2R concluded for the outbound side. Do not enumerate `9xxx` in
  `d8-iso8583.json`; instead special-case it in the dispatcher (mirroring the outbound builder's
  "raw frame, not packed message" rationale).
- **Default handler stays out of scope.** The catch-all `DefaultHandler` remains a no-op for
  genuinely-unknown MTIs; this sprint does not change its semantics (that is D4's `"*"` definition).

## Acceptance criteria

- A received `9200` (or any `9xxx`) is parsed, logged at Warning with a header breakdown and the
  `Field in Error` value, and produces **no** outbound frame.
- A received `9800`/`999` (header-invalid case) is treated identically — logged, never bounced.
- A two-peer exchange where A sends a malformed `1200` terminates: B replies `9200/004` once, and A
  emits nothing further (no `9800` ping-pong).
- No change to the outbound `9xxx` behavior added in D2R; all existing core + service tests pass.

## Out of scope (explicit non-goals)

- Defining the catch-all `"*"` handler semantics — that is D4 (handler guard).
- Any dialect change (no new `9xxx` entries in `d8-iso8583.json`).
- Automated retry or correction of the message that provoked the `9xxx` — we only log and drop.
- Persisting `9xxx` notifications to the message-trace store beyond the existing tracer path.

## Verification

- `dotnet test tests/ISO8583Net.Tests/ISO8583Tests.csproj` → all pass (incl. new receive-side tests).
- `dotnet test tests/ISO8583Service.Tests/ISO8583Service.Tests.csproj` → all pass.
- Manual/simulator replay: send a `9200/004` frame and confirm the service logs the `Field in Error`
  at Warning and emits zero bytes back.
