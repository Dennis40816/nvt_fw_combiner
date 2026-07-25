---
name: supervised-branch-development
description: Coordinate explicit multi-agent NFC branch reconstruction, R3 migration, conflict-heavy work, or release integration.
---

# Supervised Branch Development

Use only when the user requests multi-agent work or the task has multiple
branches, branch reconstruction, R3 firmware migration, release integration,
or large conflict resolution. Ordinary work uses single-writer mode:

```text
primary implementer → narrow test → coherent checkpoint
→ independent final review → final gate → PR
```

For supervised mode:

1. Pin the integration base, target, branches, risk, acceptance criteria, and
   mutable-surface ownership.
2. Assign exactly one writer per surface. Supervisors and evidence reviewers
   remain read-only.
3. Give each agent a bounded deliverable, test, and stop condition. Parallelize
   only independent work.
4. The primary agent or owner is the default integrator. Delegate a separate
   integrator only for multi-branch or high-risk release integration.
5. Integrate one reviewed checkpoint at a time, rerun the affected narrow gate,
   and run the canonical final gate once on the frozen candidate.
6. Declare blocked when every remaining path requires the same unavailable
   evidence, authority, or external-state change—not after an arbitrary number
   of polling cycles.

Report branch/head, owned surfaces, completed checks, findings, active gate,
and whether owner input is required. Do not create extra worktrees or agents
without a concrete independent task.
