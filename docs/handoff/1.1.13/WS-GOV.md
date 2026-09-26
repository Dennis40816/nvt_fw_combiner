# WS-GOV: development and release flow reset (1.1.13)

Owner: Claude Code (Opus 5.5), design drafter. Board:
[1.1.13 board](../1.1.13.md). Protocol: [handoff README](../README.md).
Deliverables: [governance ADR draft](ADR-DRAFT-governance-reset.md) and
[release workflow cleanup design](DESIGN-release-workflow-cleanup.md).

## Dispatch envelope (commander, 2026-09-26)

- **Outcome.** (1) A governance ADR draft, Status Proposed, number `00XX`:
  root fix of the capability-reuse history replay (the release re-run
  conflict), the development and release flow reset, the WS-GOVREV review
  input, and the owner decisions it needs. (2) An R3 design for the release
  workflow cleanup covering the seven inventory items of board decision 29.
  (3) The board rows for WS-GOV and the release workflow cleanup set to
  "design draft ready for review" with links. Non-goals: implementation; edits
  to `.github/workflows/`, `scripts/`, code or profiles; other worktrees.
- **Authority.** Documents only (R0 for this workstream); local commits; no
  push, pull request or GitHub write.
- **Branch and worktree.** `feature/1.1.13/ws-gov` in `<worktrees>/gov`, from
  `feature/1.1.13/wave2` at `6720f86f6`; fast-forwarded to wave 2 at
  `8682dd269` (after #457) at the commander's request before completion.
- **Write lock.** New files under `docs/handoff/1.1.13/` (this log, the ADR
  draft, the release design); the two board rows; new bug files.
- **Read first.** Root `AGENTS.md`, `docs/AGENTS.md`, the governance runbook,
  branch governance, the capability-reuse contract, the issue tracker
  configuration, the handoff README, the 1.1.12 and 1.1.13 boards, and the
  unmerged 1.1.12 drafts (`feature/1.1.12/governance-reset`,
  `feature/1.1.12/governance-design-review`); for the release design
  `release.yml`, `main-package.yml` and `release_promotion_policy.py`.
- **Acceptance.** Committed drafts; owner decisions listed with
  recommendations; risks and conflicts with other work named. Then stop: the
  commander arranges the independent design review. ADR numbers 0074 to 0077
  are taken; a capability-reuse record, if one were needed, binds checkpoint
  `84b084dd8` (these handoff documents are not governed paths, so none is).

## Ported from 1.1.12

| 1.1.12 source | 1.1.13 disposition |
| --- | --- |
| WS-GOV principles | ADR decision drivers, plus two new ones (no checkpoint serialization; cited evidence keeps resolving) |
| WS-GOV "what stays" | ADR context and the design's "What stays", unchanged in substance |
| WS-GOV "what is retired" | ADR decisions 1, 2, 5, 10, 12 and 13 and the owner table, corrected: freeze in place instead of archive (O-1); `python-worker / verify` stays required (O-4) |
| WS-GOV parallel-execution measurements (2026-09-25) | Cited with their date in the ADR context and design item 7 |
| WS-GOV "known defects fixed in the same batch" | Still open and scheduled elsewhere: `verify-all-help-text` and `version-branch-ci-gap` after the CI work (board schedule); `grilling-authority-conflict` and `skill-stale-persona-rules` with WS-AI; the release workflow's 0.9.x options move to design item 3 |
| WS-GOV and ADR-draft migration ("admitted under the current rules one last time") | Rewritten: the new validator cannot check that last record, so G1's admission is owner decision O-5 |
| ADR draft items 13-17 (agent instructions and skills) | ADR item 13, pending the WS-AI port |
| ADR draft items 11-12 (`release.yml`, predecessor parity) | Moved to the release design (items 2, 3 and 7) |
| "Rewrite the 29 links in 7 documents" | Dropped: undercounted (contracts and the Golden manifest cite records) and unnecessary under O-1 |
| WS-GOVREV findings | None exist (see below) |

## Accepted owner decisions carried from 1.1.12

Recorded 2026-09-25 in `feature/1.1.12/governance-reset:docs/handoff/1.1.12/WS-GOV.md`,
which is not on the trunk; restated here so they survive branch cleanup.

1. Retire capability-reuse records entirely; pull request fields, GitHub
   review and CODEOWNERS approval on R3 paths replace them; existing records are
   frozen; Golden, write-range and protected release gates stay. Rejected:
   records for R3 paths only; records with a digest cache.
2. Move the frozen history (records, attestations, trusted checkpoint,
   waiver) to `docs/governance/archive/` and rewrite the links. Rejected:
   freezing in place; deleting. (This draft asks to revisit it: O-1.)
3. Independent review prefers another runtime and falls back to a fresh
   session of the same runtime; the pull request states the mode; R3 needs
   owner approval; no process requires two agent products.
4. Tiered CI (re-answered the same day): no feature-push runs; path-mapped
   tests on pull requests into the trunk, skipping documentation-only changes;
   full suite plus Golden at release-branch cut, on the release pull request,
   weekly on the trunk when it changed, and on dispatch.
5. Block growth of aggregates of at least 2,000 nonblank lines without owner
   approval in the pull request; leave the list below 1,500; advisory report
   otherwise; one consolidated ADR 0021.
6. Rename `policy / polytail`; drop `python-worker / verify` from the required
   checks until WS-FLOW F11 decides the worker. (This draft amends the second
   half: O-4.)
7. 1.1.12 release workflow: remove the `0.9.17`-`0.9.19` source options;
   parallel Golden and packaging candidate jobs; keep the v0.9.16 parity jobs.
   Not implemented in 1.1.12. The parity part is superseded by board decision
   29 (inventory item 2). 7b: predecessor parity becomes a rolling baseline in
   1.1.13 after a one-time v0.9.16 alignment (done in 1.1.12 as a
   non-certifying local comparison). 7c: no further 0.9.x maintenance releases.
8. Delete merged and old unmerged remote branches; executed 2026-09-25 (204
   deleted); the owner's unmerged-branch script (board P0-6) remains open.
9. Branch-name allowlist ruleset (`main`, `*.*.*`, `feature/*.*.*/**`,
   `dependabot/**`, no bypass actor) and automatic deletion of merged branches;
   pending checklist A-5.
10. `1.1.x` trunk with release branches cut at freeze and `main` merged back
    after every release; in force since board decision 22.

## WS-GOVREV questions (self-check)

WS-GOVREV was dispatched on 2026-09-25 (`25330ce2e`) and committed no
findings; no other branch, stash or worktree holds them. The answers below
are the author's self-check against its six questions. They are **not** an
independent review, and the independent design review remains required.

1. **Fidelity.** WS-GOV decisions 4, 5, 9 and 10 are carried as decided (ADR
   items 9, 12 and 7, migration step 5); decisions 1 and 3 are carried (ADR
   items 1, 3, 5 and 6) except how R3 approval is recorded. Deviations, each an
   owner decision: decision 2 (O-1, freeze in place); the "approval through
   CODEOWNERS" of decisions 1 and 3 cannot work while the owner's account
   authors every pull request (O-2); decision 6's non-required
   `python-worker / verify` (O-4); board decision 2's single batch becomes G1
   and G2 (O-6). Decisions 7, 7b and 7c move to the release design.
2. **Byte-safety invariants.** Golden execution against the candidate
   (`--release-golden`) and in CI, the host-side write-range audit, schema,
   profile and contract checks in the structure lane, and `unknown` integrity
   never compiling as supported are code or workflow gates that G1 does not
   touch. Firmware-owner R3 approval keeps the root `AGENTS.md` rule; its
   recording mechanism is O-2. Three weakenings in the 1.1.12 draft are
   corrected: a non-required `python-worker / verify` would have made the
   release-policy, Golden-validation and validator tests non-blocking (O-4);
   retiring the history audit without a replacement would leave frozen
   evidence editable (tree pins, ADR item 2); moving the records would break
   the runtime-pinned capability policy or the Golden manifest's reference
   check (O-1).
3. **Orphans and conflicts.** At `8682dd269`, 38 files outside the frozen
   directories mention the record system. G1 updates the live ones: ADRs 0054,
   0059, 0061, 0070 and 0071 (superseded), the three ADR 0021 files
   (consolidated), root `AGENTS.md`, the runbook's gate section,
   `capability-reuse-record.md` (Historical), `docs/ci/pull-request-ci.md`
   (the required Polytail record and check names), `.github/AGENTS.md` and
   `.github/workflows/README.md` (check names, with G2), `CONTRIBUTING.md`
   (direct commits to version branches), the skills `implement`,
   `nfc-architecture-change`, `polytail` and `supervised-branch-development`
   (WS-AI), about 2,000 of the 3,903 function lines in
   `validate_repository.py` (by function name), 118 of the 179 tests in
   `tests/scripts/test_agent_governance.py` (by name) and the record references
   in `test_code_size_policy.py`, `test_skill_inventory_validation.py` and
   `test_v0916_parity_contracts.py`. The contracts and the Golden manifest keep
   their references (O-1); dated history documents stay as history. The
   superseded list is complete: only ADRs 0054, 0059, 0061, 0070 and 0071
   define record mechanics; ADR 0033 is amended through the release design and
   ADR 0068 by WS-AI.
4. **Migration order.** The CI failure-evidence change owns `ci.yml` and
   `verify.py` first. G1 deletes the record tests together with the validator
   code. G1 cannot be checked by the validator it removes (O-5); its transition
   evidence is one run of the base commit's validator. Records sealed before G1
   stay frozen, and unsealed ones are dropped into pull request fields
   (migration step 3). The check rename cannot run old and new names side by
   side, because the release policy requires the ruleset's required contexts to
   equal its closed set exactly; the rename, the policy set and the ruleset
   change together and no release runs in between (ADR item 10). The trunk and
   release-branch model fits the release workflow (dispatched from `main`,
   release pull request based on `main`, strict up-to-date rule); after G1,
   merging `main` into a release branch or the trunk is an ordinary merge.
5. **One runtime.** The review rule has a same-runtime fallback. One hidden
   dependency remains: the release policy requires an exact-head Codex review
   for a self-approved release again from version 1.2.0 (design item 4,
   RO-5). `github-review-polling` becomes explicit-only (WS-AI); the handoff
   README's `codex exec` launch is an example, not a requirement.
6. **Omissions and risks.** Shared GitHub identity (O-2); content-based R3
   outside the CODEOWNERS paths relies on the declared risk and review, as it
   effectively does today; path-mapped CI can miss cross-cutting interactions
   until the next full run; the review mode in a pull request is self-reported;
   rollback is effectively one-way once work lands under the new rules; the
   trunk and release branches have no force-push protection (ADR migration
   step 5 adds it to the settings list); ADR 0061's status line still reads
   Proposed although it is implemented and attested; the release promote
   condition skips every version outside 1.x and 2.0.0
   ([bug](../bugs/BUG-20260926-release-promote-skips-other-versions.md)).

## Owner decisions needed

ADR: O-1 frozen evidence location; O-2 R3 approval mechanism; O-3 pre-merge
review-thread gate; O-4 `python-worker / verify`; O-5 G1 admission; O-6
batches and timing; O-7 waivers. Release design: RO-1 v0.9.16 certification;
RO-2 maintenance releases; RO-3 `published_at`; RO-4 commit binding and
start; RO-5 self-approval exception and the 1.2.0 Codex requirement; RO-6
`main-package`; RO-7 parallel candidate; RO-8 source CI at pre-tag; RO-9
staging repository; RO-10 inventory additions. The risky ones for a one-at-a-time
interview: O-1, O-2, RO-1 and RO-5.

## Conflicts with other work

- CI failure evidence (R3): owns `ci.yml` and `scripts/verify.py`; G2, the
  composite action in `ci.yml` and design batch R-6 wait for it. Its
  `overwrite: true` must not reach release artifacts.
- Pre-built catalog ADR (0077): no number conflict (this draft uses `00XX`);
  `package.ps1` changes (release batch R-1 and a catalog snapshot step) need one
  writer.
- NVT marker (0076), TP SVN, F08 and the CI work are admitted under the record
  system now; each needs the migration step 3 transition if G1 lands first.
- WS-AI port: edits the same governed paths as G1 (`AGENTS.md`, skills,
  validator frontmatter rules); same batch, one writer.
- Formal 1.x comparator: owns the comparator; release batch R-5 owns only its
  release integration.

## Checkpoints

### 2026-09-26 Design drafts ready for review
State: local
Commits: `c2b9c1468` (ADR draft, release design, this log, the promote-condition
bug, two board rows)
Evidence: read-only inspection only; no product test is needed for these
documents. Verification of the committed head is recorded in the next entry.
Open: the owner decisions above (commander to schedule the interview); the
independent design review of both drafts (commander).
Next: structure check of the committed head, then stop.

### 2026-09-26 Structure check: pre-existing wave 2 failure
State: local (verification blocked by a pre-existing failure)
Commits: the commit carrying this entry (this entry, the new bug, two ADR
context bullets and one verification bullet)
Evidence: `python scripts/verify.py --structure-only` with TEMP, TMP and
TMPDIR set to `<test-area>/temp`, at `c2b9c1468` -> `structure=FAIL`, 268.4 s;
`sync_derived` changed 0 files; the only error is `final-complete
capability-reuse record changed in commit history:
docs/governance/change-records/CLI-REPORT-BUNDLE-GUARD-1113-01.json`, with no
Markdown link or other error. Cause, from Git: the record's blob is identical
at `84b084dd8` and `HEAD`; on `rev-list --ancestry-path 84b084dd8..HEAD`,
`diff-tree -m` lists it for `9b2a7369e` (tree equals parent 2, parent 1 is its
ancestor: exempt under ADR 0061) and for `beb32b930` (tree equals neither
parent: not exempt). `beb32b930` is wave 2's merge of `1.1.x`, so wave 2 at
`8682dd269` fails the same way; this branch adds only `docs/handoff/` files.
Recorded as `BUG-20260926-trunk-merge-flags-sealed-record` (P1). The targeted
link check of the five changed documents passed before the commit.
Open: **commander** — wave 2 must be rebuilt without that merge before it can
pass `policy / polytail` or merge into `1.1.x` (proposed workaround in the
bug); this branch then needs the same rebase. The follow-up commit changes
prose and adds links only to existing files; its links were checked the same
way, and the known blocker was not rerun.
Next: stop; independent design review (commander).
