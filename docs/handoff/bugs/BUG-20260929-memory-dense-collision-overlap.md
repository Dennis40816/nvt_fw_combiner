# BUG-20260929-memory-dense-collision-overlap: Dense collision list leaves no room for its card

Status: resolved
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, final scoped lifecycle review.
Where: MemoryCoverageBar.OpenLocal collision-list height budget
Observed: twelve adjacent three-byte fields in a 620-DIP-height window create
a list that consumes the available vertical space; opening a terminal card
slides it back over the list. Reproduced at 240 and 420 DIP.
Expected: the list scrolls within a bounded share of available space, retaining
room for the existing card beside it without obscuring list choices.
Evidence: `evidence/1.1.15/test-results/stage3-dense.trx`, 2 failed / 0 passed;
initial `memory-popup-tiny-dense-240.png` and `420.png` captures.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: the collision list uses at most half of the available side and
scrolls; the card retains the remaining space. `stage3-dense-final.trx`: 27
passed, 0 failed (dense cases, all 24 marker combinations and keyboard teardown).
Initial red images remain under `evidence/1.1.15/screens/diagnostic/`.
