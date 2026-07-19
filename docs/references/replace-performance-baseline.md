# v0.9.10 Replace Performance Baseline

Status: deterministic orchestration/count baseline on the reviewed `v0.9.9`
source line plus local fragmented-report allocation evidence; wall-clock,
Infrastructure staging-read, and final same-source node B/C evidence remain
open.

Captured: 2026-07-18. Executable owner:
`tests/NvtFwCombiner.Bootstrap.Tests/CompositionRunExecutionMetricsTests.cs`.

## Purpose and boundary

The harness compares the retained Preview-then-Build sequence with the
Application-owned single authoritative Automatic Build path. It uses the real
Application service, compiler, Domain engine, typed ports, immutable reports,
and output-writer contract with deterministic in-memory adapters.

It does not execute a physical tool, hard-code a fixture/tool path, add a
production metrics type, extend a report schema, expose timing as a CI
threshold, change support, or authorize UI-cached bytes for Build. It counts:

- complete Application runs;
- attempted and successful input-artifact reads;
- external-processor sessions;
- external process invocations retained in the operation report; and
- output commits.

## Deterministic comparison

| Synthetic case | Retained sequence runs / successful reads / sessions / launches | Single Automatic Build runs / successful reads / sessions / launches | Commits |
| --- | --- | --- | ---: |
| DP Replace `0x40000` | `2 / 4 / 0 / 0` | `1 / 2 / 0 / 0` | 1 |
| DP Replace `0x80000` | `2 / 4 / 0 / 0` | `1 / 2 / 0 / 0` | 1 |
| DP Replace `0x100000` | `2 / 4 / 0 / 0` | `1 / 2 / 0 / 0` | 1 |
| CtrlRAM Replace, two-command plan | `2 / 4 / 2 / 4` | `1 / 2 / 1 / 2` | 1 |
| CtrlRAM Replace, 13-command plan | `2 / 4 / 2 / 26` | `1 / 2 / 1 / 13` | 1 |
| Standalone Preview | `1 / 2 / 0 / 0` | unchanged | 0 |
| Automatic Build with one unreadable required input | no second run | `1 / 1 successful of 2 attempted / 0 / 0` | 0 |

Successful comparisons lock complete output bytes, output SHA-256, mutation
summaries, validation summaries, issue summaries, and processor argv. The
count reduction is not authority to reuse a prior Preview output: Automatic
Build still performs its one authoritative input read, execution, validation,
and atomic commit.

Run the executable baseline from the repository root:

```text
dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj --filter "FullyQualifiedName~CompositionRunExecutionMetricsTests"
```

## Shared artifact read contract

General Merge and General Replace may bind one immutable source artifact to
multiple mapping-specific address spaces. Within one Application run, let `K`
be the number of bindings and `U` the number of unique exact artifact ids whose
reads succeed. The current source-audited contract performs `U` reader calls
and `U` SHA-256 calculations instead of `K`, while retaining `K` independent
Application-owned address-space buffers, input summaries, normalization, and
validation paths.

The snapshot key is the exact artifact id: it does not normalize physical
paths and does not survive across runs. A failed read is not cached, so each
binding retries the reader and preserves its own typed failure. Tests lock a
shared successful artifact to one read with two independent outputs, and the
same missing artifact to two attempted reads and two issues. This is a
deterministic count reduction, not a universal elapsed-time claim.

## Raw Hex Editor memory baseline

The editable Raw Hex Editor must retain original bytes, working bytes, and one
source-address identity per current byte so insert/delete and undo/redo remain
exact. Commit `703f5555` stores the internal identity as `int` with a private
`-1` inserted-byte sentinel instead of `int?`; nullable original addresses are
recreated only for the public bounded viewport.

For the exact 1 MiB `Load` allocation test, the observed current-thread
allocation changes from `10,486,120` to `6,299,752` bytes, a reduction of
`4,186,368` bytes (`39.9%`). The per-byte mapping is four bytes smaller, so its
retained saving is approximately 32 MiB at the existing 8 MiB document limit.
Insert, delete, changed-range, viewport, undo/redo, and search cache behavior
remain locked. Final packaged 8 MiB load/edit/search latency and process
working set remain in the Windows manual gate.

Commit `725dc041` removes another full-document scan after a structural edit
when working and original lengths are unequal, because the length difference
already proves dirty state. Overwrites in that unequal-length state also avoid
the scan; when a later insert/delete restores equal length, the complete source
identity/value comparison still runs. The 1 MiB insert one-sample observation
changes from `13.007` to `3.151` ms (`75.8%`). This is not a universal timing
claim; exact dirty, insert/delete, undo/redo, and equal-length restoration
behavior remains the executable gate.

## Fragmented report microbaseline

`CompositionReportPerformanceBaselineTests` executes one real Application
Preview over a 20,000-byte Replace image with 10,000 disjoint one-byte
differences. It locks the complete output and report representation:

- output SHA-256
  `e7b39a736b02c1793f1c22ab4c21e29bc478bd94465614c27bd70c4ac42c25b4`;
- indented JSON length `11,720,520` characters; and
- JSON SHA-256
  `16d46159b46bcb3acdd27783321b21504a721f01e4dddef43f10fc336a49c937`.

On the same local .NET 10 test path, run-through-report current-thread
allocation changed from `20,162,040` bytes at the initial baseline to
`11,719,480` bytes after the bounded P1 slices, a reduction of `8,442,560`
bytes (`41.9%`). The exact output/JSON facts above did not change. The latest
isolated serialization run measured `58,057,368` bytes and `132.240` ms for the
first call, then `23,761,648` bytes and `52.627` ms for an exact repeated call.
The repeated allocation is only
`320,608` bytes (`1.37%`) above the `23,441,040`-byte payload floor of the final
UTF-16 string. This supports treating recurring serialization as close to its
unavoidable representation cost.

Commit `6b4379d2` reuses the universal SHA-256 text for each of the 256
possible single-byte report slices. The immediate isolated before/after
observations for the exact command above changed run-through-report allocation
from `11,646,384` to `7,541,912` bytes, a reduction of `4,104,472` bytes
(`35.2%`). The one-sample run-through time changed from `132.869` to `79.421`
ms, which is recorded only as an observation. The output SHA, JSON character
count, JSON SHA, all 10,000 ranges and hashes, and multi-byte slice hashing are
unchanged; repeated same-source p50/p95 remains the final timing gate.

A clean-process attribution experiment rented the shared `ArrayPool<byte>`
geometric buckets from 16 KiB through 16 MiB before serializing the same report.
Those buckets allocated `33,538,536` bytes; after they were returned, the exact
first serialization allocated `24,519,944` bytes. Their sum, `58,058,480`,
reproduces the observed cold allocation within `1,112` bytes. The pool buckets
therefore explain `33,538,536` of the `34,295,720` cold-versus-repeated gap
(`97.79%`); the post-return first serialization remains `758,296` bytes above
the repeated call. This attributes the dominant cold inflation and rules out
another retained full-report copy without claiming every first-call byte is
explained. A separate 16 MiB `DefaultBufferSize` candidate preserved the exact
JSON SHA and
reduced allocation to `41,267,016` bytes, but took `208.190` ms versus the
`132.240` ms cold baseline sample. It did not demonstrate a first-call latency
improvement and was rejected: pre-sizing or warming only moves the pool cost
and can over-rent for normal reports. No production serializer or schema change
follows from this result.
Fresh-process working set and latency remain part of final Node C measurement.
These numbers are diagnostic local evidence, not universal CI thresholds.

Run the focused evidence from the repository root:

```text
dotnet test tests/NvtFwCombiner.Application.Tests/NvtFwCombiner.Application.Tests.csproj --filter "FullyQualifiedName~CompositionReportPerformanceBaselineTests" --logger "console;verbosity=detailed"
```

## Hex Diff projection microbaseline

