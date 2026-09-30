# BUG-20260929-memory-collision-scroll-focus: Scroll invalidation also closes a keyboard-selected card

Status: resolved
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, dense-list correction regression.
Where: MemoryCoverageBar.OpenLocal collision ScrollChanged handler
Observed: keyboard End brings the last entry into view; unconditional scroll
invalidation closes its newly selected card. Mouse-wheel scrolling can naturally
enter a different visible row and open its correct card, contrary to the new
fixture's overly strict null-card assertion.
Expected: retain keyboard-driven exploration while bringing focus into view;
mouse scrolling must discard the old row's card and preserve the list, allowing
new pointer-enter selection.
Evidence: `evidence/1.1.15/test-results/stage3-dense-green.trx` (24 passed, 3 failed).
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: reuse the existing keyboard-origin fact to preserve focus-driven
bring-into-view. Pointer scrolling clears the old card; a new visible entry
may open its own. Exact stale-identity and subsequent pointer selection pass,
as does keyboard End/Escape. `stage3-dense-final.trx`: 27 passed, 0 failed.
