# WS-GOV: development and release flow reset (1.1.13)

Owner: Claude Code (Opus 5.5), design drafter. Board:
[1.1.13 board](../1.1.13.md). Protocol: [handoff README](../README.md).
Deliverables: [governance ADR draft](ADR-DRAFT-governance-reset.md) and
[release workflow cleanup design](DESIGN-release-workflow-cleanup.md), both
revised after the [independent design review](#design-review-2026-09-26), and
the [G0 owner checklist](G0-owner-checklist.md), revised after its own
[review](#g0-checklist-review-2026-09-26). The owner decided every question
on 2026-09-26 (board decisions 47, 49 to 56 and 65 to 67); the questions and
answers are at the end ([owner decisions](#owner-decisions-in-risk-order)),
followed by the [open questions](#open-questions-after-the-g0-review) the G0
review left.

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
- **Revision (commander, 2026-09-26).** The branch was rebuilt by cherry-pick
  onto the rebuilt wave 2 with identical trees (head `e60ba0062`); never merge
  the trunk into it again. Take in every technical correction of the design
  review (F-1 to F-9), list the owner decisions in risk order, in plain words
  with a recommendation and consequences, at the end of this log, then stop.
- **Owner decisions (commander, 2026-09-26).** The owner took every
  recommendation (board decisions 47 and 49 to 55; O-3, O-4, RO-2, RO-3, RO-4
  and RO-6 as recommended). Record them in the ADR draft, the release design
  and this log with date and decision number, marking the options not taken as
  rejected. Write `G0-owner-checklist.md`: a GitHub App as the primary agent
  identity (machine account as fallback), a pre-filled manifest with minimal
  permissions, the manifest flow, installation on this repository only, the
  owner keeping the private key, short-lived installation tokens through an
  owner-configured helper without agents reading the key, and every ruleset
  change with its parameters for item-by-item approval. Agents never create
  accounts or handle the owner's credentials. Then stop for the re-review.
- **G0 review revision (commander, 2026-09-26).** The independent review of
  `7700a7150..6eb575c30` rejected the G0 checklist (F-1 to F-10). The owner
  answered its questions as board decisions 65 (key custody is a rule, not a
  technical boundary), 66 (trunk catch-up by a same-tree pull request merge
  after G0; an owner-only force-push means) and 67 (the six unasked items
  confirmed). Revise only the WS-GOV documents on this branch: F-1 to F-10
  and a concrete owner-only bypass design, with alternatives if GitHub cannot
  do it; run the structure check; no push, no trunk merge; then stop.

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
   freezing in place; deleting. (Reversed on 2026-09-26 by board decision 51:
   frozen in place.)
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
   checks until WS-FLOW F11 decides the worker. (Second half amended on
   2026-09-26, O-4, confirmed in board decision 67: the repository-script
   aggregate stays required.)
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
independent review; they were written against the first draft (`a709af1be`,
whose item numbers they use), and the independent design review below
supersedes them where the two differ.

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

## Design review 2026-09-26

Reviewer `codex/gpt-6-astra`, implementation owner `claude-code`, fixed head
`e60ba0062`; read-only (no build, test or workflow run). Verdict for both
documents: **ACCEPT-WITH-CHANGES**; the scoped Polytail design verdict was
FAIL because P1 design gaps were open, not because a check failed. The review
also noted that removing history replay removes this class of merge and re-run
conflicts, not every release failure or ordinary merge conflict, and that the
268 s structure failure recorded below belongs to the pre-rebuild topology.

| Finding | Severity | Taken in |
| --- | --- | --- |
| F-1 R3 approval not an executable contract | P1 | ADR items 6 to 9 (review and approval records bound to the full head SHA, role and evidence, dismissed on a new head; boundaries for trunk, release branches and `main`; both roles stated explicitly by one person holding both; no gate removed before its replacement); migration G0, G1-A, G1-B |
| F-2 no reliable escalation for firmware semantics in `src/` | P1 | ADR items 3 and 4 (history-independent conservative authority map; unclassified and cross-class changes need a written classification; reviewer confirms byte, range, order, integrity and support impact; pull request fields are evidence carriers only) |
| F-3 transition misses sealed-but-unmerged records | P1 | ADR migration steps 2 to 4 (state table; explicit one-time cutover authorization binding base and head; re-pin procedure) |
| F-4 documentation skip and aggregator too loose | P1 | ADR item 13 (only the `prose` class skips product tests; the structure lane, pins, contracts, authority and document checks always run; unclassifiable changes run everything; aggregators fail on missing, failed, cancelled or unexpectedly skipped producers; negative tests) |
| F-5 rolling parity cannot replace terminal certification | P1 | Design item 2 (the comparison is an additional gate; RO-1 rewritten as keep, retire with a claims and debt record approved by both owners, or defer; the 27-route debt listed; the promote gate requires the terminal chain for every version from 2.0.0 until RO-1; environment and secrets removed only after a verified replacement; no unchanged move to a manual workflow) |
| F-6 RO-5 could turn verifiable review into self-report | P1 | Design item 4 (product-neutral exact-head reviewer evidence from an allowlisted principal other than the author; `CHANGES_REQUESTED` stays fail-closed; owner consent recorded separately, never derived; runtime identifier kept apart from the GitHub principal) |
| F-7 recovery not split by tag and Release state | P1 | ADR item 11 and design item 5 (three paths; nothing merges into `main` while a tagged release is incomplete; floor checked again at pre-tag) |
| F-8 staging too late and happy-path only | P2 | Design verification and batch R-1 (staging from R-1 with rejection, recovery, conflict, parity-failure and dry-run-refusal cases; dry-run artifacts non-promotable) |
| F-9 waiver limits not carried over | P2 | ADR item 15 (all current fields, head and scope binding, eligible-owner approval, the six non-waivable areas unchanged) |

Review notes taken in as well: the frozen-evidence guarantee is stated as
snapshot equality, and a pin change needs owner approval (ADR item 2); G1-B's
replacement approvals must not wait for G2 (migration step 1); RO-3 labels
`mergedAt` as merge-derived; RO-6 states that the rehearsal proves no
promotion authority; RO-7 and RO-8 carry the reviewer's conditions; RO-10 is
approved item by item; G2 and R-6 keep the failing-project evidence and the
attempt-to-artifact correspondence; ADR 0077 must be pinned before
integration.

## G0 checklist review 2026-09-26

Reviewer `codex/gpt-6-astra`, range `7700a7150..6eb575c30`; read-only.
Verdict: **REJECT**, the G0 checklist could not be executed as written; no P0;
the other decisions were found faithfully recorded. The owner then answered
the review's questions as board decisions 65 to 67.

| Finding | Severity | Taken in |
| --- | --- | --- |
| F-1 the helper did not isolate the key; the machine account gave agents a long-lived token | P1 | G0 checklist "who does what" and A4, A6, A8 state decision 65's rule and that it is not a technical boundary, covering the key, the password manager, DPAPI, credential stores, the helper and the owner's browser session; part B states that the machine account leaves agents a long-lived token, rests on the rule alone, and lists five added risks; ADR risks bullet |
| F-2 the manifest flow lacked the conversion | P1 | Checklist A2: the owner completes `POST /app-manifests/{code}/conversions` in the owner's own terminal within the hour; the response never enters an agent transcript, Git or a general log; key generation in the settings is for rotation, not a substitute |
| F-3 `workflows: write` granted early | P2 | Removed from the manifest; the PAT fallback adds `workflow` only before R-1 |
| F-4 six unnumbered items shown as decided | P2 | Labeled as board decision 67 in the ADR, the release design and this log |
| F-5 RS-2 against decision 23 | P1 | Decision 66 synced in ADR items 10 and migration step 2, the ADR decision table, design item 5, this log and the checklist's "After G0": test merge with equal trees and the history gate in force, else stop and ask |
| F-6 verification could damage the trunk; negatives only | P1 | Checklist part D: destructive tests only on disposable `9.9.x` and `9.9.9`; formal refs by effective-rule snapshot; rule-specific messages; a positive merge after approval; app identity proved by real writes and `/installation/repositories` |
| F-7 over-promise on new SHAs | P2 | Checklist opening and ADR item 7: the exact-head rule belongs to the G1-A authority check and R-3; same-tree new-SHA case added to part D, the ADR tests and the R-3 cases |
| F-8 rollback left Git settings broken | P2 | Checklist: non-secret Git settings and the full `main` ruleset saved first; rollback restores helper, identity and rulesets; the owner signs in again personally |
| F-9 ruleset details | P2 | Checklist part C: `target` and `enforcement` for every ruleset, UI inputs and API values in separate columns, RS-1j kept complete as saved, "Do not require status checks on creation" on for RS-2 and RS-3 so a release branch can be cut, and a check for linear-history and other rules |
| F-10 R-1 and R-3 split | P2 | Release design batches: R-1 has additions 1, 2 and 4 of decision 54, R-3 has addition 3; R-3 rejection and acceptance cases added |

Force-push means (decision 66): checklist part C makes the Repository admin
role, which on this personal repository is only the owner, the bypass actor
of the trunk and release-branch rulesets, never the app or the machine
account, with how the owner uses and records it. The questions it leaves are
at the end of this log.

## Conflicts with other work

- CI failure evidence (R3): owns `ci.yml` and `scripts/verify.py`; G2, the
  composite action in `ci.yml` and design batch R-6 wait for it, and keep its
  failing-project evidence (board decision 41) and attempt-to-artifact
  correspondence. Its `overwrite: true` must not reach release artifacts.
- Pre-built catalog ADR (0077): no number conflict (this draft uses `00XX`).
  Its text is not on this branch; before integration, pin its version and
  package diff and check catalog generation time, allowlist, provenance,
  manifest and smoke together with release batch R-1. `package.ps1` needs one
  writer.
- NVT marker (0076), TP SVN, F08 and the CI work are admitted under the record
  system now; each follows the ADR's transition table if G1-B lands first.
- WS-AI port: edits the same governed paths as G1-B (`AGENTS.md`, skills,
  validator frontmatter rules); same batch, one writer.
- Formal 1.x comparator: owns the comparator; release batch R-5 owns only its
  release integration.
- `BUG-20260926-trunk-merge-flags-sealed-record` exists on this branch and on
  wave 2 with different status lines; this revision makes this branch's copy
  identical to wave 2's so that later integration does not conflict.

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

### 2026-09-26 Revision after the independent design review
State: local
Commits: the commit carrying this entry, on `e60ba0062`. The commander rebuilt
this branch with identical trees: `c2b9c1468` became `a709af1be` and
`9498af2e7` became `e60ba0062`; the structure failure above belongs to the
pre-rebuild topology.
Evidence: every technical correction of findings F-1 to F-9 is in the ADR draft
and the release design (table above); the owner decisions are listed below in
risk order. The bug copy now equals wave 2's blob `c6f1ad748`. Verification of
this commit is recorded in the next entry.
Open: the owner interview (commander); a re-review of the revised drafts if the
commander wants one before the interview.
Next: structure check of this commit, then stop.

### 2026-09-26 Structure check of the revised head
State: verified (documents only)
Commits: `031d0c6e8` verified; the commit carrying this entry changes only
this entry.
Evidence: `python scripts/verify.py --structure-only` with TEMP, TMP and
TMPDIR set to `<test-area>/temp`, at `031d0c6e8` -> `structure=PASS`, 253.5 s
(254 s wall clock); `sync_derived` changed 0 files. A link and anchor check of
the four changed documents passed before the commit. The rebuilt topology no
longer triggers the trunk-merge failure.
Open: the owner interview and any re-review (commander).
Next: stop.

### 2026-09-26 Owner decisions recorded; G0 checklist
State: local
Commits: the commit carrying this entry, on `7700a7150`
Evidence: the ADR draft, the release design and the decision list below now
state each owner decision with its date and board decision number (47 and 49
to 55; O-3, O-4, RO-2, RO-3, RO-4 and RO-6 as recommended) and mark the options
not taken as rejected. New `G0-owner-checklist.md`: GitHub App first (a
pre-filled manifest with minimal permissions, the manifest flow, installation
on this repository only, the owner keeping the private key, one-hour tokens
through an owner-configured helper), the machine-account fallback, ruleset
changes RS-1a to RS-4 with parameters for item-by-item approval, and a
verification pull request. The release design adds one point found while
writing it: with an agent identity, the release policy must accept only
approvals that include the release owner's principal. The two board rows now
equal wave 2's text, so later integration does not conflict on them.
Verification of this commit is recorded in the next entry.
Open: the independent re-review (commander); G0 itself is the owner's action.
Next: structure check of this commit, then stop.

### 2026-09-26 Structure check of the decisions commit
State: verified (documents only)
Commits: `28bc715ba` verified; the commit carrying this entry changes only
this entry.
Evidence: `python scripts/verify.py --structure-only` with TEMP, TMP and
TMPDIR set to `<test-area>/temp`, at `28bc715ba` -> `structure=PASS`, 267.7 s;
`sync_derived` changed 0 files. A link and anchor check of the five changed
documents passed before the commit.
Open: the independent re-review (commander).
Next: stop.

### 2026-09-26 Revision after the G0 review
State: local
Commits: the commit carrying this entry, on `6eb575c30`
Evidence: findings F-1 to F-10 of the G0 review are taken in (table above);
board decisions 56 and 65 to 67 are recorded in the ADR draft, the release
design, the checklist and this log; the checklist is rewritten (decision 65
rule, manifest conversion, no `workflows`, owner-only bypass, disposable-branch
verification, full rollback, UI and API ruleset values). Verification of this
commit is recorded in the next entry.
Open: the two questions below (commander to ask the owner); the re-review
(commander).
Next: structure check of this commit, then stop.

## Owner decisions in risk order

One question at a time, highest risk first. Each item gives the question in
plain words, the options with their consequences, and a recommendation. The
review agreed with each recommendation, with the conditions stated.

**Decided 2026-09-26.** The owner took the recommendation on all nine
questions (board decisions 47 and 49 to 55, recorded in the
[1.1.12 board](../1.1.12.md)); each item below states its decision, and the
options not taken are marked rejected. The six items not asked (O-3, O-4,
RO-2, RO-3, RO-4, RO-6) were relayed by the commander as recommended on
2026-09-26 and confirmed by the owner as board decision 67. Board decisions 65
and 66 answer the questions of the G0 review (below the decisions).

### 1. RO-1: Is the v0.9.16 terminal certification still owed before 2.0.0?

ADR 0057 promises that, before 2.0.0, all 64 selected Standard Merge, AB
Merge and CtrlRAM Replace routes are compared byte for byte with the old
v0.9.16 release, and that your firmware-owner approval of the result is
independently verified. That promise is why three unused jobs sit in the
release workflow. 27 of the 64 routes still have no
test input. The planned rolling comparison (each release against the previous
one) catches accidental changes but does not prove the old-baseline promise.

- **A. Keep it.** Before 2.0.0 the certification is rebuilt as its own
  workflow bound to the 2.0.0 candidate, and the 27 routes get inputs or your
  explicit disposition. Strongest evidence; real work before 2.0.0.
- **B. Retire it.** You sign one record, as firmware owner and as release
  owner, that says which support claims stay, which are withdrawn, what happens
  to each of the 27 routes, and what you approve for each release instead. The
  old jobs, environment and secrets go after the replacement has worked on a
  real release. Less work; a weaker promise, written down.
- **C. Decide later.** Nothing changes now. Until you decide, no 2.x release
  can be promoted without the terminal chain (the R-1 gate makes that explicit
  instead of silently skipping).

Recommendation: C now, and choose A or B when 2.0.0 is planned. In every case
the rolling comparison is added as an extra check, never as a replacement.

**Decided 2026-09-26 (board decision 47): C.** A and B are not rejected; the
choice between them returns when 2.0.0 is planned. Until then no 2.x release
may be promoted without the terminal chain, and 1.x is unaffected.

### 2. O-2 and RO-5: Should agents use their own GitHub account?

Today agents push and open pull requests as you. GitHub therefore cannot tell
your approval from an agent's, and you cannot approve a pull request that your
account opened. That is why every release uses the "owner self-approval
exception", and why the 1.2.0 release would need the Codex review bot again.

- **A. Separate agent account** (a machine user or an app). You approve agent
  pull requests as a normal reviewer; GitHub binds the approval to the exact
  commit and drops it when new commits arrive; the self-approval exception and
  the Codex-only rule go away. Cost: a second account, its token on the
  machine, and Git and `gh` settings per worktree; agents must never use your
  token.
- **B. Keep one shared account** (rejected). R3 approvals use an owner-only channel (a
  protected-environment approval or an exact-commit approval comment that a
  check reads), and the exception stays an explicit choice at each release.
  GitHub cannot prove the approver was you; the documents say so. No setup
  cost.

Recommendation: A. Part two, whatever you choose: for a release you approve
yourself, the required independent review becomes "a finished review of the
exact commit by an approved reviewer account other than the author", from
any product, instead of "the Codex bot". A requested change still blocks. The
current Codex rule stays until the replacement is built and tested.

**Decided 2026-09-26 (board decision 49): A.** The owner creates the agent
identity and its key or token; agents never create accounts and never handle
the owner's credentials. The self-approval exception and the Codex-only rule
retire only after the replacement review evidence is built and tested. A
GitHub App is the primary form and a machine account the fallback (board
decision 56; [G0 owner checklist](G0-owner-checklist.md)). Key custody is a
rule, not a technical boundary (board decision 65).

### 3. O-5 and O-6: How does the switch-over happen?

The old record check cannot check the batch that removes it, so the switch
needs your one-time explicit permission. The review accepts that only if the
new protections are proven first.

- **A. Four steps.** G0: you change the GitHub settings (rulesets and, if
  chosen, the agent account). G1-A: the new checks run alongside the old
  ones and pass on a real pull request. G1-B: the switch, on your one-time
  written authorization naming the exact base and head commits. G2: the CI
  changes, after the CI evidence work. Old protection stays until the new one
  works.
- **B. Same, plus one extra old-style step** (rejected) that seals the ADR
  alone before G1-A. Same end state, one more round of the old ceremony.

Recommendation: A. Timing: G1-B before the 1.1.13 release branch is cut if
the reviews finish in time; otherwise 1.1.13 releases under the current trunk
rule (decision 23; after G0, decision 66's same-tree pull request merge) and
G1-B follows. Until G1-B, branches never merge the trunk (rebase only).

**Decided 2026-09-26 (board decision 50): A**, with the timing above; old
protection stays until the new one works.

### 4. O-1: Keep the old records where they are?

On 2026-09-25 you chose to move the 342 records and 108 owner attestations
to an archive folder. Product files point at seven of them: the capability policy,
whose hash is fixed in the application code, the Golden manifest, and the
v0.9.16 plan.

- **A. Freeze in place.** A README marks them historical, and a pin fails any
  change. The guarantee becomes "today's content equals the frozen snapshot",
  no longer "never changed in history". Changing a pin needs your approval.
- **B. Move them** (rejected). The capability policy and the Golden manifest
  must change (new hash in the application: a product and Golden change), or
  seven files stay behind and the history is split. Benefit: a tidier folder.

Recommendation: A. It reverses your 2026-09-25 choice.

**Decided 2026-09-26 (board decision 51): A**, reversing the 2026-09-25
choice; a pin change needs owner approval.

### 5. O-7: Old and new waivers

- **A. Freeze the old waivers with the records.** A new waiver is a statement
  in the pull request with every field required today (rule, scope and commit,
  reason, risk, owner, issue, approver, dates, removal condition), approved by
  the owner of the rule being waived. The six never-waivable areas stay
  never-waivable: firmware range safety, processor write ranges, integrity
  order, secrets and signing, release allowlists, independent Golden
  expectations.
- **B. Keep a waiver folder** (rejected) for new waivers as files: more
  ceremony, same limits.

Recommendation: A.

**Decided 2026-09-26 (board decision 52): A.**

### 6. RO-9: Test the first release change in a throwaway repository?

R-1 changes who may publish and when. Once a version is tagged it cannot be
retried under the same number.

- **A. Yes, before R-1 merges.** A scratch repository with the same rules
  tries a good release, rejected versions (existing, equal, lower), recovery
  after a tag, a Release conflict, parity failures and a refused dry run.
  Cost: a few hours; deleting the scratch repository afterwards is your action.
- **B. No** (rejected). Tests plus the first real release; a mistake may burn
  a version number.

Recommendation: A.

**Decided 2026-09-26 (board decision 53): A**; deleting the throwaway
repository afterwards is the owner's action.

### 7. RO-10: Add four items to the cleanup list?

Each is approved separately; a declined item stays as it is.

1. Remove the v1.1.0 manual-only packaging mode and every rule for versions
   older than the current one (replaced by the release floor).
2. Replace the promote condition that silently skips every version outside
   1.x and 2.0.0.
3. Replace the Codex-only review rule that returns at 1.2.0 (see question 2).
4. Add a dry-run mode to the release workflow (ADR 0033 promises one).

Recommendation: all four; each is small and removes a trap.

**Decided 2026-09-26 (board decision 54): all four added.**

### 8. RO-7: Run Golden and packaging in parallel in the release candidate?

Up to about 4 minutes faster, less the setup of a second job (estimate from
the 1.1.12 steps: 232 s and 332 s). Condition: both use the same commit, and
the releasable candidate exists only after Golden passes.

- **A. Yes**, in the optional speed batch after the CI evidence work.
- **B. No** (rejected), keep them in sequence.

Recommendation: A, low priority.

**Decided 2026-09-26 (board decision 55): A**, low priority, in the optional
speed batch after the CI evidence work.

### 9. RO-8: Start the release candidate before post-merge CI finishes?

Today you wait about 9 minutes after the release merge before dispatching.
The tag would still require a fresh, successful CI run on the same commit.

- **A. Yes**, in the optional speed batch.
- **B. No** (rejected), keep waiting.

Recommendation: A, low priority.

**Decided 2026-09-26 (board decision 55): A**, low priority; the tag still
requires fresh, successful CI on the same commit.

## Open questions after the G0 review

For the commander to ask the owner, highest risk first. Board decision 66
already reads the force-push means as owner-only.

### A. Should `main` also carry the owner-only bypass?

The G0 checklist gives the Repository admin role (only you, on this personal
repository) an "always" bypass on the trunk and release-branch rulesets.

- **A1. No bypass on `main`** (recommended). `main` holds released code and is
  the release policy's source of authority. For a true emergency you can still,
  as admin, set the `main` ruleset to Disabled for one push and back to Active,
  recorded in the board.
- **A2. The same bypass on `main`.** Faster in an emergency; a standing bypass
  also lets a merge into `main` skip the requirements with one checkbox.

### B. What if this repository cannot name the admin role as a bypass actor?

GitHub is expected to offer "Repository admin" in the bypass list of a
personal repository; G0 confirms it in the UI before relying on it.

- **B1. Disable the ruleset for the push** (recommended fallback). Works for
  any admin; while it is disabled, no rule of that ruleset protects the
  branches for anyone, so agents are stopped for the window, and the board
  records the window, the push and the old and new SHA.
- **B2. A separate break-glass GitHub App** as the only bypass actor, its key
  kept by you away from this machine. The bypass then names one specific
  actor; the cost is a second app and key to keep, used rarely.
