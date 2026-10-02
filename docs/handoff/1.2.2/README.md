# `1.2.2` comparator work

Status: in progress. `1.2.2` delivers the predecessor comparator of
[ADR 0078](../../adr/0078-predecessor-comparison-for-1x-releases.md) and the first formal comparisons. Scope and
order are in the [1.2.x allocation](../1.1.14/1.2.x-allocation.md) (items R35 and R36); the owner's opening
decisions are 249 to 251 on the [1.2.x board](../1.2.x.md). The contract is
[predecessor-comparison-v1](../../contracts/predecessor-comparison-v1.md); this file only tracks delivery.

## Delivery

| Item | Content | State |
| --- | --- | --- |
| R35-01, R35-02, R35-07 | report reader, contract follow-ups, start-failed classification, the pinned Git reader's opt-in for a submodule gitlink | pull request #516 |
| R35-03 | shared execution layer (`scripts/predecessor_comparison.py`) | pull request #517 |
| R35-04 | rolling mode, report assembly, gate, CLI (`scripts/predecessor_rolling.py`) | pull request #517 |
| R35-05 | v0.9.16 1.x mode (`scripts/predecessor_v0916.py`) | pull request #517 |
| R35-06 | v0.9.16 baseline executor contract and compiler-host pinning | implemented on `feature/1.2.2/executor-contract`, not yet submitted; the pull request is R3 and waits for the rehearsal |
| R35-09 | diagnostic rehearsal against real builds | in progress: both executors build and all 39 scenarios run; see "Rehearsal findings" |
| R36-01 | coverage and difference disposition for `1.2.2` (decision 251) | not started; needs the rehearsal |
| R58 | Support Matrix hover flicker (decision 253) | pull request #518 |
| R36-02, R36-03 | the two reports of record (decisions 249 and 250) | not started |

R35-06 activates both executor interfaces with complete compiler-host settings and the amendment's raw v2 binding. R35-09 remains the commander's actual-build rehearsal before reports of record.

## Open items found in review

1. **Resolved: reproducible `deterministicSha256`.** This executor-contract batch retains and validates run capture evidence while both builders and validators share the contract's exact digest projection, covered by repeated synthetic runs with different ids, timestamps, paths and inventory collection times.
2. **Resolved: the `1.2.2` milestone wording.** This batch states that candidate version `1.2.2` executes the deferred `1.2.0-release-approval` milestone under decisions 201 and 250, retaining the existing enum.
3. **Resolved: the CLI supplies the baseline builder.** Part 2 wires `V0916BaselineExecutorBuilder`, loads v2 through the candidate snapshot's amendment binding and verifies its recipe and compiler host.

The rolling inventory supplier is the commander's complete file passed through
`--published-release-inventory FILE`; this batch supplies its schema, validation
and report digests without adding a GitHub caller. `compilerHost` and
`baselineExecutor` are in effect in part 2.

To check at the rehearsal, because every test so far uses synthetic CLI reports:

- the shape of real 1.x and v0.9.16 reports, including a rejected Build (the reused compiled-authority check
  requires every operation to have succeeded, and the fingerprint rule requires a value, so a real Build rejection
  may be classified `invalid`; that fails closed);
- the report input binding ids: the product source writes the compiled address-space id as both `AddressSpaceId`
  and `ArtifactId`, in both versions, and the comparator compares those;
- checkout line endings against the formal source check, which compares working-tree bytes with Git blobs;
- the cost of a full run.

## Rehearsal findings

Found by running the comparator against real builds (baseline tag `v1.2.1`, candidate the executor-contract branch),
each fixed on that branch before the next run:

- a tag check that needs the candidate's `VERSION` to be the next patch version: the rehearsal uses a local-only
  commit that sets it;
- `git worktree add` fails on long paths: the comparator passes `core.longpaths` through the environment;
- the 1.x build's catalog generator requests runtime 10.0.0 and fails under `DOTNET_ROLL_FORWARD=Disable`: the
  build contracts add `-p:RuntimeFrameworkVersion=10.0.11`. This extends decision 79's pinning and is flagged for
  the owner in the R3 pull request;
- first complete run: all 39 scenarios `invalid`. Two causes, both under repair: routes that need the external
  combiner tool find no registered tool in the hermetic run (12 scenarios), and a provenance check written against
  synthetic fixtures refuses real 1.x reports (27 scenarios). Neither check is relaxed; the comparator is corrected.

Recorded as a bug: [`BUG-20261002-exclusive-atomic-writer-race`](../bugs/BUG-20261002-exclusive-atomic-writer-race.md).
