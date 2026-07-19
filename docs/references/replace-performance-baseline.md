# v0.9.10 Replace Performance Baseline

Status: deterministic orchestration/count baseline on the reviewed `v0.9.9`
source line plus local fragmented-report, Hex Diff, allocation, and
layout-neutral staging evidence. Path-independent component Node B/C replay and
the final annotated-`v0.9.9` upstream report replay are recorded below; final
physical-tool/golden-layout and packaged Windows evidence remain open.

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

Commit `9d65e219` applies the same one-run snapshot rule across the Bootstrap
runtime-reference boundary for the routed NT51926 Common FW 1.4.1 V2 CtrlRAM
case. Its V2 compiler must inspect the base bytes to resolve the map; those
exact host-read bytes now overlay the same physical artifact id for the
immediately following Application run. Physical base reads therefore change
from two to one, while Application still performs its normal binding read,
trust-boundary copy, SHA, normalization, validation, report, and output commit
logic. Replacement artifacts remain physical reads. This is not a UI or
cross-run cache and does not change B0's Application-level two-input counter.

## Raw Hex Editor memory and change-range baseline

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

Before commit `d056c829`, every successful value-only edit was followed by a
change-range query that separately derived value changes, structural changes,
and structural boundaries, amounting to approximately three complete-document
scans on the common equal-length path. The session now updates only the edited
or touching value span and rebuilds immutable descriptors in proportion to the
current changed-run count. Insert/delete retains the complete structural
derivation and caches that result once for the document revision; exact undo
can return to incremental value tracking without changing range semantics.

The maximum-length regression loads an 8 MiB document, applies one one-byte
edit, and performs 101 change-range lookups. The latest local single
observation was `6.681` ms and `976` current-thread allocated bytes, with every
lookup reusing the same immutable snapshot. Split, merge, overlapping edit,
undo/redo, caller-mutation rejection, and structural fallback are executable
tests rather than timing assumptions. This observation is not p50/p95 and does
not close the final packaged A8 interaction or working-set gate.

Before commit `300dd235`, Presentation immediately converted every changed
range into a row ViewModel and bound the complete collection to a
non-virtualized inspector. Its viewport also searched the complete changed
range set for every visible cell and original-row decision. A deterministic
20,000-byte alternating overwrite therefore exposed 10,000 complete row
ViewModels after one edit even though only a small inspector window was
visible.

The inspector now projects one replace-on-page 64-row window from the complete
typed range snapshot. A factory counter proves that the first page creates 64
rows, a direct jump to the 16-row tail creates only those 16 additional rows,
and a same-page jump creates none. Total count, reason, address, global index,
page navigation, `63 -> 64`, and `9999 -> 0` Next traversal remain exact. The
viewport uses a lower-bound value-range lookup and separately retained
structural indices, so data-only fragmentation is no longer multiplied by each
visible cell. A compact two-row pager, auto-width index, EN/ZH action-plus-total
automation name, keyboard buttons, and polite page status preserve the review
surface in its 336-DIP inspector.

The latest local single command observation for that 10,000-range fixture was
`10.600` ms and `3,015,056` current-thread allocated bytes. Focused UI passes
`34/34`, full UI passes `231/231`, and Architecture passes `94/94`. The
materialization count is the deterministic performance gate; the timing and
allocation are observations rather than p50/p95 or release thresholds. Final
packaged 8 MiB responsiveness, render layout, accessibility, and process
working set remain in A8.

Commit `5e30f1e1` removes the remaining per-record rebuild on a later small
value edit. Each identity-only revision now constructs a new ordered backing
list, reuses every immutable range record outside the affected or touching
span, and publishes that list through a read-only wrapper. No published list is
ever mutated in place, so an earlier snapshot and all nested cause collections
remain stable. Structural insert/delete still invalidates identity tracking and
uses the complete structural fallback; exact undo may restore the identity
path through the existing full comparison.

In the 10,000-range fixture, changing the first already-different byte from
`FF` to `FE` rebuilds the first range while the second and final range records
remain reference-identical across revisions. The latest local single
observation was `0.477` ms and `80,816` current-thread allocated bytes, with a
`128 KiB` executable ceiling. Raw editor tests pass `23/23`, full Application
passes `181/181`, Bootstrap integration passes `6/6`, Hex Editor UI passes
`25/25`, and Architecture passes `94/94`. Record reuse, snapshot identity, old
and new values, read-only collections, and the allocation ceiling are the
deterministic gates; the elapsed observation is not p50/p95. A8 remains open.

