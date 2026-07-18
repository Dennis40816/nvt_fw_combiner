# ADR 0025: Preserve firmware evidence while remediating end-to-end performance

- Status: Proposed for `v0.9.10`
- Date: 2026-07-18
- Owners: Product owner + architecture owner + firmware/process reviewer + UI reviewer
- Supersedes: None
- Superseded by: None

## Context

`v0.9.10` is an end-to-end performance remediation milestone, not only an
Automatic Build optimization. Source audit identifies four related costs:

1. Historical Automatic Build ran Preview and Build independently, duplicating
   composition and every external processor session. The provisional
   Application-owned single-run path removes that duplicate session.
2. `LegacyCombinerPostbuildProcessor` reads the complete staging firmware once
   before every command, again during shortened-output normalization, and once
   after all commands: `2C + 1` full firmware reads per session.
3. A UI command invokes the async workbench delegate directly. Any synchronous
   planning before its first incomplete await can delay the first rendered
   progress state, and CPU-heavy hashing/composition must not resume on the UI
   dispatcher.
4. Report completion synchronously parses JSON and eagerly constructs every
   output-difference row, group, fact, mutation, and raw-text binding.
   Non-virtualized `ItemsControl` content inside a `ScrollViewer` scales poorly
   when changed ranges or byte evidence are numerous.

Performance work must not weaken exact commands, output evidence, report
contracts, immutable input, staging confinement, changed-range validation, or
atomic output.

## Decision drivers

- Reduce the dominant external process, full-file I/O, allocation, and UI-thread
  costs measured on the same B/C inputs.
- Keep legacy Combiner command order, argv, version/hash, and failure attribution
  exact.
- Keep complete machine-readable report evidence even when the UI renders only
  a bounded projection.
- Make Preview/Build responsive and cancellable without adding firmware logic to
  Presentation.
- Prefer deterministic count/ownership gates over flaky timing thresholds.

## Considered options

1. Optimize only Presentation animations.
2. Parallelize or combine legacy Combiner commands.
3. Delete/truncate report differences and byte evidence.
4. Preserve execution semantics and remove redundant work at each owning
   boundary.

## Decision

Select option 4 as a staged `v0.9.10` program.

### Legacy Combiner session and readback

- Automatic Build opens one processor session and launches every declared
  command exactly once, sequentially, with unchanged argv/order.
- One host-owned staging directory and evolving firmware file remain the
  authority for the complete ordered postbuild plan.
- The adapter retains the last accepted full firmware bytes in memory. After
  each command it performs one authoritative firmware readback, verifies
  length/coverage, normalizes an approved shortened output in memory when
  necessary, writes the normalized full image back only when required, and
  carries those bytes to the next command.
- The target is `C` firmware readbacks per session rather than `2C + 1`.
  Staged-artifact immutability reads, executable verification, tree validation,
  and any normalization write are counted separately and never hidden.
- Timeout, exit-code, unexpected-file, length, cancellation, and command-level
  audit evidence remain attributable to the exact command. The host still
  performs the independent final before/after diff and rejects every write
  outside `allowedWriteRanges`.

No command is parallelized, folded into a BAT/shell string, skipped, or reused
across runs. This adapter phase is R3 and cannot be accepted without exact
full-output golden parity and firmware-owner review.

### Build responsiveness

- The command establishes active-run/progress state and yields the UI dispatcher
  before potentially blocking planning, file I/O, hashing, composition, external
  processing, or report projection.
- CPU and I/O work executes outside the UI dispatcher with the existing
  cancellation token. Only bounded phase/result state is marshalled back.
- ADR 0024 progress transitions are bounded lifecycle events, not polling or
  animation-frame callbacks.
- CI uses deterministic blocking fakes/dispatcher-heartbeat tests. Local B/C
  evidence records click-to-progress and maximum UI-thread blocked intervals;
  wall-clock numbers are evidence, not universal CI thresholds.

### Large change-report scalability

- Canonical report JSON, hashes, classifications, ranges, mutations, processor
  trace, and export remain complete and unchanged.
- JSON parsing and immutable presentation projection move off the UI dispatcher.
  The modal first receives a small summary and issue/outcome state.
- Difference/mutation/operation details are materialized lazily by selected tab
  and bounded page/window. Long collections use a virtualizing control or
  explicit paging/load-more contract rather than an `ItemsControl` that creates
  every row.
- Byte display is a bounded preview plus hashes by default. Legacy/full hex and
  raw JSON are loaded only on explicit disclosure and never expanded into one
  control per byte.
- Pagination changes only the presentation projection. Saving/exporting and
  human evidence review retain the complete report.

### Measurement and phase separation

Node B and C use identical source inputs, expected hashes, tool manifest hash,
machine/runtime settings, warm-up, and run counts. Deterministic tests lock
engine runs, artifact reads, processor sessions, launches, firmware readbacks,
progress events, report rows initially materialized, and output commits. Local
evidence records cold/warm p50/p95, allocations, peak working set,
click-to-progress, UI heartbeat, report-open time, and cancellation latency.

Combiner readback, UI scheduling, report projection, inspection snapshots, and
same-owner buffer copies remain separate commits so a regression can be
attributed and reverted without changing firmware behavior.

## Consequences

### Positive

- Long CtrlRAM Build time is no longer multiplied by a redundant session or
  redundant staging reads.
- Build interaction and report review remain responsive for large inputs and
  large change sets.
- Complete firmware/process evidence remains available for export and review.

### Negative / trade-offs

- The report UI needs an explicit lazy/virtualized presentation model.
- Background result projection requires run-id/cancellation ownership and
  dispatcher tests.
- The shared Combiner adapter change remains gated while 13-command cases lack
  independent full-output golden evidence.

### Risks and mitigations

- In-memory staging state could drift from disk -> one authoritative readback
  after every command and exact next-command/normalization tests.
- Background work could update a stale screen -> bind progress and report
  projection to run id and discard cancelled/replaced results.
- Paging could hide review-required evidence -> summary counts remain global,
  review-required groups sort first, and export always retains all rows.
- Optimization could weaken safety to meet a number -> no ratchet or benchmark
  may remove diff, hash, range, command, atomicity, or golden gates.

## Compatibility and migration

The target behavior is byte-, command-, naming-, report-contract-, and support-
neutral. The Combiner adapter implementation is internal Infrastructure work;
the complete report JSON stays compatible. Additive Application/UI contracts
for progress and lazy presentation require architecture review but do not alter
Domain operations or profiles.

## Verification

- Synthetic 2-command and 13-command tests lock one session, `C` launches, and
  `C` firmware readbacks, including shortened-output normalization.
- NT51926 Common FW 1.4.1 cascade TP-base remains the minimum public full-output
  CtrlRAM parity anchor; full bytes, SHA-256, mutations, changed ranges, command
  trace, and atomic failure are compared.
- Wrong length, missing output, staged-artifact mutation, unexpected file,
  out-of-range byte, crash, timeout, and cancellation still fail closed.
- UI tests prove progress becomes visible before a blocking fake run, heartbeat
  continues, stale runs cannot update state, and reduced motion is respected.
- Large synthetic reports lock summary correctness, bounded initial row/view
  creation, lazy paging, review-required prioritization, bounded byte preview,
  raw JSON disclosure, localization, accessibility, and complete export.
- Every phase runs Polytail; R2 UI/contracts require independent review and the
  R3 Combiner phase requires firmware-owner/golden approval before integration.
