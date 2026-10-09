# `1.2.2` comparator work

Status: in progress. `1.2.2` delivers the predecessor comparator of
[ADR 0078](../../adr/0078-predecessor-comparison-for-1x-releases.md) and the first formal comparisons. Scope and
order are in the [1.2.x allocation](../1.1.14/1.2.x-allocation.md) (items R35 and R36); the owner's opening
decisions are 249 to 251 on the [1.2.x board](../1.2.x.md); executor follow-ups
are decisions 261 and 271 to 278. The contract is
[predecessor-comparison-v1](../../contracts/predecessor-comparison-v1.md); this file only tracks delivery.

## Delivery

| Item | Content | State |
| --- | --- | --- |
| R35-01, R35-02, R35-07 | report reader, contract follow-ups, start-failed classification, the pinned Git reader's opt-in for a submodule gitlink | pull request #516 |
| R35-03 | shared execution layer (`scripts/predecessor_comparison.py`) | pull request #517 |
| R35-04 | rolling mode, report assembly, gate, CLI (`scripts/predecessor_rolling.py`) | pull request #517 |
| R35-05 | v0.9.16 1.x mode (`scripts/predecessor_v0916.py`) | pull request #517 |
| R35-06 | v0.9.16 baseline executor contract and compiler-host pinning | implemented on `feature/1.3.0/comparator-executor`; pull request to `1.3.0` pending; the pull request is R3 |
| R35-09 | diagnostic rehearsal against real builds | saved rolling result `rolling-1003a`: 39 of 39 scenarios `equal`; saved v0.9.16 result `v0916-5`: 35 of 37 bound routes `consistent`, 2 `invalid`, 27 not covered; see "Rehearsal findings"; no fresh run claimed |
| R36-01 | coverage and difference disposition for `1.2.2` (decision 251) | owner-list command and two diagnostic drafts implemented locally; the owner has not approved the `1.2.2` list |
| R58 | Support Matrix hover flicker (decision 253) | pull request #518 |
| R36-02, R36-03 | the two reports of record (decisions 249 and 250) | still pending formal runs and owner disposition; diagnostic drafts are not reports of record |

R35-06 activates both executor interfaces with complete compiler-host settings and the amendment's raw v2 binding. R35-09 remains the commander's actual-build rehearsal before reports of record.

## Open items found in review

1. **Resolved: reproducible `deterministicSha256`.** This executor-contract batch retains and validates run capture evidence while both builders and validators share the contract's exact digest projection, covered by repeated synthetic runs with different ids, timestamps, paths and inventory collection times. Two real rolling runs of one candidate (`rolling-11`, `rolling-12`) give the same digest; only the members the contract excludes differ (each process's report size and hash and its stdout hash).
2. **Resolved: the `1.2.2` milestone wording.** This batch states that candidate version `1.2.2` executes the deferred `1.2.0-release-approval` milestone under decisions 201 and 250, retaining the existing enum.
3. **Resolved: the CLI supplies the baseline builder.** Part 2 wires `V0916BaselineExecutorBuilder`, loads v2 through the candidate snapshot's amendment binding and verifies its recipe and compiler host.
4. **Implemented locally: baseline-identity comparison.** A3 supplies rolling `--baseline-report FILE`, compares the rebuilt baseline with that release's own predecessor report `candidate.executor`, and binds the admitted report bytes. The opt-in has synthetic tests; actual baseline report availability and exact-source execution remain commander checks. A v0.9.16 CLI report has no shared program identity and is not substituted for this input.

## Owner coverage and difference lists (R36-01)

These are **diagnostic rehearsal, not a report of record**. The files were
generated from saved results, without another build or comparison:

- [Rolling draft: `rolling-1003a`](rolling-1003a-owner-list-draft.md): 74 routes
  in the recorded universe, 37 covered routes and 39 `equal` scenarios;
  26 debt-set routes and 11 pending gaps. Equal scenarios do not clear the
  `blocked` gate while gap approval is pending.
