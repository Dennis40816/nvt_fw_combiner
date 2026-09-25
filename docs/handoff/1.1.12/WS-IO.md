# WS-IO: output and persistence truth (F07, F08, F20, F21)

Owner: Codex. Commander: Claude Code. Board: [1.1.12 board](../1.1.12.md),
checklist C-2. Protocol: [handoff README](../README.md).

## Dispatch envelope 2026-09-25

**Outcome.** The roadmap's 1.1.12 output and persistence repairs:

- **F07:** loose delivery keeps its exact committed path, size and hash after
  cancellation or report failure, without rerunning processors; atomic
  bundles keep one transaction.
- **F08:** persistence failures are visible and retryable.
- **F20 residual:** the remaining picker and I/O consumers capture their
  context before awaiting, reject stale results without mutation, and show
  I/O failures with a retry.
- **F21 residual:** local Report replacement is atomic and preserves the
  original destination on precommit failure; nonlocal providers get a
  best-effort disclosure, not a fake filesystem path.

Rules: revalidate each finding against the current code first (an old finding
is not proof of a current defect); attach F18 behavioral or interleaving
evidence to every repair; keep the shipped snapshot, reentry, disposal and
Report Save fixes. Non-goals: the full shutdown state machine (AUD-01
lifetime work stays later); Desktop A-FlashCode delivery semantics (WS-FLOW F6,
1.1.13).

**Authority.** R1 behavior corrections. Local commits on this branch; no push
or pull request (owner).

**Model.** `gpt-6-sol` at xhigh. Headless runs pass
`--sandbox workspace-write`.

**Branch and worktree.** `feature/1.1.12/io-persistence`, worktree
`<worktrees>/io-persistence`, rebased onto the
`1.1.x` trunk at the base refresh.

**Write lock.** This log; new files under `docs/handoff/bugs/`; delivery,
commit and report code in `src/NvtFwCombiner.Application/Composition/`
(`CompositionRunService*` delivery, commit and report partials only; not
`CompositionEngine*` or `CompositionExecutionExperience.cs`, which WS-FLOWFIX
owns); persistence and picker consumers in
`src/NvtFwCombiner.Presentation.Avalonia/`; file stores and atomic writers in
`src/NvtFwCombiner.Infrastructure/Files/`; their tests. List the exact files in
your first checkpoint; WS-PERF owns Presentation startup files.

**Read first.** Root `AGENTS.md`; the audit handoff rows AUD-01 (F08), AUD-03
(F07), AUD-04 (F20) and AUD-06 (F21) and the roadmap 1.1.12 allocation (on the trunk
since `v1.1.11`); `tests/AGENTS.md`.

**Acceptance.** Each repair has a failing-then-passing behavioral test or
interleaving evidence, affected test projects pass, and this log lists each
finding as fixed, not reproduced (with evidence) or reallocated.

**Stop and ask.** When a repair needs a shutdown lifecycle change, a public
contract or report schema change, or files in another workstream's lock.

**Bugs.** Record every bug you find as a new file under `docs/handoff/bugs/`
per the bug ledger in `docs/handoff/README.md`; cite IDs here.

**Start.** After the base refresh (checklist A-7); 1.1.11 changes the same
picker and input-result consumers.

## Amendment 2026-09-25: release by 2026-09-28 (board decision 9)

**Time box.** C-2 ships in 1.1.12 only if this branch is `verified` and its
pull request is green by **2026-09-27 18:00 +08:00**; otherwise the commander
moves it to 1.1.13. Order: F07, then F08. Start F20 and F21 residuals only if
F07 and F08 are `verified` by 2026-09-26 20:00; otherwise record them as
reallocated to 1.1.13 in your final checkpoint.

**Write lock change.** Startup files now belong to two lanes: Startup A
(Claude, everything after `main-window.opened`: catalog loading and
application, startup warm-up) and Startup B (WS-WINDOW, Codex: process launch
to `main-window.opened`, including `src/NvtFwCombiner.Desktop/`, `App.axaml*`,
`MainWindow.axaml*` and pre-window resources). Any overlap stops that part and
goes to the commander.

**Checkpoints due.** First checkpoint (exact file list, revalidation result
for F07 and F08) by 2026-09-25 23:00; then one per finding.

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

### 2026-09-25 First checkpoint: F07/F08 current-head revalidation
State: planned
Commits: pending this checkpoint
Evidence: `git status --short --branch` -> clean `feature/1.1.12/io-persistence` at `5882d7a57`; `CompositionRunService.cs:464-500` -> postcommit loose-delivery cancellation escapes; `CompositionRunPresentationViewModel.cs:190-220,338-360` -> report projection cancellation/failure drops the committed result; `LatestSnapshotPersistenceCoordinator.cs:84-123` and `MainWindow.axaml.cs:560-565,627-632` -> a save failure is recorded without user-visible retry. Source revalidation only; behavioral red/green evidence pending.
Owned mutable files selected for F07: `src/NvtFwCombiner.Application/Composition/CompositionRunService.cs`, `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/CompositionRunPresentationViewModel.cs`, `tests/NvtFwCombiner.Application.Tests/CompositionRunServiceTests.DeliveryCancellation.cs`, `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.RunProgress.cs`; this log and `docs/handoff/bugs/BUG-20260925-loose-delivery-cancel-loses-receipt.md`, `docs/handoff/bugs/BUG-20260925-report-projection-loses-committed-output.md`, `docs/handoff/bugs/BUG-20260925-persistence-failure-not-actionable.md`. Existing semantic owners are Application `CompositionRunService` for commit/delivery and Presentation `CompositionRunPresentationViewModel` for result projection; no new producer or public contract is planned. Base: `1.1.x` merge base `1c37bd718`; risk R1; narrow gates: Application and UiSmoke affected tests; final gate: structure verifier and scoped Polytail. F07 acceptance: exact committed path/size/hash survives cancellation and report projection failure without rerunning processors, while bundle delivery remains atomic.
Open: F08 requires a failure notification and retry action at `src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs`, which WS-WINDOW owns under the Amendment. Stop that part and request commander allocation after F07. `BUG-20260925-persistence-failure-not-actionable` remains open. No F20/F21 work has started.
Next: reproduce F07 with focused behavioral tests, then repair only the selected files.

