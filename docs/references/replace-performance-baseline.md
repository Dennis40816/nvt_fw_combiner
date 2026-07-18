# Replace Performance Baseline

Status: node A deterministic baseline plus a provisional node C process
prototype; neither is the reconciled B/C timing result or a firmware support
claim.

Captured: 2026-07-18 on
`feature/0.9.10/replace-performance-baseline`; node A is retained at
`74fb1028` and the provisional process prototype is `315272b3`.

## Purpose and boundary

This baseline records the current orchestration cost before automatic-Build
optimization. It exercises the existing Bootstrap Preview/Build gate through
the real Application service, profile compiler, shared Domain engine, typed
ports, immutable reports, and atomic output-writer contract. Test adapters
provide synthetic bytes and external-process audit records; no executable or
firmware fixture is used.

The test instrumentation calls the unchanged internal Bootstrap gate and
counts through deterministic clock, artifact-reader, external-processor, and
output-writer adapters. It does not add a production metrics surface, extend
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

## Current Infrastructure read model

The legacy postbuild adapter still performs two full staging-firmware reads per
command: one before the command and one inside shortened-output normalization.
It then performs one final read after all commands. This is a source-audited
model, `2C + 1` reads per processor session, not a runtime counter from the
Bootstrap baseline.

| Command count (`C`) | Reads per current session | Current automatic-Build sessions | Derived current reads |
| ---: | ---: | ---: | ---: |
| 2 | 5 | 2 | 10 (provisional C: 5) |
| 13 | 27 | 2 | 54 (provisional C: 27) |

Staged immutable-artifact verification reads are separate and must not be
hidden in future evidence. Any R3 adapter prototype remains limited to an
applicable full-byte CtrlRAM golden and firmware-owner review; broader routing
is selected only at the post-prototype decision gate.

## Current UI inspection fan-out

The pre-rebase UI asks independent helpers for firmware facts, verified
context suggestion, firmware metadata/version, CtrlRAM region/context,
memory-map rows, and coverage. Several paths reach the current static
`TryReadFirmwareImage` implementation independently. This is a source-audited
optimization candidate, not yet an exact read-count baseline.

Node B must instrument the final `0.9.9` file reader/inspection boundary and
record actual read calls and bytes for cold selection, repeated same-file
selection, IC/mode/IC-num changes, coverage projection, and report projection.
The C prototype target is at most one full-file read for one immutable display
snapshot identity/hash, with cancellation and invalidation when identity or
content changes. Build still performs its own authoritative read and hash.

## Follow-up gates

- The provisional `v0.9.10` process prototype reduces a valid automatic Build
  from two composition runs and two processor sessions to one while retaining
  an authoritative Build read, validation, the explicit Build
  preview-token/fingerprint path, and atomic promotion. It must be replayed
  against final `0.9.9` before acceptance.
- UI inspection begins only after the `0.9.9` typed workflow contracts freeze.
  One immutable asynchronous snapshot may serve display projections for one
  file identity/hash, but never Build authority.
- ADR 0024 step progress begins from the same reconciled UI boundary.
  Application publishes only bounded typed lifecycle transitions; Presentation
  localizes and animates the active step, honors reduced motion, and never
  fabricates byte percentage or infers phases from firmware-facing text.
- Infrastructure read-model changes remain separate R3 work and are limited to
  exact full-output golden scope plus firmware-owner review. Exact output
  bytes, hashes, command argv/order, mutations, differences, warnings, and
  failed-run atomicity remain mandatory parity.
- `v0.9.11` and later routing is deliberately unassigned until reviewed
  `v0.9.9` and `v0.9.10` merge and the owner accepts the B/C evidence.