- [v0.9.16 draft: `v0916-5`](v0916-5-owner-list-draft.md): 64 historical plan
  routes, 35 `consistent`, 2 `invalid`, 27 `not-covered`; two approved
  corrections with exact observed and declared bounds. The bound
  plan/amendment rows and their SHA-256 are included, as are both invalid
  causes. The invalid canonical-binding exception stays invalid.

Both drafts identify candidate commit `1a5dd0d614033e49ac2e9659ff3d8df62ed94ce0`,
result file SHA-256 and deterministic digest. They are evidence of those saved
runs, not of this patch's execution. The optional candidate policy is a
supplemental catalogue: its published route IDs absent from the historical
result are listed as outside that mode, with no inferred route rename. It
does not replace the pinned historical policy or establish execution coverage.

The existing comparison command now provides a format-only projection:

```text
python scripts/predecessor_comparison.py owner-list --result RESULT.json --output LIST.md
```

Add `--candidate-policy POLICY.json` to list additional published routes;
without it the list explicitly covers only the result's mode universe.
Add `--declaration DECLARATION.json` for rolling full declared bounds, or
`--plan PLAN.json --amendment AMENDMENT.json` for the historical rows. These
disposition documents must match the result's raw SHA-256 bindings. The two
drafts use the candidate policy; the historical draft also uses the bound
plan and amendment. The command does not overwrite an existing list, rerun
firmware, change verdicts or approve differences. It sorts the list and omits
absolute local paths and payloads. A truncated observed range list is marked
explicitly; it cannot stand in for the complete bound declaration.

## Before reports of record (R36-02, R36-03)

1. Under decision 251, the firmware owner approves the actual coverage and
   difference list for **`1.2.2`**. Decision 96's approval covered `1.1.13`
   only. Recompute the list from the final route set and inputs, dispose of
   each gap/difference, and commit the release's ledger, declaration and
   CHANGELOG IDs before the formal rolling run. The drafts confer no approval.
2. Rerun the two historical invalid routes against the corrected source.
   Decision 278 admits only the exact `ab-combiner-work` B bank read
   `[262144, 524288)` for the declared v0.9.16 baseline executor; the candidate
   keeps decision 271's audit. The saved CtrlRAM cascade invalid has a local
   decision-272 correction. Both rules are implemented locally, but a fresh
   run still must demonstrate them. The saved invalid results remain history;
   neither is waived or reclassified here.
3. In **CI or release context**, run the comparator formally against the exact
   clean candidate source and clean-settings policy. Rolling uses the complete
   published inventory selecting `v1.2.1` under decision 249, the committed
   `1.2.2` dispositions, and must produce `formal: true`, gate `clear`, no
   invalid scenario, undeclared difference or pending gap. The historical run
   uses executor v2 and the pinned plan/amendment, milestone
   `1.2.0-release-approval` under decision 250, and must be `formal: true` and
   `consistent`, with the 37 bound and 27 unbound routes separately reported.
4. Retain the two payload-free JSON reports and their exact-source/input,
   inventory, executor, environment and digest evidence as the reports of
   record. Generate owner Markdown lists from those new results, bind the
   final owner approval to the release candidate, and have the commander
   verify independent exact-head review, CI and applicable firmware/release
   evidence outside this sandbox. No report of record is produced by this
   local formatting change; missing or failing required evidence blocks release.

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

Recorded rehearsal: rolling mode `rolling-1003a`, candidate `9f806f182` plus the local version commit: 39 of 39 scenarios `equal`.
v0.9.16 mode `v0916-5`: 35 routes `consistent`, 2 `invalid`, 27 not covered; these are recorded run results, not a rerun of this patch.
The recorded invalid routes are NT51950 CtrlRAM cascade full flash (all-`Skipped` baseline refused for unknown range spaces, corrected locally under decision 272) and NT51950 AB Merge 512k (baseline copies a whole bank `[262144,524288)` out of `ab-combiner-work` after the processor; decision 278 subsequently admitted that exact v0.9.16 read). These local corrections do not change the saved invalid results; a fresh run is pending.

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
