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
isolated serialization run measured about `58.0` MB for the first call and
`23,761,648` bytes for an exact repeated call. The repeated allocation is only
`320,608` bytes (`1.37%`) above the `23,441,040`-byte payload floor of the final
UTF-16 string. This supports treating recurring serialization as close to its
unavoidable representation cost and the roughly `34` MB gap as
first-call-specific overhead. It does not yet attribute that gap among serializer metadata,
JIT, or cold pooled-buffer effects. These allocation numbers are diagnostic
local evidence, not universal CI thresholds.

Run the focused evidence from the repository root:

```text
dotnet test tests/NvtFwCombiner.Application.Tests/NvtFwCombiner.Application.Tests.csproj --filter "FullyQualifiedName~CompositionReportPerformanceBaselineTests" --logger "console;verbosity=detailed"
```

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

Legacy Combiner readback and any physical tool/golden layout dependency wait
for the owner-coordinated `0.9.9.5` predecessor-layout convergence gate. This is
not a release tag or assumed branch: P2 remains blocked until the owner supplies
the exact reviewed commit SHA and its external-tool/golden-layout reference. No
final performance or release claim is made until the same-source B/C record,
Polytail, architecture/UI review, required golden and firmware-owner gates, and
canonical verification are complete.
