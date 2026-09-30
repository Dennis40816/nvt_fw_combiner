# BUG-20260929-memory-group-emphasis-leak: group legend activity lifts every local leaf

Status: fixed
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, stage 2 over ce680fec5.
Where: MemoryCoverageInteractionBehavior.InteractionLease.Publish
Observed: the new multi-member rail lease also publishes ordinary IsActive for
every member, adding a border to every local leaf while its parent is focused.
Expected: group membership may emphasize corresponding legend rows; local leaf
decoration must still be driven only by that leaf or its card.
Evidence: stage2-regression.trx: four local seam/group cases fail with border 2
instead of 0; four other failures are superseded legend/frame expectations.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Group membership now publishes rail activity without terminal decoration. Existing seam tests and the added group-to-leaf regression pass in stage2-final-ui.trx (189/189).
