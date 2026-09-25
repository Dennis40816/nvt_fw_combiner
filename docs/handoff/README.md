# Agent handoff

Status: Active coordination protocol for concurrent agents (Codex and Claude
Code), introduced 2026-09-25 for 1.1.12.

This directory is where agents share live development state. It owns who is
doing what, on which branch, and how far it got. It does not own product
behavior (`SPEC.md`, ADRs, contracts), version allocation
([roadmap](../architecture/nfc_roadmap.md)) or completed history
(`CHANGELOG.md`); link to those owners instead of copying them.

## Files

- `<version>.md` is the version board. Its single writer is the commander named
  in it. It holds base identity, roles, workstreams, open owner decisions and
  cross-branch blockers.
- `<version>/<WS-ID>.md` is one workstream log. Its single writer is that
  workstream's owner, on that workstream's branch. It starts with the dispatch
  envelope and continues with checkpoint entries.
- `bugs/BUG-<yyyymmdd>-<slug>.md` is one found bug, across versions. See
  [Bug ledger](#bug-ledger).

## Reading another agent's state

All worktrees share one Git object store, so any agent can read the latest
committed state of another branch without checking it out:

```text
git show <branch>:docs/handoff/<version>/<WS-ID>.md
git log --oneline <version>..<branch>
```

Uncommitted work is invisible to other agents. Commit a checkpoint, with the
log entry in the same commit, before reporting progress.

## Dispatch envelope

The commander writes this at the top of a new workstream log:

- **Outcome** and non-goals.
- **Authority**: allowed actions (edit, local commit, push, PR, GitHub write)
  and the risk class.
- **Branch and worktree**, with the base commit.
- **Write lock**: the exact paths the worker may modify. Everything else is
  read-only for that worker.
- **Read first**: the owner documents and prior evidence.
- **Acceptance**: the observable result, narrow checks and evidence to record.
- **Stop and ask**: conditions that end the task instead of widening scope.

## Checkpoint entry

The worker appends entries, newest last:

```text
### <date> <short title>
State: planned | local | verified | integrated | published
Commits: <sha> ...
Evidence: <command> -> <result> (at <sha>)
Open: <blocker or question, and who must answer>
Next: <one step>
```

State words follow the root `AGENTS.md` reporting rule. `verified` names the
exact commands, results and commit.

## Rules

- One writer per file and per write-locked path. A worker that needs a path
  outside its lock records it under `Open` and stops that part.
- The committed board and logs outrank chat messages.
- Only the commander integrates into the trunk (for example `1.1.x`) or a
  release branch.
- Push, PR, GitHub writes, release, R3 approval and deletions outside the lock
  stay with the owner unless an envelope names them.
- This repository is public. Handoff files carry no machine paths (write
  `<worktrees>/<name>`), user names, private tool or transfer workflows,
  credentials or firmware bytes. Before a handoff branch is first pushed,
  the commander scans its full history and squashes away any such string;
  deleting it in a later commit is not enough.

## Bug ledger

A bug mentioned only in chat is lost at the next handoff. Every agent, Codex
and Claude Code alike, records each bug it finds in `bugs/` before its final
report of that session, whether or not the bug is in its task scope.

A bug is observed behavior, tooling or instruction text that contradicts an
accepted contract, test, specification or document, or a gate that fails for
the wrong reason. Design concerns and proposals belong in a workstream log.
An unconfirmed suspicion is still recorded, as `suspected`.

One bug per file, named by its ID, `BUG-<yyyymmdd>-<slug>`, so parallel agents
never collide. Template:

```text
# BUG-<yyyymmdd>-<slug>: <one-line title>

Status: suspected | open | fixing | fixed | wontfix | duplicate
Severity: P0 | P1 | P2 | P3
Found: <date>, <agent and model>, while <activity>, at <branch>@<sha>
Where: <path:line or command>
Observed: <what happens>
Expected: <what should happen>, per <contract, test or document path>
Evidence: <command and result, or code reference>
Owner: unassigned | <agent or person>, <branch>
Resolution: <fixed in sha with test evidence | reason | duplicate of ID>
```

Rules:

- Recording a bug does not authorize fixing it. Fix it only inside an
  authorized scope; otherwise leave it `open` for the commander or owner.
- Before starting work, read the open bugs that touch your paths. Bugs added on
  any local branch are listed by
  `git log --all --format= --name-only --diff-filter=A -- docs/handoff/bugs/`;
  read one with `git show <branch>:<path>`.
- Whoever takes a bug sets `Owner` and `fixing`; whoever closes it fills
  `Resolution`. Never delete a bug file.
- Workstream checkpoints and the version board cite bug IDs instead of
  restating them. The board lists open P0/P1 bugs that block its release.
- This repository is public: never put firmware bytes, customer data,
  credentials or private paths in a bug file; cite fixture IDs and hashes.

## Launching a worker

Either path uses the same committed envelope:

- The owner tells an open agent session: "Read
  `docs/handoff/<version>/<WS-ID>.md` on branch `<branch>` and execute it."
- The commander starts a headless worker in the workstream's worktree, for
  example `codex exec -C <worktree> "<same instruction>"`.
