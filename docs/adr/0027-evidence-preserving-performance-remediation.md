# ADR 0027: Preserve firmware evidence during end-to-end performance remediation

- Status: Accepted program for `v0.9.10`; R3 processor phases remain gated
- Date: 2026-07-18
- Owners: Product owner + architecture owner + firmware/process reviewer + UI reviewer
- Amends: ADR 0009's General Replace Build-orchestration clause; validation remains mandatory inside one authoritative Build execution
- Amended by: 2026-08-09 complete legacy-architecture retirement; 2026-08-12 evidence-sharded .NET CI
- Supersedes: The former `v0.9.10` candidate-intake assignment
- Superseded by: None

2026-08-09 amendment: the performance, evidence, immutable-byte, replay, and
single-execution decisions in this ADR remain accepted. The complete-retirement
program supersedes only the migration-era requirement to preserve the
`WorkbenchRunResult` positional CLR ABI or transport the inspection snapshot
through that envelope. `CompositionRunResult` and its typed report/inspection
results become the sole in-process Application outcome, while persisted report
wire compatibility and older report readability remain unchanged. References
below to the Workbench transport describe the historical `v0.9.10` solution,
not terminal architecture authority.

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

Atomic output commit and report readiness are separate typed UI delivery
states. A successful Build publishes its committed output identity when
`preparing-report` begins, so the destination BIN is truthfully available
before complete JSON, Hex Diff, and history projection finishes on a worker.
Preview and uncommitted failure paths never publish an artifact. The active run
continues to own the command until report publication completes, preventing a
second Build from racing report/history state.

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

At the `v0.9.10` checkpoint, Bootstrap transported the snapshot through an
additive non-positional `WorkbenchRunResult` property. The then-existing positional constructor and
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
The host treats the exact command plan as one private staging pipeline: each
command consumes the staging file left by its predecessor, and the host does
not materialize the complete firmware between commands merely to observe an
intermediate value. Intermediate gates retain process exit/timeout,
cancellation, hidden-window, expected-file existence/length, and
unexpected-file rejection. Only an evidenced short-output normalization or
selective-tail dependency may perform the smallest necessary intermediate
read.

After the last successful command, the host performs one complete firmware
read and independently diffs it against the pre-pipeline baseline. The final
changes must remain inside the union of the command plan's declared write
ranges, and output SHA/report facts must match the approved full-output golden.
No output is promoted after an intermediate failure. The owner clarification
on 2026-07-18 explicitly makes final pipeline state authoritative; intermediate
full-image values are not product evidence and are not retained as a gate.

This slice is R3. Its path-independent pipeline semantics are implemented and
tested without an external-tool or golden physical path. Before a `MERGE_MODE`
command that is allowed to return a shortened file, the adapter snapshots only
the tail after that command's minimum declared block coverage. A full-length
result discards that snapshot; a valid shortened result appends the exact tail
from the preceding pipeline state. Other command families fail closed on a
shortened result. No command output is otherwise materialized between launches,
and the final import contains the only complete staging-file read.

The later predecessor-layout reconciliation supplies the exact reviewed commit
and external-tool/golden-layout reference for physical execution and final
parity closure; it is not evidence that intermediate images need observation.
The NT51926 Common FW 1.4.1 cascade two-command case remains the minimum public
full-output parity anchor. Thirteen-command cases are count/timing evidence only
until an independent pre-base and expected output are approved. The executable
counter contract is one final successful full read per processor session, zero
intermediate full reads by default, and only explicitly evidenced selective
tail reads for shortened-output normalization.

### External processor environment lifetime

Bootstrap owns one lazily initialized external-processor environment per OS
process. The first applicable run locates the external-tools root, enumerates
and parses manifests, builds the hash-pinned registry and router, and publishes
that immutable environment with execution-and-publication thread safety.
Concurrent and later calls reuse the same router. An unavailable root and an
initialization exception are also retained, so a Build cannot repeatedly scan
or switch to mutable manifest state during the same process.

