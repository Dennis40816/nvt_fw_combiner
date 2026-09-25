# BUG-20260925-skill-stale-persona-rules: skills enforce retired Display/TP HW/TP FW rules

Status: open
Severity: P2
Found: 2026-09-25, Claude Code (Opus 5.5), while reviewing skills, at `1.1.12`@`d69b6e54a`
Where: `.agents/skills/composition-experience-change/SKILL.md:3,15,20`; `.agents/skills/firmware-profile-authoring/SKILL.md:13`
Observed: both skills instruct agents to enforce Display, TP HW and TP FW persona access rules.
Expected: skills follow the current experiences and access policy in `docs/architecture/experience-and-access-policy.md`, which no longer defines those personas.
Evidence: `grep -n -i "persona\|TP FW\|TP HW" docs/architecture/experience-and-access-policy.md` finds none of them.
Owner: Claude Code, WS-AI, `feature/1.1.12/agent-docs`
Resolution:
