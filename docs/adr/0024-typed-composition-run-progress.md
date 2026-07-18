# ADR 0024: Drive composition progress UI from typed Application phases

- Status: Proposed for `v0.9.10`
- Date: 2026-07-18
- Owners: Product owner + architecture owner + UI/accessibility reviewer
- Supersedes: None
- Superseded by: None

## Context

The shell currently exposes one truthful indeterminate progress bar and a
localized Preview/Build label. It confirms that a run is active but cannot tell
the user whether NFC is reading inputs, composing bytes, running an approved
postbuild processor, validating output, committing the artifact, or preparing
the report.

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

1. Keep only the current generic indeterminate bar.
2. Let Presentation infer a step from timers, operation ids, or status text.
3. Publish typed Application lifecycle phases and let Presentation render them.
4. Estimate a numeric percentage from elapsed time or input size.

## Decision

Select option 3. `v0.9.10` adds an additive typed progress contract owned by
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
`running-external-processor`. Application, and an Application port implemented
by the host where necessary, owns phase selection and order. Infrastructure may
report entry/exit of an approved processor through that port but does not
invent product steps. Presentation receives typed snapshots and never branches
on operation ids, processor ids, issue text, filenames, or IC labels.

The Application surface uses one `CompositionRunProgressFeed` per run. Its
bounded asynchronous channel can hold the complete seven-phase lifecycle, is
completed on success, failure, or cancellation, and never invokes a host or UI
callback inline with composition or output commit. Each immutable snapshot
contains the run id, applicable phase sequence, phases actually completed, the
current phase, and its lifecycle ordinal. Reusing one feed for another run is
rejected.

The UI shows the localized current step and the applicable step sequence.
Completed steps are visually complete, the active step has a restrained
indeterminate animation, and future steps remain inactive. Lifecycle ordinal
may be displayed as “Step N of M”; it is not a byte percentage. The current
indeterminate progress semantics remain within a phase.

Step changes are announced through one accessible live status. Animation
frames never produce accessibility announcements or Application events. When
reduced motion is enabled, the active step uses a static emphasis while state,
text, and accessibility updates remain intact. Failure, cancellation, and
success retain the last truthful phase and then yield to the normal report or
diagnostic result.

## Consequences

### Positive

- Users can see where a long CtrlRAM/postbuild run is waiting.
- The same typed lifecycle can support UI tests and local performance evidence.
- Progress does not create a second execution model or expose firmware rules to
  Presentation.

### Negative / trade-offs

- Application and Bootstrap gain a small additive asynchronous progress-feed
  contract.
- External processor progress remains phase-level until a reviewed process
  contract exposes more detail; command duration is not guessed.
- Localization and accessibility tests must cover every stable phase.

### Risks and mitigations

- Excessive events could add UI-thread work -> publish only phase transitions,
  coalesce duplicate snapshots, bound the feed to the complete lifecycle, and
  keep animation inside Presentation.
- A host callback could throw during or after output commit -> Application only
  enqueues immutable snapshots; consuming and dispatching them is host-owned
  asynchronous work and cannot change the run result.
- UI could treat lifecycle ordinal as completion percentage -> do not bind a
  determinate percentage or derive remaining time.
- Progress could drift from execution -> emit transitions at the owning
  Application/port boundary and test exact success/failure/cancellation order.
- A stale run could update a later run -> bind every snapshot to the run id and
  ignore updates after cancellation or ownership changes.

## Compatibility and migration

This is an additive Application/Bootstrap/UI contract. It does not change
profiles, ranges, operations, processor argv/order, report wire schemas,
output bytes, support state, or CLI compatibility. CLI callers may omit the
feed. The current generic progress bar remains the fallback until the
stepper is wired and verified after the final `0.9.9` UI-contract rebase.

The first provisional Application-only slice implements the enum, snapshot,
single-run bounded feed, and exact service transitions. It deliberately does
not change Bootstrap or Presentation while the final `0.9.9` workflow and UI
contracts are still moving. That wiring, localization, accessibility styling,
and reduced-motion animation remain an integration gate after rebase.

## Verification

- Application tests lock exact applicable phase order for Preview and Build,
  with and without an external processor.
- Failure, cancellation, final-validation rejection, and commit failure never
  report unexecuted phases as complete.
- Architecture tests reject Presentation inference from operation/processor ids
  and keep the progress contract out of Domain.
- ViewModel/UI smoke tests cover English and Traditional Chinese labels,
  run-id isolation, active/completed/future states, screen-reader status,
  high-contrast styling, and reduced motion.
- A bounded-event test prevents per-byte, per-frame, or polling updates from
  crossing the Application boundary.
- The feed is completed when input reading is cancelled or an adapter throws,
  and source-boundary coverage rejects inline `IProgress<T>` callbacks and any
  copy of the lifecycle contract in Domain.
- Existing byte, SHA-256, mutation, processor trace, golden, Polytail, and
  canonical full-verification gates remain mandatory.
