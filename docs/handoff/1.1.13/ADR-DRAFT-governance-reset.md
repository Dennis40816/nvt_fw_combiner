# ADR 00XX (draft): Retire capability-reuse history replay and reset the development flow

- Status: **Proposed** — draft for independent design review and owner
  decisions; not implementation authority.
- Date: 2026-09-26
- Owners: repository owner (governance, release and firmware owner); drafted
  by Claude Code for WS-GOV ([log](WS-GOV.md), [1.1.13 board](../1.1.13.md))
- Number: placeholder. The commander assigns it at integration; 0074 to 0077
  are taken.
- Risk: R2 governance by path; the owner approves it as a change of approval
  authority, CI required checks and release policy.
- Supersedes (on acceptance): ADR 0054, ADR 0059, ADR 0061, ADR 0070,
  ADR 0071; consolidates the three ADR 0021 files.
- Amends: ADR 0033 through the [release workflow cleanup
  design](DESIGN-release-workflow-cleanup.md).
- Ports: the unmerged 1.1.12 draft
  `feature/1.1.12/governance-reset:docs/handoff/1.1.12/ADR-DRAFT-governance-reset.md`
  and the owner's WS-GOV decisions 1-10 of 2026-09-25 (carried in the
  [log](WS-GOV.md#accepted-owner-decisions-carried-from-1112)). "Board
  decision N" means the numbered owner decisions in the
  [1.1.12 board](../1.1.12.md).

## Context

The product protections work and this ADR keeps them: one planner and
executor, checked half-open ranges, host-side write-range enforcement, Golden
execution against every release candidate, and CI-owned protected publication
([ADR 0033](../../adr/0033-ci-owned-stable-release-promotion.md)).

Around that core, the capability-reuse record system
([ADR 0054](../../adr/0054-finalize-capability-reuse-records.md),
[0059](../../adr/0059-trusted-initial-capability-checkpoint.md),
[0061](../../adr/0061-tree-transparent-merge-history-normalization.md),
[0070](../../adr/0070-bounded-local-r1-continuation.md),
[0071](../../adr/0071-final-integration-path-ownership.md); contract
[`capability-reuse-record.md`](../../governance/capability-reuse-record.md))
became the main cost and failure source of the development flow.

- **History replay.** For every sealed final record, every external-authority
  attestation, the trusted checkpoint and each retired legacy record,
  `scripts/validate_repository.py` (`_record_changed_in_commits_after`) walks
  `git rev-list --ancestry-path <revision>..HEAD` and reads each commit with
  `git diff-tree -m`. A merge that lists an audited path against any parent
  fails unless ADR 0061's one exception holds: two parents, the merge tree
  equals exactly one parent, and the other parent is an ancestor of that
  parent (`_is_tree_transparent_containment_merge`). At `8682dd269` the
  repository holds 342 records and 108 attestations (327 and 103 at `v1.1.11`).
- **Release re-run conflict, 1.1.12.** Release pull request #449 merged into
  `main` as `405603dbe`, then the release run stopped on unresolved P1 review
  threads. The fixes were finalized on `1.1.12` (#454). The next release pull
  request (#455) failed the "PR head contains the exact reviewed base" check,
  and merging `main` into `1.1.12` failed the history audit: that merge lists
  the newly finalized records against `405603dbe`, which is not an ancestor of
  the tree-equal side. The release shipped only after a fresh candidate branch
  re-finalized the fixes under a new owner attestation (#456)
  ([bug](../bugs/BUG-20260926-release-rerun-history-conflict.md)).
- **Every release under the trunk model.** Merging a release's `main` merge
  back into a diverged `1.1.x` lists every record finalized on the trunk since
  the release cut against the `main` parent, so the audit fails at every
  release, not only after a stopped one (board, "Release re-run analysis").
  Board decision 23 contains it for now: the trunk is rebased, never merged,
  onto `main`, and fast-forwarded to `main` after each release before anything
  is finalized again. In effect the trunk cannot finalize during a release.
- **Parallel work serializes on one checkpoint.** Each merged pull request that
  seals records creates a new evidence checkpoint (after #457: `84b084dd8`).
  An admission must name the latest checkpoint; one made on an older checkpoint
  cannot be reconciled after the newer one is merged in and must be rewritten
  (board working rules, 2026-09-26). Every seal forces the other R2-R3
  workstreams in flight to realign.
- **Cost that grows with history.** Structure validation replays the whole
  record history: 82 s median in CI and 150-240 s locally for
  documentation-only changes (2026-09-25). On 2026-09-01 the history scan used
  the whole 600-second lane limit, and v1.1.0 shipped under waiver
  `REL-110-FULL-VERIFY-OWNER-WAIVER-01`.
- **Ceremony that is red by design.** A reviewed head fails the final gate
  until a separate direct-child evidence commit. One-task exceptions sit in the
  permanent contract: one delivery document, two cutover SHAs and one task's
  auxiliary-path reconciliation. ADR 0061 is implemented and was
  release-owner attested (`GOV-MERGE-TOPOLOGY-110-01`), yet its status line
  still reads Proposed.
- **Identity.** Reviewer independence is a case-insensitive comparison of agent
  labels. Agents push and open pull requests under the owner's GitHub identity,
  so GitHub cannot tell the owner from an agent, and the owner cannot approve a
  pull request that account authored. Every release therefore uses the owner
  self-approval exception of ADR 0033.
- **Contracts cite the evidence by path.** The runtime-pinned capability policy
  `docs/contracts/canonical-capability-policy-v1.json` (SHA-256 fixed in
  `BuiltInCanonicalCapabilityPolicy.ExpectedSha256`) and the canonical Golden
  manifest (`contractReference`, checked for existence by
  `scripts/canonical_golden_validation.py`) cite three `CTRLRAM-AB-*` records;
  the v0.9.16 parity plan cites two records and two attestations.

The owner decided on 2026-09-25 to retire the record system (1.1.12 WS-GOV
decisions 1 and 2) and on 2026-09-26 named the retirement of history replay as
the root fix of the release re-run conflict (board decision 23). The 1.1.12
draft was never reviewed: WS-GOVREV was dispatched but committed no findings.
This draft ports it, corrects it for the evidence above, and answers the
WS-GOVREV questions in the [log](WS-GOV.md#ws-govrev-questions-self-check) as
a self-check, which does not replace the independent review.

## Decision drivers

- Guard outcomes (bytes, ranges, Golden output, release artifacts, protected
  publication), not the paperwork of other gates.
- Use platform primitives (Git, pull requests, required checks, rulesets,
  protected environments) before re-implementing them in scripts.
- Validation cost grows with the change, not with repository history.
- Parallel work never waits on a repository-wide checkpoint.
- Historical evidence stays immutable, and contracts that cite it keep
  resolving.
- Every rule has one canonical owner; every workflow works with one agent
  runtime.

## Considered options

1. Extend ADR 0061 to a two-level containment merge (analysis option B).
   Covers the 1.1.12 topology only: a trunk merge-back where both sides carry
   new records still fails, and checkpoint serialization stays.
2. Keep records but audit content instead of history: each sealed record must
   still equal its first final blob at `HEAD`. Removes the merge sensitivity,
   but keeps the direct-child evidence commit, the red reviewed head and the
   checkpoint serialization.
3. Relax the pull-request base check (analysis option C). Hides a real base
   mismatch and still needs option 1. Rejected.
4. **Retire records, attestations and history replay; freeze the existing
   evidence; move admission facts into the pull request and R3 approval onto a
   platform-recorded owner action.** Selected; this is 1.1.12 WS-GOV decision 1.

## Decision

### Evidence and admission

1. The validator stops reading, validating and replaying capability-reuse
   records, external-authority attestations, the trusted checkpoint and
   retired legacy records. History audit
   (`_record_changed_in_commits_after`, `_read_commit_path_batch`,
   `_is_tree_transparent_containment_merge`), checkpoint derivation, path
   coverage and minimum-risk classification, the two cutover constants and the
   reviewer-name comparison are deleted with their tests. No gate walks
   repository history. The pull-request base check (`git merge-base
   --is-ancestor`) stays.
2. The existing evidence is **frozen in place** (owner decision O-1):
   `docs/governance/change-records/`,
   `docs/governance/external-authority-attestations/`,
   `docs/governance/waivers/` and
   `docs/governance/trusted-initial-capability-checkpoint.v1.json` keep their
   paths, and each directory gains a README that names it historical. The
   validator compares each frozen path's Git tree or blob ID at `HEAD`, and a
   clean index and worktree for it, with a pinned constant; any addition,
   change or deletion fails. The check costs one `git rev-parse` per path.
3. The pull request is the admission record. The template gains fields for
   outcome and non-goals, risk, owner search with the semantic owner and
   disposition (`reuse`, `extend-owner`, `reject-duplicate`), affected
   authority and paths, narrow tests and final gate, the review mode used and
   its reviewer, and R3 evidence. Workstream logs in `docs/handoff/` keep the
   working state; Git and pull-request history keep provenance.
4. R0-R3 stay the vocabulary for choosing evidence, reviewers and approvals
   (root [`AGENTS.md`](../../../AGENTS.md)).

### Review and approval

5. An R1-R2 change is reviewed by another agent runtime. When that runtime is
   unavailable (token limits, or a developer with one runtime), a fresh session
   of the same runtime reviews, preferably with another model, and never shares
   the author's conversation. The pull request states the mode. No process
   requires two agent products (1.1.12 WS-GOV decision 3).
6. An R3 change needs the owner's approval of the exact reviewed head,
   recorded by GitHub rather than in a committed file (owner decision O-2).
   Recommended: agents act under a separate GitHub identity; CODEOWNERS lists
   the R3 and governance paths and drops the catch-all `*` (today's explicit
   entries plus `/testdata/golden/` and `/VERSION`, which the validator treats
   as R3 but CODEOWNERS covers only through `*`); the ruleset requires
   code-owner review. The owner then approves agent pull requests as an
   ordinary reviewer, and the release no longer needs the self-approval
   exception. Firmware-semantic R3 still needs firmware-owner
   review, byte and Golden evidence and the exact write-range audit; release R3
   still needs release-owner evidence (root `AGENTS.md`, unchanged).

### Branches, releases and CI

7. Branch model (1.1.12 WS-GOV decision 10, in force since board decision 22):
   `main` holds released code and `v*` tags; the minor-line trunk (`1.1.x`)
   receives `feature/<version>/<topic>` pull requests; a release branch `X.Y.Z`
   is cut from the trunk at feature freeze, takes release fixes (merged back
   into the trunk), opens the release pull request into `main` with a merge
   commit and is deleted after its tag; after publication `main` is merged back
   into the trunk as an ordinary merge. With item 1 these merges no longer
   conflict with sealed evidence; board decision 23 ends when item 1 lands.
8. When a release run fails after its release pull request merged, a fix
   branch that contains `main` goes to `main` by pull request and a new run is
   dispatched; history is never rewritten, and the Release Closure Record lists
   every failed run. So that review findings cannot first surface after the
   merge, a release pull request cannot merge with an unresolved P0/P1 review
   thread (owner decision O-3).
9. CI is tiered (1.1.12 WS-GOV decision 4): feature-branch pushes run nothing;
   pull requests into the trunk run the test projects mapped from changed paths
   and skip documentation-only changes; the full suite with Golden runs when a
   release branch is cut, on the release pull request into `main`, on every
   push to `main` (the release admission requires that run), weekly on the
   trunk when it changed, and on manual dispatch. Test shards reuse one build.
   Always-run aggregators report every required check name, so skipped work
   never leaves a required check pending.
10. Required checks (amends 1.1.12 WS-GOV decision 6; owner decision O-4):
    `policy / polytail` is renamed after what it runs (for example
    `repository / structure`). `python-worker / verify` is renamed (for example
    `python / repository-scripts`) and **stays required**: it aggregates the
    repository-script shards that test the release policy, Golden validation and
    the validator. Only its CRC-worker lane leaves, when WS-FLOW F11 retires
    the worker. The rename, the closed check set in
    `scripts/release_promotion_policy.py`, the `main` ruleset and every
    document that names the checks (`.github/AGENTS.md`, the workflows README,
    `docs/ci/pull-request-ci.md`, ADR 0033) change in one step, with no release
    in between.
11. The release workflow is cleaned up under its own R3 design
    ([release workflow cleanup](DESIGN-release-workflow-cleanup.md)). This ADR
    sets only the flow rules above.

### Size and agent instructions

12. Size policy (1.1.12 WS-GOV decision 5): an aggregate with at least 2,000
    nonblank lines may not grow unless the owner approves that growth in the
    pull request, and it leaves the list below 1,500. Everything else gets one
    advisory report. The three ADR 0021 files become this rule; the allocation
    machinery is removed.
13. Agent instructions and skills follow the 1.1.12 WS-AI decisions 1-5: one
    root `AGENTS.md`; `.agents/skills/` canonical with derived `.claude/`
    projections (extends ADR 0068); the `nfc-` skill prefix; 23 skills reduced
    to 18; `code-review` and `polytail` merged into `nfc-review`; the hybrid
    interview style. The WS-AI port confirms or amends this item. It lands in
    the same batch because it edits the same governed paths (1.1.12 board
    decision 2: one ADR, one batch).

## Canonical owners after the reset

| Rule | Owner |
| --- | --- |
| Risk classes, review rule, single-runtime rule, pointers | root `AGENTS.md` |
| Execution sequence, pull request fields, path-to-test map | `docs/governance/development-execution-workflow.md` |
| Branch model, naming, release closure and recovery | `docs/governance/branch-version-and-release-governance.md` |
| R3 path map | `.github/CODEOWNERS` |
| Frozen evidence pins | `scripts/validate_repository.py` (one constant per frozen path) |
| Size policy | this ADR and `scripts/code_size_policy.py` |
| Release contract | `docs/ci/release-package.md`, ADR 0033 as amended |
| Version allocation, parity schedule | `docs/architecture/nfc_roadmap.md` |
| Skill inventory and routing | `.agents/skills/manifest.json`, `docs/governance/agent-skill-routing.md` |
| Live state and bugs | `docs/handoff/README.md` |

## Consequences

### Positive

- Merges between `main`, the trunk and release branches stop failing on sealed
  evidence; a stopped release is fixed with an ordinary pull request.
- Structure validation stops growing with history, and parallel workstreams
  stop realigning on each other's checkpoints.
- The reviewed head is the mergeable head; there is no evidence commit.
- Owner approval and review identity come from GitHub, not from label strings.

### Negative / trade-offs

- The machine-checked "admission before implementation" ledger ends. Pull
  request fields and history carry that provenance, and their completeness is
  reviewed, not validated.
- Content-based R3 outside the CODEOWNERS paths (for example a firmware-semantic
  change in `src/`) relies on the declared risk and on review. Today the
  validator also enforces only a path-based minimum, so the author already
  makes this judgement.
- Path-mapped CI can miss a cross-cutting interaction until the next full run
  (release pull request, push to `main`, weekly trunk run).
- After further work lands under the new rules, reverting to the record system
  needs a new trusted checkpoint (ADR 0059 activation). The decision is
  effectively one-way.

### Risks and mitigations

- A same-runtime reviewer shares blind spots -> fresh session, another model,
  review mode stated in the pull request.
- Agents could act with the owner's credentials -> O-2 separates identities;
  without it the procedural rule stays that agents never approve, attest or
  merge for the owner.
- A renamed required check blocks merges until the ruleset changes -> the owner
  changes the ruleset in the same window, with no release in between.
- In-flight records at the cut-over -> the transition rule in migration step 3.

## Compatibility and migration

1. Sequence: the CI failure-evidence change merges first (it owns `ci.yml` and
   `scripts/verify.py` now). Then batch G1: validator retirement and freeze
   pins with their tests, `AGENTS.md`, the runbook, branch governance, the
   record contract marked Historical, the pull request template, CODEOWNERS
   (O-2), ADR status links, and the WS-AI instruction and skill changes. Then
   batch G2 (R3): CI tiers and the required-check rename with the ruleset. The
   release workflow batches follow their own design.
2. G1 admission (owner decision O-5). Recommended: no final record, because the
   validator that would check it is what G1 removes. The owner's acceptance of
   this ADR is the authority; the independent design and fixed-head reviews are
   recorded in the pull request; one transition run of the base commit's
   validator shows that every record at the base is sealed and valid.
   Alternative: two steps, the ADR admitted and sealed alone, then the
   mechanism.
3. In-flight records: records sealed before G1 merges stay frozen. A branch
   with an unsealed `design-active` record either seals and merges before G1,
   or removes the record when it merges G1 and carries its admission facts into
   its pull request. If another seal lands first, G1 merges the trunk and
   re-pins.
4. Status links: ADRs 0054, 0059, 0061, 0070 and 0071 become Superseded with a
   link here; the three ADR 0021 files become one; `capability-reuse-record.md`
   becomes Historical and stays to read the frozen records.
5. GitHub settings change only with owner approval and never during a release:
   required-check names, conversation resolution (O-3), code-owner review and
   the agent identity (O-2), the branch-name allowlist and automatic branch
   deletion (checklist A-5), and force-push protection for the trunk and
   release branches.

## Verification

- The validator runs no `rev-list` or `diff-tree` (today every such call
  belongs to the record code); a test fails if it does. Freeze-pin tests:
  adding, changing, deleting or renaming a frozen file fails; a clean tree
  passes.
- Scratch-repository topology tests pass structure validation: the 1.1.12
  re-run (`main` merged into a release branch after a later finalization) and a
  trunk merge-back with new commits on both sides.
- Contract references still resolve: canonical Golden validation and the
  capability policy load unchanged, and no contract or Golden byte changes.
- Structure validation time is recorded before and after on the same machine.
- Links resolve, and each rule in the owner table has exactly one owner.
- The first release after G1 merges `main` back into the trunk without a
  conflict.

## Open owner decisions

Recommendations first; each needs an explicit owner answer.

- **O-1 Frozen evidence location.** Recommended: freeze in place with pins.
  This reverses 1.1.12 WS-GOV decision 2 (move to `docs/governance/archive/`),
  because moving would change the runtime-pinned capability policy and the
  Golden manifest (R3 capability and Golden authority) for no product benefit,
  or split the history across two directories.
- **O-2 R3 approval mechanism.** Recommended: a separate agent GitHub identity
  plus code-owner review on the CODEOWNERS paths. Alternative: keep one
  identity; the owner approves R3 heads through a protected environment or an
  exact-head comment that a required check reads, with the same procedural
  trust as today's attestations. The 1.1.12 draft's "owner approval through
  CODEOWNERS" cannot work while the owner's account authors every pull request.
- **O-3 Pre-merge review-thread gate.** Recommended: the `main` ruleset's
  "require conversation resolution" (no code). Alternative: a readiness check
  that reuses the release policy's P0/P1 classification (code, a new required
  check, and `ci.yml` changes).
- **O-4 `python-worker / verify`.** Recommended: keep it required as the
  repository-script aggregate (amends 1.1.12 WS-GOV decision 6, which would
  have made release-policy and Golden-validation tests non-blocking).
- **O-5 G1 admission.** Recommended: without a final record, as in migration
  step 2.
- **O-6 Batches and timing.** Board decision 2 asked for one batch; this
  draft splits it into G1 and G2 because `ci.yml` now has another writer (the
  CI failure-evidence change) and the check rename needs its own ruleset
  window. Recommended: G1 lands before the 1.1.13 release branch is cut, if the
  independent review finishes in time; otherwise the 1.1.13 release runs under
  board decision 23 and G1 lands right after it.
- **O-7 Frozen directories.** Confirm that `docs/governance/waivers/` is frozen
  with the records; after G1 a waiver is a pull request statement.
