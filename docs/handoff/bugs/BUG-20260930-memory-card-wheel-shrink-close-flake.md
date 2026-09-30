# BUG-20260930-memory-card-wheel-shrink-close-flake: the card-wheel test loses its card after shrinking it under load

Status: fixed (on `feature/1.1.15/memory-layout`, pull request #492, pending merge)
Severity: P3
Found: 2026-09-30, Claude Code commander (Claude Opus 5.5), CI run `36672274823` (`dotnet / test (ui)`, job
`109749638184`) on pull request #492 head `60843aaec`: the test failed on attempt 1 and passed on the retry, so the
decision 193 gate marked it flaky.
Where: `NvtFwCombiner.UiSmoke.Tests.MemoryCoveragePopupTests.CardWheelDoesNotScrollTheAncestorPage`
(`direction: 0`), `tests/NvtFwCombiner.UiSmoke.Tests/MemoryCoveragePopupTests.Lifecycle.cs`
Observed: attempt 1 TRX: `Assert.IsType() Failure: Value is null` in `BoundsInWindow` at the card's centre
(Lifecycle.cs line 162): the card had closed. The `direction: 0` case shrinks the open card to `MaxHeight = 120`
while the pointer still rests on the disclosure toggle; the shorter card can leave the pointer outside it, which
starts the passive close grace (decision 189 M2). On a loaded runner the grace elapsed before the test read the
card.
Expected: the test keeps the pointer on the card, as a user scrolling the card does; the product behavior (a
card closes after its grace once the pointer has left it) is intended.
Evidence: amplification: with a 500 ms wait after the shrink the test failed locally at the same assertion every
run; with the pointer moved back onto the card after the shrink it passed with the same wait. Without the wait the
case had passed 25 of 25 loaded local runs, so the red evidence is the CI TRX plus the amplified run.
Owner: Claude Code, feature/1.1.15/memory-layout.
Resolution: after `card.MaxHeight = 120`, the test moves the pointer to the card's centre before it scrolls. No
production change.