The dev.3 packaged 8 MiB exercise then exposed a separate Search/Next cost:
the document snapshot was shared, but every same-query Next action still ran
the complete ASCII scan and recreated its result. Commit `509dedd7` keys one
completed result by document revision and ordinal query, copies its bounded
retained index into an immutable collection once, and delegates all next,
wrap, and truncated-index fallback policy to Application. A changed query or
document revision runs a new authoritative scan. Selection through retained
index 4,095 reuses the result; a request at index 4,096 of a truncated dense
result fails the cache lookup and rescans, preserving the complete global
match index rather than guessing from partial data.

The deterministic regression proves that start, Next, and end-of-document
wrap call the authoritative search delegate once; different queries call it
twice; sync and async entry points agree; mutation invalidates both caches; and
callers cannot mutate the shared match index. Application raw-search tests pass
`26/26`, Bootstrap search-performance tests pass `11/11`, Hex Editor UI passes
`25/25`, Architecture passes `94/94`, Polytail passes, and independent R2
architecture review reports no P0-P2 findings.

For development-only packaged evidence, the same deterministic
8,388,608-byte input (SHA-256
`0424143612b91825a9f886af14ffeda5a57d2bf560a23d47dd8bfa81015a3c38`)
contained 254,208 `NVT-PERF` matches. Dev.4 advanced
`0x0 -> 0x21 -> 0x42 -> 0x63`; working set read 466,010,112 bytes after the
first search and 460,611,584 bytes after the fourth, with every observation
Responding. Dev.3 had instead grown from 507,944,960 bytes after its edit
exercise to 552,583,168 bytes after four searches. These are uncontrolled
separate sessions, not a paired benchmark or percentile claim. The exact final
candidate must still repeat A8 on the reviewed clean machine.

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
The path-independent fresh-process replay below records component latency; the
packaged-process working set remains a separate final gate. These numbers are
diagnostic local evidence, not universal CI thresholds.

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

The `HEX_DIFF_BASELINE` line is non-gating local evidence. Node B/C p50 and p95
values require the same isolated-process run count, machine/runtime, input
hashes, power mode, and monitoring setup at both nodes; the replay below uses
that protocol. A single warm repeat is not a percentile claim, and testhost
working set does not replace the packaged Windows first-frame/manual acceptance
record.

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
slice alone makes no latency-improvement claim. The ten-sample component replay
below supplies p50/p95; packaged first-page evidence remains open.

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
materialization, and jump behavior remain locked. The component replay below
supplies p50/p95 and testhost handoff working-set observations; packaged
first-page and Windows process working-set evidence remain open.

## Path-independent component Node B/C replay

On 2026-07-19, the report and Hex Diff microbaselines were replayed on Windows
`10.0.26200`, .NET SDK `10.0.302`, `win-x64`, and an Intel Core i9-10900F
(10 cores/20 logical processors). The active Windows plan was Ultimate
Performance (`e9a42b02-d5df-448d-aa00-03f14749eb61`) on a desktop with no
`Win32_Battery` device. No profiler, ETW capture, external process sampler, or
performance feature flag was enabled; tests used their default configuration
and only the test-owned timers, allocation counters, GC counters, and process
working-set reads shown by the harness.

Node B used a detached, tracked-clean worktree and Node C used the separate,
tracked-clean main worktree synchronized to its origin. Their project-local
`bin`/`obj` output trees were therefore distinct. Before each component pair,
the B worktree was checked out at the full SHA named below, both projects were
built once in Debug for `net10.0`, and their exact-filter invocations used
`--no-build --no-restore`. No testhost warm-up sample was run or discarded.
Ten B/C samples then alternated without concurrency, and every invocation
created a fresh testhost. The p50 is the median. The p95 is the nearest-rank
value; with ten samples it is the observed maximum. These local values are
evidence, not portable CI thresholds.

The Hex Diff comparison uses the unchanged
`ReportHexDiffEmitsColdWarmProjectionAndJumpObservations` test at component
Node B `aaa9edca60265aa5f0e1585495b949ac3ea010a3` and Node C
`3d427b0bd3afca60cb8aa1c1d6967cc6ce0202e8`. Every sample preserved the same output
SHA-256 `5e53ca2936f1d2345bc1299ead189d94454fd14e5d9d585c7a68646bc25df996`,
`6,163,125` JSON characters, the unchanged synthetic 10,000-range input
generator, asserted total count, bounded 64/65 navigator materialization, and
the selected address-jump result. The observation does not claim a digest of
all projected range identities. The deterministic 256 KiB reference input
SHA-256 was
`206ddc54b8a62b0dc62ba79249d5a04d951aca42470028704b604b3747ee269d`;
the two-byte replacement input SHA-256 was
`857b915078ad488cff951bded73cfb4021129efb943c37604781c79f0726bb41`.

| Hex Diff observation | Node B value | Node C value | Observation |
| --- | ---: | ---: | --- |
| Cold projection to first page, p50 / p95 | `150.592 / 200.696 ms` | `145.827 / 170.064 ms` | p50 `3.16%` lower; p95 `15.26%` lower |
| Repeated projection to first page, p50 / p95 | `110.849 / 155.150 ms` | `109.402 / 142.773 ms` | p50 `1.31%` lower; p95 `7.98%` lower |
| Off-page address jump, p50 / p95 | `1.079 / 2.020 ms` | `1.058 / 1.672 ms` | Same selected range and `51,952` allocated bytes |
| Cold projection allocation, identical in all 10 samples | `10,883,824` bytes | `9,561,312` bytes | `12.15%` lower |
| Repeated projection allocation, identical in all 10 samples | `10,869,728` bytes | `9,531,832` bytes | `12.31%` lower |
| Handoff working set, p50 / p95 | `107,188,224 / 108,027,904` bytes | `105,037,824 / 105,385,984` bytes | p50 `2.01%` lower; includes both live models |

The ten cold elapsed samples were B
`154.828, 135.757, 153.421, 156.833, 147.764, 133.392, 200.696, 137.526,
144.975, 165.451` ms and C
`145.285, 154.222, 146.369, 136.779, 170.064, 136.848, 137.304, 149.567,
162.221, 141.396` ms. The ten repeated samples were B
`118.082, 102.689, 108.302, 107.669, 118.097, 102.106, 155.150, 113.396,
101.397, 144.501` ms and C
`114.644, 110.494, 124.326, 104.145, 109.722, 106.641, 100.658, 109.083,
142.773, 103.663` ms.

The ten off-page jump samples were B
`1.032, 1.005, 2.020, 1.086, 1.093, 1.072, 1.285, 1.029, 1.034, 1.656` ms
and C
`1.071, 1.060, 1.672, 0.987, 1.057, 0.971, 0.978, 1.037, 1.353, 1.062`
ms. Every cold projection reported Gen0/Gen1/Gen2 deltas `0/0/0`; every
successor projection reported `1/1/1` at both nodes. In all 20 samples,
`testhostLifetimePeakWorkingSet` equalled the handoff working-set observation
listed below. It remains a testhost-lifetime peak that includes fixture
composition and JSON setup, not an isolated projection-interval or packaged
process peak.

The ten handoff working-set samples were B
`107663360, 106983424, 107028480, 106946560, 107560960, 107290624,
108027904, 107175936, 107200512, 107122688` bytes and C
`105385984, 105029632, 105328640, 105009152, 105046016, 104890368,
104976384, 105193472, 104992768, 105373696` bytes. Each snapshot includes the
cold and successor models alive together and is not a steady-state or packaged
process measurement.

The upstream fragmented-report comparison uses component Node B
`fbd7ee7c374458ecba84dd9b34493f28ee1adb5a` and Node C
`3d427b0bd3afca60cb8aa1c1d6967cc6ce0202e8`. The measured run-through and first-serialization regions
are the same; later Node C assertions and the repeated-serialization observation
occur after both measured regions. Every sample preserved 10,000 exact
one-byte differences, output SHA-256
`e7b39a736b02c1793f1c22ab4c21e29bc478bd94465614c27bd70c4ac42c25b4`,
`11,720,520` JSON characters, and JSON SHA-256
`16d46159b46bcb3acdd27783321b21504a721f01e4dddef43f10fc336a49c937`.
The 20,000-byte reference SHA-256 was
`28b4f41a7f3ee6d8cc87272db6e09c6d3566551fd4d18702b041a21658272a85`;
the deterministic replacement SHA-256 was the output SHA-256 above.