Installing, removing, or changing a manifest/tool layout therefore requires an
application or CLI-process restart. This is the explicit refresh boundary; no
physical root is embedded in source and no environment survives a restart.
Per-run processor sessions, private staging directories, process launches, and
output validation remain independent. The cached manifest registry does not
cache executable trust: the resolver still checks executable existence and
SHA-256 against the retained manifest before each transform.

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

### 0.10.x Canonical Core Convergence amendment

Within one accepted user operation/revision, every expensive semantic step is
performed once and its immutable result is shared by downstream projections:

- one trusted document parse/normalization per catalog publication;
- one capability resolution per matching authoring revision and resolution
  token;
- one artifact read/inspection per matching file stamp and definition hash;
- one compilation and one engine execution per Preview/Build attempt; and
- one execution per declared processor stage.

Output naming, Memory Layout, report generation, UI, and CLI formatting consume
the accepted immutable result. They do not reread firmware, re-resolve,
recompile, rerun the engine, or rerun a processor. This is orchestration-owned
single evaluation, not correctness dependence on a process-global cache.
Clearing optional caches may require recomputation in a later operation but
cannot change results.

Deterministic CI gates prefer invocation/work counts, byte parity, bounded
allocation, and unique test ownership over flaky universal timing limits.
Local/CI evidence still records cold/warm p50/p95, allocation, and peak memory;
a timing ratchet is introduced only after a stable reproducible baseline.
Canonical verifier lanes must not execute the same test owner twice.

### 2026-08-12 evidence-sharded .NET CI amendment

The public `python scripts/verify.py --all` command remains the complete local
and release verifier. The pull-request workflow may execute its .NET evidence
owners on separate runners only through the following closed DAG, which
supersedes the earlier single-runner planning restriction:

- one Windows producer owns the pinned SDK, Windows process-orchestration
  probe, restore, evaluated source-ownership check, whitespace formatter, and
  complete Release solution build;
- three Windows test producers own the exact `bootstrap`, `ui`, and `core`
  project partitions declared only in `scripts/verify.py`; each project is run
  unfiltered with one TRX and one paired JSON/Cobertura report;
- one always-run finalizer retains the stable required-check name
  `dotnet / build-test`, validates all producer results and manifests, and is
  the only CI owner that aggregates .NET coverage and validates the CtrlRAM
  fixture manifest.

The partition is a disjoint and complete set of eight test projects. Ordinary
test failure does not skip later projects in the same shard; timeout,
cancellation, or workflow termination stops that runner and its owned process
tree. A build failure is primary. Missing, failed, duplicate, unknown,
wrong-SHA, wrong-SDK, path-escaping, symlinked, hash-mismatched, counter-drifted,
or extra evidence fails the finalizer before coverage or fixture acceptance.
Artifacts contain logs, TRX, and coverage only; they never contain firmware
payloads, private fixtures, build outputs, `bin`, or `obj`.
Each producer copies only its declared regular files into a clean upload root.
The finalizer downloads the four exact artifact names into separate roots,
validates each root before forming the logical union, and rejects cross-owner
path reuse rather than flattening producer provenance.

This is scheduling of existing evidence owners, not a second verifier. The
workflow contains no project paths, filters, coverage merge logic, expected
test counts, or firmware rules. Release/package workflows continue to run
`python scripts/verify.py --all`. Acceptance requires two fresh same-SHA runs
whose workflow-to-finalizer duration is at most 300 seconds, with all eight
projects, the two declared Infrastructure skips, GoldenRegression 17/17,
coverage policy, fixture validation, and cleanup unchanged. One miss stops the
experiment rather than adding shards or weakening a gate.

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
- R3 Combiner final-read pipeline cannot be claimed until the external-tool/
  golden layout, full-output evidence, and firmware-owner review are available.

## Compatibility and release impact

The target was byte-, command-, naming-, report-schema-, support-, and package-
neutral. Typed progress and the in-memory inspection snapshot were additive
Application/Bootstrap/UI contracts. That checkpoint's snapshot transport
preserved the `WorkbenchRunResult` positional ABI and was excluded from JSON;
the 2026-08-09 amendment retires that CLR transport while preserving these
external and evidence properties.

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
