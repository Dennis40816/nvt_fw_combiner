# WS-GOV: development and release flow reset (1.1.13)

Owner: Claude Code (Opus 5.5), design drafter. Board:
[1.1.13 board](../1.1.13.md). Protocol: [handoff README](../README.md).
Deliverables: [governance ADR draft](ADR-DRAFT-governance-reset.md) and
[release workflow cleanup design](DESIGN-release-workflow-cleanup.md), both
revised after the [independent design review](#design-review-2026-09-26), the
ADR draft again after the [second design review](#design-re-review-2026-09-27)
and the [third](#design-re-review-3-2026-09-27) and
[fourth](#design-re-review-4-2026-09-27), and the
[G0 owner checklist](G0-owner-checklist.md), revised after its own
[reviews](#g0-checklist-review-2026-09-26). The owner decided every question
on 2026-09-26 (board decisions 47, 49 to 56, 65 to 67, 77 and 78) and on
2026-09-27 the priority, the G1-B scope, the governance owner and the G1-A
direction (decisions 100 to 102 and 105); the questions and answers are at the end
([owner decisions](#owner-decisions-in-risk-order)), followed by the
[questions the G0 review left](#questions-after-the-g0-review), now decided.
The board allocated ADR number 0080 to the governance draft.

## G0 local execution checkpoint — 2026-09-27

The owner explicitly authorized the assistant to execute the prepared G0
setup in this task. This is task-specific execution authority, not a standing
change to the operator rules. Machine-specific IDs, key paths and private
rollback files remain in the owner's local record outside Git.

Local R1 pre-edit admission for the Git compatibility correction:

- Source: `c61f10e3f1bfc03b287b256dd0526588cf2f4dc9` on this worktree.
- Existing owner: `Read-NfcCredentialInput` in `g0-scripts/NfcG0.Common.ps1`;
  production consumer: `nfc-app-token-helper.ps1`, followed by the unchanged
  exact-repository `Test-NfcCredentialScope` gate before token/key access.
- Reproduction: Git 2.53.0.windows.1 sends `capability[]`, `capability[]`,
  `protocol`, `host`, `path`, `wwwauth[]` on an HTTPS push. A diagnostic with
  no key access or credential output reproduced `Invalid credential request`
  before token acquisition; the App API wrapper itself already works.
- Owned implementation surfaces: that existing parser and
  `g0-scripts/tests/NfcG0.Tests.ps1`. Documentation records the result and
  patched file inventory. No change to credentials, permissions, token
  handling, repository scope, recording guards or firmware behavior.
- Acceptance: valid repeated advisory arrays work; malformed lines,
  duplicate scalar fields and unrelated repository scopes remain rejected.
- Narrow gate: focused regression red/green and the existing complete G0
  Pester suite using fake secret material, followed by independent scoped
  correctness/security review before installing the compatibility patch.
- Residual boundary: this local patch and its live setup verification do not
  certify repository integration, the complete D1–D7 owner acceptance, or a
  release. Keep those states separate.

Execution and review outcome (2026-09-27):

- Parser regression: original implementation failed 2 of 4 focused cases;
  corrected implementation passed 4/4. Complete G0 Pester suite: 42 passed,
  zero failed, using fake secrets with the existing test-area temp root.
  Three subprocess fixtures also needed to choose the first `pwsh`
  application returned, matching the production wrapper's existing behavior
  on hosts with more than one executable. This is a test harness correction.
- Independent scoped correctness/security review: PASS for that local R1
  diff, plus 16 independent parser/scope checks. Helper and wrapper production
  files are unchanged. The checklist records the two changed file hashes.
- The App is installed on this repository only. The daily helper/wrapper
  now use a private root outside AppData. Original encrypted files and
  private backups were copied opaquely, all copied file hashes matched, and
  key owner/protected ACL/user-only access were preserved. Machine paths,
  private IDs and rollback files are recorded only outside Git. D0 is
  partially verified as recorded below; a Codex child shell is not external
  evidence. The old virtualized copy contains a private-key file and is
  retained. Only the owner decides whether to delete it, after confirming
  the durable location works and the Bitwarden backup is valid; external
  verification alone does not authorize agent deletion.
- Rulesets were applied and read back against the reviewed request bodies;
  effective rules on the formal refs and disposable release ref were saved.
  The original apply stopped after `main` because GitHub added
  `required_reviewers: []` and
  `require_extra_approval_for_unattributed_changes: true` to the response.
  The UI identified the latter as extra approval for unattributed Copilot
  changes. A bounded reconciliation retained the pending transaction and
  pre-change snapshots, compared all approved fields, admitted only those
  two server-added defaults, then applied and verified the remaining
  reviewed requests. No protection was weakened; the tag ruleset was
  unchanged. The script's strict readback compatibility issue remains open;
  do not blindly rerun apply or treat the reconciliation as a script fix.
- D2 passed through the relocated helper: disposable trunk/release creation,
  direct trunk push rejection, trunk force-push/deletion rejection, and
  release force-push rejection. The App feature probe was pushed and
  [disposable PR #460](https://github.com/Dennis40816/nvt_fw_combiner/pull/460)
  was created through the wrapper; its author is the App, and the
  installation-token repository query lists only this repository.
- External follow-up reported by Claude commander (2026-09-27): from Claude
  Code's Git Bash outside Codex, the helper and DPAPI files were visible;
  `git push --dry-run` to a disposable external-check ref authenticated
  through the helper with exit 0; `ls-remote` confirmed no branch was
  created. This passes external visibility/authentication at dry-run level,
  not the complete D0 write check. Wrapper-path visibility was not reported.
  This entry attributes the evidence to Claude; Codex did not repeat it.
- Still open: D0 external wrapper-path check and actual push, decision 82
  Bitwarden backup confirmation, D4 owner approvals/review behavior, D5 positive merge, D6
  owner bypass test, D7 cleanup of the protected `9.9.x` ref, and A8
  owner-session cleanup. These are not
  marked passed by the local script tests or by this commit. The current
  document/code change is local only, for commander review and batch 2c;
  the owner authorized commit on this branch, without a push.
- D7 cleanup (owner approved, 2026-09-27, recorded by Codex for this batch):
  disposable PR #460 was closed without merging; the App deleted the
  disposable remote branches `9.9.9` and `feature/1.1.13/g0-check` with an
  atomic push using exact-SHA leases, and readback confirms both refs are
  absent. Protected `9.9.x` remains at
  `e6e991af32d76d99ad156a7f86baa662947db8d9` for the owner's C2 deletion. No
  owner bypass, credential change or ruleset change was made. Cleanup does
  not pass D4-D6. The old virtualized key copy is untouched and remains under
  the owner's deletion decision. The Claude commander confirmed the PR state
  and the three refs on GitHub afterwards.
- D0 wrapper path (Claude commander, 2026-09-27, outside Codex): the installed
  daily-use folder holds `Invoke-NfcGh.ps1`, `nfc-app-token-helper.ps1` and
  `NfcG0.Common.ps1`, visible from an unpackaged process, and their SHA-256
  values match the reviewed inventory in the checklist. The D0 actual
  external push remains open.

MSIX runtime guard follow-up (explicitly deferred under the owner's allowed
scope option; not implemented or covered by the parser review):

- Extend the existing `NfcG0.Common.ps1`; do not create another setup path.
  Detect package identity with `GetCurrentPackageFullName`: only
  `APPMODEL_ERROR_NO_PACKAGE (15700)` means unpackaged; expected success or
  insufficient-buffer results mean packaged; unexpected failures stop.
- For packaged callers, reject AppData destinations before any side effect.
  Resolve the physical existing target or nearest existing ancestor, append
  any nonexistent tail, and compare with equally resolved AppData roots.
  Cover junction/symlink ancestors and short-name aliases; lexical prefix
  checking alone is insufficient. Guard and IO must share a path base.
- Cover App creation (including dry-run), ruleset preview/apply/restore,
  helper installation/configuration and runtime key access, and the wrapper
  before it invokes the helper. Keep Common loading side-effect-free.
  Reject before browser/listener, conversion, Bitwarden, key access, backup
  IO or GitHub calls. Emit fixed errors without supplied private paths.
- Add Pester coverage for package API outcomes, AppData roots and children,
  case and `..`, nonexistent tails, sibling names, junctions and 8.3 aliases;
  test each caller with fake side-effect counters and private sentinels.
  Update affected reviewed hashes and obtain independent review of the
  concrete new diff. The parser PASS does not cover this new guard.
- This requires its own bounded security change across multiple callers.
  The current delivery therefore enforces unpackaged setup, an external
  private root and D0 through explicit operator checks. It does not promise
  protection against malicious concurrent filesystem mutation by the same
  Windows user.

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
- **G0 re-review revision (commander, 2026-09-26).** The second review of
  `6eb575c30..205a45ec4` again rejected the checklist: F-2, F-3, F-4 and F-10
  closed, F-1's original problem closed, F-5, F-6 and F-8 partly closed, new
  F-11 to F-13. The owner decided the two open questions as board decisions
  77 (a standing owner-only bypass on `main` as well, never a review or release
  exemption, with a recorded procedure) and 78 (pause the ruleset for a bounded
  window when an admin bypass cannot be set). Revise only the WS-GOV documents:
  F-11 (credential settings backed up by the owner), F-12 (an owner push path
  around the App helper), F-5, F-6, F-8, F-13 (the bypass and pause
  procedures), F-7's remaining sentence, and the API readings (the
  personal-repository permission UI, `do_not_enforce_on_create`, and marking
  every fact not verified locally); run the structure check; then stop.
- **G0 third review revision (commander, 2026-09-26).** The third review of
  `205a45ec4..b102135a5` rejected the checklist again, on a narrower scope:
  F-11, F-12, F-8, F-7 and the API readings closed, decision 77 faithfully
  recorded, no new owner decision needed. Revise only the WS-GOV documents:
  F-13 (C3 freezes the whole ruleset scope, restores whatever the push did,
  keeps the freeze if restoring fails, and lifts it only after the rules and
  bypass list are verified), F-6 (D6 split into C2 and C3; helper checks split
  by app and machine account), F-5 (freeze `main` and the trunk from pinning
  the SHAs to the post-merge check; `--match-head-commit` only), F-14 (every
  step names its operator and GitHub identity, citing decision 65), and two
  WS-TEST items: reference ADR 0079's amendment of ADR 0027, and use the joint
  `prose` definition, revised in G2 with its checker and document list.
- **G0 scripts (commander, 2026-09-27).** The owner-run G0 scripts of board
  decisions 80 and 82 passed their fourth independent security review (G0SR4,
  ACCEPT). Copy the reviewed folder unchanged into
  `docs/handoff/1.1.13/g0-scripts/`, checking every file's SHA-256 against the
  source; rewrite the G0 checklist so that A1 and A2 use
  `New-NfcGitHubApp.ps1` (manifest, conversion, the key in DPAPI and
  Bitwarden), the ruleset steps and the rollback use `Set-NfcRulesets.ps1`
  (item-by-item approval, transaction record, restore), and the helper and
  wrapper steps use `nfc-app-token-helper.ps1` and `Invoke-NfcGh.ps1`; list
  the G0SR4 owner notes at the top; label every setup-script step owner →
  owner; run the structure check; no push; then stop.
- **Second design review revision (commander, 2026-09-27).** The second
  design review of the ADR draft at `acbe5654d`, against the rules of `1.1.x`
  `e6e991af3`, returned ACCEPT-WITH-CHANGES with P1 F-1 and F-2 and P2 F-3 to
  F-6. Take in every finding and complete the partly closed first-round
  items; record decisions 100 (development speed before R-1), 101 (G1-B keeps
  its scope, the WS-AI changes included, stated with its waiting cost, not
  narrowed) and 102 (the owner is the governance owner, a role separate from
  the firmware and release owner); write G1-A's deliverables and acceptance
  so that its design admission can start. Only the ADR draft and this log
  change; no edit under `scripts/`, `.github/` or `docs/adr/`; commit on this
  branch after the structure check, no push; then stop.
- **Third design review revision (commander, 2026-09-27).** The third design
  review of `cb72ee0b0` returned ACCEPT-WITH-CHANGES: the second review's F-3
  to F-6 closed, three new P1 findings on the authority check's platform
  mechanism and one P2. The owner chose the simpler direction (decision 105).
  Replace the target-branch-context check, the trusted publisher and the
  commit-status fallback by an ordinary `pull_request` check; state its
  triggers, failure conditions, its division with the native code-owner
  review, last-push approval and required checks, and which safeguards are
  machine-enforced and which procedural; write F-2 and F-3 as procedural
  safeguards with a pre-merge verification and an owner self-change check;
  fix F-4 (no-downgrade over the old governed predicate plus floor; the
  schema and the checker's direct dependencies governance R3); update G1-A's
  deliverables, acceptance, negative cases and size; reduce the owner input
  before admission to what it needs; record the review and decision 105 here;
  structure check, commit, no push; then stop.
- **Fourth design review revision (commander, 2026-09-27).** The fourth
  review of `168de342b` returned ACCEPT-WITH-CHANGES with decision 105
  accepted as the premise and four procedural or wording items: F-1 separate
  a base that already has the checker from its first introduction (record
  that no base checker exists, cite the governance gates still in force, an
  independent fixed-head review and an explicit owner bootstrap approval,
  never a base-checker result; also for the first release pull request into
  `main`), with the self-change statement and G1-A acceptance; F-2 the
  commander compares the base's authority workflow, checker, schema and
  dependencies before the merge (rebase and re-review, or run the current
  base checker and attach it; deliberate differences go to the self-change
  check), with a "base tightens the checker, old head unchanged" case; F-3
  the authority block, valid review records and role evidence form the
  owner's approval snapshot, and any change stops the merge until the owner
  reconfirms, with a "evidence replaced or role added after approval, CI
  still green" case; F-4 qualify M1 to M7. Record the review here; structure
  check, commit, no push; then stop.

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

## Design re-review 4 2026-09-27

Reviewer `codex/gpt-6-astra`, implementation owner `claude-code`, fixed head
`168de342b26b81b537207ac74199163b97d1880c` (diff `cb72ee0b0..168de342b`);
read-only (Git, the third review, decision 105, the classifier functions and
the ruleset files; the official documentation could not be fetched from the
review environment). Verdict: **ACCEPT-WITH-CHANGES**. Decision 105 is
accepted as the design premise: no tamper-resistant publisher or automatic
stale-result invalidation is required. The third review's F-1, F-2 and F-4
are closed at design level, and its F-3 was reasonably turned into a
procedural safeguard but needed this round's F-2 and F-3. No listed governed
path was found downgraded; the coverage test is still G1-A's to prove. The
scoped Polytail design verdict was FAIL because of the open P1 findings, not
because a check failed.

| Finding | Severity | Taken in |
| --- | --- | --- |
| F-1 the self-change procedure asked for a base-checker result that does not exist when the check is first introduced | P1 | ADR "Changes to the check itself" split into two cases. First introduction (the G1-A pull request, and the first release pull request that brings the check into `main`): the pull request records that the base has no checker and claims no base result; the governance gates still in force on that base stand in (for G1-A the record rules, base validator, existing required checks and release-owner attestation; for the release pull request the ADR 0033 release gates); an independent fixed-head review covers the introduced check (for the release pull request, equal blob IDs to the trunk's with G1-A's review cited); the owner gives an explicit bootstrap approval. G1-A's intro, owner actions and acceptance updated |
| F-2 re-running an old run does not apply a base update of the authority code | P1 | Pre-merge verification step 1: the commander compares the base tip's authority workflow, checker, schema, policy and checker dependencies with the versions the latest run used (its job summary now records their blob IDs); if the pull request runs older versions, it is rebased and re-reviewed, or the current base checker is run against its head and attached, and a failure stops the merge; a deliberate difference goes to the self-change check. New acceptance case: a disposable base branch tightens the checker while an older pull request head stays unchanged |
| F-3 an `edited` run re-checks presence and form, not what the owner approved | P2 | The approval snapshot (head SHA, authority block, valid review records) recorded when the owner approves; pre-merge step 4 compares it with the live content, and any change stops the merge until the owner reconfirms explicitly (a new approving review, or a comment from the owner's account naming the head SHA and the change). New safeguard P9 and acceptance case: evidence replaced or a role added after the approval while the check stays green |
| F-4 the machine table claimed more than the rules give | P2 | M1 to M4 and M7 limited to Active rulesets in the normal, non-bypass flow; M4 separates the conclusions GitHub accepts (success, neutral, skipped) from the authority workflow's own success-or-failure design; M7 states that deletion is blocked on `main` and the trunk only (the release-branch ruleset has no deletion rule); M5 and M6 are the result of the checker that ran, when it ran, with P2 and P9 carrying whether it still holds |

The reviewer judged the owner input before admission (principals and the
firmware-semantic `src/` folders; contracts defaulting to both roles)
sufficient. Found while revising: the release-branch ruleset should gain the
new required context only when every open release branch contains the
workflow, since a pull request into an older release branch could never
report it (G1-A owner actions).

Evidence summary proposed by the reviewer for `designReview.evidence`, valid
only with a blocked outcome and describing the reviewed head, not this
revision:

> Independent read-only fourth-round R3 design review by codex/gpt-6-astra,
> implementationOwner claude-code, at commit
> 168de342b26b81b537207ac74199163b97d1880c: ACCEPT-WITH-CHANGES. Owner
> decision 105 is accepted as the design premise; no tamper-resistant
> publisher or automatic stale-result invalidation is required. Third-round
> F-1, F-2 and F-4 are closed at design level. G1-A design admission remains
> blocked pending an executable bootstrap procedure when no base checker
> exists, handling of authority-code changes on the live base, and explicit
> owner reconfirmation of changed roles or evidence on the same head. Qualify
> the machine-guarantee table to match ruleset exceptions and accepted check
> conclusions. No files were modified, and no builds, tests or live ruleset
> verification were performed.

A fixed-head check of this revision is still needed before G1-A's design
review can be recorded as approved (commander).

## Design re-review 3 2026-09-27

Reviewer `codex/gpt-6-astra`, implementation owner `claude-code`, fixed head
`cb72ee0b077cafbcb9ff03c5aaf199fed725a493` (diff `acbe5654d..cb72ee0b0`),
current rules at `1.1.x` `e6e991af32d76d99ad156a7f86baa662947db8d9`;
read-only, with GitHub's documentation read for the platform facts (no build,
test or live repository check). Verdict on the ADR draft:
**ACCEPT-WITH-CHANGES**, not yet acceptable as a complete design, and G1-A's
`designReview.outcome` could not be `approved`. The second review's F-3 to F-6
closed, its F-2 mostly (the new F-4 remains), its F-1 partly (the new F-1 to
F-3). No listed R3 script was found downgraded. The scoped Polytail design
verdict was FAIL because of the open P1 findings, not because a check failed.

**Owner decision 105 (2026-09-27, [1.1.12 board](../1.1.12.md)).** After this
review the owner chose the simpler direction: the authority check is an
ordinary pull request CI check that classifies paths and verifies the
declared roles and evidence, failing closed; the exact-head human approval is
the rulesets' code-owner review after the last push, which the owner gives
for every role; the remaining risks (a pull request can edit the check it
runs; an earlier green result on the same head) are procedural safeguards
stated in the ADR, not machine guarantees. Decisions 103 (users receive
1.2.0) and 104 (1.1.x versions still go through the full release process)
were found not to conflict with the staged design.

| Finding | Severity | Taken in |
| --- | --- | --- |
| F-1 a target-branch-context workflow is loaded from the default branch (`main`), which does not fit G1-A's rollout on the trunk | P1 | Removed by decision 105: the check runs on ordinary `pull_request` events, whose workflow comes from the pull request, so it runs on the trunk from G1-A on, and on its own pull request (the bootstrap case, informative only). The tamper-resistant alternative is ADR option 6, not chosen |
| F-2 two event types give no single result; the commit-status fallback had no publisher, no naming plan and no protection against a head-controlled workflow | P1 | Removed: one workflow, one job, one context, `pull_request` types `opened`, `synchronize`, `reopened`, `ready_for_review` and `edited`; no review-event trigger, publisher or fallback. The approval is GitHub's last-push code-owner review. A pull request that edits its own check is safeguard P1, carried by the self-change check: the base branch's checker run against the head and attached, the pull request's statement of changed verdicts, the owner's explicit self-change statement in the approval, and no merge on the check's own result alone |
| F-3 an earlier green result on the same head had no invalidation or concurrency contract | P1 | Accepted as procedural (decision 105; safeguards P2 and P3): the pre-merge verification re-runs the check (a re-run reads the live description, review records and base), confirms through the API that it passed on the current head SHA and that the owner's approval is on that SHA's last push, and merges with `--match-head-commit`; the remaining window is stated. The machine claims were reduced to M1 to M7 of the new safeguards table; a live case shows a review record edited after a green run staying green until the pre-merge re-run |
| F-4 the no-downgrade comparison applied the old floor function to every path; the policy schema was not governance R3 | P2 | ADR item 4: the comparison covers only the paths the old validator governs (`_is_capability_reuse_governed_path` with `_capability_reuse_minimum_risk`); other paths are accepted by the explicit new rules (prose list and its consumer limit, R1 defaults, coverage of every exclusion). The schema and every repository file the checker imports or reads are governance R3, with a dependency test |

Also taken in: the G0 state from board batch 2c (D0's external push, D4's
exact-head approval on #461, D5's merge of #461 without a bypass; D4's
dismissal and identical-tree observations not recorded there, the second now
safeguard P3); a CODEOWNERS consistency test instead of a generator in G1-A;
G1-A's deliverables, acceptance and size re-estimated (about 900 to 1,500
lines instead of 1,800 to 3,000); the owner input before admission reduced to
the principals and the firmware-semantic `src/` folders.

Evidence summary proposed by the reviewer for `designReview.evidence`, valid
only with a blocked outcome and describing the reviewed head, not this
revision:

> Independent read-only third-round design review by codex/gpt-6-astra,
> implementationOwner claude-code, at cb72ee0b077cafbcb9ff03c5aaf199fed725a493
> against the rules at e6e991af32d76d99ad156a7f86baa662947db8d9:
> ACCEPT-WITH-CHANGES. Second-round F-3 through F-6 are closed, and
> authority-role classification is substantially corrected. G1-A design
> admission remains blocked pending a viable default-branch workflow
> bootstrap, a trusted exact-head result publication contract with a fully
> specified fallback, and stale-result invalidation and concurrency handling.
> Clarify the classifier comparison domain and classify the authority policy
> schema as governance R3. No files were modified, and no builds, tests, or
> live repository configuration checks were performed.

Decision 105 changes what "closed" means for F-1 to F-3: they are answered
by removing the mechanism and naming procedural safeguards, not by a machine
contract. An independent check of this revision is still needed before G1-A's
design review can be recorded as approved (commander).

## Design re-review 2026-09-27

Superseded in part: the authority check wiring taken in for F-1 below was
replaced after the [third review](#design-re-review-3-2026-09-27) by board
decision 105.

Reviewer `codex/gpt-6-astra`, implementation owner `claude-code`, fixed head
`acbe5654d6cfa179d64fd604c38a95c24addc498`, current rules at `1.1.x`
`e6e991af32d76d99ad156a7f86baa662947db8d9`; read-only (Git reads, document
and code search; no build, test or live GitHub check; the G0 state was taken
from this log). Verdict on the ADR draft: **ACCEPT-WITH-CHANGES**. The
retirement direction and the staged G1-A/G1-B transition hold; G1-A admission
stayed blocked on the two P1 gaps. The scoped Polytail design verdict was
FAIL because of those gaps, not because a check failed. R-1 was not needed
first.

| Finding | Severity | Taken in |
| --- | --- | --- |
| F-1 the new checks lacked required-check wiring, a trusted policy source and re-evaluation | P1 | ADR "Authority check wiring (G1-A)": one always-run job producing the required context `governance / authority` from GitHub Actions, never neutral or skipped, and the App without `checks`/`statuses` write; `pull_request_target` (opened, edited, synchronize, reopened, ready_for_review, labeled, unlabeled) and `pull_request_review` (submitted, edited, dismissed) instead of `ci.yml`'s four events, live-state reads, a verdict bound to head, base and policy blobs, re-evaluation of open pull requests after a base policy change; checker and policy from the live base, head read only as data, the stricter of base and head policy; principals by user ID and login in the policy; fail-closed on missing policy or principal, API error, timeout, rate limit, incomplete page, a head moving during the run, or a failed producer; residual trust for a pull request that edits the workflow; platform facts verified live with a commit-status fallback. G1-A builds this wiring itself, without `ci.yml` or `verify.py`. The negative cases (withdrawn approval, edited evidence, same-PR policy downgrade, missing producer, and others) are in "G1-A delivery and acceptance"; one real pull request is only part of the evidence |
| F-2 no role union, R3 scripts all put under release, no governance R3 role | P1 | ADR item 4: each map entry has a risk floor and a role set; every matching entry applies (highest floor, union of roles), no first match; author and reviewer text only adds; an unclassified path is governance R3 and cleared only by the governance owner's exact-head approval; floors never below today's validator; an initial map table; the 21 `CAPABILITY_REUSE_R3_SCRIPTS` assigned one by one (five firmware, two firmware and release, 13 release, `release_promotion_policy.py` release and governance) and three R2 scripts raised. Item 7: three roles with decision 102's governance owner, the approval contract (approving review, `commit_id` equal to the head, principal listed for the role, one block per role with head SHA and evidence, dismissal and changes-requested handling) |
| F-3 acceptance and retirement took effect at different times | P2 | ADR header ("Effect of acceptance"; "Supersedes, effective when the authorized G1-B merges") and migration step 0: acceptance accepts the staged design only; step 2 lists the old obligations the G1-B authorization replaces for G1-B itself and the gates it does not replace |
| F-4 pins recomputed from a base that lacks the READMEs | P2 | ADR item 2 and migration step 4: one order, the pins are G1-B's final snapshot (base evidence plus exactly three READMEs; checkpoint blob unchanged), proven by the frozen-path diff and named in the authorization |
| F-5 G0 state and decision 100 not in the ADR | P2 | ADR "State on 2026-09-27" (App and three rulesets applied, the trunk rules, D0 partly, D4 to D6 and the rest open, so G0 is applied but not accepted), the identity bullet, item 8 (in force since G0 versus added by G1-A and G2), item 10 (decision 66 as the current rule, decision 23 history, equal trees never replacing the history gate), R-1 paused (decision 100) |
| F-6 no single-writer rule after the records; G1-B scope | P2 | ADR item 3 "Single writer" (root `AGENTS.md` rule, execution-workflow procedure, stated as procedural: CODEOWNERS is not a write lock) and the owners table; migration step 1 separates G1-B's parts required for the retirement from those carried by decision 101; Consequences state the waiting cost of decision 101 without narrowing G1-B |

First-round findings the review found only partly closed:

| First round | Second review | Now |
| --- | --- | --- |
| F-1 executable R3 approval | head SHA, role and evidence done; wiring missing | closed by F-1 and F-2 above |
| F-2 firmware-semantic escalation | direction right; union and script split missing | closed by F-2 above |
| F-3 sealed-but-unmerged records | mostly closed | F-3 and F-4 above; the inventory row now names each owed item's carrier |
| F-4, F-5 to F-9 | closed at design level | unchanged |

Other review notes taken in: the transitional double approval while both gates
run, and the table of what G1-B removes and keeps (ADR Consequences); the
"remove checkpoint rebinding first" alternative recorded as option 5 and not
pursued; ADR 0079's acceptance (decision 81) synchronized, so item 13 now
waits for its T4b activation. Owner input the review listed is in the ADR's
"Owner input still needed"; decisions 101 and 102 already answer two of its
questions, and decision 100 settled the priority.

Evidence summary proposed by the reviewer for `designReview.evidence`, valid
only with a blocked outcome, never as `approved`:

> Independent read-only design re-review by codex/gpt-6-astra,
> implementationOwner claude-code, at acbe5654d6cfa179d64fd604c38a95c24addc498
> against the rules at e6e991af32d76d99ad156a7f86baa662947db8d9:
> ACCEPT-WITH-CHANGES. The retirement direction and staged G1-A/G1-B
> transition are sound. G1-A admission remains blocked pending an enforceable
> required-check lifecycle and trusted policy source, plus complete
> authority-role classification and governance R3 escalation. Clarify cutover
> effectiveness, frozen-pin construction, retained single-writer
> responsibility, and the applied-but-not-fully-accepted G0 state. No build,
> tests, or live GitHub verification were performed.

This revision is not itself reviewed: recording G1-A's `designReview.outcome`
as `approved` needs an independent check of this revision (commander).

## Governance ADR design re-review 5 2026-09-27

`codex/gpt-6-astra` reviewed `664bbe868`: ACCEPT-WITH-CHANGES, no P0 or P1.
Fourth-round F-1 (bootstrap) and F-2 (base authority code) are closed; F-4
is substantively resolved. The commander made the two narrow corrections: the
approval snapshot keeps the full approval-relevant review content, so a body
edit under the same review id, head SHA and verdict is detected (F-3), and the
gate-division table cites M4's accepted conclusions and the Active,
non-bypass condition (F-4). A confirmation of these edits follows.

## Release cleanup design re-review 2026-09-27

`codex/gpt-6-astra` re-reviewed the release workflow cleanup design at
`2f7cfd720` against the trunk `e6e991af3`: ACCEPT-WITH-CHANGES, no P0 or P1.
The first-round findings F-1 to F-9 are closed in the design. R-1 may be
admitted with `designReview.outcome: approved`. New findings: F-10 (refresh
the source inventory; `package.ps1` and ADR 0057 changed on the trunk) and
F-11 (staging must reproduce the G0 controls and the `GITHUB_TOKEN` boundary)
must close before R-1 merges and are written into the design at `f12194a5d`;
F-12 (R-2 must say whether the composite action shares setup only or a common
build entry point) must close before R-2 is admitted. The review found no
branch-ruleset conflict that blocks releasing 1.1.13 with the current
workflow: the workflow never pushes `main`, tags are created with
`GITHUB_TOKEN` under the unchanged tag ruleset, and only the release pull
request into `main` now follows the new approval rules. Owner prerequisites:
the App `workflows` permission together with the reviewed helper
permission-set change before the first remote workflow write; the staging
repository before R-1 merges; the G0 owner-approval and bypass or fallback
checks (D4-D6) and a tag ruleset without bypass before the release; the
release-owner attestation at R-1's final evidence head.

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

## G0 checklist re-review 2026-09-26

Reviewer `codex/gpt-6-astra`, range `6eb575c30..205a45ec4`; read-only.
Verdict: **REJECT** again; no P0. Closed: F-2, F-3, F-4, F-10, and F-1's
original problem; partly closed: F-5, F-6, F-8; new: F-11 to F-13. The owner
then decided the two open questions as board decisions 77 and 78.

| Finding | Severity | Taken in |
| --- | --- | --- |
| F-11 the backup let an agent read `credential.*` values, which may hold secrets | P1 | Checklist "Before you start": you back up and check the raw credential settings in your own unrecorded terminal and give the agent only the facts you confirm as non-secret; the agent reads only `user.*` itself and later checks only the repository scope |
| F-12 an owner push would still go through the App helper | P1 | Checklist C2 "Your push path": a one-command helper override (empty entry, then Git Credential Manager, browser sign-in, no token on the command line) or a separate owner clone; the actor is confirmed afterwards and the agent path checked unchanged |
| F-5 the test merge was not bound to the real merge; post-merge failure handling | P2 | Checklist "After G0": both parent SHAs recorded and checked on the test merge, read again just before merging, `--match-head-commit`, parents and trees confirmed after the merge; a failure before the merge stops it, a failure after it is recorded and stops integration and releases for the owner to decide |
| F-6 identity proof and bypass-free positive path | P2 | Checklist part D: D3a for the app and D3b for the machine account; D5 merged by the agent identity, which has no bypass, or by you with the bypass option unchecked and confirmed; D6 alone tests the bypass |
| F-8 rollback did not restore the helper | P2 | Checklist rollback: the repository-scope key unset, original values re-added in order with any empty entry, or left unset if there was none; secret-bearing settings restored by you; the result compared with your raw backup |
| F-13 the reach of an `always` bypass and the pause | P2 | Checklist C2 and C3 (decisions 77 and 78): what a bypass skips and what it never skips (tag ruleset, `release` environment, release checks); before, during and after steps; record; ADR item 8, risks and decision table; release design item 5 |
| F-7 one over-promising sentence left in this log | P3 | Decision list question 2, option A: a correction note |

API readings taken in: the personal-repository Collaborators page may show no
role selector (checklist part B); `do_not_enforce_on_create` only exempts the
required-status-checks rule at creation, with RS-2's example now creating the
trunk; and a new checklist table lists every platform fact not verified
locally with the step of G0 that confirms it.

## G0 checklist third review 2026-09-26

Reviewer `codex/gpt-6-astra`, range `205a45ec4..b102135a5`; read-only.
Verdict: **REJECT**, one P1 and three P2 left; closed: F-11, F-12, F-8, F-7
and the API readings; decision 77 found faithfully recorded, decision 78's
direction recorded but its procedure incomplete. No new owner decision.

| Finding | Severity | Taken in |
| --- | --- | --- |
| F-13 the pause froze only the target branch and had no failure path | P1 | Checklist C3: the freeze covers every branch the ruleset covers and starts before it is disabled; the ruleset is exported first and set Active again whatever the push did; a failed restore or a passed window keeps the freeze until you have restored and verified it; the freeze lifts only after Active, rules and bypass list are verified; ADR item 8 |
| F-6 verification of the accepted fallback | P2 | Checklist D6 split into D6-C2 and D6-C3; D6-C3 freezes every `*.*.x` branch, `1.1.x` included, and records the disable and restore times, the export and the push actor without expecting a bypass evaluation; C2 step 6 checks the agent path separately for the app and the machine account |
| F-5 the base could still move | P2 | Checklist "After G0": `main` and the trunk frozen from pinning the SHAs to the post-merge check; your approval of the head; only `gh pr merge --match-head-commit`; the browser path removed there and in D5 |
| F-14 operator and identity per step | P2 | Checklist "Operators and identities" (decision 65): you operate accounts, the conversion, keys, tokens, PATs and other secrets, rulesets and repository settings, local credential and identity settings, the bypass and the pause; an agent operates the App-identity pushes, pull requests, merges and verification cleanup; every step carries **operator → identity** |

WS-TEST items, from its design review: the ADR draft now names ADR 0079 as
related (it amends ADR 0027's evidence-sharded CI section and replaces its
300-second clause on acceptance, board decision 72), makes item 13's
pull-request tier wait for that amendment, and uses the joint `prose`
definition ("not an input to any product test or other semantic
verification; only listed generic document-structure checks may read it"),
with G2 revising the wording, its checker and the list of document checks to
migrate in one batch; document assertions are decided test by test (board
decision 71).

## Conflicts with other work

- CI failure evidence (R3): owns `ci.yml` and `scripts/verify.py`; G2, the
  composite action in `ci.yml` and design batch R-6 wait for it, and keep its
  failing-project evidence (board decision 41) and attempt-to-artifact
  correspondence. Its `overwrite: true` must not reach release artifacts.
- Pre-built catalog ADR (0077): the board allocated 0080 to this governance
  ADR and 0077 to the catalog, so the numbers do not collide. Its text is not
  on this branch; before integration, pin its version and
  package diff and check catalog generation time, allowlist, provenance,
  manifest and smoke together with release batch R-1. `package.ps1` needs one
  writer.
- NVT marker (0076), TP SVN, F08 and the CI work are admitted under the record
  system now; each follows the ADR's transition table if G1-B lands first.
- WS-AI port: edits the same governed paths as G1-B (`AGENTS.md`, skills,
  validator frontmatter rules); same batch, one writer.
- WS-TEST (ADR 0079): depends on this ADR's authority map (item 4) and CI
  tiers (item 13), shares the `prose` definition, and lands its R3 parts
  (workflow and required-check changes) through G2.
- Rolling parity P-2 and release batch R-1 both edit
  `scripts/v0916_parity_certification.py`; the board gives it one writer at a
  time (the batch admitted first).
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

### 2026-09-26 Structure check of the G0 revision
State: verified (documents only)
Commits: `c61e47749` verified; the commit carrying this entry changes only
this entry.
Evidence: `python scripts/verify.py --structure-only` with TEMP, TMP and
TMPDIR set to `<test-area>/temp`, at `c61e47749` -> `structure=PASS`, 263.0 s;
`sync_derived` changed 0 files. A link and anchor check of the four changed
documents passed before the commit.
Open: the two questions at the end of this log; the re-review (commander).
Next: stop.

### 2026-09-26 Revision after the G0 re-review
State: local
Commits: the commit carrying this entry, on `205a45ec4`
Evidence: F-5 to F-8 and F-11 to F-13 of the re-review are taken in (table
above); board decisions 77 and 78 are recorded in the checklist, the ADR draft,
the release design and this log; the ADR draft carries its allocated number
0080. Verification of this commit is recorded in the next entry.
Open: the re-review (commander).
Next: structure check of this commit, then stop.

### 2026-09-26 Structure check of the G0 re-review revision
State: verified (documents only)
Commits: `ba0227cdd` verified; the commit carrying this entry changes only
this entry.
Evidence: `python scripts/verify.py --structure-only` with TEMP, TMP and
TMPDIR set to `<test-area>/temp`, at `ba0227cdd` -> `structure=PASS`, 306.5 s;
`sync_derived` changed 0 files. A link and anchor check of the four changed
documents passed before the commit.
Open: the re-review (commander).
Next: stop.

### 2026-09-26 Revision after the G0 third review
State: local
Commits: the commit carrying this entry, on `b102135a5`
Evidence: F-5, F-6, F-13 and F-14 of the third review and the two WS-TEST
items are taken in (table above). Verification of this commit is recorded in
the next entry.
Open: the re-review (commander).
Next: structure check of this commit, then stop.

### 2026-09-26 Structure check of the G0 third revision
State: verified (documents only)
Commits: `878f4b1a7` verified; the commit carrying this entry changes only
this entry.
Evidence: `python scripts/verify.py --structure-only` with TEMP, TMP and
TMPDIR set to `<test-area>/temp`, at `878f4b1a7` -> `structure=PASS`, 264.0 s;
`sync_derived` changed 0 files. A link and anchor check of the four WS-GOV
documents passed before the commit.
Open: the re-review (commander).
Next: stop.

### 2026-09-27 G0 scripts added; checklist rewritten around them
State: local
Commits: `90ed39c92` (the scripts) and the commit carrying this entry
Evidence: each of the 11 copied files has the SHA-256 of its reviewed source
(listed at the top of the checklist); `tests/NfcG0.Tests.ps1` also matches
the hash named in G0SR4. The nested `.gitattributes` keeps the bytes on
checkout: four files deleted and checked out again kept their hashes. The
wrapper's parameter binding was tried locally with a copy of its parameter
block (PowerShell 7.6, no GitHub): some `gh` options are refused or dropped
before they reach `gh`, recorded in checklist A6 as a usage limit. The
scripts are unchanged, so two points are left to their author: that limit,
and the README sentence that still describes the scripts as kept outside the
repository. Verification of this commit is recorded in the next entry.
Open: the final review (commander).
Next: structure check of this commit, then stop.

### 2026-09-27 Structure check of the G0 script rewrite
State: verified (documents only)
Commits: `3d2540e0c` verified; the commit carrying this entry changes only
this entry.
Evidence: `python scripts/verify.py --structure-only` with TEMP, TMP and
TMPDIR set to `<test-area>/temp`, at `3d2540e0c` -> `structure=PASS`, 276.0 s;
the working tree stayed clean. A link and anchor check of the four WS-GOV
documents passed before the commit.
Open: the final review (commander).
Next: stop.

### 2026-09-27 Revision after the second design review
State: local (verified before commit, documents only)
Commits: the commit carrying this entry, on `acbe5654d`; only the ADR draft
and this log change.
Evidence: F-1 to F-6 of the [second design review](#design-re-review-2026-09-27)
and the partly closed first-round items are taken in (tables above); board
decisions 100 to 102 are recorded in the ADR draft. New ADR parts: "State on
2026-09-27", the authority policy with risk floors and role sets (item 4),
three roles with the governance owner (item 7), "Authority check wiring
(G1-A)", migration step 0 and the G1-B authorization contents, the
single-writer rule (item 3), and "G1-A delivery and acceptance". Facts checked
at `e6e991af3` for this revision: 359 records, all final-complete; 112
attestations; the six open inherited authorities of the trusted checkpoint
each have an approving attestation; `CAPABILITY_REUSE_R3_SCRIPTS` holds 21
scripts; `ci.yml` listens to four `pull_request` event types;
`REQUIRED_RELEASE_CHECKS` is the closed set of three. A link and anchor check
of the four WS-GOV documents passed. Structure check before the commit:
`python scripts/verify.py --structure-only` with TEMP, TMP and TMPDIR set to
`<test-area>/temp`, on the working tree of this commit except this result
line -> `structure=PASS`, 169.1 s; `sync_derived` changed 0 files; only the
existing code-size warnings, unrelated to these documents.
Open: an independent check of this revision before G1-A's design review can
be recorded as approved (commander); the owner input listed at the end of the
ADR draft.
Next: stop.

### 2026-09-27 Revision after the third design review (decision 105)
State: local (verified before commit, documents only)
Commits: the commit carrying this entry, on `cb72ee0b0`; only the ADR draft
and this log change.
Evidence: the [third review](#design-re-review-3-2026-09-27) and decision 105
are taken in (table above). ADR changes: options 4 and 6; items 6 to 9
rewritten (review records read by the check on each run; the last-push
code-owner approval as the machine-enforced approval; role naming
procedural; the identical-tree case); a new section "Authority check and its
safeguards (G1-A)" with the `pull_request` triggers, the failure
conditions, the division of the gates, the table of machine-enforced (M1 to
M7) and procedural (P1 to P8) safeguards, the pre-merge verification and the
self-change check; item 4's no-downgrade domain and the schema and
dependencies as governance R3; the G0 state from board batch 2c; G1-A's
deliverables, acceptance and size; the owner input before admission. The ADR
no longer describes a target-branch-context workflow, a review-event
trigger, a result publisher or a commit-status fallback, except as the
option not chosen. A link and anchor check of the four WS-GOV documents
passed. Structure check before the commit: `python scripts/verify.py
--structure-only` with TEMP, TMP and TMPDIR set to `<test-area>/temp`, on the
working tree of this commit except this result line -> `structure=PASS`,
173.2 s; `sync_derived` changed 0 files; only the existing code-size
warnings, unrelated to these documents.
Open: an independent check of this revision (commander); the owner input
listed at the end of the ADR draft.
Next: stop.

### 2026-09-27 Revision after the fourth design review
State: local (verified before commit, documents only)
Commits: the commit carrying this entry, on `168de342b`; only the ADR draft
and this log change.
Evidence: F-1 to F-4 of the [fourth review](#design-re-review-4-2026-09-27)
are taken in (table above): the safeguards table qualified (M1 to M7) with
the new P9; the pre-merge verification now has five steps (base authority
code, re-run, head and approval, approval snapshot, merge); the self-change
check has its first-introduction case with the bootstrap approval; the job
summary records the versions a run used; G1-A's intro, deliverables, owner
actions and live cases follow, including the release-branch ruleset timing;
the ADR header lists the fourth review. A link and anchor check of the four
WS-GOV documents passed. Structure check before the commit: `python
scripts/verify.py --structure-only` with TEMP, TMP and TMPDIR set to
`<test-area>/temp`, on the working tree of this commit except this result
line -> `structure=PASS`, 182.7 s; `sync_derived` changed 0 files; only the
existing code-size warnings, unrelated to these documents.
Open: a fixed-head check of this revision (commander); the owner input
listed at the end of the ADR draft.
Next: stop.

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
  token. (Corrected after the G0 reviews: GitHub drops an approval when new
  reviewable commits arrive, not necessarily for a new SHA with identical
  changes; the exact-commit rule comes from the G1-A authority check and the
  R-3 release policy.)
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

## Questions after the G0 review

Asked by the commander, highest risk first, and decided by the owner on
2026-09-26 as board decisions 77 and 78. Board decision 66 already read the
force-push means as owner-only.

### A. Should `main` also carry the owner-only bypass?

The G0 checklist gives the Repository admin role (only you, on this personal
repository) an "always" bypass on the trunk and release-branch rulesets.

- **A1. No bypass on `main`** (recommended; rejected). `main` holds released
  code and is the release policy's source of authority. For a true emergency
  you can still, as admin, set the `main` ruleset to Disabled for one push and
  back to Active, recorded in the board.
- **A2. The same bypass on `main`.** Faster in an emergency; a standing bypass
  also lets a merge into `main` skip the requirements with one checkbox.

**Decided 2026-09-26 (board decision 77): A2**, with a procedure: a bypass
skips every rule of its ruleset, so it is an owner action outside the normal
flow and never a review or release exemption; the old and new SHAs and a
recovery ref are recorded first, related writes and releases stop, the
effective rules are verified afterwards, approvals and evidence are renewed on
the new head, and existing tags and release artifacts are never rewritten
(G0 checklist, C2).

### B. What if this repository cannot name the admin role as a bypass actor?

GitHub is expected to offer "Repository admin" in the bypass list of a
personal repository; G0 confirms it in the UI before relying on it.

- **B1. Disable the ruleset for the push** (recommended fallback). Works for
  any admin; while it is disabled, no rule of that ruleset protects the
  branches for anyone, so agents are stopped for the window, and the board
  records the window, the push and the old and new SHA.
- **B2. A separate break-glass GitHub App** (rejected) as the only bypass
  actor, its key kept by you away from this machine. The bypass then names one
  specific actor; the cost is a second app and key to keep, used rarely.

**Decided 2026-09-26 (board decision 78): B1.** Every agent stops related
writes and releases for a bounded window; protection is restored and
verified afterwards, and the board records it (G0 checklist, C3).

## G1-A activation and acceptance (2026-09-27)

- **Merge.** #463 merged into `1.1.x` as `50c0998e3` (reviewed head `fa87b19fd`, final evidence `e3fa02975`,
  attestation head `93326303c`); owner approval of that head, recorded under board decision 141 with the bootstrap
  statement in the pull request; all eleven checks green; pre-merge record on the pull request.
- **Required context.** Board decision 142: Codex added `governance / authority` (GitHub Actions) to the trunk ruleset
  (RS-2, `*.*.x`) after the owner's Confirm access; API and web read-back agree and nothing else changed. The
  release-branch ruleset stays unchanged because open release branches (`0.1.0` to `1.1.12`) lack the workflow.
- **Case 1 (#465, triggers and edits).** 1 failure (run 108623988421), 2 success (run 108624065938), 3 failure (run 108624130035), 4 success (run 108624193063), 5 failure (run 108624266824), 6 success (run 108624338352), 7 success (run 108624338352), 8 failure (run 108624606992): opened without a record red; record green; risk lowered to R0 red;
  restored green; identical-tree push red until a new record; record edited after a green run keeps the old green
  result and only the pre-merge re-run turns it red (P2: the procedure closes this gap).
- **Case 2 (#465, required).** With the context required and red, the pull request is BLOCKED (read from the merge
  state, no merge attempt). Cancelled and missing results were not produced separately.
- **Case 3 (#467 into a disposable base, base tightening).** Green before the base tightened; the step 1 comparison
  flags the older policy blob on the head; the current base checker run against the unchanged head fails (exit 1); a
  new run after the tightening is red (stricter of base and head). Not executed: re-running the old green run, which
  needs Actions write that the App lacks.
- **Cases 4-6 (#466, policy description only).** 4.1 success (run 108625978434), 4.2 success (run 108627190906), 6 failure (run 108627269012): self-change: the trunk checker run against the head passed
  and its output is posted on the pull request; after the owner's approval, replacing the role evidence on the same
  head keeps the check green while the commander's step 4 snapshot comparison stops the merge (P9); D4: a diff-changing
  push dismissed the owner's approval (#463), an identical-tree push kept it (#466) while the check turned red for the
  missing record, so P3 stays procedural (pre-merge step 3 asks for a new approval on the exact head).
- **Still open for "in force" (ADR 0080 item 9):** one real pull request other than G1-B merged under G1-A with its
  recorded pre-merge verification (planned: batch 3), and the owner's confirmation. Evidence files are in the test area
  under `evidence/g1a-activation/`.

## D6 owner bypass drill (2026-09-28, C2 path)

- Step 1 (board record): branch `9.9.x` (disposable, trunk pattern); old SHA
  `e6e991af32d76d99ad156a7f86baa662947db8d9`; intended force push to its first
  parent `54974d5ccbe7864042c244aa2d7b4617a08104cc`, then deletion; reason: D6-C2
  drill before the G1-B merge (decision 146); recorded 2026-09-28.
- Step 3 (freeze): nothing pushes to or merges into `9.9.x`; no open pull
  request targets it.
- Steps 2, 4, 5 (owner) and 6, 7, 10 (agent): results recorded below when done.
- Result (2026-09-28, run by Codex in the owner's session under decision 148;
  the owner signed in through Git Credential Manager):
  - Step 2: `recovery/9.9.x-20260928` created at `e6e991af3` (01:15-01:17 +08:00).
  - Step 4: force push `e6e991af3...54974d5cc` to `9.9.x` (01:17:46 +08:00); the
    push reported "Bypassed rule violations for refs/heads/9.9.x". Deletion of
    `9.9.x` (01:18:02 +08:00) succeeded.
  - Step 5: the repository activity API lists `force_push` and
    `branch_deletion` for `9.9.x` by `Dennis40816`. The rule insights need an
    admin view (the App gets 403); the owner may confirm the two bypass
    entries on the page. The owner credential was signed out at once
    (`git credential-manager github logout`; `github list` no longer shows the
    account), which closes A8 for this drill.
  - Step 6: agent path unchanged: the local helper is still the empty entry
    plus the App helper, `useHttpPath` is `true`, and the App wrapper still
    reaches `/installation/repositories` (this repository only).
  - Step 7: the effective rules for `9.9.x` (ruleset 24060410, RS-2) are still
    deletion, non-fast-forward, pull request and required status checks.
  - Open: the owner deletes `recovery/9.9.x-20260928` after G1-B merges (D7).
- D7 (2026-09-28, decision 152, run by Codex in the owner's session):
  `recovery/9.9.x-20260928` (at `e6e991af3`) deleted at 06:54:51 +08:00; no
  `recovery/*` or `9.9.*` branch remains; the agent helper path is unchanged.
  The GCM sign-out reported no stored `Dennis40816` account (the account list is
  empty), so the owner confirms in Windows Credential Manager that no
  `git:https://github.com` entry remains (A8).
- A8 (2026-09-28): the owner deleted the Git Credential Manager GitHub entry, and
  the commander ran `gh auth logout --hostname github.com --user Dennis40816` at
  the owner's request; Windows Credential Manager lists no GitHub entry, `gh auth
  status` reports no login, and the App wrapper still reaches this repository only.
  G0 is complete except two owner confirmations the checklist requires: the
  rule insights showing both D6 bypasses (checklist C2 step 5 and D6; the App
  cannot read that admin view) and the Bitwarden backup confirmation
  (`docs/handoff/1.1.13.md`, G0 still-open list).
- G0 owner confirmations (2026-09-28): this repository has no rule insights
  view (Settings → Rules lists Rulesets only), so the D6 bypass evidence is
  the repository Activity instead: the `9.9.x` force push `e6e991a` →
  `54974d5` by Dennis40816 at 2026-09-27T17:17:46Z and the `9.9.x` deletion
  by Dennis40816 at 17:18:02Z. The commander read both through the App
  (Activity API) and a read-only browser capture kept in the test area; the
  owner confirmed both, and confirmed the Bitwarden backup. G0 is complete.
