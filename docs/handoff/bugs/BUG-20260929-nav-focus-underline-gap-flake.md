# BUG-20260929-nav-focus-underline-gap-flake: navigation underline/gap region test fails intermittently at unchanged code

Status: open; test-only guard committed on `feature/1.1.15/flaky-fixes`, but a full-project local run still failed intermittently
Severity: P2
Found: 2026-09-29, Claude Code commander (Claude Opus 5.5), while checking CI on pull request #484 (run
`36519114436`, job `109248204239`), at `feature/1.1.14/release-back-merge`@`a4e9ef6bf`
Where: `NvtFwCombiner.UiSmoke.Tests.NavigationFocusIndicatorTests.UnderlineAndGapRegionsNeverOverlapAtAnyRenderScaling`
(render scaling 1, both `dark: False` and `dark: True`; this full FQN is what the CI flaky-test bug gate matches)
Observed: both rows of this test failed in that one CI run. The code at `a4e9ef6bf` is identical to `main`
`32808e943` for the affected navigation/render paths (`git diff --name-status 32808e943 a4e9ef6bf` lists only
`SPEC.md` and `docs/` changes), where the same `ci` workflow passed, and the test also passed on pull requests
#480 (`feature/1.1.14/integration-b`) and #483 (`1.1.14`). No code change in the failing run explains the
failure.
Expected: the test passes deterministically: the underline and focus-gap regions never overlap at any render
scaling, per the test's own assertion and the selected-page underline rule of decisions 32 and 178. Earlier
passes do not rule out a real but intermittent rendering or focus race.
Evidence: CI run `36519114436`, job `109248204239`, pull request #484 head `a4e9ef6bf`; comparison runs on `main`
`32808e943` (passed) and pull requests #480/#483 (passed) on their own branches.
Second observation: pull request #486, CI run `36543531383`, head `347b8f3a8`, failed only
`UnderlineAndGapRegionsNeverOverlapAtAnyRenderScaling(scale: 1, dark: True)` at
`NavigationFocusIndicatorTests.cs:331`: `expected a rendered focus ring around Home; found 0 matching pixels`.
The archived TRX is `evidence/1.1.15/ci-flakes/pr486-ui-test-results.trx` in the test area.
The missing focus ring, rather than an overlap between two painted regions, is the observed failure.
Owner: Codex `gpt-6-sol`, `feature/1.1.15/flaky-fixes`.
Resolution: test-only change fixed in `0700e25f4`. `AwaitHistoryReadyAsync` waits for report
history and calls `Dispatcher.UIThread.RunJobs()`, which drains the previously posted
quiet-shell focus job. Startup work after history (report, diagnostics, deferred views, or
external environment) can still affect focus; the exact interleaving behind the CI frame
has not been established. The test now waits for `StartupWork` and drains queued input work
before focusing Home. It also checks Home's focus, `:focus-visible` class, and `BoxShadow`
after the render pump, so a future missing ring gives direct focus-state evidence. The
original pixel thresholds and gap/overlap assertions remain unchanged. The original focused
test did not reproduce locally in 30 runs (120 parameter cases); the CI TRX is the red
evidence. The corrected focused test passed 30/30 runs (120 parameter cases) with the same
command. Full `UiSmoke` project result is recorded in `docs/handoff/1.1.15/WS-FLAKES.md`.
On 2026-09-30, one full `UiSmoke` run failed this test's `(scale: 1, dark: True)` row
(1 failure among 1,861 tests). Its quiet output did not retain the assertion message.
An unchanged full rerun with TRX logging passed 1,861/1,861, and the focused class
passed 13/13. The test correction remains unchanged; the cause of this new local
failure and whether it matches the earlier missing-ring frame are open.