| Fragmented report observation | Node B value | Node C value | Observation |
| --- | ---: | ---: | --- |
| Application run through complete report, p50 / p95 | `114.900 / 127.140 ms` | `81.325 / 103.226 ms` | p50 `29.22%` lower; p95 `18.81%` lower |
| Run-through allocation, identical in all 10 samples | `20,162,040` bytes | `7,541,912` bytes | `62.59%` lower |
| First indented serialization, p50 / p95 | `160.942 / 172.545 ms` | `150.742 / 171.121 ms` | p50 `6.34%` lower; p95 `0.83%` lower |

The ten run-through elapsed samples were B
`106.839, 111.179, 113.519, 112.788, 116.281, 107.790, 121.013, 127.140,
126.965, 126.838` ms and C
`79.465, 82.289, 103.226, 78.794, 80.361, 82.639, 78.741, 78.227, 92.674,
95.508` ms. The first-serialization samples were B
`145.881, 138.059, 168.960, 172.351, 145.547, 148.607, 157.268, 167.677,
172.545, 164.617` ms and C
`153.058, 144.498, 157.005, 151.554, 143.798, 151.057, 142.327, 146.250,
150.428, 171.121` ms.

The replay therefore supports a material upstream report-generation and
allocation improvement. Hex Diff first-page latency is improved but remains
noisy and modest at p50; its repeatable gains are lower projection allocation
and handoff working set. Cold serialization p95 is effectively unchanged,
which agrees with the prior shared-pool attribution and does not justify a new
serializer or schema. Packaged first-page, dispatcher heartbeat,
click-to-first-step, accessibility, physical Legacy Combiner, and clean-machine
working-set evidence remain separate gates.

## Final annotated-v0.9.9 upstream report replay

On 2026-07-19, the unchanged initial
`FragmentedReplaceReportPreservesTenThousandDifferenceFacts` harness from
`fbd7ee7c374458ecba84dd9b34493f28ee1adb5a` was applied as a test-only overlay
to two detached worktrees. Node B's production source was the commit selected
by the annotated `v0.9.9` tag,
`270e803e1f043ffd56d8568c7e80c7f771a35d7e`; Node C's production source was
`4a2b6c0a04a8aac302830cabf60ba8080a39a8d8`. Both nodes compiled the same test
without a production adapter or physical fixture/tool path, and used separate
project-local `bin`/`obj` trees.

The machine/runtime/power settings match the component replay above. Each node
ran one exact-filter validation/warm-up sample that was discarded. Ten recorded
B/C pairs then alternated without concurrency; every `--no-build --no-restore`
invocation created a fresh testhost. The p50 is the median and p95 is the
nearest-rank value, which is the observed maximum for ten samples. These are
local comparison values, not portable CI thresholds.

Every recorded sample preserved 10,000 exact one-byte differences, output
SHA-256
`e7b39a736b02c1793f1c22ab4c21e29bc478bd94465614c27bd70c4ac42c25b4`,
`11,720,520` JSON characters, and JSON SHA-256
`16d46159b46bcb3acdd27783321b21504a721f01e4dddef43f10fc336a49c937`.
The 20,000-byte reference and replacement inputs therefore remain the same
deterministic values recorded for the component replay; no expected output was
regenerated.

| Final predecessor report observation | Annotated `v0.9.9` | `v0.9.10` Node C | Observation |
| --- | ---: | ---: | --- |
| Application run through complete report, p50 / p95 | `118.581 / 145.196 ms` | `88.037 / 96.553 ms` | p50 `25.76%` lower; p95 `33.50%` lower |
| Run-through current-thread allocation, p50 / p95 | `20,181,656 / 20,181,656` bytes | `7,541,912 / 7,591,112` bytes | p50 `62.63%` lower |
| First indented serialization, p50 / p95 | `160.803 / 200.909 ms` | `166.777 / 190.699 ms` | p50 `3.72%` higher; p95 `5.08%` lower; no serialization improvement claim |

The ten run-through samples were B
`127.298, 113.255, 121.888, 145.196, 132.099, 115.889, 109.565, 108.087,
121.273, 108.250` ms and C
`77.594, 88.305, 92.144, 96.553, 87.769, 95.060, 91.600, 80.551, 87.006,
86.659` ms. The ten first-serialization samples were B
`200.909, 147.008, 164.270, 192.806, 186.073, 157.336, 155.165, 154.841,
165.134, 154.059` ms and C
`159.708, 165.109, 176.550, 190.699, 164.368, 168.445, 173.832, 154.325,
156.183, 177.772` ms.

