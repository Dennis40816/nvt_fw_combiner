# WS-PERF: startup performance

Owner: Codex. Commander: Claude Code. Board: [1.1.12 board](../1.1.12.md),
checklist C-1. Protocol: [handoff README](../README.md).

## Dispatch envelope 2026-09-25

**Outcome.** Every launch, cold and warm, measured from process launch: the
first main window is visibly presented within **500 ms**, and all startup
loading (catalog validation, required page readiness, deferred startup views)
completes within **2,000 ms**. Warm baseline on the window-handle proxy:
1.333 s to window, then 4.846 s to catalog applied, 6.518 s to warm-up
finished.

1. Measure first: one warm-up plus five scored warm launches, and separate
   controlled cold launches, on the same machine, settings, build flavor and
   arguments; raw stage timings; visible-presentation evidence (a window
   handle alone does not prove presentation). Commit the measuring script so
   anyone can reproduce it.
2. Break down the 4.846 s post-window interval. Hypotheses to test, not
   causes: catalog and profile loading, validation and compilation, repeated
   work, UI materialization; also, from code mapping, catalog publication
   compiling every static route at startup (`CanonicalCapabilityCatalogSource.Load`).
3. Optimize through the existing startup and catalog owners. Background work
   still counts toward 2,000 ms.
4. Check that the first Merge and Replace navigation does not inherit the
   wait.
5. Report gains, memory and allocation trade-offs and residual delays. A
   missed target names its blocker; the limit is never relaxed.

Non-goals: CtrlRAM cold first-open and F14/F15 (1.2.8); broad profile
reference convergence (1.2.1); weakening validation, catalog completeness,
honest loading and error feedback, page readiness, independent page instances
or bounded lifetime.

**Authority.** R1, or R2 if an optimization changes catalog or compilation
semantics (for example lazy route compilation): then pause for architecture
review. Local commits on this branch; no push or pull request (owner). Final
acceptance (actual package on the owner machine) is the owner's.

**Model.** `gpt-6-sol` at xhigh; escalate to `gpt-6-astra` for a catalog or
compilation redesign. Headless runs pass `--sandbox workspace-write`.

**Branch and worktree.** `feature/1.1.12/startup-performance`, worktree
`<worktrees>/startup-performance`, rebased onto
the `1.1.x` trunk at the base refresh.

**Write lock.** This log; new files under `docs/handoff/bugs/`;
`tools/NvtFwCombiner.PerformanceProbe/`; startup and catalog code in
`src/NvtFwCombiner.Desktop/`, `src/NvtFwCombiner.Bootstrap/`,
`src/NvtFwCombiner.Application/Capabilities/`,
`src/NvtFwCombiner.Infrastructure/Composition/` catalog and registry loading,
`src/NvtFwCombiner.Infrastructure/Bundles/`, and Presentation startup files
(`MainWindow*`, shell construction and catalog application); their tests.
List the exact files in your first checkpoint. WS-IO owns picker,
persistence and Report files in the same projects: overlap stops that part.

**Read first.** Root `AGENTS.md`; the roadmap "Owner-approved 1.1.12 startup
optimization" section and the audit handoff "Startup optimization assigned to
1.1.12" section (on the trunk since `v1.1.11`); board C-1.

**Acceptance.** Items 1-5 above with committed measurement tooling, a stage
breakdown table in this log, before and after numbers on comparable launches,
and passing affected tests. Final C-1 acceptance happens at freeze on the
owner machine (checklist D-2).

**Stop and ask.** When a target looks unreachable without changing validation
or catalog semantics; when a change would touch profiles, contracts or
firmware bytes; when owner-machine measurement is needed.

**Bugs.** Record every bug you find, in or out of scope, as a new file under
`docs/handoff/bugs/` per the bug ledger in `docs/handoff/README.md`. Cite bug
IDs here; fix only what is in scope.

**Start.** Right after the base refresh (checklist A-7).

## Amendment 2026-09-25: release by 2026-09-28 (board decision 9)

**Owner change.** Claude Code (Opus 5.5) now owns this workstream as Startup
A: everything after `main-window.opened` (catalog loading, validation,
compilation and application; startup warm-up of views). Startup B, process
launch to `main-window.opened`, moves to WS-WINDOW (Codex) on
`feature/1.1.12/startup-window`.

**Baseline** (board, "Release plan to 2026-09-28"): `main-window.opened` to
`startup-warmup.catalog-state.applied` 5,005 ms with 770 MB allocated; warm-up
completed 6,498 ms after managed entry.

**Write lock (Startup A).** This log; new bug files;
`src/NvtFwCombiner.Application/Capabilities/`; catalog and registry loading in
`src/NvtFwCombiner.Infrastructure/Composition/` and
`src/NvtFwCombiner.Infrastructure/Bundles/`; catalog application and startup
warm-up code in `src/NvtFwCombiner.Presentation.Avalonia/` (files listed in
the first checkpoint); `scripts/measure-startup.ps1`; their tests.

**Due.** Pull request by 2026-09-27 12:00 +08:00, reviewed by a fresh Codex
thread.

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
