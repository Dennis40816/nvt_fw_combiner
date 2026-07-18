# Replace Performance Baseline

Status: deterministic synthetic baseline; not a firmware support or timing
claim.

Captured: 2026-07-18 on
`feature/0.9.10/replace-performance-baseline` at `0dafe1ef`.

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

## Deterministic results

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

## Current Infrastructure read model

The legacy postbuild adapter still performs two full staging-firmware reads per
command: one before the command and one inside shortened-output normalization.
It then performs one final read after all commands. This is a source-audited
model, `2C + 1` reads per processor session, not a runtime counter from the
Bootstrap baseline.

| Command count (`C`) | Reads per current session | Current automatic-Build sessions | Derived current reads |
| ---: | ---: | ---: | ---: |
| 2 | 5 | 2 | 10 |
| 13 | 27 | 2 | 54 |

Staged immutable-artifact verification reads are separate and must not be
hidden in future evidence. The R3 adapter optimization remains deferred until
the applicable full-byte CtrlRAM goldens and firmware-owner review close.

## Follow-up gates

- `v0.9.11` may reduce a valid automatic Build from two composition runs and
  two processor sessions to one, while retaining an authoritative Build read,
  validation, preview-token/fingerprint policy, and atomic promotion.
- UI inspection and same-owner byte-copy baselines remain separate phases.
- `v0.9.15` may change the Infrastructure read model only after `v0.9.14`
  golden closure. Exact output bytes, hashes, command argv/order, mutations,
  differences, warnings, and failed-run atomicity remain mandatory parity.
