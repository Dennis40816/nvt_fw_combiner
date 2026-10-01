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
| R35-03 | shared execution layer (`scripts/predecessor_comparison.py`) | implemented and reviewed in the comparator batch |
| R35-04 | rolling mode, report assembly, gate, CLI (`scripts/predecessor_rolling.py`) | implemented and reviewed in the comparator batch |
| R35-05 | v0.9.16 1.x mode (`scripts/predecessor_v0916.py`) | implemented and reviewed in the comparator batch |
| R35-06 | v0.9.16 baseline executor contract and compiler-host pinning | not started; needs the release owner |
| R35-09 | diagnostic rehearsal against real builds | not started; needs R35-06's executor |
| R36-01 | coverage and difference disposition for `1.2.2` (decision 251) | not started; needs the rehearsal |
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
   `run_rolling(published=...)`. The CLI currently passes none, so `rolling --formal` ends
   `PREDECESSOR_BASELINE_INVALID`. The rehearsal and the v0.9.16 report (R35-09, R36-03) need the CLI to pass
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

Recorded as a bug: [`BUG-20261002-exclusive-atomic-writer-race`](../bugs/BUG-20261002-exclusive-atomic-writer-race.md).
