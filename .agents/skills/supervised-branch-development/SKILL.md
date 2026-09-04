---
name: supervised-branch-development
description: Coordinate multi-agent NFC branch reconstruction, R3 migration, conflict-heavy work, or release integration.
---

# Supervised Branch Development

Use for multi-agent reconstruction, R3 firmware migration, release integration,
or large conflict resolution. Ordinary work remains single-writer:

```text
primary implementer → narrow test → coherent checkpoint
→ independent final review → final gate → PR
```

## Model routing

Select from current available model metadata by capability, difficulty, and
risk; model labels are not permanent role assignments. Use the strongest
suitable reasoning model for complex architecture, decision, and review work;
a capable implementation model for the selected cross-file/R2/R3 writer; and a
faster, lower-cost model for bounded discovery, logs, tests, or mechanical work
with exact paths. Sol, Terra, and Luna are current examples/default heuristics,
not hard requirements. An explicit owner model request wins when the model is
available and suitable; otherwise disclose the limitation. A model choice never
downgrades repository risk, required gates, or evidence.

## Dispatch and execution

1. Pin the integration base, target, branches, risk, acceptance criteria, and
   one writer for each mutable surface. Parallelize only independent work.
2. Before every dispatch, the conductor emits a user-visible line in this exact
   field order: `role / model / reasoning effort / inherited-or-override /
   read-only-or-write / scope / selection reason`. Name the exact model when
   tooling exposes it. When tooling exposes only inheritance, say `inherits
   parent` and disclose the parent identity only when known; never guess.
3. Send every worker one fixed dispatch envelope containing: `task`, `source`,
   `base`, `risk`, `authority`, `outcome`, `non-goals`, `semantic owner`,
   `capability-reuse disposition`, `mutable paths`, `model reason`, `tests`,
   `gates`, `deliverables`, and `stop conditions`.
4. Require the standard worker report: status, actual model/config, base/head,
   owned paths, files changed, commands/results, diff summary, unresolved gates,
   and owner input needed. The final report repeats actual model/config when
   known; when tooling exposes only inheritance it says `inherits parent` and
   discloses the parent identity only when known, never guessing. Discovery
   workers report evidence and do not edit beyond their exact assigned R0/R1
   mechanical paths.
5. Review the frozen head independently. Route every correction to the same
   writer, rerun its affected narrow gate, and replace the frozen candidate
   before repeating only the invalidated review lane.
6. The primary agent or owner integrates reviewed checkpoints. Run the
   canonical final gate once on the frozen candidate. Declare blocked only when
   every remaining path requires the same unavailable evidence, authority, or
   external-state change.

## Branch hygiene

Reuse one task/version branch. Do not create an extra worktree or branch unless
there is a concrete independent need. Every handoff states the exact base and
head, lists tracked and untracked changes, preserves documentation, and never
cleans up or deletes files without owner authorization.

Report branch/head, owned surfaces, completed checks, findings, active gate,
and whether owner input is required.
