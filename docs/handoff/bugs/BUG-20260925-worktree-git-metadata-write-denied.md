# BUG-20260925-worktree-git-metadata-write-denied: checkpoint cannot be committed

Status: closed, not a repository defect
Severity: P3 (dispatch configuration)
Found: 2026-09-25, Codex worker (model configuration not exposed), while recording WS-PARITY checkpoint, at `feature/1.1.12/v0916-alignment`@`10a95ffe28c25989319875266cec3bfd8855c603`
Where: `git add -- docs/handoff/1.1.12/WS-PARITY.md`; `.git/worktrees/v0916-alignment/index.lock`
Observed: Git reports `Unable to create .../index.lock: Permission denied`; both `git add` and `git commit` fail. The worktree metadata directory has an explicit write-deny access rule. The checkpoint and this bug record remain uncommitted.
Expected: The workstream owner can commit an owned checkpoint on its own branch, per `docs/handoff/README.md` and the WS-PARITY dispatch.
Evidence: `git add` and `git commit` on the owned log both failed before creating a commit; `git status --short` still showed only the modified log after the attempt. `Get-Acl` on the worktree metadata directory showed explicit `Deny Write` entries.
Owner: unassigned; worktree metadata permission must be repaired outside this lane's Write lock
Resolution: 2026-09-25, commander triage. The deny entries belong to Codex Windows sandbox
identities, not the user account (a write as the user succeeds). The `workspace-write`
sandbox keeps `.git` read-only by design, and `--add-dir` does not lift it. Rule from now
on: sandboxed Codex workers leave changes uncommitted, and the commander commits each
checkpoint on the worker's branch (board, Parallel-work rules).
