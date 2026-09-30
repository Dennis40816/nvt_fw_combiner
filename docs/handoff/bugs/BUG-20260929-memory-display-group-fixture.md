# BUG-20260929-memory-display-group-fixture: style fixture omits typed display group

Status: fixed
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, stage 1 resumption at
feature/1.1.15/memory-layout over 37c6c9586.
Where: tests/NvtFwCombiner.UiSmoke.Tests/XamlControlStyleContractTests.SemanticStates.cs
Observed: MemoryCoverageRetainsStateAndThemeNeutralFillRole expects a Base group
but constructs its kept segment without the new Application-owned DisplayGroup.
Expected: the fixture supplies the typed Base fact; Presentation must not restore
its deleted kept-to-group classifier (decision 189, R09 A-11).
Evidence: evidence/1.1.15/test-results/stage1-resume-ui.trx: 190 passed, 1 failed.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: stage 1 completion commit following 37c6c9586 supplies DisplayGroup.Base
in the fixture. `stage1-resume-ui-corrected.trx` and `stage1-final-ui.trx`:
191 passed, zero failed. Application tests independently cover kept grouping.

Correction review: the complete Release UiSmoke run over 8010d7770's correction
also exposes missing typed SlaveLeft/SlaveRight facts in the same style fixture
after absent groups become explicitly nullable. `review-full-uismoke.trx`
records this new failure; the correction supplies those original group facts
without restoring the Presentation classifier. See WS-MEMLAYOUT's final gates.
