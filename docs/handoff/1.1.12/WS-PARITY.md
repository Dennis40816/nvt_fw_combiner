# WS-PARITY: one-time alignment with v0.9.16

Owner: Codex runs; Claude Code reviews; the owner disposes. Board:
[1.1.12 board](../1.1.12.md), checklist C-3, WS-GOV decision 7b.
Protocol: [handoff README](../README.md).

## Dispatch envelope 2026-09-25

**Outcome.** A complete difference table between the 1.1.x candidate and the
v0.9.16 predecessor, produced with the existing ADR 0057 mechanism
(`scripts/v0916_parity_certification.py`, plan
`docs/contracts/v0916-parity-certification-v1.json`). One row per published
route and case: route, IC, workflow, equal or different, differing byte
ranges, and a proposed explanation citing the intended change it matches
(for example the DP Replace retirement, the NT51920/25/30/31 retirement, or a
CHANGELOG entry), or "unexplained". The owner confirms each difference;
an unexplained difference becomes a bug.

Non-goals: changing expected bytes, profiles or product code; certifying
support.

**Authority.** R0 evidence work. Local commits of this log and bug files only.
Building the predecessor executor from its pinned tag is allowed in the test
area; private firmware and outputs stay there.

**Model.** `gpt-6-sol` at xhigh. Headless runs pass
`--sandbox workspace-write`.

**Branch and worktree.** `feature/1.1.12/v0916-alignment`, worktree
`<worktrees>/v0916-alignment`, rebased onto the
`1.1.x` trunk at the base refresh.

**Write lock.** This log and new files under `docs/handoff/bugs/`.

**Read first.** `docs/adr/0057-v0916-black-box-parity-certification.md`;
`docs/contracts/v0916-parity-certification-v1.md`; the parity jobs in
`.github/workflows/release.yml` (gated on `2.0.0`); `CHANGELOG.md` from 0.9.16
on; `AGENTS.md`.

**Acceptance.** Every route and case in the plan is covered or listed as not
runnable with the reason; the table is in this log with commands, source SHA
and output hashes; the evidence can be regenerated from committed tooling.

**Stop and ask.** When the script cannot run for a 1.x candidate without
changing `scripts/` or the release workflow (governed; the commander decides
with checklist B-7); when the predecessor cannot be rebuilt as pinned.

**Bugs.** Record every unexplained difference and every other bug as a new
file under `docs/handoff/bugs/` per the bug ledger; cite IDs here.

**Start.** Early, right after the base refresh (checklist A-7), so
differences surface long before freeze.

## Amendment 2026-09-25: release by 2026-09-28 (board decision 9)

**Candidate.** The `1.1.12` product source, today equal to `v1.1.11`
(`1c37bd718`). Record the exact command so the commander can rerun it on the
frozen candidate on 2026-09-27; record how long one full run takes.

**Due.** The complete difference table, with a proposed explanation per
difference, by **2026-09-26 18:00 +08:00**, so the owner can dispose of each
difference in the 2026-09-26 evening window. Report partial coverage rather
than waiting past the deadline.

**Stop and ask** (replaces the B-7 reference): when the script cannot run for
a 1.x candidate without changing `scripts/` or a workflow, stop and report
the exact blocker; the commander decides.

**Machine.** Other lanes build and measure on the same machine. Do not run
more than one heavy job (build, test run, parity run) at a time.

**Common to every lane (decision 9).** Base `feature/1.1.12/handoff`; pull
requests target the `1.1.12` integration branch; the commander pushes and
opens them, the worker never pushes. The current rules still apply in full:
complete the capability-reuse gate your change requires
(`docs/governance/development-execution-workflow.md`), run the affected tests,
and have `python scripts/verify.py --structure-only` pass on your final commit
before you report `verified`. Builds must not leave modified
`packages.lock.json` files; restore them if a build rewrites them. Record every
bug in the bug ledger. The live board is `git show 1.1.x:docs/handoff/1.1.12.md`
(section "Release plan to 2026-09-28").

## Checkpoints

### 2026-09-25 1.x comparator admission blocked
State: local
Commits: none; last HEAD `10a95ffe28c25989319875266cec3bfd8855c603` (Git metadata permission denied)
Evidence: `git rev-parse 'v1.1.11^{}'` -> candidate product source `1c37bd718d8c4fbc5cc247952229e8b2f9f54ce8`; `git rev-parse HEAD` before this checkpoint -> `10a95ffe28c25989319875266cec3bfd8855c603`.
Evidence: read `docs/contracts/v0916-parity-certification-v1.json`, `docs/contracts/v100-candidate-source-executor-v1.json`, `scripts/v0916_parity_certification.py:5991-6095`, ADR 0057, and the parity jobs in `.github/workflows/release.yml`. The immutable plan pins candidate executor head `dfaedec4519fae185e5934b79a225b6c67c7d547` (the v1.0.0 comparison), while `compare` accepts only `--plan`, `--candidate-artifact-dir`, and `--output-root`. It requires protected-run environment values and artifact proof, and validates the candidate against that pinned executor. The workflow invokes parity comparison only for `2.0.0`; ADR 0057 explicitly skips the protected comparator for 1.x. The same plan declares 64 selected routes, 37 with canonical input bindings and 27 without; `compare` calls `build_required_execution_matrix` before candidate admission and fails with `PARITY_FIXTURE_MISSING` for missing route bindings.
Coverage: 0/64 route comparisons executed; all 64 are currently not runnable through this plan for the 1.1.12 source. The 27 route IDs with an additional input-evidence blocker are listed in `canonicalInputAuthority.currentlyMissingRouteIds` in the pinned plan. No output hashes, differing byte ranges, explanations, or full-run duration exist for this candidate. No predecessor or candidate build was started. A rerun command for the frozen 1.1.12 source cannot be specified using the current `compare` CLI and immutable plan without changing governed authority.
Open: **Stop and ask.** Commander must decide the authorized 1.x comparison path and its governed `scripts/` or workflow changes, plus how to handle the 27 missing canonical route inputs. This lane cannot produce the requested difference table under its Write lock. Owner disposition of any eventual byte differences remains pending. Checkpoint commit also failed because `.git/worktrees/v0916-alignment/index.lock` cannot be created; see `BUG-20260925-worktree-git-metadata-write-denied`. This checkpoint and bug record remain uncommitted until the worktree metadata permission is repaired by its owner. The final-commit `python scripts/verify.py --structure-only` gate cannot be claimed because there is no final commit.
Next: Stop this lane and report both blockers to the commander; resume only after a new executable authority and dispatch are recorded and the worktree metadata permission is repaired.

### 2026-09-25 Option A: candidate side run, predecessor build blocked
State: local
Commits: the commit carrying this entry (harness, table, two bug records)
Evidence: authority is board decision 10, option A: a non-certifying local comparison of the 37 routes with canonical inputs; `scripts/`, workflows, contracts, ADR 0057, profiles and testdata unchanged. Work area `<test-area>/parity-1112/`; every build and run set TEMP, TMP and TMPDIR to `<test-area>/temp` and checked the commander's timing flag first. Table: `docs/handoff/1.1.12/parity/v0916-local-comparison.md` (path-free JSON beside it).
Evidence: sources exported without touching shared refs, from the worktree root: `git archive --format=tar v0.9.16 | tar -x -C <test-area>/parity-1112/b` (1.2 s) and `git archive --format=tar 1c37bd718d8c4fbc5cc247952229e8b2f9f54ce8 | tar -x -C <test-area>/parity-1112/c` (2.9 s). The v0.9.16 export matches all 15 files the baseline executor contract declares (global.json, 7 lock files, 7 external tools).
Evidence: predecessor with the exact pinned commands, in `b`: `dotnet restore src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj --locked-mode --runtime win-x64` -> exit 1, `NU1004` for all 7 CLI-graph projects (3 s); the pinned no-restore build then exits 1. Recorded as `BUG-20260925-v0916-baseline-restore-nu1004`.
Evidence: feasibility probe, not used for any comparison, in a separate export `bp`: `dotnet restore src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj --force-evaluate --runtime win-x64` (exit 0, 3 s), then the exact pinned build `dotnet build src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore -p:ContinuousIntegrationBuild=true -p:PathMap=<bp>=/_/src` (exit 0, 10 s). `NvtFwCombiner.Cli.exe` SHA-256 `093212528d1048acdb43563ba795353e1ac872f8b6a3aa99251782e958ad1a30` equals the contract's `cliAssembly`; the runtime closure has the contract's 431 files but 88,359,996 instead of 88,367,164 bytes (closure `e77fe074...f42a`, contract `18da1123...b3b0`). The lock rewrite added only empty `net10.0/win-x64` targets and changed first-party project ranges `0.9.2` -> `0.9.16`; no package id, version or content hash changed.
Evidence: candidate in `c`: `dotnet restore src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj --locked-mode --disable-parallel --runtime win-x64` (exit 0, 10 s); `dotnet build src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore -m:1 -p:ContinuousIntegrationBuild=true -p:PathMap=<c>=/_/src` (exit 0, 34 s); no lock file changed. CLI exe `2709ccda1d77a4ab2a72e789da07eda9cabe558f0dc51813e225da31529567d6`; closure 366 files, 92,947,508 bytes, `66e41c6a3d6eebcdcdd86041be23ed6ce6d18441535ff351014743cc16c84689`.
Evidence: harness, from the worktree root, with `NFC_TEST_AREA_ROOT` and TEMP set: `python docs/handoff/1.1.12/parity/v0916_local_compare.py --work-dir <test-area>/parity-1112/run-c1 --candidate-cli <c>/src/NvtFwCombiner.Cli/bin/Release/net10.0/win-x64/NvtFwCombiner.Cli.exe --candidate-label "<label>" --baseline-blocker "<bug id>" --table-json <test-area>/parity-1112/run-c1-table.json --pause-flag <timing flag>` -> exit 0, `{"candidate-only": 36, "not-covered": 27, "error": 1}`; 726 s wall clock including timing pauses, 462 s of route execution, 23 s canonical materialization and validation. Rendering: `python docs/handoff/1.1.12/parity/render_table.py --table-json docs/handoff/1.1.12/parity/v0916-local-comparison.json --explanations docs/handoff/1.1.12/parity/explanations.json --output docs/handoff/1.1.12/parity/v0916-local-comparison.md`.
Coverage: 0 equal, 0 different, 1 error, 27 not covered (no canonical input), 36 candidate-only. The error is the NT51950 FW1.x cascade CtrlRAM full-flash route: its plan-bound 512 KiB base is rejected as an AB reference while its map declares 256 KiB (`BUG-20260925-nt51950-cascade-ctrlram-plan-base`, suspected). Candidate-side facts: all 13 Standard and AB outputs equal their Golden expected outputs; the NT51951 cascade CtrlRAM output equals the plan's approved-correction candidate hash `1536d344...4ebd`; the four runnable TP-work routes pass the candidate half of the transitive proof. No difference exists yet to explain; the retirements (DP Replace, NT51920/25/30/31) affect no selected route and only explain why those routes are outside the 64.
Evidence: `python scripts/verify.py --structure-only` (TEMP/TMP/TMPDIR = `<test-area>/temp`) at `890d8aede` -> `structure=PASS`, exit 0, 150 s; `sync_derived` changed 0 files; the only warnings are two pre-existing Polytail size warnings in `src/NvtFwCombiner.VersionManagement.Application`. The next commit changes only this line and one Method sentence in the comparison document.
Open: **Stop and report (predecessor cannot be built as pinned).** The commander decides whether option A may use the probe executor (`--force-evaluate` RID restore of the exact tag export plus the exact pinned build), or another predecessor path. Owner disposition of differences waits for that run.
Next: on approval, use the existing probe build as the predecessor and run the same harness with `--baseline-cli <bp>/src/NvtFwCombiner.Cli/bin/Release/net10.0/win-x64/NvtFwCombiner.Cli.exe --baseline-label "<label>"` into a new work directory (about 15 to 20 minutes for both sides), render, explain each difference and commit.

### 2026-09-25 Option A comparison complete with the approved predecessor build
State: local
Commits: the commit carrying this entry (rerun results, table, explanations, one new and two updated bug records, per-invocation timing-flag check in the harness)
Evidence: commander decision 2026-09-25: option A uses the probe predecessor build; the comparison stays **non-certifying** (no receipts, package admission, owner attestation or verdict) and `BUG-20260925-v0916-baseline-restore-nu1004` stays open (now P2) for the formal 1.1.13 comparator.
Evidence: predecessor build method, unchanged since the previous checkpoint: `git archive --format=tar v0.9.16 | tar -x -C <test-area>/parity-1112/bp`; in `bp`, `dotnet restore src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj --force-evaluate --runtime win-x64` (exit 0, 3 s), then the exact pinned build `dotnet build src/NvtFwCombiner.Cli/NvtFwCombiner.Cli.csproj --configuration Release --runtime win-x64 --self-contained true --no-restore -p:ContinuousIntegrationBuild=true -p:PathMap=<bp>=/_/src` (exit 0, 10 s). `NvtFwCombiner.Cli.exe` SHA-256 `093212528d1048acdb43563ba795353e1ac872f8b6a3aa99251782e958ad1a30` equals the contract's `cliAssembly`. The runtime closure has the contract's 431 files but 7,168 fewer bytes (88,359,996 against 88,367,164) and a different closure hash (`e77fe074...f42a` against `18da1123...b3b0`); the SourceLink-missing hypothesis is unverified. Candidate executor unchanged (`2709ccda...67d6`, product source `1c37bd718`).
Evidence: from the worktree root, with `NFC_TEST_AREA_ROOT` and TEMP/TMP/TMPDIR = `<test-area>/temp`: `python docs/handoff/1.1.12/parity/v0916_local_compare.py --work-dir <test-area>/parity-1112/run-bc1 --candidate-cli <c>/src/NvtFwCombiner.Cli/bin/Release/net10.0/win-x64/NvtFwCombiner.Cli.exe --candidate-label "<label>" --baseline-cli <bp>/src/NvtFwCombiner.Cli/bin/Release/net10.0/win-x64/NvtFwCombiner.Cli.exe --baseline-label "<label>" --table-json <test-area>/parity-1112/run-bc1-table.json --pause-flag <timing flag>` -> exit 0, `{"equal": 34, "not-covered": 27, "error": 1, "different": 2}`; 659 s wall clock, no timing pauses, 14 s canonical materialization, 501 s candidate and 141 s v0.9.16 execution (average invocation 4.9 s and 1.5 s). Then `python docs/handoff/1.1.12/parity/render_table.py --table-json docs/handoff/1.1.12/parity/v0916-local-comparison.json --explanations docs/handoff/1.1.12/parity/explanations.json --output docs/handoff/1.1.12/parity/v0916-local-comparison.md`.
Coverage: 34 equal, 2 different, 1 error, 27 not covered (no canonical input). All 14 rebuilt CtrlRAM bases are identical across versions; the four runnable TP-work routes pass all four transitive checks; no process changed an input.
Differences: (1) NT51951 CtrlRAM FW1.x cascade full-flash, 2,816 bytes in `[0xA11C,0xA120)`, `[0xA130,0xA134)`, `[0x2D428,0x2D42C)`, `[0x2D43C,0x2D440)`, `[0x33B10,0x34600)`, both 524,288 bytes, v0.9.16 `7d657a3d...dcca2`, candidate `1536d344...4ebd`: equals the plan's owner-approved Diff NF preservation correction in both hashes, count and ranges (fact). (2) NT51950 CtrlRAM FW1.x cascade TP-work, the same 2,816 bytes in the same five ranges, both 225,280 bytes, v0.9.16 `cfae1591...e47e`, candidate `a239645d...c643`: the reports show v0.9.16 writing the Diff CtrlRAM record over `[0x33200,0x34600)` and the candidate over `[0x33200,0x33B10)` (fact); proposed explanation: the intended Diff NF preservation of CHANGELOG 0.10.1 (`99766df75`, #188) under the NT51950 alias fact scope; the plan approves the correction only for NT51951, recorded as `BUG-20260925-v0916-plan-nt51950-tp-work-correction-missing`. No unexplained difference.
Error: NT51950 CtrlRAM FW1.x cascade full-flash fails on **both** sides after building the same 512 KiB base (`ff9ad012...aa32`): v0.9.16 `profile.v2.compile.map-selection-invalid`, candidate `input.bank-reference.*`; the route map declares 256 KiB. Not a version difference; `BUG-20260925-nt51950-cascade-ctrlram-plan-base` updated with the v0.9.16 outcome.
Open: owner disposition of the two differences (09-26 evening window); the three bugs above for the commander and the 1.1.13 formal comparator. For the frozen-candidate rerun on 2026-09-27: rebuild the candidate from the frozen SHA with the same commands and rerun the harness into a new work directory (about 11 minutes on a quiet machine); map the renamed NT51950 AB route identity if a later policy is used.
Next: commander review of the table.
