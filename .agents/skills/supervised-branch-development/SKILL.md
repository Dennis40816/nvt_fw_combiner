---
name: supervised-branch-development
description: Coordinate multi-agent development on version and feature branches with a single writer, a continuous read-only reviewer/supervisor, and a separate integrator. Use for branch reconstruction, milestone development, asynchronous GitHub/Codex review, agent handoffs, progress supervision, merge admission, or deciding whether an active goal is genuinely blocked.
---

# Supervised Branch Development

Keep Git as the source of truth. Treat chat and delegation messages as handoffs, not authority over the current tree.

## Assign Roles

Use at most one owner for each mutable surface.

- **Implementer**: owns edits, tests, phase commits, and pushes on one non-`main` branch.
- **Supervisor/reviewer**: stays read-only, continuously reviews pushed or explicitly staged phases, runs independent checks, and returns findings and admission decisions. Do not silently become the implementer.
- **Integrator**: consumes only approved clean SHAs, resolves integration-only conflicts, runs final gates, and manages PR/release flow.
- **Owner/human**: retains the R3 firmware, golden, processor authority, security, and release decisions required by `AGENTS.md`.

Do not let multiple agents edit the same branch or shared files concurrently. Freeze ownership before parallel work.

## Establish Branch Authority

At the start of supervision, record:

```text
version integration branch and authoritative base SHA
feature branch, HEAD, upstream, and worktree state
implementer, supervisor, and integrator ownership
risk class and affected layers
milestone checklist with fixed denominators
required local, GitHub, and human gates
recovery ref or verified bundle before history reconstruction
```

Create independently reviewable work on `feature/<version>/<topic>` from the exact version branch. Merge it back to that version branch, never directly to `main`. Preserve an externally verifiable recovery point before rebase, replay, or branch replacement.

## Run the Supervision Loop

Repeat this loop after every pushed phase, explicit staged-review request, test failure, or changed external review state. Do not wait for another user prompt while an active goal still has safe work.

1. Read the latest handoff, then independently verify branch, HEAD, ancestry, status, and diff ownership.
2. Review only the declared phase plus the minimum predecessor contracts needed to validate it.
3. Check implementation, tests, architecture boundaries, Polytail concerns, and residual human gates.
4. Return `APPROVE`, `PASS-WITH-HUMAN-GATE`, or `CHANGES-REQUIRED` with P0-P3 findings and exact evidence.
5. Relay actionable findings to the implementer through the available delegation channel. If no direct channel exists, publish a precise handoff without editing the implementer's branch.
6. Admit the next independent phase after local approval. Keep final merge/release gates pending until their required external reviews arrive.
7. Re-read the branch before every verdict; never approve a stale SHA.

The supervisor may use a clean detached worktree only when isolation is
necessary. Default to the existing worktree and keep one local worktree when
the tasks are sequential. Record every worktree created, remove temporary
worktrees immediately after their evidence is captured, and prune stale
registrations. It may remove only temporary review state that it created. It
must not modify, stage, commit, rebase, merge, or push the implementation
branch.

## Treat Asynchronous Review as Non-Blocking

Request GitHub `@codex review` as required by `AGENTS.md`, but separate development progress from merge authority.

- A pending GitHub review is a **pending merge gate**, not a blocked development goal.
- Continue local review, independent phases, evidence audits, documentation reconciliation, package preparation, and failure diagnosis that do not depend on that response.
- A distinct local supervisor review may admit the next phase; it does not waive the required GitHub review before merge.
- Do not repeatedly comment `@codex review` without a new reviewed SHA or an addressed actionable thread.
- When GitHub responds, inspect thread-aware findings, route fixes to the implementer, and request re-review only after the fix is pushed and locally reviewed.
- Never merge or tag merely because the remote reviewer is slow or unavailable.

When polling is requested, use `$github-review-polling`. Lock the request comment
and exact head SHA, then read direct REST reviews, inline comments, issue
comments, and reactions. Do not infer completion from `gh pr view` aggregation,
an `eyes` reaction, an earlier-head response, or a bot login mismatch caused by
the `[bot]` suffix.

CI in progress is also pending, not blocked. Diagnose a failed check once, apply the repository retry policy, and continue unrelated safe work.

## Block Only at a Real Impasse

Use blocked status only when all remaining paths require the same unavailable input or authority and no meaningful independent work remains. Examples include:

- an R3 firmware-owner decision required to choose ranges, operation order, CRC behavior, or golden bytes;
- missing private evidence or an external tool package needed by every remaining task;
- protected-branch, permission, or release authority that only the owner can exercise;
- an unresolved P0/P1 whose safe resolution requires an external decision;
- the same blocker has been confirmed for at least three consecutive supervision cycles, including after a resumed goal.

Do not block because:

- GitHub Codex review or CI has not replied yet;
- one agent is idle while another independent phase exists;
- a final human gate is known but not yet on the critical path;
- one test failed and focused diagnosis remains possible;
- the work is large, slow, uncertain, or needs another local review cycle.

If no active goal exists, do not create one unless the user explicitly requests it. If a goal exists, leave it active while non-blocking work remains.

## Admit Commits and Integration

Allow an implementer phase commit when the scope is coherent, owned files are explicit, the narrow test and `git diff --check` pass, and residual gates are recorded. Do not require `verify.py --all` for every intermediate phase.

Allow integration only when:

1. the exact SHA is pushed and the worktree is clean;
2. local supervisor findings have no unresolved P0/P1;
3. phase-local tests and required architecture/contract/golden gates pass;
4. human R3 gates are explicit and satisfied before the affected merge boundary;
5. required GitHub review and CI are green at the merge boundary;
6. the integrator confirms target ancestry and reviewed-tree equivalence.

Run `python scripts/verify.py --all` once on the final R1-R3 candidate. Keep package, protected-`main`, tag, provenance, and release checks separate from feature completion.

## Close A Release And Clean Branches

After a stable release, create the next exact-version integration branch from the
peeled stable tag/current `main`, never from the newest-looking feature name or
timestamp. Record the predecessor SHA before opening new work.

Inventory every open PR and local/remote branch before cleanup. Classify each as
`keep`, `superseded`, `archive`, or `delete-candidate` using ancestry, patch/tree
equivalence, open-review intent, and replacement evidence. A merged or old base
name alone is not proof that unique work is disposable.

- Close a PR only when it is merged, explicitly abandoned, or demonstrably
  replaced. Leave a closing comment naming the replacement PR/commit/tag and any
  intentionally deferred residue.
- Keep ambiguous or independently valuable work open and route it to the owner.
- Preserve a recovery ref/bundle before history reconstruction.
- Do not delete remote branches until the owner approves an exact deletion list.
- Never batch-close PRs or delete refs based only on age, naming, or
  `git branch --merged`.

Use `docs/governance/branch-version-and-release-governance.md` for version
selection, admission, release-note, and post-release cleanup rules.

## Require a Complete Handoff

Every agent handoff must state:

```text
role and owned scope
branch / HEAD / upstream / clean or dirty state
committed, staged, and untracked files
completed checklist items and fixed-denominator percentage
commands and exact results
P0-P3 findings and review verdict
active step and next reviewable SHA
pending local, GitHub, and human gates
whether owner input is needed now or only at merge/release
```

Report progress from completed checklist items only. Do not derive percentages from elapsed time, commit count, or line count.
