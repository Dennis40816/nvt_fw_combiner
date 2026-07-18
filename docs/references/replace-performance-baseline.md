# 0.9.10 Performance Baseline

Status: node A deterministic baseline plus a provisional node C process
prototype; neither is the reconciled B/C timing result or a firmware support
claim.

Captured: 2026-07-18 on
`feature/0.9.10/replace-performance-baseline`; node A is retained at
`74fb1028` and the provisional process prototype is `315272b3`.

## Purpose and boundary

Node A records the historical orchestration cost before automatic-Build
optimization. It exercises the historical shared Preview/Build gate through
the real Application service, profile compiler, shared Domain engine, typed
ports, immutable reports, and atomic output-writer contract. Test adapters
provide synthetic bytes and external-process audit records; no executable or
firmware fixture is used.

The retained comparison test executes the historical two-run sequence and the
provisional Application-owned single-run path through deterministic clock,
artifact-reader, external-processor, and output-writer adapters. It does not
add a production metrics surface, extend
`composition-report-v1`, change `CompositionRunReport`, add a runtime
dependency, expose timing as a CI threshold, or grant a UI cache authority over
Build. It counts only facts observable at the existing port/report boundary:

- Preview and Build Application runs;
- successfully reported input-artifact reads;
- non-skipped external-processor operation sessions; and
- completed external-process invocations retained in operation reports.

Elapsed time, allocations, UI file inspection, and Infrastructure staging-file
reads require separate measurement owners. They are not inferred from these
counters.

## 0.9.9 synchronization boundary

The `0.9.8-0.9.10` coordination task confirmed on 2026-07-18 that `0.9.9`
owns legacy/V2 retirement, public contracts, the support matrix, typed IC
family/evidence semantics, and workflow exposure. This performance branch must
not recreate those policies or infer them from UI text.

The current integration-safe predecessor is `0.9.9` `7e18cfa0`. The reported
feature phase `980fb39b` is not a rebase base because later support, evidence,
family, Reference FlashCode, and UI work is still uncommitted. This baseline
must therefore be rerun after the reviewed final `0.9.9` integration SHA is
available.

## Node A deterministic results

Command:

```text
.\.dotnet\dotnet.exe test tests\NvtFwCombiner.Bootstrap.Tests\NvtFwCombiner.Bootstrap.Tests.csproj -c Release --filter "FullyQualifiedName~CompositionRunExecutionMetricsTests" --nologo --verbosity minimal
```

| Synthetic case | Composition runs | Successful input reads | Processor sessions | Process invocations | Output commits |
| --- | ---: | ---: | ---: | ---: | ---: |
| Automatic DP Replace, `0x40000` | 2 | 4 | 0 | 0 | 1 |
| Automatic DP Replace, `0x80000` | 2 | 4 | 0 | 0 | 1 |
| Automatic DP Replace, `0x100000` | 2 | 4 | 0 | 0 | 1 |
| Automatic CtrlRAM Replace, 2-command plan | 2 | 4 | 2 | 4 | 1 |
| Automatic CtrlRAM Replace, 13-command plan | 2 | 4 | 2 | 26 | 1 |
| Standalone Preview | 1 | 2 | 0 | 0 | 0 |
| Automatic Build with failed Preview | 1 | 1 successful of 2 attempted | 0 | 0 | 0 |

All successful cases compare the complete final bytes. Automatic Build commits
once; Preview-only and failed Preview commit nothing. The counters are stable
behavior ratchets, not approval to reuse Preview bytes as Build authority.

## Provisional node C process result

The first process prototype moves automatic Build ownership into Application.
It executes current inputs once, runs final-output validation, and atomically
commits exactly that execution's accepted bytes. The explicit `BuildAsync`
path still requires a matching approved Preview token; standalone Preview
remains non-committing.

| Synthetic case | Node A runs / reads / sessions / launches | Provisional C runs / reads / sessions / launches | Commit |
| --- | --- | --- | ---: |
| Automatic DP Replace, each declared capacity | `2 / 4 / 0 / 0` | `1 / 2 / 0 / 0` | 1 |
| Automatic CtrlRAM Replace, 2-command plan | `2 / 4 / 2 / 4` | `1 / 2 / 1 / 2` | 1 |
| Automatic CtrlRAM Replace, 13-command plan | `2 / 4 / 2 / 26` | `1 / 2 / 1 / 13` | 1 |
| Standalone Preview | `1 / 2 / 0 / 0` | `1 / 2 / 0 / 0` | 0 |
| Automatic Build with an unreadable required input | `1 / 2 attempted / 0 / 0` | `1 / 2 attempted / 0 / 0` | 0 |

The comparison test executes both the retained two-run sequence and the
single-run path. It compares complete output bytes, output SHA-256, every
mutation summary, and processor command argv. Additional regression evidence
confirms that a failed final-output validation commits nothing, and that the
NT51926 cascade TP-base full-output golden retains its expected bytes, SHA-256,
changed ranges, mutation hashes, and two-command processor trace. The public
NT51950/NT51951 DP oracle and DP CLI Build cases also pass.

These are deterministic count and parity results on the provisional
`7e18cfa0` predecessor. No elapsed-time, allocation, working-set, or UI-thread
improvement is claimed until final `0.9.9` supplies node B and the same source
is used to rebuild node C.

## Before/after comparison

| Node | Source | Interpretation |
| --- | --- | --- |
| A | `74fb1028` on predecessor `7e18cfa0` | Historical pre-rebase diagnosis only. |
| B | Final reviewed `0.9.9`, before optimization | Authoritative before baseline. |
| C | Same reconciled source plus the `0.9.10` prototype | Compared with B for the performance decision. |

B and C must use the same clean machine/runtime settings, warm-up and run
counts, inputs and expected-output hashes, and Legacy Combiner manifest hash.
The record includes cold/warm p50/p95, allocations, peak working set,
UI-thread blocked time, composition runs, artifact reads, processor sessions,
process launches, staging reads/writes, and output commits. CI locks
deterministic counts and parity; timing remains recorded evidence rather than a
flaky threshold.

The first B/C parity anchors are:

- public deterministic NT51950/NT51951 DP Replace at `0x40000`, `0x80000`,
  and `0x100000`, with owner-golden-backed 51950 256 KiB and 51951 512 KiB
  base smoke cases from
  `testdata/public-synthetic/dp-replace/nt51950-nt51951-dp-replace-oracle-v1.json`
  and `testdata/golden/standard-merge-gen-flash/manifest.json`;
- NT51926 Common FW 1.4.1 cascade TP-base case
  `nt51926-cascade-tp-base-self-regression-20260717`, the only current
  CtrlRAM baseline in this matrix with a committed independent full expected
  output and two-command Legacy Combiner parity, declared in
  `testdata/golden/ctrlram-replace/manifest.json`; and
- UI selection, IC/mode/IC-num change, coverage projection, and report
  projection after the final typed `0.9.9` workflow state is rebased.

CtrlRAM fixtures without an independent expected output may contribute only
count/timing evidence. General Replace has no owner full-output golden and is
excluded from firmware parity claims.

## Infrastructure readback comparison

Before the ADR 0025 adapter slice, Legacy Combiner postbuild performed two full
staging-firmware reads per command: one before the command and one inside
shortened-output normalization, followed by one final read. This is the
source-audited `2C + 1` predecessor model, not a runtime counter from the
Bootstrap baseline.

The conservative slice at `67bc5a4e` reduced that model to `C` full reads but
is superseded by the selective-read phase. Evidence says only `MERGE_MODE` may
shorten output. Normal, NT-based, and CRC-only commands now run consecutively
with one length-metadata check after each command and one complete firmware read
after the whole session.

Before each `MERGE_MODE`, the host preserves only `[maximum declared write end,
expected EOF)`. If the output is short but still covers declared writes, the
host appends the missing suffix from that tail. This retains changes from prior
commands without a pre-command full read. Other short output fails closed.

| Command count (`C`) | Pre-optimization full reads/session | `67bc5a4e` full reads | Selective phase, non-merge full reads | Metadata checks |
| ---: | ---: | ---: | ---: | ---: |
| 2 | 5 | 2 | 1 final | 2 |
| 13 | 27 | 13 | 1 final | 13 |

A plan with `M` merge-mode commands adds `M` partial tail reads and appends only
when a command actually shortens; it still has one final full read. Tail byte
counts remain separate from staged immutable-artifact reads.

Infrastructure tests pass `186` with `2` platform skips and cover two-command
NT51926 final-only behavior, a synthetic 13-command final-only plan,
merge-mode shortening at a non-boundary output length, restoration of a tail
changed by a prior command, and rejection of shortened CRC-only output. The
NT51926 Common FW 1.4.1 cascade TP-base CLI golden passes complete bytes and
SHA-256.

