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
| R35-06 | v0.9.16 baseline executor contract and compiler-host pinning | implemented on `feature/1.2.2/executor-contract`, rebased on the trunk, not yet submitted; the pull request is R3 |
| R35-09 | diagnostic rehearsal against real builds | rolling mode against `v1.2.1`: 36 of 39 scenarios `equal`, 3 `invalid` and waiting for an owner decision; v0.9.16 mode at milestone `1.2.0-release-approval`: 35 of the 37 routes with canonical input `consistent`, 2 `invalid` and waiting for owner decisions; see "Rehearsal findings" |
| R36-01 | coverage and difference disposition for `1.2.2` (decision 251) | not started; needs the rehearsal |
| R58 | Support Matrix hover flicker (decision 253) | pull request #518 |
| R36-02, R36-03 | the two reports of record (decisions 249 and 250) | not started |

R35-06 activates both executor interfaces with complete compiler-host settings and the amendment's raw v2 binding. R35-09 remains the commander's actual-build rehearsal before reports of record.

## Open items found in review

1. **Resolved: reproducible `deterministicSha256`.** This executor-contract batch retains and validates run capture evidence while both builders and validators share the contract's exact digest projection, covered by repeated synthetic runs with different ids, timestamps, paths and inventory collection times. Two real rolling runs of one candidate (`rolling-11`, `rolling-12`) give the same digest; only the members the contract excludes differ (each process's report size and hash and its stdout hash).
2. **Resolved: the `1.2.2` milestone wording.** This batch states that candidate version `1.2.2` executes the deferred `1.2.0-release-approval` milestone under decisions 201 and 250, retaining the existing enum.
3. **Resolved: the CLI supplies the baseline builder.** Part 2 wires `V0916BaselineExecutorBuilder`, loads v2 through the candidate snapshot's amendment binding and verifies its recipe and compiler host.
4. **Open: baseline-identity comparison.** The comparison promised by the contract is not implemented yet; it is item A3 of the commander's work list, with no board item yet.

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

Current state: rolling mode `rolling-1003a`, candidate `9f806f182` plus the local version commit: 39 of 39 scenarios `equal`.
v0.9.16 mode `v0916-5`: 35 routes `consistent`, 2 `invalid`, 27 not covered; these are recorded run results, not a rerun of this patch.
The invalid routes are NT51950 CtrlRAM cascade full flash (all-`Skipped` baseline refused for unknown range spaces, fixed locally here under decision 272) and NT51950 AB Merge 512k (baseline copies a whole bank `[262144,524288)` out of `ab-combiner-work` after the processor; decision 271 allows only three write ranges of 4 bytes each; an owner decision is pending, and nothing is relaxed).

Found by running the comparator against real builds (baseline tag `v1.2.1`, candidate the executor-contract branch),
each fixed on that branch before the next run:

- a tag check that needs the candidate's `VERSION` to be the next patch version: the rehearsal uses a local-only
  commit that sets it;
- `git worktree add` fails on long paths: the comparator passes `core.longpaths` through the environment;
- the 1.x build's catalog generator requests runtime 10.0.0 and fails under `DOTNET_ROLL_FORWARD=Disable`: the
  build contracts add `-p:RuntimeFrameworkVersion=10.0.11`. The release owner approved this extension of
  decision 79 on 2026-10-02 (decisions 275 and 277) for every comparator-built program: the v0.9.16 baseline,
  the 1.x baseline and the candidate, and only for those builds;
- first complete run: all 39 scenarios `invalid`, because checks written against synthetic reports met the reports
  a CLI really writes. No check was relaxed without a decision; each cause has a bug record:
  - the order and capture checks misread a written report
    ([`BUG-20261002-predecessor-checks-misread-written-report`](../bugs/BUG-20261002-predecessor-checks-misread-written-report.md), fixed);
  - the staged CLI found no external tools
    ([`BUG-20261002-predecessor-cli-finds-no-external-tools`](../bugs/BUG-20261002-predecessor-cli-finds-no-external-tools.md), fixed);
  - the range check refused a declared `ReplaceExisting` overlay
    ([`BUG-20261002-predecessor-range-check-refuses-declared-overlay`](../bugs/BUG-20261002-predecessor-range-check-refuses-declared-overlay.md), fixed);
  - the CtrlRAM Replace CLI sorts its inputs, the comparator expected the binding order
    ([`BUG-20261002-predecessor-ctrlram-input-order`](../bugs/BUG-20261002-predecessor-ctrlram-input-order.md), fixed);
  - executed commands, work address spaces and processor mutation rows
    ([`BUG-20261002-adr0057-checks-refuse-written-processor-reports`](../bugs/BUG-20261002-adr0057-checks-refuse-written-processor-reports.md)):
    the owner decided the three rules (decision 261) and they are implemented for the comparator; the ADR 0057
    terminal path is unchanged;
- rolling mode after decision 261 (`rolling-11`): 36 scenarios `equal` (9 Standard Merge, 24 CtrlRAM Replace, 3 AB
  Merge), with no informational difference between `v1.2.1` and the candidate. The three NT51950 AB Merge scenarios
  are `invalid`: their external combiner writes a work address space and the report lists no output difference, so
  the write-range audit of decision 261 has nothing to read. This needs an owner decision
  ([`BUG-20261002-predecessor-work-space-processor-lists-no-output-difference`](../bugs/BUG-20261002-predecessor-work-space-processor-lists-no-output-difference.md));
  the gate is blocked by it and by the 11 pending gap approvals;
- reproducibility: a second run of the same candidate (`rolling-12`) gives the same `deterministicSha256`;
- v0.9.16 mode (`v0916-4`, diagnostic, milestone `1.2.0-release-approval`): the baseline executor v2 builds from the
  tag and reproduces every pin after one fix
  ([`BUG-20261002-predecessor-v0916-builder-counts-restored-packages`](../bugs/BUG-20261002-predecessor-v0916-builder-counts-restored-packages.md)).
  Of 64 plan routes, 35 are `consistent` (29 exact output, 2 with the approved semantic correction, 4 TP prefix
  transitive), 27 are not covered and 2 are `invalid`, so the result is `invalid`:
  - NT51950 AB Merge 512k: the `v0.9.16` report also works in `a-bank-work` and `b-bank-work`, which decision 261
    does not name, and on both sides the combiner writes a work address space (the open question above);
  - NT51950 CtrlRAM cascade full flash, where the plan expects the baseline to reject: the `v0.9.16` Preview exits 1
    with `profile.v2.compile.map-selection-invalid` and a report whose nine operations are `Skipped`, two of them
    processors without an executed command. The per-side checks refuse that report before the side can count as a
    typed rejection. Both are items of
    [`BUG-20261002-adr0057-checks-refuse-written-processor-reports`](../bugs/BUG-20261002-adr0057-checks-refuse-written-processor-reports.md)
    and need an owner decision.

The rehearsal evidence (reports, process logs, saved CLI reports) is in the test area under
`evidence/1.2.2/p2c/rehearsal`; a run named `shadow-*` there is a diagnostic with prototype rules and is never
evidence.

Also recorded as a bug: [`BUG-20261002-exclusive-atomic-writer-race`](../bugs/BUG-20261002-exclusive-atomic-writer-race.md).
