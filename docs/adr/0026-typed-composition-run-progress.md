# ADR 0026: Drive composition progress UI from typed Application phases

- Status: Accepted for `v0.9.10`
- Date: 2026-07-18
- Owners: Product owner + architecture owner + UI/accessibility reviewer
- Supersedes: None
- Superseded by: None

## Context

An indeterminate Preview/Build indicator can show that a run is active, but it
cannot truthfully explain whether NFC is reading inputs, composing bytes,
running an approved postbuild processor, validating output, committing the
artifact, or preparing the report.

Presentation cannot derive those stages from operation ids, reason strings,
filenames, elapsed timers, or report text. Those signals are incomplete and
would move workflow and processor semantics into the UI. A percentage would
also be misleading because composition and external tools do not expose
byte-level completion.

## Decision drivers

- Show the current real execution step without fabricating percentage progress.
- Keep firmware and processor semantics out of Presentation.
- Preserve one Application use case for UI and CLI and one composition engine.
- Keep progress updates bounded so animation does not create UI-thread pressure.
- Support localization, screen readers, high contrast, cancellation, and
  reduced motion.

## Considered options

1. Keep only a generic indeterminate bar.
2. Let Presentation infer a step from timers, operation ids, or status text.
3. Publish typed Application lifecycle phases and let Presentation render them.
4. Estimate a numeric percentage from elapsed time or input size.

## Decision

Select option 3. `v0.9.10` owns an additive typed progress contract in
Application. One run may publish only applicable, ordered lifecycle phases:

```text
preparing
reading-inputs
executing-composition
running-external-processor
validating-output
committing-output
preparing-report
```

Preview omits `committing-output`. A plan without an external processor omits
`running-external-processor`. Application owns phase selection and order.
Presentation receives typed snapshots and never branches on operation ids,
processor ids, issue text, filenames, IC labels, or elapsed time.

One `CompositionRunProgressFeed` belongs to one run. Its bounded asynchronous
channel can hold the complete seven-phase lifecycle, completes on success,
failure, or cancellation, and never invokes a host or UI callback inline with
composition or output commit. Each immutable snapshot contains the run id,
applicable phase sequence, completed phases, current phase, and lifecycle
ordinal. When an atomic Build commit succeeds, the `preparing-report` snapshot
also carries the committed output identity. Preview and every failed or
uncommitted Build leave it absent. Reusing one feed for another run is
rejected.

Bootstrap exposes optional progress-aware facade overloads while preserving the
existing public CLR overloads used by CLI and tests. Presentation observes the
feed asynchronously, rejects another run id, and updates only bounded state.
The same authoritative composition execution remains responsible for output,
report, and commit.

The UI shows localized current and applicable steps. Completed steps are
complete, the active step has restrained indeterminate emphasis, and future
steps remain inactive. “Step N of M” is a lifecycle ordinal, not a byte
percentage. Step changes use one accessible live status. Animation frames do
not produce accessibility announcements or Application events. Reduced motion
uses a static active-state emphasis while text and accessibility state remain.

Presentation projects a separate typed delivery state: running, artifact
committed, and report ready. After the committed output snapshot arrives, the
BIN is already available at its atomically promoted destination while complete
JSON, Hex Diff, and history projection continue on a worker. Report-ready is
published only after that projection completes. The run retains command
ownership until then so a second Build cannot race report/history publication.

## Consequences

### Positive

- Users can see where a long CtrlRAM/postbuild run is waiting.
- Users can distinguish a usable committed BIN from optional report projection.
- Deterministic phase evidence replaces guessed timers or percentages.
- Progress does not create a second execution model or expose firmware rules to
  Presentation.

### Negative / trade-offs

- Application and Bootstrap carry a small additive asynchronous contract.
- External processor progress remains phase-level until a separately reviewed
  process contract exposes more detail.
- Localization and accessibility tests must cover every stable phase.

### Risks and mitigations

- Excessive events could add UI-thread work: publish only phase transitions,
  coalesce duplicates, and keep animation inside Presentation.
- A callback could change execution: Application only enqueues immutable
  snapshots; consuming them cannot change the run result.
- UI could imply percentage completion: do not bind determinate percentage or
  derive remaining time.
- Progress could drift from execution: emit transitions only at the owning
  Application boundary and test exact order.
- A stale run could update a later run: bind every snapshot to the run id and
  ignore updates after cancellation or ownership change.

## Compatibility and release impact

This is an additive Application/Bootstrap/Presentation contract. It does not
change profiles, ranges, operations, processor argv/order, report schemas,
output bytes, naming, support state, CLI behavior, dependencies, or packaging.
Source size is measured for maintainability but is not a `v0.9.10` performance
acceptance criterion.

## Verification

- Application tests lock exact applicable phase order for Preview and Build,
  with and without an external processor.
- Failure, cancellation, final-validation rejection, and commit failure never
  mark unexecuted phases complete.
- Architecture tests keep the contract out of Domain and reject Presentation
  inference from operation or processor ids.
- UI acceptance tests must cover English and Traditional Chinese labels,
  run-id isolation, active/completed/future states, screen-reader status, high
  contrast, and reduced motion. Current executable evidence covers the typed
  steps, the artifact-committed/report-ready boundary, polite live region, and
  reduced-motion animation switch; high-contrast progress evidence remains an
  explicit release gate.
- A bounded-event test prevents per-byte, per-frame, or polling updates from
  crossing the Application boundary.
- Existing byte, SHA-256, mutation, processor trace, golden, Polytail, and final
  verification gates remain mandatory.
