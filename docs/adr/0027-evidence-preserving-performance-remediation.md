# ADR 0027: Preserve firmware evidence during end-to-end performance remediation

- Status: Accepted program for `v0.9.10`; R3 processor phases remain gated
- Date: 2026-07-18
- Owners: Product owner + architecture owner + firmware/process reviewer + UI reviewer
- Amends: ADR 0009's General Replace Build-orchestration clause; validation remains mandatory inside one authoritative Build execution
- Supersedes: The former `v0.9.10` candidate-intake assignment
- Superseded by: None

## Context

`v0.9.10` is an end-to-end process, I/O, allocation, report, and UI performance
milestone. It is not only an animation or source-size task. Source and baseline
evidence identified these costs:

1. Automatic Build historically ran complete Preview and Build executions,
   duplicating input reads, composition, external processing, validation,
   hashes, differences, report generation, and allocation.
2. Legacy Combiner sessions perform repeated full staging-firmware readback.
3. Synchronous work before an incomplete await, report projection, report
   history persistence, relocalization, or file inspection can delay the UI
   dispatcher.
4. Large reports can eagerly parse, materialize, retain, relocalize, and render
   more state than is visible.
5. The existing Change Report is list-first and has only bounded hex previews,
   although reviewers need a spatial before/output comparison.
6. Exact-length firmware images cross multiple defensive copy boundaries; some
   copies belong to distinct trust owners, while others may be redundant.

Performance work must not weaken exact commands, output evidence, immutable
input, staging confinement, changed-range validation, atomic output, support
truth, or golden parity.

## Decision drivers

- Reduce dominant repeated execution, process, full-file I/O, allocation, and
  UI-thread costs measured on the same before/after inputs.
- Keep Legacy Combiner command order, argv, tool identity, and failure
  attribution exact.
- Keep complete machine-readable report evidence even when UI rendering is
  bounded.
- Make Preview, Build, progress, report review, and history interaction
  responsive and cancellable.
- Prefer deterministic count/ownership gates over flaky universal timing
  thresholds.

## Considered options

1. Optimize only Presentation animation.
2. Parallelize or combine Legacy Combiner commands.
3. Delete or truncate report differences and byte evidence.
4. Preserve execution semantics and remove redundant work at each owning
   boundary.

## Decision

Select option 4 as separately reviewable `v0.9.10` phases.

### One authoritative Automatic Build

Automatic Build executes the Application composition use case once and commits
that same validated output. It does not run Preview and Build back-to-back.
Deterministic tests lock one execution, artifact-read, processor-session, and
commit count while preserving output bytes/hash and the compared mutation,
validation, issue, and processor-command report facts. This amends ADR 0009's
phrase "preview-before-build": General Replace Build still performs the same
current-state planning and validation before commit, but inside this one
authoritative execution rather than by invoking a complete Preview run first.

### Dispatcher and large-report responsiveness

Active-run state is published before potentially expensive work. Planning,
file I/O, hashing, composition, external processing, JSON projection, history
projection, and relocalization execute outside the dispatcher where their
contracts permit. Only bounded immutable state is published back, guarded by
cancellation and monotonic generation ownership.

Canonical report JSON, hashes, classifications, ranges, mutations, processor
trace, raw export, and legacy readability remain complete. Presentation uses
sequential indexing, bounded pages, lazy details, compact inactive history
entries, off-dispatcher persistence/projection, and latest-request ownership.
Paging or virtualization changes only Presentation materialization; it cannot
hide review-required counts or alter export evidence.

### Successful Replace inspection snapshot

Application may attach a full before/output inspection snapshot only when all
of these are true:

- final `CompositionRunResult.Status` is `Succeeded`;
- composition kind is Replace;
- the selected output initializer is a canonical reference initializer;
- the exact initializer `ReferenceSpaceId` exists in the authoritative bound
  inputs; and
- reference and final output lengths are equal.

The reference is the defensive byte copy already owned by Application input
binding. `CompositionRunResult` owns one defensive output copy, and its public
output plus inspection snapshot share that same output backing. The snapshot
does not create another full output copy. It transports both the canonical
reference-space id and the authoritative compiled output-space id from the
Application plan. Merge, input failure, validation or preview-token publication
failure, planning-only, and blocked results carry no snapshot.

Bootstrap transports the snapshot through an additive non-positional
`WorkbenchRunResult` property. The existing positional constructor and
`Deconstruct` shape stay intact. The property is JSON-ignored. The snapshot is
not part of `CompositionRunReport`, `composition-report-v1`, raw report JSON,
saved report history, or report export.

This snapshot is session-local inspection evidence. Presentation must not
reopen a source path to recreate it, persist the bytes, use it as Preview/Build
input, infer firmware meaning from it, or treat it as support evidence. Build
continues to read and hash inputs through the Application use case. A report
reopened after process restart degrades explicitly to its stored bounded hex
preview and range evidence unless a future separately reviewed artifact
attachment contract verifies complete bytes by size and SHA-256.

### Read-only Change Report Hex Diff

The Changes experience becomes a resizable two-column review workspace:

- a visually dominant left viewport shows bounded/virtualized 16-byte final
  output rows with address, hex, and ASCII; no editing, undo, save, work buffer,
  or per-byte structural source map is admitted;
