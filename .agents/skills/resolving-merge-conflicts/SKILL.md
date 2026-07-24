---
name: resolving-merge-conflicts
description: Resolve an in-progress Git merge or rebase while preserving branch intent, unrelated changes, and repository verification gates.
---

# Resolving Merge Conflicts

For NFC repository work, apply
[Agent Skill Routing](../../../docs/governance/agent-skill-routing.md) before
acting.

1. Run read-only preflight: inspect branch/status, the active Git operation,
   merge/rebase heads, merge base, conflict list, and unrelated changes.
2. Verify that the operation is on the intended source/target branch. If it is
   not, preserve any unique work with a recovery ref or patch, then safely abort
   or restore the intended state. Report the recovery pointer.
3. For a valid operation, find the primary source and intent for every side:
   commits, issue/PR, contract/profile, tests, and release or firmware evidence.
4. Resolve one conflict at a time. Preserve compatible intent, choose the side
   that matches the operation's declared goal when intent conflicts, and never
   invent new firmware behavior.
5. Review the resolved diff for conflict markers, duplicated semantics, lost
   changes, generated/private payloads, and line-ending-only noise.
6. Stage only the resolved files owned by this operation. Preserve unrelated
   worktree changes; never use broad staging.
7. Run the narrow affected checks and the canonical gate required by
   `AGENTS.md`. Continue or commit the merge/rebase only after the resolved
   state is independently reviewable.

Completion requires a clean conflict state, preserved unrelated changes,
documented trade-offs, and passing required checks. If the correct intent
cannot be established, stop before staging and request the missing decision.
