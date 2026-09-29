# BUG-20260926-public-tree-local-user-paths: tracked documents carry the developer's local user-profile paths

Status: fixed (current files merged into `1.1.x` by #459; the private-string check for new changes merged into `1.1.x` by integration A #476, merge `7e283c126`; released in v1.1.13, `1268f72d2`, 2026-09-28)
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
Resolution: the current files were fixed by `DOC-HYGIENE-1113-PRIVATE-PATHS-01`, merged into `1.1.x` by #459;
the remaining matches are the sealed records and the frozen waiver kept by decisions 51 and 52. Open: the
private-string check that rejects a non-placeholder user-profile path in new changes.

Closed (2026-09-29): the private-string check (TODO 56) merged into `1.1.x` by integration A `7e283c126` (#476), an ancestor of the v1.1.13 release merge `1268f72d2` (#479, published 2026-09-28).