- `Show original rows` inserts the verified reference row below each visible
  changed output row without duplicating the full comparison buffers;
- the upper-right information panel shows Application/report-owned reason,
  expected versus review-required verdict, evidence, section/field subject,
  changed count, and before/after hashes;
- the lower-right range navigator lists review-required ranges first, then
  expected ranges in address order; every range is a half-open offset range in
  the snapshot's authoritative compiled output address space, and keyboard or
  pointer selection jumps the viewport there and synchronizes all panels; and
- color is never the only signal; keyboard focus, accessible text, high
  contrast, and reduced motion retain equivalent state.

Only visible rows/cells are materialized. Range lookup uses an ordered index,
not a complete scan on every pointer or scroll event. Report classifications
and semantic reasons remain authoritative; Presentation never derives firmware
meaning from an address. Preview-only historical data is labelled as such and
must never masquerade as a complete Hex Diff. Presentation displays the typed
output-space id in address labels and validates every jump against that space's
snapshot length; it never invents the identity from profile, IC, or UI labels.

### Legacy Combiner and staging I/O

Combiner commands remain sequential, exact, and attributable. No command is
parallelized, folded into a BAT/shell string, skipped, or reused across runs.
Any reduction in intermediate full-firmware readback must preserve staged-file
existence/length checks, documented short-output normalization, unexpected-file
rejection, independent final before/after diff, allowed-write-range validation,
timeouts, cancellation, and hidden process windows.

This slice is R3. It waits for the owner-coordinated `0.9.9.5`
predecessor-layout convergence gate and the same-source node B baseline. That
identifier is not a release tag or an assumed branch. The gate is unresolved
until the owner supplies the exact reviewed commit SHA and its external-tool and
golden-layout reference; P2 must record both before implementation begins. The
NT51926 Common FW 1.4.1 cascade two-command case is the minimum public
full-output parity anchor. Thirteen-command cases are count/timing evidence only
until an independent pre-base and expected output are approved.

### Measurement and code size

Node B and C use the same reconciled source, inputs, expected hashes, tool
manifest hash, machine/runtime settings, warm-up policy, and run count.
Deterministic CI evidence locks counts and parity. Local evidence records
cold/warm p50/p95, allocation, GC, peak working set, dispatcher heartbeat,
report stages, first Hex Diff page, jump latency, and cancellation latency.

Source size remains an exact maintainability measurement, not a `v0.9.10`
performance KPI. No phase may raise a ratchet merely to pass, or remove a safety
check, report fact, golden, or test to lower source size.

The executable counter scope and remaining record fields are maintained in the
[v0.9.10 Replace Performance Baseline](../references/replace-performance-baseline.md).

## Consequences

### Positive

- Build no longer multiplies complete composition/process work.
- Large report and history interaction remain bounded and responsive.
- Change review gains spatial byte context without reusing editable editor
  state or serializing full firmware into reports.
- Copy removal is explicit about trust ownership and testable backing identity.

### Negative / trade-offs

- An in-session complete Hex Diff is unavailable after restart unless a future
  verified attachment contract is approved.
- Report UI needs a focused read-only viewport and range index.
- R3 Combiner readback cannot be claimed until the external-tool/golden layout,
  full-output evidence, and firmware-owner review are available.

## Compatibility and release impact

The target is byte-, command-, naming-, report-schema-, support-, and package-
neutral. Typed progress and the in-memory inspection snapshot are additive
Application/Bootstrap/UI contracts. The snapshot transport preserves the
existing `WorkbenchRunResult` positional ABI and is excluded from JSON.

Candidate-IC intake, new workflow support, profile promotion, range changes,
processor authority, report-schema redesign, and release publication are not
part of this milestone. Their later version is selected only after reviewed
`v0.9.9` and `v0.9.10` merge.

## Verification

- DP Replace uses 256/512/1024 KiB deterministic cases; CtrlRAM uses exact
  two-command and 13-command counters with failed-input zero-commit coverage.
- Automatic Build locks one execution, complete output-byte/hash parity, and
  parity of the mutation, validation, issue, and processor-command facts named
  by the executable baseline.
- Snapshot tests lock canonical output reference selection, caller isolation,
  shared result/output backing, successful Preview and Build transport,
  publication-failure absence, and JSON exclusion.
- Hex Diff UI acceptance tests must cover small, 10,000-range, and fragmented
  reports; bounded visible row/control count; typed output-space address labels
  and jump validation; review-first order; original-row toggle; fallback;
  localization; accessibility; high contrast; reduced motion; stale completion;
  and cancellation. The H1 consumer and executable coverage now include
  non-color Changed/Original/Selected/verdict cues, a two-pixel selected
  outline, and the exact production run projection path with its verified
  snapshot. Pre-cancel publishes no partial report state, and an identity-
  matched 10,000-range stale run cannot overwrite a newer report or append
  history. Rendered Windows high-contrast/manual visual evidence remains
  pending; the source contract does not substitute for effective-theme
  rendering.
- Every phase runs Polytail and independent architecture/UI review. R3 process
  changes additionally require exact golden parity and firmware-owner review.
- `python scripts/verify.py --all` remains the final branch gate after the known
  source-ratchet and predecessor-layout gates are reconciled; thresholds are not
  changed as a shortcut.
