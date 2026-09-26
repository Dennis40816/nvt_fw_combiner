# BUG-20260926-public-tree-local-user-paths: tracked documents carry the developer's local user-profile paths

Status: open
Severity: P3
Found: 2026-09-26, the WS-TEST T1 draft review and a commander scan of the tracked tree at `9861d800c`
Where: current files `tests/README.md` (two owner-reference image paths), `.agents/skills/assess-refactor-progress/SKILL.md`,
`docs/adr/0021-code-size-ratchet-system-activity-amendment.md`, `docs/architecture/0.9.x-completion-roadmap.md` and
`docs/references/verification-report.md`; also the sealed records `LAUNCHER-106-UI-01` and `UI-114-MEMORY-CARDS-31` and the
frozen waiver `REL-110-FULL-VERIFY-OWNER-WAIVER-01`
Observed: these files name absolute paths under the developer's Windows user profile (the local account name, temporary
clipboard images and generated-image folders). The repository is public. Test sources that use user-profile paths use
placeholder names (`owner`, `operator`) and are not affected; test-area paths (`D:/NvtFwCombiner-TestArea/...`) name no user.
Expected: tracked text names no private user-profile path; an evidence reference uses a repository-relative path or a
test-area path. The repository's private-string check should reject a user-profile path with a non-placeholder account name
in new changes.
Evidence: `git grep -i "C:[\/]Users[\/]"` over the tracked tree.
Owner: 1.1.13 documentation hygiene, a small governed batch for the current files (ADR, skill and roadmap paths need a
record). The sealed records and the frozen waiver stay as they are (decisions 51 and 52) unless the owner decides
otherwise, and Git history is not rewritten.
Resolution:
