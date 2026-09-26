# Agent Model Routing

Status: Active repository authority for current per-runtime agent model-tier defaults.

Selection principles live in
[`AGENTS.md`](../../AGENTS.md#delegation-and-model-selection) and
[`supervised-branch-development`](../../.agents/skills/supervised-branch-development/SKILL.md#model-routing);
this document is their single current-mapping owner, so a model rename is a
one-file edit instead of scattered prose:

- Choose a model and reasoning effort by capability, task difficulty, and
  risk. A model name is not a permanent role assignment.
- An explicit, available, suitable owner model request wins; otherwise
  disclose the limitation instead of substituting silently.
- A model or reasoning-effort choice never lowers repository risk
  classification, required [gates](../../AGENTS.md#risk-adaptive-gates), or
  evidence obligations, regardless of which runtime or model executes the work.

## Current defaults — 2026-09-26 (owner decision 56)

Each runtime's task classes and models are decision 56's own wording, paired
directly so no task class is shared or inferred across runtimes. The tier
column is a readability label, not a cross-runtime equivalence: Claude's
strongest-reasoning tier includes complex implementation, but Codex's
strongest-reasoning model is scoped to review only, and Codex's own
implementation work stays on its capable-implementation model regardless of
complexity. Update this section when a runtime's owner decision changes its
models or task classes; it never changes the principles above.

### Claude

| Tier | Task class | Model |
| --- | --- | --- |
| Strongest reasoning | R3 firmware semantics, architecture design, complex cross-module implementation | Opus 5.5 |
| Capable implementation | Clear-spec R1 implementation, document and board edits, review-driven revisions | Sonnet 5 |
| Fast, lower-cost | Pure lookup and inventory | Haiku 4.5 |

### Codex

| Tier | Task class | Model |
| --- | --- | --- |
| Strongest reasoning | Firmware and architecture review | `gpt-6-astra` |
| Capable implementation | Code review and implementation | `gpt-6-sol` |
| Fast, lower-cost | Mechanical verification | `gpt-6-luna` |

When the dispatching tool exposes neither a model name nor a reasoning-effort
control for a tier — for example, a surface that only inherits its caller's
configuration — write `inherits parent` for that field rather than guessing or
naming a model the tooling does not expose.

## Dispatch disclosure

Use the field order
[`supervised-branch-development`](../../.agents/skills/supervised-branch-development/SKILL.md#dispatch-and-execution)
already defines: `role / model / reasoning effort / inherited-or-override /
read-only-or-write / scope / selection reason`. This document does not define
a second disclosure format.

## When the current tools do not offer a listed model

The tool configuration actually exposed is the evidence, never this table or a
model's own prose. When a tier's listed model is unavailable:

1. Choose the closest available model that still meets the tier's
   capability/risk bar; do not drop to a lower tier merely to save cost.
2. Name the substitution in the dispatch line's selection reason instead of
   reporting the listed model as if it ran.
3. Report the gap so this table can be corrected; an unavailable model is a
   reason to update this document, not to relax a gate or skip disclosure.