The owner clarified on 2026-07-18 that code size is measured but is not a
`v0.9.10` optimization priority. The maintainable I/O seam and deterministic
counters first updated the exact production ratchet from `56,720` to `56,821`.
The isolated Build scheduling slice then records `56,838`, and the first
background/bounded report slice records `57,115`. The first immutable UI
inspection slice records `57,636`, including exact partial aggregates of
`4,727` for `WorkbenchCompositionService` and `3,082` for
`MainWindowViewModel`; no safety check or evidence was removed to offset these
changes. The first typed Application progress slice records `57,916`; its
bounded feed and lifecycle tests are retained as correctness infrastructure,
not claimed as a performance win based on source size. The lazy report-detail
slice records `58,112`; its acceptance is the deterministic materialization
result below, not a lower line count.
The sequential JSON-slice phase records `58,179`; this is only the exact
maintainability ratchet. Its acceptance is the measured summary-ready and
allocation result with complete report/detail parity, not code size.

Staged immutable-artifact verification reads are separate and must not be
hidden in future evidence. During the conservative phase, the first canonical
full attempt exposed a stale size ratchet and its post-change retry exceeded the
local 60-second command budget before returning a verdict. The selective phase
passes the structure validator at the owner-authorized measured ratchet; the
same full command is not retried again inside that retry budget. Final-`0.9.9`
replay, canonical full verification, independent review, and firmware-owner
approval therefore remain required. The 13-command case has count/sequence
evidence, not independent full-output golden parity, and cannot broaden support.

## Current UI inspection fan-out

The pre-rebase UI asked independent helpers for firmware facts, verified
context suggestion, firmware metadata/version, CtrlRAM region/context,
memory-map rows, and coverage. Several paths reached the current static
`TryReadFirmwareImage` implementation independently.

The first provisional C inspection slice now captures one SHA-256-identified
immutable byte snapshot on a background worker for each file-picker or
drag/drop selection. Firmware facts, bounded IC-marker guidance, verified
IC-number suggestion, output naming, and CtrlRAM firmware-version confirmation
project from that snapshot. Tests lock one artifact-reader call for those typed
facts, publish the selected path before capture starts, prove capture is not on
the UI thread, cancel/reject a stale selection result, and retain facts/output
naming after the selected source is deleted. Re-selecting a path captures a new
identity; Build does not receive the UI snapshot and still performs its own
authoritative reads and hashes.

This is not yet a complete one-read CtrlRAM selection claim. The
category-dependent CtrlRAM region, dynamic slot, coverage, memory-map, and
postbuild-readiness APIs still accept a base path and overlap active `0.9.9`
support/evidence work. They must consume the reconciled snapshot only after the
final `0.9.9` rebase.

Node B must instrument the final `0.9.9` file reader/inspection boundary and
record actual read calls and bytes for cold selection, repeated same-file
selection, IC/mode/IC-num changes, coverage projection, and report projection.
The optimized C target is at most one full-file read for one immutable display
snapshot identity/hash, with cancellation and invalidation when identity or
content changes. Build still performs its own authoritative read and hash.

## Build responsiveness and report scale

Before the first scheduling slice, the ViewModel established run ownership and
then invoked the workbench delegate directly. Synchronous planning before the
delegate reached its first incomplete await could delay the initial progress
repaint. The provisional C slice now captures the selected IC, IC number, mode,
slot paths, mappings, and output options before any yield; it publishes active
state, explicitly yields the dispatcher, and invokes the unchanged
Workbench/Application run on a background worker with the existing
cancellation token. A deterministic blocking fake records progress event order
before worker entry and confirms the worker is not the caller thread. A second
smoke changes the live IC immediately after scheduling and confirms that the
run report retains the captured IC and file bindings.

The first report slice now parses run results and manually loaded reports on a
background worker with cancellation, then publishes the immutable model on the
UI context. The modal binds bounded pages: 8 summary sections, 8 difference
groups, 24 rows per expanded group, 40 mutations, 24 operation-flow/detail or
postbuild rows, and 40 issues at a time. Review-required groups/rows sort first.
Legacy complete hex fields render at most a 64-byte preview while
`LoadedReportJson`, history, save, and export retain the complete payload.

The second report slice now scans the cloned output-difference JSON array only
for stable global counts, expected/review status, section grouping, and summary
rows. Detailed `ReportLineViewModel` instances are memoized by source index and
created only through the page the user requests. Collapsed groups start with
zero detail rows; expanding one group creates at most 24, and Load more creates
only its next bounded page. All group and global views share the same memoized
row instance. A deterministic 1,000-difference/40-section test records 0 detail
row models after summary publication, 24 after first expansion, and 25 after
the final row of that section is requested; loading eight more group headers
leaves the count at 25.

The raw tab, `LoadedReportJson`, history, save, and export continue to retain
the complete JSON. This slice also removes the pager's eager all-item object
array copy. It does retain one cloned `JsonElement` array so lazy rows remain
valid after the parser document is disposed. Authoritative node B/C still must
measure summary-ready/detail-ready latency, allocation, and working set against
the final `0.9.9` report shape; a virtualizing-control comparison remains open.

