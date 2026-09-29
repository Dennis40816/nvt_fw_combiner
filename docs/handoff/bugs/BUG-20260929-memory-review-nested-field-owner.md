# BUG-20260929-memory-review-nested-field-owner: Nested field may retain a pre-coalesced owner

Status: fixed
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra author review of the correction over 8010d7770.
Where: MemoryLayoutProjector.Sections.cs, DeclaredSectionOwner
Observed: A field nested in the second code sibling returns that sibling,
while the code sibling itself returns the first sibling's section identity.
Expected: Field and parent use the same final declared section identity.
Evidence: review-nested-fixture-red.trx reproduces the missing field (0 passed,
1 failed); review-parent-final-green.trx passes all 72 MemoryLayout cases.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: Resolve a declared code ancestor through its final section owner,
so its nested fields and the coalesced code siblings use one stable identity.
