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