Node B must record click-to-first-progress, dispatcher heartbeat gap,
summary-ready/detail-ready latency, initial row/control count, allocation, and
working set for bounded public synthetic reports ranging from small to many
thousands of difference rows. Node C keeps complete saved/exported JSON while
moving parsing/projection off the dispatcher and bounding initial materialized
detail through lazy tabs plus virtualization or paging.

### Local performance probe

`tools/NvtFwCombiner.PerformanceProbe` provides the repeatable, non-verifying
measurement entry point for the timing values that must not become CI
thresholds. It records source SHA and dirty state, OS/runtime/CPU/GC settings,
the Legacy Combiner manifest hash, cold plus warm p50/p95, current-thread report
allocation, working-set deltas, bounded report row counts, Build
click-to-active latency, and maximum dispatcher-heartbeat gap. The report cases
contain 24, 1,000, and 10,000 synthetic output differences; saved JSON includes
each generated payload hash. The UI scheduling case uses the tracked NT51926
Standard Merge golden because it requires no external executable and validates
its output SHA before retaining a measurement. It measures only the shared UI
run lifecycle and does not make a Replace support or performance-parity claim.

Run it from the repository root in Release configuration. Output files are
create-new so an earlier B/C record is never silently overwritten:

```text
.\.dotnet\dotnet.exe run --project tools\NvtFwCombiner.PerformanceProbe\NvtFwCombiner.PerformanceProbe.csproj -c Release -- --warmup 2 --iterations 10 --output <new-evidence-path>.json
```

The probe is a local evidence producer, not another repository verifier.
`python scripts/verify.py --all` remains the only canonical final gate, while
deterministic composition, artifact-read, processor-session, process-launch,
staging-read, output-commit, progress-event, and byte/report-parity assertions
remain in their focused tests. After final `0.9.9` reconciliation, nodes B and C
must run this command under identical options and machine/runtime conditions;
the resulting JSON remains untracked review evidence unless the owner approves
an explicit sanitized evidence record.

## Typed run-progress core

The first provisional ADR 0024 Application slice now owns seven stable phases:
preparing, input reading, composition, external processing, final validation,
output commit, and report preparation. Each run receives one bounded
asynchronous feed containing at most those seven immutable transitions.
Duplicate processor operations coalesce into one processor phase, skipped work
is never marked completed, and failure or cancellation completes the feed at
the last truthful phase. Application enqueues snapshots and does not execute a
host/UI callback inline, including after atomic output commit.

Seven focused tests lock Preview and automatic-Build order, two external
operations with one processor transition, input failure, cancellation, final
validation rejection, and commit-adapter failure. This is lifecycle contract
evidence only: Bootstrap consumption, run-id ownership at the ViewModel,
localized step labels, accessible live status, high contrast, and reduced
motion remain pending until the final `0.9.9` UI-contract rebase. Node B/C must
also record event count, click-to-first-step, dispatcher heartbeat impact, and
cancellation latency; no lifecycle ordinal is a byte percentage.

## Follow-up gates

- The provisional `v0.9.10` process prototype reduces a valid automatic Build
  from two composition runs and two processor sessions to one while retaining
  an authoritative Build read, validation, the explicit Build
  preview-token/fingerprint path, and atomic promotion. It must be replayed
  against final `0.9.9` before acceptance.
- UI inspection begins only after the `0.9.9` typed workflow contracts freeze.
  One immutable asynchronous snapshot may serve display projections for one
  file identity/hash, but never Build authority.
- ADR 0024 Application progress is provisionally implemented behind a bounded
  asynchronous feed. Bootstrap/UI wiring begins from the reconciled UI
  boundary; Presentation localizes and animates only the active step, honors
  reduced motion, and never fabricates byte percentage or infers phases from
  firmware-facing text.
- ADR 0025 makes Combiner, Build responsiveness, and large-report scalability
  first-class gates. Exact sequential commands remain `C` launches; non-merge
  plans use metadata plus one final full read, and merge-mode plans add only
  selective tail preservation. UI work moves off the dispatcher and complete
  report evidence is presented through bounded lazy projections rather than
  eager control creation.
- Infrastructure read-model changes remain separate R3 work and are limited to
  exact full-output golden scope plus firmware-owner review. Exact output
  bytes, hashes, command argv/order, mutations, differences, warnings, and
  failed-run atomicity remain mandatory parity.
- `v0.9.11` and later routing is deliberately unassigned until reviewed
  `v0.9.9` and `v0.9.10` merge and the owner accepts the B/C evidence.
