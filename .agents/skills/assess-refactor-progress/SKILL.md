---
name: assess-refactor-progress
description: Report consistent evidence-backed 0.10.x refactor progress. Use when the owner asks how much of the refactor is complete, requests a percentage or current frontier, or wants progress compared across sessions.
---

# Assess Refactor Progress

Report four separate metrics instead of one ambiguous percentage:

1. **Ticket completion** — completed GitHub issues divided by the complete
   owner-approved dependency-plan inventory.
2. **Headless canonical foundation** — completed Canonical pilot and Headless
   tickets divided by all tickets in those waves.
3. **Weighted total** — the declared program weights below, applied to live
   completion inside each wave group.
4. **Unified preload lifecycle** — completed v0.10.5 PL tickets divided by
   the complete approved PL inventory.

GitHub Issues own live completion. The repository dependency plan owns only the
stable ticket universe, waves, outcomes, and blocker edges. Never infer
completion from local commits, PR titles, code volume, elapsed time, or an old
report.

## Generate The Snapshot

From the repository root, run:

```powershell
$env:PYTHONUTF8 = "1"
python .agents/skills/assess-refactor-progress/scripts/assess_progress.py `
  --format markdown
```

Use `--format json` when another tool or report will consume the result. Use
`--issues-json <path>` only for deterministic tests or an explicitly disclosed
offline snapshot; never present fixture state as live GitHub state.

The script applies these owner-priority weights:

| Group | Included dependency-plan waves | Weight |
| --- | --- | ---: |
| Baseline | `Baseline` | 5% |
| Headless canonical foundation | `Canonical pilot`, every `Headless …` wave | 50% |
| Deferred UI | `Deferred UI` | 15% |
| Deletion | `Runtime deletion`, `Compatibility deletion` | 10% |
| Core convergence | `Core convergence` | 15% |
| Integration | `Integration` | 5% |

The weights remain the closed legacy-refactor program metric; the separately
reported preload metric does not rewrite that historical allocation. Do not
silently change weights. A new wave or missing planned issue makes the snapshot
low-confidence and requires an owner-reviewed skill/model update.

## Report The Result

Keep the script's metric names and rounding. Lead with:

```text
Ticket completion: n/N (x.x%)
Headless canonical foundation: n/N (x.x%)
Unified preload lifecycle: n/N (x.x%)
Weighted total: x.x%
Confidence: High | Medium | Low
```

Then list:

- the current dependency-ready frontier;
- evidence identities (Git head, dependency-plan hash, GitHub query time);
- data-quality warnings or human gates;
- a short explanation when the three percentages differ.

An open ticket is on the frontier only when it has `ready-for-agent` and every
declared blocker is completed. A closed issue counts as complete only when
GitHub reports `stateReason=COMPLETED`.

This skill is read-only. It never edits issues, labels, PRs, plans, goals, or
release state.

## Validate The Skill

```powershell
$env:PYTHONUTF8 = "1"
python tests/scripts/test_assess_refactor_progress.py
python C:/Users/liusx/.codex/skills/.system/skill-creator/scripts/quick_validate.py `
  .agents/skills/assess-refactor-progress
python scripts/validate_repository.py
```
