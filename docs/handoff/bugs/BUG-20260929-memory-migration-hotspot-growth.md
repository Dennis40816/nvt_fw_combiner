# BUG-20260929-memory-migration-hotspot-growth: migration exceeds existing size baselines

Status: fixed
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, stage 1 resumption over 37c6c9586.
Where: MergePresentationViewModel.Memory.cs and ShellTextResources.DynamicText.cs
Observed: structure-only rejects aggregate growth of +2 and +1 nonblank lines.
Expected: preserve ADR 0080 item 17 baselines without changing policy.
Evidence: first resumed structure-only run fails those two size gates.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: stage 1 completion commit following 37c6c9586. Reflowed the explicit
tuple declaration and shortened the now-single-parameter method declaration.
The attempted inferred tuple and incomplete enum switch failed IDE0008/IDE0072;
both were corrected without suppressions. Existing aggregate baselines remain
2158 and 3226. `stage1-final-structure.log`: PASS; `stage1-final-ui.trx`: 191 passed.