`ReportHexDiffEmitsColdWarmProjectionAndJumpObservations` uses the verified
in-session Replace snapshot and a synthetic 10,000-range report. One isolated
test process records cold and repeated projection-to-first-bounded-page time,
current-thread allocation, GC deltas, testhost working set, and one off-page
address jump. The assertions retain exact report/snapshot output identity,
64-row navigator pages, at most 65 materialized navigator rows after a pinned
selection, and the selected range semantics. The repeated projection
intentionally keeps the cold model alive while the successor is built,
matching the transient publication
handoff in which the previous report remains current until its replacement is
complete; `workingSetAfterWarm` is therefore a handoff snapshot, not a claim
about steady retained state. It is sampled immediately after the successor
projection and before the measured jump. `testhostLifetimePeakWorkingSet` is
the testhost process high-water since startup, including fixture composition
and JSON setup; it is not an isolated projection-interval peak.

Run the focused observation from the repository root:

```text
dotnet test tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj --filter "FullyQualifiedName~ReportHexDiffEmitsColdWarmProjectionAndJumpObservations" --logger "console;verbosity=detailed"
```

The `HEX_DIFF_BASELINE` line is non-gating local evidence. Final Node B/C p50
and p95 values require the same isolated-process run count, machine/runtime,
input hashes, power mode, and monitoring setup at both nodes. A single warm
repeat is not a percentile claim, and testhost working set does not replace the
packaged Windows first-frame/manual acceptance record.

Using the same local .NET 10 exact-filter command in isolated test processes,
the retained `aaa9edca` observation allocated
`10,883,824`/`10,869,728` bytes for cold/repeated projection. Commit `65da6758`
creates each per-index `Lazy<T>` only when that report row is requested; the
same observation allocates
`9,203,800`/`9,189,704` bytes. Each path removes exactly `1,680,024` bytes
(`15.4%`) while preserving output SHA, `6,163,125` JSON characters, the
10,000-range identity/bounds assertions, 64/65 navigator materialization, and
the `51,952`-byte jump allocation. The corresponding one-sample elapsed values
were `131.124`/`102.023` ms before and `140.850`/`119.596` ms after, so this
slice makes no latency-improvement claim. Repeated same-source p50/p95 and
packaged first-page evidence remain open.

Commit `c22c2a68` removes the additional full-report UTF-8 mirror from the
long-lived lazy row and Hex Diff factories. The production shell and report
history already retain the same original JSON string, so those factories now
share that string and parse exact UTF-16 character slices; the transient UTF-8
buffer is retained only for the initial root parse and wire index. An isolated
temporary full-GC diagnostic measured the 10,000-range model's retained delta
at `7,567,592` bytes before and `1,491,936` bytes after, a reduction of
`6,075,656` bytes (`80.3%`). The diagnostic was removed rather than promoted to
a reachability-sensitive CI threshold. It is product-path evidence, not a
general claim for standalone `FromJson` callers that would otherwise release
their input string.

The same isolated projection observation allocates `9,553,408`/`9,531,816`
bytes after this retained-memory change, an increase of `349,608` (`3.8%`)
and `342,112` (`3.7%`) bytes over the preceding cold/repeated values. This is
an explicitly retained transient-allocation tradeoff, not a latency
improvement claim. Strict UTF-8 validation, raw surrogate-pair coverage,
last-top-level-property semantics, cancellation, output identity, bounded
materialization, and jump behavior remain locked. Repeated same-source p50/p95,
packaged first-page evidence, and Windows working-set observation remain open.

Commit `64a45efd` also reuses the UTF-8 byte count already produced by a
successful JSON projection when the active report is captured or relocalized
in history. This removes the prior second full-string UTF-8 sizing scan from
the dispatcher after report publication. Older persisted metadata entries and
parse-error reports retain the original fallback scan, and the small artifact
path is still counted. The history schema, JSON payload, warning threshold,
entry order, and reopen behavior are unchanged. This is a source-audited
whole-report scan removal; packaged click-to-history timing remains open.

Commit `7185c0e2` bounds retained best-effort local history by both 12 entries
and a 16 MiB UTF-8 soft budget. The newest report is never dropped; older
entries are evicted from the tail until the budget is met. A single newest
report may exceed the soft budget so active review is not lost. Persisted
history files above 64 MiB are rejected from file metadata before their JSON
text is read, bounding legacy/corrupt startup input. The machine-readable run
report and local history schema are unchanged. This is a deterministic
retention/I/O bound; packaged startup working set remains open.

## Startup report restoration baseline

Commit `7b6f1ab6` removes persisted-history and explicit startup-report work
from the window constructor. The production path begins after `OnOpened`,
yields once, reads local history on a worker, prepares the bounded 12-entry
history and latest review on a worker, then reads and projects any explicit
`--load-report` source on a worker. Publication order remains history, startup
argument diagnostics, explicit report, and finally `--open-report`.

Deterministic tests lock successful publication, source failure, malformed
JSON exclusion, valid-but-invalid-shape error degradation, close cancellation,
and latest-generation rejection for both slow history and slow explicit
sources. A stale source is rejected before the expensive report projection.
These are responsiveness and ordering contracts, not elapsed-time evidence.
The exact packaged candidate must still record cold first-interactive-frame
behavior with a full 12-entry history and with an explicit large report; those
rows remain in the Windows manual gate.

Commit `9bfc780f` also removes preference-file serialization and atomic
promotion from the dispatcher property-change handler. Report history and
immutable shell preferences now share the same typed, serialized,
latest-wins coordinator and the bounded close drain waits for both. Tests lock
cancelled-save non-publication, atomic latest-snapshot promotion, superseded
queue cancellation, fault recovery, and close completion. This is a UI-thread
I/O ownership improvement; no elapsed-time claim is made until the packaged
rapid-settings row is recorded.

Commit `1c0dc687` then resolves the persisted language before constructing the
shell ViewModel. The language selector is aligned during guarded
initialization, so a Traditional Chinese startup no longer builds the complete
English projection and immediately relocalizes it a second time. Normal
post-construction language changes remain active. This is a deterministic
single-initialization contract; packaged first-frame evidence remains open.

## Firmware evidence scope

The synthetic counter cases are process-contract evidence, not firmware
support or golden claims. Final same-source node B/C uses:

- NT51950/NT51951 public deterministic DP Replace at 256/512/1024 KiB;
- owner-golden-backed NT51950 256 KiB and NT51951 512 KiB base smoke where the
  tracked manifests apply; and
- NT51926 Common FW 1.4.1 cascade two-command full-output parity as the minimum
  CtrlRAM golden anchor.

The 13-command case remains count/timing-only. General Replace contract tests
do not substitute for an owner full-output golden.

## Remaining node B/C record

Before and after results must use identical source, input/expected hashes,
Legacy Combiner manifest hash, machine/runtime settings, warm-up policy, and
run count. Record:

- cold/warm p50 and p95;
- allocations, GC counts, and peak working set;
- dispatcher heartbeat and click-to-first-step;
- composition runs, artifact reads, processor sessions, launches, staging
  metadata/full/selective reads and writes, and commits;
- report discovery, hashing, creation, serialization, projection, history
  capture/save/reopen/relocalization;
- Hex Diff first bounded page and address-jump latency; and
- output SHA, mutations, report facts, process-window behavior, cancellation,
  and atomic-failure parity.

Legacy Combiner final-read implementation and any physical tool/golden layout
dependency wait for the owner-coordinated `0.9.9.5` predecessor-layout
convergence gate. This is not a release tag or assumed branch: P2 remains
blocked until the owner supplies the exact reviewed commit SHA and its
external-tool/golden-layout reference. The target is already fixed: one private
sequential pipeline, zero intermediate full reads by default, one full read
after the final successful command, final diff against the pre-pipeline bytes
and the union of declared write ranges, and only evidenced selective reads or
normalization. Intermediate full-image values are not parity evidence. No final
performance or release claim is made until the same-source B/C record,
Polytail, architecture/UI review, required golden and firmware-owner gates, and
canonical verification are complete.
