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

A formal run is refused with `PREDECESSOR_CONTRACT_PENDING` until R35-06 is in effect.

## Open items found in review

The first two items are carried to the contract change that goes with R35-06, because they need a change under
`docs/contracts/`:

1. **`deterministicSha256` does not reproduce between real runs.** Each process entry of a report carries the size
   and SHA-256 of the written CLI report (which holds a run id and two timestamps), stdout and stderr hashes that
   cover temporary paths, and the temporary root length. The builder fixtures reproduce only because the synthetic
   CLI is constant. Decide after the rehearsal shows which members vary: keep those capture identities in separate
   run evidence, or exclude a named set from the digest; then test repeated runs with different ids, timestamps and
   paths.
2. **No milestone value for the `1.2.2` report of the v0.9.16 1.x mode.** The report schema's milestone values are
   `1.1.13-final-candidate`, `1.2.0-release-approval` and `before-ro-1-decision`, and the contract still describes
   the mode as a milestone check. Decision 250 makes the deferred milestone comparison block `1.2.2`: either state
   that the `1.2.2` run is the deferred `1.2.0-release-approval` milestone (decisions 201 and 250), or add a value.
3. **The CLI still has no supplier for release inventory or the baseline builder.** The rolling report of record
   (R36-02) needs a release-host adapter that supplies the published stable release inventory to
   `run_rolling(published=...)`. The CLI passes none, so once R35-06 is in effect `rolling --formal` ends
   `PREDECESSOR_BASELINE_INVALID` (today it is refused earlier with `PREDECESSOR_CONTRACT_PENDING`). The rehearsal and the v0.9.16 report (R35-09, R36-03) need the CLI to pass
   the baseline executor builder of R35-06; until then every `v0916-1x` CLI run ends
   `PREDECESSOR_CONTRACT_PENDING`.

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
