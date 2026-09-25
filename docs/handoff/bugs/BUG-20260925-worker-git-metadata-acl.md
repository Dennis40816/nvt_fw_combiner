# BUG-20260925-worker-git-metadata-acl: linked-worktree Git metadata denies worker checkpoint commits

Status: open
Severity: P2
Found: 2026-09-25, Codex worker (GPT-6), while committing the first WS-IO checkpoint, at `feature/1.1.12/io-persistence`@`5882d7a57`
Where: linked-worktree Git metadata for `<worktrees>/io-persistence`
Observed: ordinary `git add` cannot create `index.lock`; `git commit` cannot open `COMMIT_EDITMSG`; linked-worktree `git update-ref` cannot create `HEAD.lock`. An alternate index and direct update of this branch ref produced the checkpoint, but the normal linked-worktree index remains stale.
Expected: a dispatched worker can make authorized local checkpoint commits on its own branch, per `docs/handoff/README.md` and `docs/handoff/1.1.12/WS-IO.md`.
Evidence: each ordinary Git write returned `Permission denied`; `git --git-dir=<common-git-dir> update-ref refs/heads/feature/1.1.12/io-persistence` succeeded.
Owner: unassigned; commander to repair the worktree environment.
Resolution:
