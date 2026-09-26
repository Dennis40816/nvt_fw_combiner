# ADR 00XX (draft): Retire capability-reuse history replay and reset the development flow

- Status: **Proposed** — revised after the independent design review; not
  implementation authority.
- Date: 2026-09-26 (revised the same day)
- Owners: repository owner (governance, release and firmware owner); drafted
  by Claude Code for WS-GOV ([log](WS-GOV.md), [1.1.13 board](../1.1.13.md))
- Review: independent design review by `codex/gpt-6-astra` at `e60ba0062`,
  ACCEPT-WITH-CHANGES (P1 F-1 to F-7, P2 F-8 and F-9). This revision takes in
  every technical correction ([log](WS-GOV.md#design-review-2026-09-26)); the
  owner decisions are listed, in risk order, at the end of the log.
- Number: placeholder. The commander assigns it at integration; 0074 to 0077
  are taken.
- Risk: R2 governance by path; every part that changes approval authority is
  R3.
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
  parent (`_is_tree_transparent_containment_merge`). The repository holds 342
  records (all final-complete) and 108 attestations (327 and 103 at `v1.1.11`).
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
  Board decision 23 contains it for now: the trunk's first push is rebased,
  never merged, onto `main`, and after every release pull request the trunk is
  fast-forwarded to `main` before anything is finalized again. In effect the
  trunk cannot finalize during a release.
- **Trunk merged into a working branch, 2026-09-26.** `feature/1.1.13/wave2`
  merged `1.1.x` after #457 sealed `CLI-REPORT-BUNDLE-GUARD-1113-01`
  (`beb32b930`). Wave 2 had documentation-only commits of its own, so the merge
  tree equals neither parent: the audit reports the byte-identical record as
  changed, and every descendant fails the structure gate
  ([bug](../bugs/BUG-20260926-trunk-merge-flags-sealed-record.md)). Wave 2 and
  its branches were rebuilt by rebase; merging the trunk into a branch is now
  forbidden.
- **Parallel work serializes on one checkpoint.** Each merged pull request that
  seals records creates a new evidence checkpoint (after #457: `84b084dd8`).
  An admission must name the latest checkpoint; one made on an older checkpoint
  cannot be reconciled after the newer one is brought in and must be rewritten
  (board working rules, 2026-09-26). Every seal forces the other R2-R3
  workstreams in flight to realign.
- **Cost that grows with history.** Structure validation replays the whole
  record history: 82 s median in CI and 150-240 s locally for
  documentation-only changes (2026-09-25), and 268 s locally for this draft's
  first documentation-only commit (2026-09-26). On 2026-09-01 the history scan
  used the whole 600-second lane limit, and v1.1.0 shipped under waiver
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
This draft ports it and corrects it for the evidence above and for the
independent design review of 2026-09-26. Removing history replay removes this
class of merge and re-run conflicts; it does not remove ordinary merge
conflicts or other release failures.

## Decision drivers

- Guard outcomes (bytes, ranges, Golden output, release artifacts, protected
  publication), not the paperwork of other gates.
- Use platform primitives (Git, pull requests, required checks, rulesets,
  protected environments) before re-implementing them in scripts.
- Validation cost grows with the change, not with repository history.
- Parallel work never waits on a repository-wide checkpoint.
- No gate is switched off before its replacement is in force.
- An approval binds an exact head, a named authority role and its evidence.
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
   evidence; keep a history-independent authority map; enforce review and R3
   approval through exact-head, role-bound platform records.** Selected; this
   is 1.1.12 WS-GOV decision 1, completed by the review corrections.

## Decision

### Evidence and admission

1. The validator stops parsing, validating and replaying capability-reuse
   records, external-authority attestations, the trusted checkpoint and
   retired legacy records. History audit (`_record_changed_in_commits_after`,
   `_read_commit_path_batch`, `_is_tree_transparent_containment_merge`),
   checkpoint derivation, record path coverage, the two cutover constants and
   the reviewer-name comparison are deleted with their tests. No gate walks
   repository history. Path classification survives without history (item 4);
   the pull-request base check (`git merge-base --is-ancestor`) stays.
2. The existing evidence is **frozen in place** (owner decision O-1):
   `docs/governance/change-records/`,
   `docs/governance/external-authority-attestations/`,
   `docs/governance/waivers/` and
   `docs/governance/trusted-initial-capability-checkpoint.v1.json` keep their
   paths. Each directory first gains a README that names it historical; then
   the validator pins each frozen path's Git tree or blob ID and requires the
   same ID at `HEAD` and a clean index and worktree for it, so any addition,
   change or deletion fails. The check costs one `git rev-parse` per path. The
   guarantee changes: the pins prove that the current content equals the frozen
   snapshot; they no longer prove that no commit ever changed and restored a
   file. Changing a pin is a `governance` change under item 4.
3. The pull request carries the admission evidence; it is not enforcement. The
   template records outcome and non-goals, declared risk and class, owner
   search with the semantic owner and disposition (`reuse`, `extend-owner`,
   `reject-duplicate`), affected authority and paths, narrow tests and final
   gate, the review record (item 6), R3 evidence, and any waiver (item 15). The
   reviewer confirms in writing the byte, range, order, integrity and support
   impact (or its absence), the existing semantic owner, callers and typed
   contract, and the test evidence. Enforcement comes from the authority check
   (item 4), the review and approval checks (items 6 and 7), the rulesets
   (item 8) and CI.
4. **Authority path map.** One canonical, machine-readable map (for example
   `docs/governance/authority-path-map.json`) classifies paths of the current
   tree, never history. It is conservative: it may over-classify, never
   under-classify.
   - `firmware` (R3, firmware owner): `profiles/`, `testdata/golden/`,
     `tools/crc-worker/`, the firmware-semantic source owners (initially
     `src/NvtFwCombiner.Domain/`, `src/NvtFwCombiner.Profiles/`,
     `src/NvtFwCombiner.Contracts/`, and the `Composition`, `ExternalTools`,
     `FlashMaps`, `Metadata` and `Capabilities` folders of
     `src/NvtFwCombiner.Application/` and `src/NvtFwCombiner.Infrastructure/`;
     the implementation confirms the list with the firmware owner), and
     firmware contracts such as the capability policy and the parity plan.
   - `release` (R3, release owner): `.github/workflows/`, `.github/actions/`,
     `VERSION`, `docs/ci/release-package.md`, release contracts, and today's
     release, packaging and signing scripts (`CAPABILITY_REUSE_R3_SCRIPTS`).
   - `governance` (R2, owner approval): every `AGENTS.md`, `.agents/skills/`,
     `docs/governance/`, `docs/policies/`, `docs/adr/`, `docs/specs/`,
     `SPEC.md`, the roadmap, `.github/CODEOWNERS`, the map itself, the frozen
     pins, `scripts/validate_repository.py`, `scripts/verify.py` and the other
     scripts.
   - `code` (R1): the rest of `src/`, `tests/` and `eng/`.
   - `prose` (R0): an explicit allowlist of ordinary prose that no test or
     script reads (initially `docs/handoff/` Markdown).
   - `unclassified`: any path the map does not match.
   The authority check diffs the pull request base against its head (two
   points; deletions and both sides of a rename count). It fails when the
   declared risk is below the highest class touched, when an `unclassified` or
   cross-class change lacks a written manual classification in the pull
   request, or when an approval required by item 7 is missing. A
   firmware-semantic change found outside the `firmware` class is R3 by that
   classification. `.github/CODEOWNERS` becomes a derived projection of the map
   (ADR 0068 synchronization), so the two cannot drift.
5. R0-R3 stay the vocabulary for choosing evidence, reviewers and approvals
   (root [`AGENTS.md`](../../../AGENTS.md)).

### Review and approval

6. **Independent review, R1 to R3.** Another agent runtime reviews; when it is
   unavailable (token limits, or a developer with one runtime), a fresh session
   of the same runtime reviews, preferably with another model, and never shares
   the author's conversation. No process requires two agent products (1.1.12
   WS-GOV decision 3). The evidence is one exact-head review record on the pull
   request with the full 40-character head SHA, the reviewer's runtime and
   model identifier (for example `codex/gpt-6-astra`), the review mode, the
   verdict with its open P0/P1 count, and a completed state. GitHub records the
   principal that posted it; the runtime identifier and the GitHub principal
   stay separate fields, and neither is inferred from the other. A new head
   needs a new record. Independence between agent sessions is procedural:
   GitHub proves who posted, and for which head, not which session reviewed.
7. **R3 human authority.** An R3 change needs the item 6 review **and** an
   approval from each applicable authority: the firmware owner for `firmware`
   (including a firmware-semantic change classified manually), the release
   owner for `release`, both when both are touched. An approval counts only if
   it:
   - is recorded by GitHub on the exact head, with its full SHA: an approving
     review from the owner's principal, or, if O-2 keeps one shared identity,
     the owner-only channel chosen there;
   - names its authority role and links the evidence it approves: the item 6
     review, and for `firmware` the byte and Golden evidence and the exact
     write-range audit, for `release` the release-owner evidence;
   - is current: a new head dismisses it (ruleset `dismiss_stale_reviews_on_push`
     and `require_last_push_approval`), and the authority check compares its
     commit with the head.
   One person may hold both owner roles, but then approves each role
   explicitly, each with its own evidence. Firmware-semantic R3 still needs the
   firmware-owner review, byte and Golden evidence and the exact write-range
   audit of root `AGENTS.md`; release R3 still needs release-owner evidence.
8. **Execution boundaries.**

   | Boundary | Gates |
   | --- | --- |
   | Pull request into the trunk (`1.1.x`) or a release branch | required checks (items 13 and 14), authority check (item 4), exact-head review (item 6), R3 approvals (item 7), conversation resolution (item 12) |
   | Release pull request into `main` | the same over the whole release diff, the full suite with Golden, the release owner's approval of the exact release head, an up-to-date base |
   | Release workflow from `main` | ADR 0033 admission at the candidate, pre-tag and pre-Release boundaries; the protected `release` environment |

   Rulesets on `main` and on `*.*.*` (the trunk and release branches) require
   pull requests, the required checks, `dismiss_stale_reviews_on_push`,
   `require_last_push_approval`, code-owner review from the derived
   CODEOWNERS, conversation resolution, no bypass actor, and block force pushes
   and deletion.
9. **No gate goes before its replacement.** The record gate stays active until
   the rulesets of item 8 and the checks of items 4, 6 and 7 are in force and
   have passed on at least one real pull request. The migration splits G1
   accordingly, and every G1 part that changes approval authority is R3.

### Branches, releases and CI

10. Branch model (1.1.12 WS-GOV decision 10, in force since board decision 22):
    `main` holds released code and `v*` tags; the minor-line trunk (`1.1.x`)
    receives `feature/<version>/<topic>` pull requests; a release branch
    `X.Y.Z` is cut from the trunk at feature freeze, takes release fixes (merged
    back into the trunk), opens the release pull request into `main` with a
    merge commit and is deleted after its tag; after publication `main` is
    merged back into the trunk as an ordinary merge. With item 1 these merges no
    longer conflict with sealed evidence; board decision 23 ends when G1-B
    lands.
11. **Release recovery follows the tag and Release state:**
    - No tag yet: fix through a pull request into `main` and dispatch a new
      run.
    - Tag created, publication incomplete and recoverable: re-run only the
      failed promotion in the same run. Its checks (same run id, same candidate
      artifact and digest, live authority with `main` still at the workflow
      SHA) must still hold, so nothing merges into `main` first. The
      first-publication floor ("tag absent") does not apply here; the
      existing-tag validation does.
    - Immutable Release incomplete or conflicting: never repaired in place; the
      owner decides a new version.
    While a tagged release is incomplete, nothing merges into `main`: advancing
    `main` ends same-run recovery, and a new run cannot reuse the tagged
    version. A new release checks the floor at the candidate and again at the
    pre-tag boundary. History is never rewritten, and the Release Closure
    Record lists every failed run.
12. A pull request, including the release pull request, cannot merge with an
    unresolved review thread (owner decision O-3), so findings do not first
    surface after the merge. Resolved threads do not prove that a review
    finished; the completed item 6 record on the exact head does. The three
    release boundaries keep their P0/P1 check.
13. **CI tiers** (1.1.12 WS-GOV decision 4, corrected):
    - Every pull request runs the structure lane: links, frozen pins, contracts
      and schemas, the authority check, and the document checks that today
      live in `Architecture.Tests` (the 2,500-line ceiling over
      `docs/**/*.md`, which failed the 1.1.12 roadmap edit, and the roadmap
      assertions). They move into the structure lane or stay mapped to the
      documents they read.
    - Only `prose`-class paths skip product tests. Every other change runs the
      test projects mapped from its paths; a change the mapping cannot classify
      runs the full suite.
    - The full suite with Golden runs when a release branch is cut, on the
      release pull request into `main`, on every push to `main` (release
      admission requires that run), weekly on the trunk when it changed, and on
      manual dispatch. Test shards reuse one build.
    - Each required check comes from an always-run aggregator that passes only
      when every required producer succeeded or the mapping declared it not
      applicable for that exact head; a missing, failed, cancelled or
      unexpectedly skipped producer fails it.
    - Negative tests cover changed-path mapping, deletions and renames on both
      sides, cross-layer dependencies, an unclassified path, and a cancelled or
      skipped producer.
14. Required checks (amends 1.1.12 WS-GOV decision 6; owner decision O-4):
    `policy / polytail` is renamed after what it runs (for example
    `repository / structure`). `python-worker / verify` is renamed (for example
    `python / repository-scripts`) and **stays required**: it aggregates the
    repository-script shards that test the release policy, Golden validation and
    the validator. Only its CRC-worker lane leaves, when WS-FLOW F11 retires
    the worker. The rename, the closed check set in
    `scripts/release_promotion_policy.py`, the `main` ruleset and every
    document that names the checks (`.github/AGENTS.md`, the workflows README,
    `docs/ci/pull-request-ci.md`, ADR 0033) change in one maintenance window,
    with no release in between.
15. **Waivers.** A waiver is a pull request statement bound to its head SHA and
    scope. It names the rule or tool, scope, reason, risk, owner, issue,
    approver, creation and expiry dates and removal condition, and the
    approver is the owner of the authority waived, approving under item 7. No
    waiver may weaken firmware range safety, processor write ranges, integrity
    order, secrets or signing, release allowlists, or independent Golden
    expectations. These rules move unchanged from `docs/policies/polytail.md`
    into the runbook; the new location grants no wider waiver power.
16. The release workflow is cleaned up under its own R3 design
    ([release workflow cleanup](DESIGN-release-workflow-cleanup.md)). This ADR
    sets only the flow rules above.

### Size and agent instructions

17. Size policy (1.1.12 WS-GOV decision 5): an aggregate with at least 2,000
    nonblank lines may not grow unless the owner approves that growth in the
    pull request, and it leaves the list below 1,500. Everything else gets one
    advisory report. The three ADR 0021 files become this rule; the allocation
    machinery is removed.
18. Agent instructions and skills follow the 1.1.12 WS-AI decisions 1-5: one
    root `AGENTS.md`; `.agents/skills/` canonical with derived `.claude/`
    projections (extends ADR 0068); the `nfc-` skill prefix; 23 skills reduced
    to 18; `code-review` and `polytail` merged into `nfc-review`; the hybrid
    interview style. The WS-AI port confirms or amends this item; it lands with
    G1-B because it edits the same governed paths.

## Canonical owners after the reset

| Rule | Owner |
| --- | --- |
| Risk classes, review rule, single-runtime rule, pointers | root `AGENTS.md` |
| Execution sequence, pull request fields, review and approval records, waivers | `docs/governance/development-execution-workflow.md` |
| Authority classes and paths | the authority path map; `.github/CODEOWNERS` is derived from it |
| Branch model, rulesets, naming, release closure and recovery | `docs/governance/branch-version-and-release-governance.md` |
| Frozen evidence pins | `scripts/validate_repository.py` (one constant per frozen path) |
| Size policy | this ADR and `scripts/code_size_policy.py` |
| Release contract | `docs/ci/release-package.md`, ADR 0033 as amended |
| Version allocation, parity schedule | `docs/architecture/nfc_roadmap.md` |
| Skill inventory and routing | `.agents/skills/manifest.json`, `docs/governance/agent-skill-routing.md` |
| Live state and bugs | `docs/handoff/README.md` |

## Consequences

### Positive

- Merges between `main`, the trunk and release branches stop failing on sealed
  evidence; a stopped release follows one of three written recovery paths.
- Structure validation stops growing with history, and parallel workstreams
  stop realigning on each other's checkpoints.
- The reviewed head is the mergeable head; there is no evidence commit.
- Approvals bind a head, a role and evidence, and GitHub dismisses them when
  the head changes.

### Negative / trade-offs

- The machine-checked "admission before implementation" ledger ends. Pull
  request fields carry that evidence, and their completeness is reviewed.
- The frozen evidence proves its current content, not its history.
- The conservative map over-classifies some changes, so the owner approves
  more pull requests than the path minimum strictly needs.
- Path-mapped CI can miss a cross-cutting interaction until the next full run.
- After work lands under the new rules, returning to the record system needs a
  new trusted checkpoint and new admissions (ADR 0059 activation), not a
  revert.

### Risks and mitigations

- A same-runtime reviewer shares blind spots -> fresh session, another model,
  runtime identifier recorded.
- Agents could act with the owner's credentials -> O-2 separates identities;
  with one shared identity the approval rests on procedural trust, and the
  documents say so instead of claiming separation.
- An author under-declares risk -> the map sets a floor by path; unclassified
  and cross-class changes need a written classification; the reviewer confirms
  the byte, range, order, integrity and support impact.
- A renamed required check blocks merges until the ruleset changes -> one
  maintenance window, no release in between.
- In-flight records at the cut-over -> the transition table below.

## Compatibility and migration

1. **Sequence.**
   - **G0 (owner, GitHub settings):** the O-2 identity arrangement and the
     item 8 rulesets for `main` and `*.*.*`, applied in a maintenance window,
     not during a release.
   - **G1-A (admitted under the current rules, with its own record):** the
     authority map and check, the review and approval checks, the derived
     CODEOWNERS and the pull request template. Both gates run side by side;
     G1-A must pass on at least one real pull request.
   - **G1-B (the cutover, O-5):** retire record validation, add the frozen
     pins, mark the record contract Historical, supersede the ADRs, move the
     rules into their owners, and land the WS-AI changes. Its
     approval-authority parts are R3.
   - **G2 (R3):** CI tiers and the required-check rename, after the CI
     failure-evidence change merges (it owns `ci.yml` and `scripts/verify.py`
     now).
   - The release workflow batches follow their design.
2. **G1-B admission (O-5).** The owner gives an explicit, one-time cutover
   authorization that binds the base SHA and the G1-B head SHA. The design and
   fixed-head reviews run on that head, and the base commit's validator passes
   at the base as transition evidence. That the new validator no longer reads
   records is not itself an exemption from the old rules.
3. **Transition states.**

   | State when G1-B merges | Handling |
   | --- | --- |
   | Sealed and merged (final record and attestations in G1-B's base) | Frozen by the pins; nothing changes. |
   | Sealed on a branch, not merged | Preferred: merge before G1-B, which then re-pins (step 4). Otherwise the branch keeps its sealed commits as history (their SHAs are cited in its pull request), rebases its product commits onto the trunk without adding files to the frozen paths, and passes the new gates; nothing pending becomes complete by the move. |
   | Active (`design-active`) | The record is dropped on rebase; its admission facts move to the pull request; the new gates apply. |
   | Blocked, or owner evidence still owed | Stays an open gate in the pull request or the board; the migration never closes it. At `e60ba0062` every record on the trunk line is final-complete, and the six open inherited authorities of the trusted checkpoint carry approving attestations. |

4. **Re-pin.** Whenever G1-B's base moves, it is rebased (never merged) onto
   the new base; the pins are recomputed from that base, the base validator is
   run there again, and the exact-head review and approvals are repeated.
5. **Status links.** ADRs 0054, 0059, 0061, 0070 and 0071 become Superseded
   with a link here; the three ADR 0021 files become one;
   `capability-reuse-record.md` becomes Historical and stays to read the frozen
   records.
6. **GitHub settings** change only with owner approval and never during a
   release: G0, the check rename window (G2), the branch-name allowlist and
   automatic branch deletion (checklist A-5).

## Verification

- The validator runs no `rev-list` or `diff-tree` (today every such call
  belongs to the record code); a test fails if it does.
- Freeze-pin tests: adding, changing, deleting or renaming a frozen file fails;
  a clean tree passes; a pin change is classified `governance`.
- Authority-check tests: each class; an unclassified path; a cross-class
  change; a rename or deletion counted on both sides; a declared risk below the
  class; a missing role approval; an approval on an older head; one person with
  both roles stating only one; an approval without evidence links.
- Scratch-repository topology tests pass structure validation: the 1.1.12
  re-run (`main` merged into a release branch after a later finalization), a
  trunk merge-back with new commits on both sides, and the trunk merged into a
  working branch after a seal (the `beb32b930` shape).
- Contract references still resolve: canonical Golden validation and the
  capability policy load unchanged, and no contract or Golden byte changes.
- G1-A passes on one real pull request while the record gate still runs.
- Structure validation time is recorded before and after on the same machine.
- The first release after G1-B merges `main` back into the trunk without a
  conflict.

## Open owner decisions

The [log](WS-GOV.md#owner-decisions-in-risk-order) lists them in risk order,
with options and consequences. Summary: O-1 freeze in place (recommended) or
move to an archive; O-2 separate agent identity (recommended) or one shared
identity with procedural trust; O-3 conversation resolution as the pre-merge
gate (recommended); O-4 keep the repository-script aggregate required
(recommended); O-5 one-time cutover authorization for G1-B; O-6 G0, G1-A,
G1-B and G2 in that order, with G1-B before the 1.1.13 release branch if the
reviews finish in time; O-7 freeze the old waivers and keep the waiver limits
of item 15.
