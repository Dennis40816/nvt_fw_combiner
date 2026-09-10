# ADR 0069: Application Input Diagnostic Evidence

- Status: Accepted
- Date: 2026-09-06
- Risk class: R2
- Supersedes: None
- Superseded by: None

## Context

The compiled input-load evaluator already determines whether a profile-declared
source range is unreadable, outside the supplied input, or a repeated-byte
placeholder. The input inspector and composition runtime both consumed that
evaluator but retained only independently observed `CompositionIssue` values.
Consequently, the durable Application Preview/Build report could disclose the
issue but could not bind a compact UI detail to the declared range, observed
length, or repeated byte without recalculating profile facts in Presentation.

This report projection must remain path-free and must not turn a diagnostic
detail into an admission, profile, severity, or execution rule.

## Decision

1. `Application.Composition.InputDiagnosticEvidence` is the immutable,
   path-free evidence projection. It contains a compiler-declared address
   space, optional observed length and required half-open end, optional exact
   `ByteRange`, and at most one repeated byte. It retains no raw source path
   or source contents.
2. `CompiledInputLoadValidationEvaluator` returns one single-pass typed result
   containing the exact `CompositionIssue` instance and optional evidence for
   its first failing uniform candidate. A uniform finding names that candidate
   range and `candidate[0]`, never the whole BIN. Inspector and runtime each
   call the same evaluator and consume that call's issue/evidence together;
   neither starts a second evaluation or byte scan within the same use case to
   recover diagnostic facts, nor does the runtime match issues by code,
   message, or operation.
3. Existing source-view incomplete length evaluation emits the observed length
   and required end. It includes a source range only when the compiler exposes
   one exactly.
4. `CompositionRunReport.InputDiagnostics` is optional and contains immutable
   `InputDiagnosticSummary` entries. Each summary carries a zero-based index
   into that report's final `Issues`, the compiler-declared slot id, and the
   evidence. Report construction rejects null, duplicate, or out-of-bounds
   indexes; the runtime rejects an associated issue that is not the same object
   instance occurring exactly once in its final issue list.
5. Empty diagnostics normalize to `null` and use
   `JsonIgnore(WhenWritingNull)`. The field is available only on real
   Application Preview/Build reports. Diagnostic previews, UI blocked-state,
   profiles, `BlocksBuild`, severity, output bytes, ranges, output naming, and
   canonical `composition-report-v1` remain unchanged.

## Considered options

1. Have Presentation inspect bytes or rediscover ranges from issue codes.
2. Start a second evaluator invocation or byte scan within one inspection/run
   solely to recover diagnostic facts after the primary evaluation.
3. Extend the existing evaluator with immutable evidence and project it by
   exact issue identity.

Option 3 is selected. Options 1 and 2 create duplicate semantic paths and
cannot safely distinguish equal-looking issues.

## Consequences

- Compact UI cards and report details can render compiler-owned, bounded input
  facts with defensive parsing of optional historical JSON.
- Older reports preserve their output shape because `InputDiagnostics` is
  omitted when no evidence exists.
- The public Application report constructor rejects invalid evidence state;
  callers must supply a nonblank slot, a nonnegative unique in-bounds index,
  and nonempty evidence/range facts.
- This ADR grants no firmware support, profile, golden, processor, or release
  authority.

## Verification

- Application tests cover source-view incomplete actual/required facts;
  warning and error uniform candidates containing `00`, `FF`, and `A5`; same
  code/operation issues at distinct indexes; defensive snapshots; invalid
  indexes; and JSON field shape/omission.
- Bootstrap headless inspection tests cover real standard compiled uniform
  warnings and verify evidence identifies only the declared range and one
  repeated byte.
- UI `IssueCardProjectionTests` owns defensive JSON read/legacy fallback
  coverage for the report projection.
