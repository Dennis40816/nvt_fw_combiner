# BUG-20260925-record-filename-taskid: change record filename differs from its taskId

Status: fixed
Severity: P1
Found: 2026-09-25, Claude Code (Opus 5.5), while surveying governance, at `feature/1.1.11/reliability-and-info`@`70ab0b7f3`
Where: `docs/governance/change-records/common-tp-event-buffer-1111-01.json` on that branch
Observed: the file stem is lower case; its `taskId` is `COMMON-TP-EVENT-BUFFER-1111-01`.
Expected: file stem equals `taskId`, per `scripts/validate_repository.py:2907-2910` (case-sensitive comparison).
Evidence: the validator appends `capability-reuse filename must equal taskId`, so `python scripts/verify.py --structure-only` fails on that branch and the required `policy / polytail` check blocks integration.
Owner: Codex (the 1.1.11 work owner), `feature/1.1.11/reliability-and-info`
Resolution: fixed in `v1.1.11` by `4fe9cb9bb`: the validator now compares the file stem and `taskId` ASCII case-insensitively (`scripts/validate_repository.py:2923`) and rejects case-fold collisions; the record kept its lower-case name. The rule leaves with the capability-reuse retirement (WS-GOV decision 1). Verified 2026-09-25 by Claude Code against the published tag.