### 2026-09-25 F07 local correction and cross-lock stop
State: local
Commits: `807c294bc` (first checkpoint); this F07 checkpoint commit pending
Evidence: `LooseDeliveryCancellationRetainsCommittedPrimaryReceipt` failed with `TaskCanceledException` at the postcommit loose-delivery await before the correction, then passed 1/1. `PostcommitReportCancellationKeepsCommittedOutputVisible` failed with `No output` before the UI correction, then passed 1/1 with exact committed path, size and SHA-256 read from the output file. `dotnet test tests/NvtFwCombiner.Application.Tests/NvtFwCombiner.Application.Tests.csproj --no-restore` -> 1612/1612. `dotnet test tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj --no-restore` -> initially 1652/1653; `SharedInputFileCountsRolesSeparatelyFromBundleCopies` failed again alone because it used the process Desktop as a bundle parent. After setting its parent to its own `TempWorkspace`, the focused test passed 1/1 and the full UiSmoke project passed 1653/1653. The restore's `packages.lock.json` modifications were restored, and `git diff --check` passed. Scoped Polytail of the selected Application/Presentation owner diff: PASS for the local F07 correction; no duplicate commit owner, changed firmware bytes/ranges, or new report schema. This is local test evidence, not an integration or release pass.
Findings: `BUG-20260925-loose-delivery-cancel-loses-receipt` fixed; `BUG-20260925-report-projection-loses-committed-output` locally corrected for cancellation and generic post-result projection exceptions, with direct non-cancellation Report-failure reproduction still open; `BUG-20260925-output-confirmation-test-desktop-default` fixed. `BUG-20260925-cli-report-failure-hides-receipt` is suspected from a CLI write/print ordering path outside this lock. `BUG-20260925-worker-git-metadata-acl` remains open: ordinary linked-worktree Git writes are denied, so commits use the alternate index at `test area/temp/ws-io-index.tmp` and a direct update of this branch ref; the normal index remains stale.
Open: F07 end-to-end Report failure remains unverified, including the CLI path in `src/NvtFwCombiner.Cli/` outside this Write lock. F08 `BUG-20260925-persistence-failure-not-actionable` requires a visible failure/retry hookup in `MainWindow.axaml.cs`, assigned to WS-WINDOW by the Amendment. Stop those parts for commander allocation. F20/F21 did not start; with F07/F08 unverified at this final WS-IO checkpoint, reallocate their residual work to 1.1.13 under the time-box rule. C-2 is not `verified` and has no green pull request evidence.
Next: commander resolves the CLI and WS-WINDOW path ownership, then supplies the remaining F07/F08 evidence and decides C-2 release allocation. Run `python scripts/verify.py --structure-only` against this final local commit before any `verified` report.

### 2026-09-25 Final local checkpoint after structure gate
State: local
Commits: `807c294bcf8b0309739894b339f4c5cde377ae0a` (first checkpoint), `d4261e7bafa15d8c9c62dfadfc3b6641392936d1` (F07 correction); this status checkpoint commit pending
Evidence: after the F07 commit, `python scripts/verify.py --structure-only` first failed before validation because the active Python lacked `yaml` (`BUG-20260925-structure-verifier-missing-yaml`). With `PyYAML==6.0.3` installed only in the test area and supplied by process `PYTHONPATH`, `sync_derived.py` reported zero changes; `validate_repository.py` then failed because `CompositionRunService.cs` and `CompositionRunPresentationViewModel.cs` lack a `design-active/current-final` capability-reuse record. No changed `packages.lock.json` remains. The normal linked-worktree index still cannot be updated due `BUG-20260925-worker-git-metadata-acl`; the alternate index reports a clean F07 commit.
Base clarification: this branch descends from `feature/1.1.12/handoff` at `bb327a509f02a20d23349ad66acc79ed0c172a4b`, as required by the Amendment. The first checkpoint's `1.1.x` merge-base value `1c37bd718` described its relationship to that moving trunk, not its dispatch base.
Open: the integration record belongs under `docs/governance/change-records/`, outside this Write lock. F07's direct non-cancellation Report-failure evidence and the CLI path (`BUG-20260925-cli-report-failure-hides-receipt`) need allocation; F08's `MainWindow.axaml.cs` hookup is in WS-WINDOW's lock (`BUG-20260925-persistence-failure-not-actionable`). F20/F21 residuals are reallocated to 1.1.13 under the Amendment. C-2 remains local and cannot be reported `verified`, integrated or published.
Next: commander assigns the crossed write locks and record owner, repairs normal Git metadata access, and decides whether C-2 can still meet the release green-PR deadline; the worker stops here.
