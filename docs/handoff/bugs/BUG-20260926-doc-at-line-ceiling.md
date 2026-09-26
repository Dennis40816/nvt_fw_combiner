# BUG-20260926-doc-at-line-ceiling: a UI handoff sits one line under the text-file ceiling

Status: open
Severity: P3 (next edit breaks CI)
Found: 2026-09-26, WS-TEST design on `feature/1.1.13/test-architecture`
Where: `docs/ui/v1.1.x-custom-options-layout-handoff.md`
Observed: the file has 2,499 lines; `RepositoryTextFilesStayBelowEmergencyCeiling` fails at 2,500, so an edit
that adds two lines fails the core shard.
Expected: the file is split or its dated history moved out (as the roadmap was) before its next edit.
Owner: 1.1.13 (documentation tidy; the same history-archive pattern as `ROADMAP-HISTORY-1113-01`).
Resolution: not fixed.