Node B reported `20,181,656` run-through allocated bytes in every recorded
sample. Node C reported `7,541,912` bytes in nine samples and `7,591,112` bytes
in one sample. The exact output/report digests make this a valid final
predecessor comparison for upstream report creation. It does not create a
`v0.9.9` Hex Diff baseline, because that UI did not exist at the predecessor;
Hex Diff before/after remains the same-source component comparison above.
Physical Legacy Combiner parity and packaged UI timings remain separate gates.

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
history files above 64 MiB are rejected from the opened file before BOM
inspection or JSON deserialization, bounding legacy/corrupt startup input. The
machine-readable run report and local history schema are unchanged. This is a deterministic
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

Commit `69a59441` bounds the remaining intentional synchronous preference read
before ViewModel construction. `ShellPreferenceFileStore` rejects a local
preference file above 64 KiB from the opened file before payload
deserialization; the same valid UTF-8 JSON loads at exactly 65,536 bytes and falls back to
System/English/reduced-motion off at 65,537 bytes. Normal version-1 preference
files, async atomic save, cancellation, and post-construction switching are
unchanged. Focused preferences pass `4/4`, full UI `229/229`, Architecture
`94/94`, and independent R1 UI/architecture review reports no P0/P1. This is a
corrupt or abnormal local-state bound, not a packaged startup-latency claim;
final preference interaction/restoration remains in the Windows manual gate.

Commit `c47b28c9` removes the whole-file text intermediate from local-state
restore. Current UTF-8 history/preferences deserialize directly from the
bounded opened stream. Legacy UTF-16 LE/BE and UTF-32 LE/BE BOM documents are
identified before the overlapping UTF-16 prefix, positioned after the exact
BOM, and streamed through a UTF-8 transcoder; compatibility no longer requires
`ReadToEnd`. UTF-8 BOM remains on the direct serializer path.

The warmed 4 MiB current-format history observation allocated `25,227,920`
bytes before this slice. The committed regression preserves the complete
report string and requires both current UTF-8 and legacy UTF-16 restore to
allocate no more than `12,582,912` current-thread bytes. This is a steady-state
allocation ceiling, not a retained-memory or startup-latency measurement.

The same opened snapshot shares delete but never write. A deterministic
Windows-target regression keeps the old reader open while async atomic save
publishes the latest path, then proves that the reader contains only the old
complete snapshot, a subsequent load contains only the latest snapshot, and
no temporary file remains. Deserialization completes before the handle closes;
schema projection begins afterward. Missing, malformed, oversized and valid
unsupported-schema inputs still use the best-effort fallback. Five BOM
encodings, the held-reader replacement, focused local persistence `14/14`,
full UI `237/237`, Architecture `94/94`, Polytail, and independent R2 UI and
architecture review pass with no P0-P3 finding. Local schema, latest-wins
persistence, dispatcher ownership, report content, and firmware behavior are
unchanged. The exact packaged 12-entry cold-start and working-set rows remain
open in the Windows manual gate.

## Pinned catalog discovery baseline

Commit `d7bd4ddc` removes document-sized canonicalization allocations from the
normal repository path used to hash-pin the built-in CtrlRAM Postbuild and TP
flash-map catalogs. Those tracked files are respectively 192,703 and 46,947
bytes, contain only ASCII bytes, and already use LF line endings. The loader now
hashes that exact immutable span and writes the SHA-256 result into stack
storage instead of decoding a complete UTF-8 string, normalizing an unchanged
string, and encoding a second complete byte array.

The warmed deterministic 262,144-byte regression changes from 786,688 to 152
current-thread allocated bytes, removing 786,536 bytes (`99.98%`). The output
hash is exact. This is an allocation bound, not a startup-latency percentile.
CR/CRLF, form feed, every non-ASCII sequence including NEL/LS/PS and invalid
UTF-8, hash mismatch, and strict JSON remain on the original normalization or
fail-closed path. Focused catalog tests pass `26/26`, full Infrastructure passes
`243/243` with two platform-specific skips, Bootstrap passes `465/465`, and
Architecture passes `94/94`. Polytail and independent R2 review report no
P0-P3 finding. Final packaged first-frame and process working-set evidence
remain in the Windows manual gate.

## Immutable staged-artifact verification

Commit `151310d8` removes the remaining full-size verification allocation for
each immutable named artifact after a generic or Legacy external-processor run.
The host still verifies the exact length and complete content after the process
exits; it now compares sequential windows using a pooled buffer requested at no
more than 128 KiB instead of calling `ReadAllBytesAsync` and materializing
another artifact-sized `byte[]`. Exact matches still read every byte, while a
length or content mismatch fails closed and may stop earlier. The buffer is
cleared on return after success, mismatch, cancellation, or exception.

This does not remove the independent staged-source gate, reuse a result across
runs, cache executable trust, or weaken final firmware diff verification. The
Legacy exact-file projection remains because it is the private mutable file the
tool consumes; only its redundant post-process full-file allocation is removed.
Seven direct tests cover a multi-window exact file, first/window-boundary/final
byte changes, truncation, extension, and pre-cancellation. Generic and Legacy
adapter integration tests retain `external-tool.staged-artifact.modified`.
Focused coverage passes `11/11`, full Infrastructure passes `241/241` with two
platform-specific skips, Architecture passes `94/94`, Polytail passes, and
independent R3 review reports no P0-P3 finding. Physical-tool/golden parity,
firmware/security owner review, and canonical verification remain mandatory.

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

The layout-neutral Legacy Combiner runner now executes one private sequential
pipeline with zero intermediate complete-file reads and one complete read after
the final successful command. It retains process, artifact, staging-tree, and
length gates after each launch. Only `MERGE_MODE` shortened-output handling
captures a selective tail after the command's minimum declared coverage, then
appends the exact predecessor tail if the tool actually shortens the file;
other shortened command families fail closed. The final host diff still uses
the pre-pipeline bytes and union of declared write ranges. Intermediate
full-image values are not parity evidence.

The later predecessor reconciliation supplies the reviewed physical
external-tool/golden layout and repeats the exact two-command golden plus
13-command count/timing evidence. No final performance or release claim is made
until that same-source B/C record, Polytail, architecture review, required
golden and firmware-owner gates, and canonical verification are complete.

The layout-neutral counter surface executes two- and thirteen-command plans
through the real Infrastructure processor with one sequential session and the
exact launch count. A source audit locks one complete staging-file read in the
pipeline, located at final import, and rejects a synchronous full-read
substitute. Focused processor tests pass `26/26`; full Infrastructure passes
`234/234` with two platform-specific skips; Architecture passes `93/93`; and
the B0 harness plus both tracked NT51926 owner-golden-backed Legacy Combiner
cases pass `9/9` on the current development layout. The golden run is
development evidence only and must be repeated after the physical layout is
reconciled. Independent R3 architecture review reports no P0/P1 finding and
passes with the final firmware-owner, layout-reconciliation, Node B/C, and
canonical-verification gates still mandatory.

External-processor discovery now has one explicit OS-process lifetime. The
first applicable call performs the parent-root search, recursive manifest
enumeration/parsing, registry construction, and router construction; concurrent
and later calls receive the same immutable environment. The deterministic
lifetime tests prove successful, unavailable, and invalid initialization each
invoke their factory once. A missing root remains unavailable and an invalid
manifest remains fail closed until process restart. A restart is the explicit
refresh boundary for any tool/manifest layout change.

This does not cache process results, staging state, or executable trust. Each
transform still creates a private run directory and the resolver rechecks the
selected executable's existence and SHA-256 against the retained manifest. No
physical tool path is embedded. Focused lifetime plus current-layout NT51926
owner-golden-backed tests pass `5/5`, and Architecture passes `94/94`. The
unrelated profile-list regression expectation is reconciled at `2d227cb2` with
the already registered NT51930 DP Replace row from annotated `v0.9.9`; the
focused test passes `1/1`, full Bootstrap passes `465/465`, and Architecture
passes `94/94`. The test blob exactly matches annotated `v0.9.9`, and
independent review reports no P0-P3 finding. Final Node B/C records one
discovery/manifest/registry build per process and repeats physical-layout parity
after predecessor reconciliation. Independent R2 architecture review reports
no P0/P1 finding and passes with the final Node B/C, reconciled-layout,
domain-owner, and canonical-verification gates retained.
