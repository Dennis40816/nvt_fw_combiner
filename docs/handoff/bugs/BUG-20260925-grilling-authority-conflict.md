# BUG-20260925-grilling-authority-conflict: SPEC and AGENTS.md disagree on the decision workflow; routing omits three skills

Status: open
Severity: P3
Found: 2026-09-25, Claude Code (Opus 5.5), while reviewing skills, at `1.1.12`@`d69b6e54a`
Where: `SPEC.md:2090-2093`; `AGENTS.md:227-234`; `docs/governance/agent-skill-routing.md`
Observed: SPEC closes architecture and terminology decisions through the explicit `grilling` workflow; `AGENTS.md` assigns that to `grill-with-docs` and limits `grilling` to requested generic interviews. The routing document never mentions `capture-firmware-ui`, `locate-golden-evidence` or `open-firmware-example`.
Expected: one owner for the decision workflow and a routing entry for every active skill in `.agents/skills/manifest.json`.
Evidence: `grep -c <skill> docs/governance/agent-skill-routing.md` returns 0 for each of the three skills.
Owner: Claude Code, WS-AI, `feature/1.1.12/agent-docs`
Resolution:
