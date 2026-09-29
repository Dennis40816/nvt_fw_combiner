# BUG-20260929-nav-focus-underline-gap-flake: navigation underline/gap region test fails intermittently at unchanged code

Status: open
Severity: P2
Found: 2026-09-29, Claude Code commander (Claude Opus 5.5), while checking CI on pull request #484 (run
`36519114436`, job `109248204239`), at `feature/1.1.14/release-back-merge`@`a4e9ef6bf`
Where: `NavigationFocusIndicatorTests.UnderlineAndGapRegionsNeverOverlapAtAnyRenderScaling` (render scaling 1, both
`dark: False` and `dark: True`)
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
Owner: unassigned; triage together with the other flaky-test follow-up
([BUG-20260928-launcher-admission-deadline-test-flake](BUG-20260928-launcher-admission-deadline-test-flake.md),
reopened Bootstrap host wall-clock race) rather than as a separate one-off investigation.
Resolution: not fixed; cause unknown. First observation only. Candidate area to check first: interaction between
the decision 178 startup-focus change (focus starts on inconspicuous shell content instead of a navigation tab)
and render/measurement timing for the underline and focus-gap regions — a timing-sensitive layout pass racing the
deferred focus placement is a plausible but unconfirmed mechanism. No production behavior, firmware bytes, or
navigation/focus contract is asserted to have changed; the selected-page underline rule of decision 32/178 is
unaffected as far as currently known.
