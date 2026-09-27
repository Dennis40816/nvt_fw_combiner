# ADR 0080 (draft): Retire capability-reuse history replay and reset the development flow

- Status: **Proposed** — revised after the third independent design review
  (2026-09-27) to the simpler authority check the owner chose (decision 105);
  awaiting the check of this revision and the owner's acceptance; not
  implementation authority.
- Date: 2026-09-26; revised 2026-09-27.
- Owners (board decision 102, three roles recorded separately, today all held
  by the repository owner): the **governance owner** (governance rules,
  permissions, approval policy), the **firmware owner** and the **release
  owner**. Drafted by Claude Code for WS-GOV ([log](WS-GOV.md),
  [1.1.13 board](../1.1.13.md)).
- Reviews: first design review by `codex/gpt-6-astra` at `e60ba0062`,
  ACCEPT-WITH-CHANGES (P1 F-1 to F-7, P2 F-8 and F-9;
  [log](WS-GOV.md#design-review-2026-09-26)); the two G0 checklist reviews of
  2026-09-26, both REJECT ([log](WS-GOV.md#g0-checklist-review-2026-09-26));
  second design review by `codex/gpt-6-astra` at `acbe5654d` against the rules
  of `1.1.x` `e6e991af3`, ACCEPT-WITH-CHANGES (P1 F-1 and F-2, P2 F-3 to F-6;
  [log](WS-GOV.md#design-re-review-2026-09-27)); third design review by
  `codex/gpt-6-astra` at `cb72ee0b0`, ACCEPT-WITH-CHANGES (P1 F-1 to F-3 on the
  authority check's platform wiring, P2 F-4;
  [log](WS-GOV.md#design-re-review-3-2026-09-27)). The owner answered the
  third review's P1 findings with decision 105, which replaces that wiring by
  an ordinary pull request check, GitHub's native last-push approval and
  named procedural safeguards.
- Owner decisions: 2026-09-26, board decisions 49 to 52, 56, 65 to 67, 77, 78,
  80 and 82 and, for the release side, 47 and 53 to 55; O-3 and O-4 confirmed
  in decision 67 ([owner decisions](#owner-decisions-2026-09-26)); 2026-09-27,
  decisions 100 to 102 and 105 ([below](#owner-decisions-2026-09-27)).
- Number: 0080, allocated on the [1.1.13 board](../1.1.13.md) on 2026-09-26.
- Risk: R3. The authority policy, the authority check and the approval rules
  change approval authority (governance owner); the new workflow and, in G2,
  the release policy's closed check set are release paths (release owner). No
  firmware byte, range, profile or Golden input changes.
- Effect of acceptance: accepting this ADR accepts the **staged design**
  ([migration step 0](#compatibility-and-migration)). It changes no rule by
  itself; every retirement clause takes effect only when an authorized G1-B
  merges.
- Supersedes, **effective when the authorized G1-B merges** (not on
  acceptance): ADR 0054, ADR 0059, ADR 0061, ADR 0070, ADR 0071; consolidates
  the three ADR 0021 files.
- Amends: ADR 0033 through the [release workflow cleanup
  design](DESIGN-release-workflow-cleanup.md).
- Related: ADR 0079 (test architecture, WS-TEST), accepted as board decision 81
  on 2026-09-26, owns test selection and partitions and replaces ADR 0027's
  300-second clauses (decision 72); its partition, selection and coverage
  changes take effect only through their own admitted batches and its T4b
  activation. Item 13's pull-request tier depends on that activation.
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
  parent (`_is_tree_transparent_containment_merge`). At `1.1.x` `e6e991af3`
  the repository holds 359 records, all final-complete, and 112 attestations
  (327 and 103 at `v1.1.11`).
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
  Board decision 23 contained it until G0: the trunk was fast-forwarded to
  `main` after every release pull request, before anything was finalized
  again. Since G0's trunk ruleset blocks direct pushes (2026-09-27), board
  decision 66's same-tree pull request merge is the current rule (item 10).
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
  documentation-only changes (2026-09-25), and 250-310 s locally for this
  draft's documentation-only commits (2026-09-26 and 27, WS-GOV checkpoints).
  On 2026-09-01 the history scan used the whole 600-second lane limit, and
  v1.1.0 shipped under waiver `REL-110-FULL-VERIFY-OWNER-WAIVER-01`.
- **Ceremony that is red by design.** A reviewed head fails the final gate
  until a separate direct-child evidence commit. One-task exceptions sit in the
  permanent contract: one delivery document, two cutover SHAs and one task's
  auxiliary-path reconciliation. ADR 0061 is implemented and was
  release-owner attested (`GOV-MERGE-TOPOLOGY-110-01`), yet its status line
  still reads Proposed.
- **Identity.** Reviewer independence is a case-insensitive comparison of agent
  labels. Until G0, agents pushed and opened pull requests under the owner's
  GitHub identity, so GitHub could not tell the owner from an agent and the
  owner could not approve a pull request that account authored; every release
  used the owner self-approval exception of ADR 0033. Since G0 (2026-09-27)
  agents act as the owner-created GitHub App ([state](#state-on-2026-09-27)),
  and the owner can approve their pull requests as an ordinary reviewer. The
  self-approval exception and the Codex-only review rule stay in the release
  policy until their replacement is built and tested (decision 49; release
  design R-3).
- **Contracts cite the evidence by path.** The runtime-pinned capability policy
  `docs/contracts/canonical-capability-policy-v1.json` (SHA-256 fixed in
  `BuiltInCanonicalCapabilityPolicy.ExpectedSha256`) and the canonical Golden
  manifest (`contractReference`, checked for existence by
  `scripts/canonical_golden_validation.py`) cite three `CTRLRAM-AB-*` records;
  the v0.9.16 parity plan cites two records and two attestations.

The owner decided on 2026-09-25 to retire the record system (1.1.12 WS-GOV
decisions 1 and 2) and on 2026-09-26 named the retirement of history replay as
the root fix of the release re-run conflict (board decision 23). On
2026-09-27 the owner put this reset, with the WS-TEST stages that shorten
verification, before release workflow cleanup R-1 (decision 100). The 1.1.12
draft was never reviewed: WS-GOVREV was dispatched but committed no findings.
This draft ports it and corrects it for the evidence above and for two
independent design reviews. Removing history replay removes this class of
merge and re-run conflicts; it does not remove ordinary merge conflicts, test
failures or other release failures.

### State on 2026-09-27

- **G0 applied, not fully accepted.** The owner-created GitHub App is installed
  on this repository only; agents push and open pull requests through the
  owner-installed token helper and `gh` wrapper (decisions 65, 80 and 82).
  Disposable pull request #460 was authored by the App, and the installation
  token lists only this repository. The three branch rulesets are applied and
  were read back against the reviewed request bodies: `main` (updated), the
  trunk `*.*.x` and release branches `*.*.*` (new); the tag ruleset is
  unchanged. GitHub added two defaults to the readback
  (`required_reviewers: []`,
  `require_extra_approval_for_unattributed_changes: true`), admitted by a
  recorded reconciliation ([log](WS-GOV.md#g0-local-execution-checkpoint--2026-09-27)).
- **Rules in force on the trunk and release branches:** pull request required;
  one approval; code-owner review (today's CODEOWNERS, `*` and every high-risk
  path to the owner); stale approvals dismissed on push; approval of the last
  reviewable push; conversation resolution; merge commits only; force pushes
  blocked (deletion also blocked on the trunk); the three existing required
  checks (`policy / polytail`, `python-worker / verify`, `dotnet / build-test`,
  not strict, not required on creation); the owner-only bypass as configured.
  `main` has the same review rules and keeps its required checks exactly as
  they were saved before G0.
- **Accepted since (board, batch 2c, 2026-09-27):** D0's actual external push
  (the batch 2c branch, pushed through the App helper from outside the Codex
  package); D4's owner approval of pull request #461 at its exact head after
  the last push; D5, the App's normal merge of #461 (`c524e7d2b`) without a
  bypass, after the required checks, that approval and the resolution of its
  one review thread. The checklist's D4 also observes the dismissal of an
  approval by a diff-changing push and what GitHub does with an
  identical-tree push; the board does not record those two observations,
  and the second now matters (item 7).
- **Still open:** D6 (owner bypass or pause), the decision 82 Bitwarden backup
  confirmation, the owner's C2 deletion of the protected disposable `9.9.x`,
  and A8. G0 is therefore applied and mostly, not fully, accepted.
- **Not yet in force:** the authority check (G1-A) and every retirement of
  G1-B.
- **Order (decision 100):** this ADR's acceptance, G1-A, G1-B and the WS-TEST
  stages come before release workflow cleanup R-1, which is paused with its
  draft kept. G1-A waits for neither R-1, G2 nor the CI failure-evidence
  change.

## Decision drivers

- Guard outcomes (bytes, ranges, Golden output, release artifacts, protected
  publication), not the paperwork of other gates.
- Use platform primitives (Git, pull requests, required checks, rulesets,
  protected environments) before re-implementing them in scripts.
- Validation cost grows with the change, not with repository history.
- Parallel work never waits on a repository-wide checkpoint.
- No gate is switched off before its replacement is in force.
- An approval binds an exact head, a named authority role and its evidence.
- Every safeguard states whether a machine enforces it or a procedure does;
  a procedural safeguard is never presented as a machine guarantee.
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
   evidence; keep a history-independent authority policy, checked by an
   ordinary pull request check; take the exact-head R3 approval from GitHub's
   native code-owner review of the last push; name the remaining gaps as
   procedural safeguards.** Selected; this is 1.1.12 WS-GOV decision 1,
   completed by the review corrections and decision 105.
5. First remove only the checkpoint rebinding, before G1-A. Not pursued: the
   checkpoint also fixes path coverage, the path-state digest and the
   reconciliation ancestry, so it needs a separate admission base and
   integration base and new ownership rules, while history replay, the
   evidence commit and re-review stay. The second review judged it likely
   slower than completing G1-A and G1-B; it is reconsidered only if G1-A is
   blocked for long.
6. Make the authority check tamper-resistant: a check whose workflow and
   policy a pull request cannot change, re-evaluated on review events, with a
   trusted result publisher for the exact head and invalidation of an earlier
   result on the same head. The third review found that this needs a
   default-branch bootstrap (a target-branch-context workflow is loaded from
   `main`, not from the trunk), a publisher with write permission that the
   candidate cannot influence, and a stale-result and concurrency contract.
   Not chosen (decision 105): the owner accepts the remaining risks as
   procedural safeguards instead. A later ADR can add such a boundary if a
   machine guarantee becomes necessary.

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
2. The existing evidence is **frozen in place** (O-1, board decision 51,
   which reverses the 2026-09-25 choice to move it to an archive folder):
   `docs/governance/change-records/`,
   `docs/governance/external-authority-attestations/`,
   `docs/governance/waivers/` and
   `docs/governance/trusted-initial-capability-checkpoint.v1.json` keep their
   paths. **Pin construction (one order):** G1-B adds one `README.md` naming
   each of the three directories historical, and nothing else under the
   frozen paths. The pins are the **G1-B final snapshot**: the Git tree ID of
   each directory at the G1-B head, which is its base tree plus that one
   README, and the blob ID of the checkpoint file, which equals its base blob.
   G1-B proves that the original evidence is unchanged:
   `git diff --name-status <base> <head> -- <frozen paths>` lists exactly the
   three README additions, and the checkpoint blob is identical at base and
   head. The expected pins are part of the cutover review and authorization
   (migration step 2). From G1-B on, the validator reads the pins from one
   data file (for example `docs/governance/frozen-evidence-pins.json`) and
   requires the same IDs at `HEAD` and a clean index and worktree for those
   paths, so any addition, change or deletion fails; the check costs one
   `git rev-parse` per path. The guarantee changes: the pins prove that the
   current content equals the frozen snapshot; they no longer prove that no
   commit ever changed and restored a file. The pin file is a governance R3
   path (item 4).
3. The pull request carries the admission evidence; it is not enforcement. The
   template records outcome and non-goals, the declared risk and any added
   roles (item 4), the implementation owner and the owned paths, owner search
   with the semantic owner and disposition (`reuse`, `extend-owner`,
   `reject-duplicate`), affected authority and paths, narrow tests and final
   gate, the review record (item 6), R3 evidence, and any waiver (item 15).
   The reviewer confirms in writing the byte, range, order, integrity and
   support impact (or its absence), the existing semantic owner, callers and
   typed contract, and the test evidence. Enforcement comes from the authority
   check (items 4 and 6 and
   [its safeguards](#authority-check-and-its-safeguards-g1-a)), GitHub's
   native approval rules (items 7 and 8) and CI; what they leave open is named
   there as procedural.

   **Single writer.** The records' exactly-once path coverage retires with
   them; the rule it enforced stays. Root `AGENTS.md` keeps "one writer per
   mutable surface", and from G1-B the execution workflow owns its procedure:
   every pull request names its implementation owner and owned paths; the
   commander assigns each mutable surface to one open workstream at a time on
   the board; another workstream that needs the surface waits for the merge,
   or takes it over through a handoff recorded on the board and in both pull
   requests after the earlier writer has stopped; an overlap found at review
   or rebase stops both writers until the commander decides. This is a
   procedural safeguard: CODEOWNERS routes reviews and is not a write lock,
   rulesets do not limit which paths a pull request touches, and Git merge
   conflicts detect only textual overlap.
4. **Authority policy.** One canonical, machine-readable file (for example
   `docs/governance/authority-policy.json`) holds the path map and the role
   principals (item 7). The map classifies paths of the current tree, never
   history, and is conservative: it may over-classify, never under-classify.
   Each entry has two independent fields: a **risk floor** (R0-R3) and a set of
   **authority roles** (`governance-owner`, `firmware-owner`,
   `release-owner`), empty unless the path needs a named R3 approval.
   - **Overlap.** Every entry that matches a path (after a default entry's
     exclusions, below) applies. The path's floor is
     the highest floor and its roles are the union of the roles. A change's
     floor is the highest over its paths and its roles the union over them.
     There is no first-match rule, and a cross-class change needs no written
     classification: the union is always the strictest reading.
   - **Changed paths.** The check takes the change the pull request brings:
     from the merge base of the live base-branch tip and the head, to the
     head. Additions, modifications, deletions and both sides of a rename or
     copy count.
   - **Additions only.** The author's declared roles in the pull request and
     the roles a reviewer adds in the item 6 record raise the requirement
     (for example a firmware-semantic change outside the firmware paths gets
     `firmware-owner`). No text removes a role or lowers a floor; only a
     governance change to the map does. A declared risk below the floor fails.
   - **Unclassified paths.** A path no entry matches has floor R3 and role
     `governance-owner`: the pull request declares that role with a proposed
     classification, which may add roles, and the owner's approval (item 7)
     accepts it. The author's text alone never clears it. G1-A's map covers
     every tracked path of its base tree, so this case arises only for new
     locations.
   - **No downgrade of today's governed paths.** For every tracked path of
     G1-A's base that the current validator governs
     (`_is_capability_reuse_governed_path`), the map's floor is at least that
     validator's floor (`_capability_reuse_minimum_risk`); a test proves it
     while those functions exist. The comparison covers only governed paths:
     for the others the old function returns a default R1 that never applied,
     so they are accepted by the explicit rules of this item instead (the
     prose list and its consumer limit, the R1 defaults, and the coverage of
     every exclusion).

   Initial map (G1-A fixes the exact patterns; where the table names a group,
   the implementation lists its members and the named owner confirms them).
   A "rest of" row is a default entry that excludes the paths of the more
   specific rows; exclusions exist only for such defaults, so that, for
   example, a prose file under `docs/handoff/` matches the prose row and not
   the R1 default for `docs/`. The coverage test proves that every excluded
   path is matched by another entry.

   | Paths | Floor | Roles |
   | --- | --- | --- |
   | `profiles/`, `testdata/` (Golden and every fixture), `refcode/`, `tools/crc-worker/` (caches excluded), `tests/NvtFwCombiner.GoldenRegression.Tests/`; the firmware-semantic source owners (initially `src/NvtFwCombiner.Domain/`, `src/NvtFwCombiner.Profiles/`, `src/NvtFwCombiner.Contracts/`, and the `Composition`, `ExternalTools`, `FlashMaps`, `Metadata` and `Capabilities` folders of `src/NvtFwCombiner.Application/` and `src/NvtFwCombiner.Infrastructure/`; confirmed by the firmware owner) | R3 | firmware |
   | `docs/contracts/`: each file firmware, release or both; a file the map does not name individually gets both | R3 | firmware and/or release |
   | `external-tools/` (external processors and their packaged allowlist) | R3 | firmware, release |
   | `.github/workflows/`, `.github/actions/`, `VERSION`, `docs/ci/release-package.md`, the package inputs `CHANGELOG.md`, `LICENSE` and `THIRD_PARTY_NOTICES.md`, and the release scripts of the table below | R3 | release |
   | Approval authority: the authority policy file and its schema, the authority checker, every repository file it imports or reads to reach its verdict (G1-A lists them, and a test proves the list against the checker's imports), its tests, the authority workflow (also release by its folder), `.github/CODEOWNERS`, root `AGENTS.md`, `docs/governance/development-execution-workflow.md`, `docs/governance/branch-version-and-release-governance.md`, `docs/policies/`, the frozen evidence paths and the pin file, and the G0 scripts and ruleset bodies (`docs/handoff/**/g0-scripts/`) | R3 | governance |
   | `.agents/`, `.codex/`, `.claude/`, every other `AGENTS.md`, the rest of `docs/governance/`, `docs/adr/`, `docs/specs/`, `SPEC.md` and the four canonical architecture documents, the rest of `.github/`, the rest of `scripts/`, `tests/scripts/`, `eng/`, `third-party/`, `CONTRIBUTING.md`, `SECURITY.md`, root build and repository configuration (`Directory.*`, `global.json`, `NuGet.config`, `*.slnx`, `.gitattributes`, `.gitignore`, `.editorconfig`) | R2 | — |
   | The rest of `src/` and `tests/`; the rest of `docs/` (the prose list excluded); `README.md` | R1 | — |
   | `prose`: an explicit list of files that are not an input to any product test or other semantic verification; only listed generic document-structure checks may read them (Markdown links and anchors, the line ceiling, encoding and file-name rules). A document a topic test still reads is not prose. Joint definition with WS-TEST (ADR 0079); initially `docs/handoff/` Markdown | R0 | — |
   | Any path no entry matches | R3 | governance |

   The current R3 scripts (`CAPABILITY_REUSE_R3_SCRIPTS` at `e6e991af3`) and
   three scripts the conservative map raises:

   | Script | Roles |
   | --- | --- |
   | `ab_merge_fixture_validation.py`, `create_candidate_ic_intake.py`, `create_ctrlram_universal_sentinel.py`, `diagnostic_golden_validation.py`, `intake_ic_reference.py` | firmware |
   | `canonical_golden_validation.py` (Golden expectations; release admission runs it), `external_tool_policy.py` (processor policy and packaged allowlist) | firmware, release |
   | `create_update_catalog.py`, `update_source_registry_policy.py`, `edit_update_source_registry.py`, `package.ps1`, `package-distribution-launcher.ps1`, `publish-github.ps1`, `publish-github.sh`, `render_release_notes.py`, `sign-release.ps1`, `sign-release.sh`, `sign_release.py`, `signing_policy.py`, `smoke-release.ps1` | release |
   | `release_promotion_policy.py` (it also encodes the release approval policy, decision 102) | release, governance |
   | Raised from R2: `v0916_parity_certification.py` (terminal certification, decision 47), `release_source_pins.py` (projects the capability policy, the package trust index and the Golden allowlist) | firmware, release |
   | Raised from R2: `deploy-update-source.ps1` | release |

   `.github/CODEOWNERS` agrees with the policy, because GitHub's code-owner
   review is the approval mechanism (item 7): every path with a role is owned
   by that role's principals, and the default line keeps every other path with
   the owner, as today. G1-A proves this with a consistency test; a generated
   projection (ADR 0068) can replace the test once a role has a principal of
   its own.
5. R0-R3 stay the vocabulary for choosing evidence, reviewers and approvals
   (root [`AGENTS.md`](../../../AGENTS.md)).

### Review and approval

6. **Independent review, R1 to R3.** Another agent runtime reviews; when it is
   unavailable (token limits, or a developer with one runtime), a fresh session
   of the same runtime reviews, preferably with another model, and never shares
   the author's conversation. No process requires two agent products (1.1.12
   WS-GOV decision 3). The evidence is one **review record**: a pull request
   review of type comment, posted through the API with its `commit_id` set to
   the head, whose body carries one machine-readable block: the full
   40-character head SHA, the reviewer's runtime and model identifier (for
   example `codex/gpt-6-astra`), the review mode, the verdict, the open P0/P1
   count, a completed state, and any roles the reviewer adds (item 4). The
   authority check reads the records on each run. A record counts only when
   its `commit_id` and its SHA both equal the head, it is complete, its verdict
   accepts and its open P0/P1 count is zero, and its GitHub principal is on
   the policy's reviewer list (the agent App, the fallback machine account,
   the owner); a comment by anyone else, on this public repository, is
   ignored. The latest record of each listed principal on the head decides.
   Posting or editing a review starts no run: the check stays red until its
   next run (a push, a description edit or a re-run), and the pre-merge
   verification re-runs it. GitHub records the principal that posted it; the
   runtime identifier and the GitHub principal stay separate fields, and
   neither is inferred from the other. A new head needs a new record.
   Independence between agent sessions is procedural: GitHub proves who
   posted, and for which head, not which session reviewed.
7. **R3 human authority.** An R3 change needs the item 6 review **and** an
   approval for each role in its role set (item 4): the firmware owner for
   firmware paths and firmware-semantic additions, the release owner for
   release paths, and the **governance owner** (decision 102) for changes to
   governance rules, permissions and approval policy: the governance paths of
   item 4, unclassified paths, and governance changes declared by the author
   or a reviewer. Release, signing and workflow `permissions:` changes take
   the release owner, and also the governance owner when they change who may
   approve, publish or bypass (by the map, or declared by the author or a
   reviewer where the map does not already require it). Under decision 105
   the parts are:
   - **The last-push approval: GitHub's own (machine-enforced).** An approving
     review by a code owner of the changed files on the most recent reviewable
     push, given by someone other than its pusher, is required by the
     rulesets (`require_code_owner_review`, `require_last_push_approval`,
     `dismiss_stale_reviews_on_push`); a push that changes the diff dismisses
     it. CODEOWNERS gives every role path to that role's principals (item 4).
     Today the owner holds all three roles and gives this one approval for
     every role the change needs. Agents act under their own GitHub identity,
     a GitHub App (a machine account is the fallback), which the owner creates
     and holds the key of, and which cannot approve its own pull request;
     agents never create accounts or apps and never handle the owner's
     credentials (O-2, board decisions 49 and 56; setup in the
     [G0 owner checklist](G0-owner-checklist.md)). Under one Windows user this
     separation is a rule, not a technical boundary (board decision 65).
   - **The roles and their evidence: declared in the pull request, checked for
     presence (machine).** The description declares the roles and, for each,
     its evidence: always the item 6 review; for `firmware-owner` also the byte
     and Golden evidence and the exact write-range audit; for `release-owner`
     the release-owner evidence; for `governance-owner` the rule, permission
     or approval-authority change and, for an unclassified path, its
     classification. The authority check verifies that each required role is
     declared with well-formed evidence entries; it does not judge their
     content, which the owner does.
   - **Role naming (procedural).** The owner's approval states the roles it
     exercises (decision 102), each with its own statement when one person
     holds several; a statement for one role never implies another. GitHub
     does not read the approval's text.
   - **A new SHA with an identical tree.** The authority check turns red on
     every new SHA until a review record names it (machine). Whether GitHub
     also requires a new approval after a push that keeps the tree identical
     is D4's second observation (G0 checklist); until D4 shows it does, a new
     approval for such a SHA is part of the pre-merge verification
     (procedural).
   Firmware-semantic R3 still needs the firmware-owner review, byte and Golden
   evidence and the exact write-range audit of root `AGENTS.md`; release R3
   still needs release-owner evidence.
8. **Execution boundaries.**

   | Boundary | Gates |
   | --- | --- |
   | Pull request into the trunk (`1.1.x`) or a release branch | required checks (items 13 and 14), `governance / authority` from G1-A (items 4 and 6), GitHub's code-owner approval of the last push (item 7), conversation resolution (item 12), the pre-merge verification |
   | Release pull request into `main` | the same over the whole release diff, the full suite with Golden, the release owner's approval of the exact release head, an up-to-date base. `governance / authority` runs on it once the release branch contains the workflow; it becomes a required `main` context only in G2 (below) |
   | Release workflow from `main` | ADR 0033 admission at the candidate, pre-tag and pre-Release boundaries; the protected `release` environment |

   **In force since G0 (2026-09-27):** active branch rulesets on `main`, the
   trunk (`*.*.x`) and release branches (`*.*.*` without `*.*.x`) require pull
   requests, one approval, the existing required checks,
   `dismiss_stale_reviews_on_push`, `require_last_push_approval`, code-owner
   review from today's CODEOWNERS and conversation resolution, allow merge
   commits only, and block force pushes; `main` and the trunk also block
   deletion, while a release branch is deleted after its tag
   ([state](#state-on-2026-09-27); D6 not yet accepted).
   **Added by G1-A:** the required context `governance / authority` on the
   trunk and release-branch rulesets, with GitHub Actions as its required
   source, and the CODEOWNERS consistency test. **Added in G2:** the same
   context on `main`, in the one maintenance window that also renames the
   checks, because the release policy requires `main`'s required contexts to
   equal its closed set exactly (`REQUIRED_RELEASE_CHECKS` in
   `scripts/release_promotion_policy.py`). Until then the release owner's
   approval of a release pull request cites its green `governance /
   authority` result on the exact head, and the commander does not ask for the
   merge while it is red (procedural).

   The only bypass actor is the owner, through the Repository admin role, on
   `main`, the trunk and release branches: the standing force-push means of
   board decisions 66 and 77; the agent identity is never a bypass actor, and
   the tag ruleset keeps an empty bypass list. A bypass skips every rule of
   its ruleset, so it is an owner action outside the normal flow and never a
   review or release exemption: before it the old and new SHA and a recovery
   ref are recorded and related writes and releases stop; afterwards the
   effective rules are verified, approvals and evidence are renewed on the
   new head, and existing tags and release artifacts are never rewritten. It
   does not bypass the tag ruleset, the protected `release` environment or
   the release workflow's checks. If this repository cannot name the admin
   role as a bypass actor, the ruleset is paused for the push instead, for a
   bounded window (board decision 78): writes and releases stop on every
   branch the ruleset covers, not only the target; the ruleset is set Active
   again whatever the push did; if restoring fails or the window passes, the
   freeze stays until the owner has restored the ruleset, and it ends only
   after the ruleset is verified Active with its rules and bypass list
   complete. The exact parameters and both procedures are in the
   [G0 owner checklist](G0-owner-checklist.md), part C.
9. **No gate goes before its replacement.** The record gate stays active until
   G1-A is **in force**: its required context is active on the trunk and
   release-branch rulesets, it met every acceptance criterion of
   [G1-A delivery and acceptance](#g1-a-delivery-and-acceptance), and at least
   one real pull request other than G1-B has merged under it with the
   pre-merge verification recorded. D4, with its identical-tree observation,
   and D5 of G0 are accepted before G1-A is declared in force (D5 and D4's
   approval step were shown on #461), and D6 before the G1-B cutover. Every G1
   part that changes approval authority is R3.

### Authority check and its safeguards (G1-A)

Decision 105 sets the design: an ordinary pull request check that classifies
the change and verifies the declared roles and evidence, failing closed;
GitHub's native code-owner approval of the last push as the exact-head human
approval; and the remaining risks accepted as procedural safeguards, named
below and never claimed as machine guarantees. G1-A builds the check in a
workflow of its own; it does not wait for G2, and it does not edit `ci.yml`
or `scripts/verify.py`, which the CI failure-evidence change owns.

- **Producer and triggers.** A new workflow (for example
  `.github/workflows/authority.yml`) runs on `pull_request` with the types
  `opened`, `synchronize`, `reopened`, `ready_for_review` and `edited`: the
  declared risk, roles and evidence live in the description, and `ci.yml`'s
  four types do not include `edited`. One job produces the context
  `governance / authority`. It has no `if:` condition, no path filter and no
  `continue-on-error`, runs on draft pull requests too, and ends in success or
  failure, never neutral or skipped, which GitHub counts as passing. It has
  read-only `contents` and `pull-requests` permissions, uses no secret, and
  fetches the full history. It reads the live pull request (description,
  head, base branch) and the review records through the API rather than the
  event payload, so a re-run of an older run evaluates the current state. The
  rulesets require the context from GitHub Actions, and the agent App has no
  `checks` or `statuses` write permission.
- **What it checks.** The changed paths of item 4 against the policy; the
  declared risk and roles against the floor and the role union, including the
  roles review records add; the evidence entries of each required role (item
  7); for R1 to R3, a valid review record on the head (item 6); and the policy
  files against their schema. It uses the head's policy and, when the base
  has one, the base's too, the stricter applying to each path. A missing base
  policy is accepted only when the pull request itself adds the policy file
  (the G1-A pull request, and the first release pull request into `main`
  after it); such a pull request changes the check itself (below).
- **It fails when** the description's authority block is missing or
  malformed; the declared risk is below the floor; a required role is not
  declared, or is declared without its evidence entries; an unclassified path
  has no governance-owner role and classification; an R1 to R3 change has no
  valid review record on the head; a policy is missing (outside the case
  above), violates its schema, or names a role without a principal; an API
  call fails, times out or hits a rate limit, or a list is incomplete; a Git
  command fails; or the live head differs from the checked-out head (the push
  that moved it starts a new run).
- **Principals.** The policy lists, for each role, its GitHub principals by
  user ID and login, and the reviewer list of item 6. It stores only public
  account identities: App IDs, installation IDs, keys and machine paths stay
  in the owner's private record (decision 65). Changing a principal is a
  governance R3 change. The owner supplies the values before G1-A admission.

**Division of the gates.**

| Gate | Enforced by | What it guarantees |
| --- | --- | --- |
| Required checks | the rulesets (GitHub) | every required context, `governance / authority` included, succeeded for the head commit being merged; a missing or failed context blocks |
| `governance / authority` | a CI job running the pull request's own workflow and checker | at the time of its run: paths classified, risk and roles declared, evidence entries present, a review record on the head |
| Exact-head human approval | the rulesets' code-owner review, last-push approval and stale dismissal (GitHub) | a code owner, today the owner, approved the most recent reviewable push; a diff-changing push dismisses it |
| Review threads | the rulesets' conversation resolution | no unresolved thread at merge |
| Evidence content, role naming, stale results, changes to the check itself | the owner and the commander | the pre-merge verification and the self-change check below |

**Machine-enforced and procedural safeguards.**

| # | Safeguard | Kind | Carried by |
| --- | --- | --- | --- |
| M1 | Every change to a protected branch goes through a pull request | machine | rulesets |
| M2 | A code owner approved the most recent reviewable push, and not as its pusher; a diff-changing push dismisses the approval | machine | rulesets |
| M3 | No unresolved review thread at merge | machine | rulesets |
| M4 | The required contexts succeeded for the merged head commit | machine | rulesets |
| M5 | Floor, role union, declared roles, evidence entries, review record on the head, policy schema, and fail-closed inputs | machine for a pull request that does not change the check | authority check |
| M6 | A new SHA has no valid review record until a new record names it | machine, same condition | authority check |
| M7 | Force pushes and deletion blocked; only the owner can bypass | machine | rulesets |
| P1 | A pull request can edit the check it runs (workflow, checker, policy, schema, files the checker reads), so its own result proves nothing about that change. The same holds today for every required check, since each runs the head's workflow | procedural | self-change check |
| P2 | An earlier green result on the same head survives a change that starts no run: a new or edited review record, a base policy or checker change, a run that was skipped, queued, cancelled or never triggered, or a re-run of an older run | procedural | pre-merge verification |
| P3 | A push that keeps the tree identical may keep the approval | procedural until D4 shows GitHub asks again | pre-merge verification |
| P4 | The owner's approval names each role it exercises (decision 102) | procedural | owner |
| P5 | The evidence is correct (bytes, Golden, write ranges, release evidence), not only present | procedural | owner and reviewer |
| P6 | A firmware-semantic or approval-authority change outside the mapped paths is declared | procedural | author and reviewer (item 4) |
| P7 | Reviewer independence; one writer per mutable surface | procedural | items 6 and 3 |
| P8 | Before G2, `governance / authority` is not required on `main` | procedural | release owner's approval cites it (item 8) |

**Pre-merge verification (procedural, decision 105).** Before every merge
into a protected branch, after the owner's approval:

1. The commander re-runs `governance / authority` on the pull request (a
   re-run reads the live description, review records and base) and waits for
   its result.
2. Through the API the commander confirms that this run's check reports
   success for the current head SHA, and that the owner's approval is on that
   SHA and on its most recent push; for an identical-tree push under P3, a
   new approval is asked for.
3. The merge names that head (`gh pr merge --match-head-commit <sha>`) and
   happens on the owner's go-ahead. If anything changed in between, the steps
   start again.

The run id and head SHA of steps 1 and 2 are recorded in the pull request.
The window between step 2 and the merge is not machine-closed; the
`--match-head-commit` guard only rejects a moved head.

**Changes to the check itself (procedural, decision 105).** A pull request
that changes the authority workflow, the checker, the policy or its schema,
or a file the checker reads to reach its verdict (all governance R3 by item
4) runs its own version of the check. For it:

- the commander runs the base branch's checker, from a clean checkout of the
  base, against the pull request's head, and attaches the result;
- the pull request states which verdicts the change alters and why;
- the owner's approval contains an explicit self-change statement: the change
  to the check was reviewed, the base checker's result on this head was read,
  and this pull request's own requirements were not lowered by the change
  (or the owner accepts the stated lowering);
- the commander never asks to merge it on its own check result alone.

This also covers the pull requests that add the policy (the bootstrap case).

### Branches, releases and CI

10. Branch model (1.1.12 WS-GOV decision 10, in force since board decision 22):
    `main` holds released code and `v*` tags; the minor-line trunk (`1.1.x`)
    receives `feature/<version>/<topic>` pull requests; a release branch
    `X.Y.Z` is cut from the trunk at feature freeze, takes release fixes (merged
    back into the trunk), opens the release pull request into `main` with a
    merge commit and is deleted after its tag; after publication `main` is
    merged back into the trunk. **Current transitional rule, until G1-B (board
    decision 66):** the trunk catches up with `main` through a pull request
    with a merge commit, only when the merge tree equals both parent trees
    **and** the history gate in force passes with that merge as `HEAD`;
    otherwise the commander stops and asks the owner (procedure: G0 checklist,
    "After G0"). Equal trees are not ADR 0061's normalization exception and
    never replace the history gate. Board decision 23's fast-forward applied
    before G0 and is history now: the trunk ruleset blocks direct pushes.
    **After G1-B**, with item 1, these merges no longer conflict with sealed
    evidence and the catch-up is an ordinary merge pull request; its
    approvals cite the release evidence of what it brings back.
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
    unresolved review thread (O-3, the ruleset's conversation resolution,
    confirmed in board decision 67), so findings do not first
    surface after the merge. Resolved threads do not prove that a review
    finished; the completed item 6 record on the exact head does. The three
    release boundaries keep their P0/P1 check.
13. **CI tiers** (1.1.12 WS-GOV decision 4, corrected):
    - Every pull request runs the structure lane: links, frozen pins (from
      G1-B), contracts and schemas, the policy's coverage test, and the
      generic document checks. The authority check is its own required context
      (above), because it also runs on description edits. Of the document checks
      that today live in `Architecture.Tests` (for example the 2,500-line
      ceiling over `docs/**/*.md`, which failed the 1.1.12 roadmap edit, and
      the roadmap assertions), each moves into the structure lane, stays mapped
      to the documents it reads, or is deleted, decided test by test with the
      owner (board decision 71).
    - Only `prose`-class paths skip product tests, and only once the generic
      document checks, the line ceiling included, run in the structure lane;
      until then those paths keep the Architecture tests. Every other change
      runs the test projects that ADR 0079's selection map assigns to its
      paths; a change the map cannot classify runs the full suite. G2 revises
      the `prose` wording, its checker and the list of document checks to
      migrate in the same batch.
    - The full suite with Golden runs when a release branch is cut, on the
      release pull request into `main`, on every push to `main` (release
      admission requires that run), weekly on the trunk when it changed, and on
      manual dispatch. Test shards reuse one build.
    - Each required check comes from an always-run aggregator that passes only
      when every required producer succeeded or was found not applicable by
      the finalizer recomputing the same selection for the exact base and head
      (ADR 0079 item 9); a missing, failed, cancelled or unexpectedly skipped
      producer fails it. The pull-request tier therefore waits for ADR 0079's
      T4b activation: until then every project runs unfiltered as today.
    - Negative tests cover changed-path mapping, deletions and renames on both
      sides, cross-layer dependencies, an unclassified path, and a cancelled or
      skipped producer.
14. Required checks (amends 1.1.12 WS-GOV decision 6; O-4, confirmed in board
    decision 67):
    `policy / polytail` is renamed after what it runs (for example
    `repository / structure`). `python-worker / verify` is renamed (for example
    `python / repository-scripts`) and **stays required**: it aggregates the
    repository-script shards that test the release policy, Golden validation and
    the validator. Only its CRC-worker lane leaves, when WS-FLOW F11 retires
    the worker. The rename, the closed check set in
    `scripts/release_promotion_policy.py` (which then also gains
    `governance / authority`), the `main` ruleset and every document that
    names the checks (`.github/AGENTS.md`, the workflows README,
    `docs/ci/pull-request-ci.md`, ADR 0033) change in one maintenance window,
    with no release in between.
15. **Waivers** (O-7, board decision 52). The old waivers are frozen with the
    records. A new waiver is a pull request statement bound to its head SHA and
    scope. It names the rule or tool, scope, reason, risk, owner, issue,
    approver, creation and expiry dates and removal condition, and the
    approver is the owner of the authority waived, approving under item 7. No
    waiver may weaken firmware range safety, processor write ranges, integrity
    order, secrets or signing, release allowlists, or independent Golden
    expectations. These rules move unchanged from `docs/policies/polytail.md`
    into the runbook; the new location grants no wider waiver power.
16. The release workflow is cleaned up under its own R3 design
    ([release workflow cleanup](DESIGN-release-workflow-cleanup.md)). This ADR
    sets only the flow rules above. Its batch R-1 is paused by decision 100.

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
    interview style; the dual-runtime agent documents. The WS-AI port confirms
    or amends this item. It lands with G1-B because the owner kept G1-B's
    designed scope (decision 101), not because the record retirement needs it
    (migration step 1 separates the two).

## Canonical owners after the reset

| Rule | Owner |
| --- | --- |
| Risk classes, review rule, R3 roles, single-writer rule, single-runtime rule, pointers | root `AGENTS.md` |
| Execution sequence, pull request fields, single-writer procedure, review and approval records, waivers | `docs/governance/development-execution-workflow.md` |
| Authority classes, paths, roles and principals | the authority policy; `.github/CODEOWNERS` is derived from it |
| Branch model, rulesets, naming, release closure and recovery | `docs/governance/branch-version-and-release-governance.md` |
| Frozen evidence pins | the pin file, read by `scripts/validate_repository.py` |
| Size policy | this ADR and `scripts/code_size_policy.py` |
| Test selection, partitions, categories | ADR 0079 (test architecture) |
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
- The human approval is GitHub's own last-push code-owner review; the
  roles and evidence a change needs are checked on every push and description
  edit; what neither enforces is a named procedure (decision 105).

What G1-B removes, and what it keeps (second review):

| Cost today | After G1-B |
| --- | --- |
| Rebinding the checkpoint and redoing admissions after each seal | removed |
| Per-record finalization, batch coverage, evidence commit, JSON attestation | removed; pull request evidence and exact-head approvals instead |
| A reviewed head that fails the final gate because of `design-active` | removed |
| A new review and owner approval for every new SHA, one with an identical tree included | **kept** |
| A real test failure and its fix and re-verification | **kept**; G1-B fixes no test |
| Full-suite duration | not addressed by G1-A or G1-B; WS-TEST and G2 own it |

### Negative / trade-offs

- The machine-checked "admission before implementation" ledger ends. Pull
  request fields carry that evidence, and their completeness is reviewed.
- The frozen evidence proves its current content, not its history.
- The conservative map over-classifies some changes (for example every
  contract and every `testdata/` file is R3), so the owner states roles on
  more pull requests than the path minimum strictly needs. Narrowing an entry
  is a governance R3 change.
- Single-writer ownership becomes procedural again (item 3).
- The authority check is not tamper-proof: a pull request can change the
  check it runs, and an earlier green result can outlive a later change on the
  same head (P1 and P2 of the safeguards table). Decision 105 accepts this;
  the self-change check and the pre-merge verification carry it, and each
  merge costs one re-run of the check.
- Path-mapped CI can miss a cross-cutting interaction until the next full run.
- While G1-A runs beside the record gate, it adds cost: the old review binds
  the implementation head, while the new review record and the GitHub
  approval bind the final pull request head, which includes the evidence
  commit, so an R3 pull request of that period is reviewed and approved
  twice. The cost ends with G1-B.
- Keeping the WS-AI changes in G1-B (decision 101) delays the retirement: G1-B
  cannot fix its head until the WS-AI port is designed and reviewed; its
  larger diff lengthens the fixed-head review and makes a base move (rebase,
  re-pin, re-review, new authorization) more likely; until then every seal
  still realigns the other workstreams, evidence commits continue and each
  structure run still replays the history (250-310 s locally for this
  draft). No estimate of the delay in days is given; it depends on the WS-AI
  port, which is not yet designed on this branch. The owner chose this
  knowingly; this ADR does not narrow G1-B.
- After work lands under the new rules, returning to the record system needs a
  new trusted checkpoint and new admissions (ADR 0059 activation), not a
  revert.

### Risks and mitigations

- A same-runtime reviewer shares blind spots -> fresh session, another model,
  runtime identifier recorded.
- Agents run under the owner's Windows user, so the app's private key, the
  owner's password manager and credential stores, the token helper and the
  owner's browser session are technically within their reach -> the owner
  accepted a procedural rule (board decision 65): agents never read, copy or
  use them and obtain one-hour installation tokens only through the
  owner-configured helper; the documents state that this is a rule, not a
  technical boundary. The machine-account fallback leaves agents a long-lived
  token and rests on the rule alone (G0 checklist, part B).
- An author under-declares risk -> the map sets a floor and roles by path,
  unions them over the change, and treats unclassified paths as governance R3;
  author and reviewer text can only add; the reviewer confirms the byte,
  range, order, integrity and support impact.
- A pull request lowers the policy it is checked against -> the stricter of
  the base and head policy applies; if it also changes the checker or the
  workflow, only the self-change check stops it (procedural, P1).
- A pull request changes the authority check itself -> GitHub's code-owner
  review requires the owner's approval, which carries the self-change
  statement and the base checker's result on the head (procedural, P1).
- A green result goes stale on the same head (review record edited, base
  policy tightened, a run skipped or cancelled) -> the pre-merge verification
  re-runs the check and binds the merge to the head it passed on
  (procedural, P2).
- A renamed required check blocks merges until the ruleset changes -> one
  maintenance window, no release in between.
- A standing owner bypass skips every rule of its ruleset -> it is used only
  under the procedure of board decision 77 (recorded SHAs and recovery ref,
  stopped writes and releases, verified rules, renewed approvals and
  evidence, untouched tags and release artifacts) and never as a review or
  release exemption; without an admin bypass, the bounded pause of board
  decision 78.
- In-flight records at the cut-over -> the transition table below.

## Compatibility and migration

0. **Effect of acceptance.** Accepting ADR 0080 accepts the staged design and
   the order below; it changes no rule by itself. Items 1 and 2, the
   supersession of ADRs 0054, 0059, 0061, 0070 and 0071, the Historical status
   of the record contract, the move of the waiver rules and every other
   retirement clause take effect **only when an authorized G1-B merges** (step
   2). Until then every change, G1-A included, follows the current rules:
   records, admission against the latest checkpoint, finalization, evidence
   commits and attestations. The rules this ADR adds (items 4, 6 and 7) bind a
   branch only once `governance / authority` is required on its ruleset
   (item 9).
1. **Sequence** (board decisions 50 and 100). Old protection stays until the
   new one works.
   - **G0 (owner, GitHub settings):** applied on 2026-09-27; D0, D5 and D4's
     approval step accepted, the rest open ([state](#state-on-2026-09-27)).
     D4's identical-tree observation is recorded before G1-A is declared in
     force, D6 before the G1-B cutover.
   - **G1-A (R3, admitted under the current rules with its own record):** the
     authority policy and schema, the authority check and its workflow, the
     CODEOWNERS consistency test, the pull request template and the
     procedures of decision 105; its deliverables, owner actions and
     acceptance are listed in
     [G1-A delivery and acceptance](#g1-a-delivery-and-acceptance). Both gates
     run side by side.
   - **G1-B (the cutover, R3), required for the retirement:**
     - the validator's record code and its tests removed (item 1), and the
       record references in `test_code_size_policy.py`,
       `test_skill_inventory_validation.py` and
       `test_v0916_parity_contracts.py` updated;
     - the three READMEs, the pin file and the pin check (item 2);
     - `capability-reuse-record.md` Historical; ADRs 0054, 0059, 0061, 0070
       and 0071 Superseded; ADR 0080 added to `docs/adr/` as Accepted;
     - every live instruction to create or finalize records replaced: root
       `AGENTS.md` (the gate pointer, the R3 roles of decision 102, the
       single-writer rule), the execution workflow (admission through pull
       request fields, the single-writer procedure, review and approval
       records, waivers from `docs/policies/polytail.md`), branch governance
       (item 10 after G1-B), the record text of `docs/ci/pull-request-ci.md`
       and `CONTRIBUTING.md`, and the record steps of the skills `implement`,
       `nfc-architecture-change`, `polytail` and
       `supervised-branch-development`, which would otherwise tell agents to
       add files under frozen paths;
     - the transition inventory (step 3) and the cutover authorization
       (step 2).
   - **G1-B, carried by decision 101 (not required for the retirement):** the
     WS-AI changes of item 18 (skill renames with the `nfc-` prefix, the
     `nfc-review` merge, 23 to 18 skills, `.claude/` projections, the
     dual-runtime agent documents, the hybrid interview style, and the skill
     inventory and routing changes they need) and the size-policy
     consolidation of item 17. They share G1-B's writer and paths; their
     waiting cost is stated under Consequences.
   - **G2 (R3):** CI tiers, the required-check rename and `governance /
     authority` on `main` in one maintenance window (item 14), after the CI
     failure-evidence change merges (it owns `ci.yml` and `scripts/verify.py`
     now). G2 also carries the R3 parts of ADR 0079 that change workflows or
     what a required check accepts, and revises the `prose` wording, its
     checker and the list of document checks to migrate together.
   - The release workflow batches follow their design; R-1 resumes after
     G1-B and the WS-TEST stages (decision 100).
2. **G1-B authorization (board decision 50).** After G1-B's final head is
   fixed and reviewed, the owner gives an explicit, one-time written cutover
   authorization. It names:
   - the base SHA, the G1-B head SHA and the expected frozen pins (three tree
     IDs, one blob ID);
   - the transition inventory (step 3);
   - **the obligations it replaces for G1-B itself:** its capability-reuse
     `design-active` admission and design-review field, its `final-complete`
     finalization with path coverage and `pathStateDigest`, the direct-child
     evidence commit, the binding to the latest checkpoint, and the
     external-authority attestation files. These are replaced by the pull
     request's admission fields, the item 6 review record, and the owner's
     item 7 approval of the last push naming the governance-owner and
     release-owner roles (and firmware-owner if firmware paths are touched),
     with `governance / authority` in force, the pre-merge verification, and
     the self-change check if G1-B touches the check;
   - **the gates it does not replace:** the base commit's validator passes at
     the base (the old rules hold for everything being frozen); the new head
     passes the new structure gate, pins included, and every required check;
     `python scripts/verify.py --all` passes on the exact head (G1-B is a
     cross-cutting change); the independent design review and the fixed-head
     review with no open P0/P1; the pin diff of item 2; the scratch-repository
     topology tests; conversation resolution; G1-A in force with its real
     pull request evidence; D4 to D6 accepted.
   A new base or head voids the authorization; a new one names the new SHAs
   and pins. ADR acceptance does not stand in for it. G1-B lands before the
   1.1.13 release branch is cut if the reviews finish in time; otherwise
   1.1.13 releases under item 10's transitional rule (decision 66) and G1-B
   follows. That the new validator no longer reads records is not itself an
   exemption from the old rules.
3. **Transition states.**

   | State when G1-B merges | Handling |
   | --- | --- |
   | Sealed and merged (final record and attestations in G1-B's base) | Frozen by the pins; nothing changes. |
   | Sealed on a branch, not merged | Preferred: merge before G1-B, which then re-pins (step 4). Otherwise the branch keeps its sealed commits as history (their SHAs are cited in its pull request), rebases its product commits onto the trunk without adding files to the frozen paths, and passes the new gates; nothing pending becomes complete by the move. |
   | Active (`design-active`) | The record is dropped on rebase; its admission facts move to the pull request; the new gates apply. |
   | Blocked, or owner evidence still owed | Stays an open gate in the pull request or the board; the migration never closes it. The inventory lists each item, the owner or Golden evidence it owes and the pull request or board row that carries it. At `e6e991af3` all 359 records on the trunk line are final-complete, and the six open inherited authorities of the trusted checkpoint carry approving attestations. |

4. **Re-pin.** Whenever G1-B's base moves, it is rebased (never merged) onto
   the new base; the pins are recomputed as in item 2 (the new base's
   evidence plus the three READMEs), the pin diff and the base validator are
   run again, and the exact-head review, the approvals and the authorization
   are repeated.
5. **Status links.** ADRs 0054, 0059, 0061, 0070 and 0071 become Superseded
   with a link here; the three ADR 0021 files become one;
   `capability-reuse-record.md` becomes Historical and stays to read the frozen
   records.
6. **GitHub settings** change only with owner approval and never during a
   release: G0; G1-A's required context on the trunk and release-branch
   rulesets; the check rename window with the `main` context (G2); the
   branch-name allowlist and automatic branch deletion (checklist A-5).

## G1-A delivery and acceptance

G1-A is R3 (a workflow and approval policy). It is admitted under the current
rules with its own capability-reuse record bound to the latest checkpoint and
an approved design review; its final evidence needs the release-owner
attestation for the workflow, and its governance-owner approval is the
owner's GitHub approval of its last push naming that role, since the record
system has no governance attestation type. Because the check runs on
`pull_request` events, it also runs on the G1-A pull request itself with that
pull request's own policy (the bootstrap case); that result is informative
only, and the self-change check applies.

**Deliverables** (the admission fixes the exact list):

1. The authority policy (the path map with floors, roles and default-entry
   exclusions; the role principals; the reviewer list) and its schema under
   `docs/governance/`, covering every tracked path of the base tree.
2. The checker (for example `scripts/authority_check.py`): a pure evaluation
   over the diff, the base and head policies, the description's authority
   block and the review records, and a thin read-only GitHub adapter with
   complete pagination. Owner search covers the exact-head review parsing of
   `scripts/release_promotion_policy.py` and `scripts/collect_review_handoff.py`
   before a new owner is chosen.
3. Tests under `tests/scripts/` with recorded API responses for the negative
   cases below; the coverage test (every tracked path matched, no downgrade of
   today's governed paths, every exclusion covered, the script table of item
   4); the dependency test (every repository file the checker imports or reads
   is on the governance R3 list); the CODEOWNERS consistency test.
4. The workflow: the `pull_request` types of the
   [check](#authority-check-and-its-safeguards-g1-a), one job, read-only
   permissions, no secret, full history, no `if:`, path filter or
   `continue-on-error`.
5. The pull request template: the authority block (declared risk, roles and
   the evidence of each, implementation owner, owned paths) and the
   review-record format.
6. A section of the execution workflow on review records, the pre-merge
   verification and the self-change check while both gates run; the full rule
   move is G1-B's.
7. Not in G1-A: `ci.yml`, `scripts/verify.py`, the validator's record code,
   `scripts/release_promotion_policy.py`, the `main` ruleset and a CODEOWNERS
   generator.

**Owner actions:** the principals before admission (below); the G1-A pull
request's approval naming the governance-owner and release-owner roles, with
its self-change statement, and the release-owner attestation; adding
`governance / authority` (GitHub Actions source) to the trunk and
release-branch rulesets through the reviewed ruleset procedure, read back and
recorded; D4's identical-tree observation.

**Acceptance.** Unit cases use recorded responses; each negative case must
fail the check:

- Classification: declared risk missing or below the floor; an unclassified
  path without the governance-owner role and a classification; a cross-class
  change missing one of its roles; a rename from a code path into a firmware
  path, and a deletion of a firmware file, each without the firmware role; a
  head policy that lowers its own paths while the base has a policy (the
  base's requirement still applies); a role that a review record adds but the
  description lacks.
- Evidence: a required role declared without its evidence entries; a firmware
  role without its Golden or write-range entry; a governance role without its
  change statement.
- Review records: none on an R1 to R3 change; one on an older head (by
  `commit_id` or by SHA); one from a principal not on the list, such as an
  outside commenter; open P0/P1 above zero; a rejecting or incomplete record.
- Inputs: a policy that violates its schema or names a role without a
  principal; a missing base policy when the pull request does not add the
  policy file; an API error, timeout, rate limit or incomplete page; a Git
  failure; a live head that differs from the checked-out head.
- Positive unit cases: a head policy that raises its paths applies at once; a
  prose-only change passes as R0 without a review record; a complete R3 change
  passes.

Live cases run on disposable pull requests into `1.1.x`, closed without
merging and their branches deleted by the App, and on one real pull request:

- The check runs on `opened`, `synchronize`, `edited` and `ready_for_review`;
  an edit that drops a required role or lowers the declared risk turns it red;
  a new push, an identical-tree one included, turns it red until a new review
  record exists and the check runs again.
- After the context is required on the trunk and release-branch rulesets, a
  red, cancelled or missing result leaves the pull request blocked (read from
  its merge state, without trying to merge).
- The stale-result case of P2: a review record edited after a green run
  leaves the result green; the pre-merge re-run turns it red. The record shows
  that the procedure, not the platform, closes this gap.
- D4's identical-tree observation (G0 checklist), which settles P3.
- The self-change check done once, on the G1-A pull request or a later policy
  change: the base checker's result on the head attached, and the owner's
  statement in the approval.
- Positive: one real pull request, other than G1-B, merges into `1.1.x` with
  every required check green, its review record, the owner's last-push
  approval naming its roles, and the recorded pre-merge verification (run id
  and head SHA).

G1-A is in force when all of this is recorded in the WS-GOV log and the owner
confirms it (item 9).

**Expected size.** About 900 to 1,500 lines, almost all in new files: the
checker 300 to 500, its tests 400 to 700, the workflow 40 to 60, the policy
and schema 200 to 300 (the path map is most of it), and the template,
runbook section and consistency tests 100 to 150. Basis: the previous design
was estimated at 1,800 to 3,000 lines by comparison with
`scripts/release_promotion_policy.py` (2,327 lines, tests 2,831); decision
105 removes its approval parser, the review-event handling, the result
publisher, the re-run job and the CODEOWNERS generator. The uncertainty is
mostly the size of the path map and of the recorded API fixtures.

## Verification

- The validator runs no `rev-list` or `diff-tree` (today every such call
  belongs to the record code); a test fails if it does.
- Freeze-pin tests: adding, changing, deleting or renaming a frozen file fails;
  a clean tree passes; the pin diff between base and G1-B head lists exactly
  the three README additions; a pin change is classified governance R3.
- Authority-check tests: the acceptance cases of G1-A.
- Scratch-repository topology tests pass structure validation: the 1.1.12
  re-run (`main` merged into a release branch after a later finalization), a
  trunk merge-back with new commits on both sides, and the trunk merged into a
  working branch after a seal (the `beb32b930` shape).
- Contract references still resolve: canonical Golden validation and the
  capability policy load unchanged, and no contract or Golden byte changes.
- G1-A is in force (item 9) before G1-B merges.
- Structure validation time is recorded before and after on the same machine.
- The first release after G1-B merges `main` back into the trunk without a
  conflict.

## Owner decisions (2026-09-26)

Decided by the owner on 2026-09-26 and recorded in the
[1.1.12 board](../1.1.12.md) decision list; the options and consequences put
to the owner are in the [log](WS-GOV.md#owner-decisions-in-risk-order).

| Decision | Chosen | Rejected |
| --- | --- | --- |
| O-1 existing evidence (board decision 51) | Freeze in place with READMEs and pins; a pin change needs owner approval. Reverses the 2026-09-25 archive choice. | Moving to `docs/governance/archive/` (would change the runtime-pinned capability policy and the Golden manifest, or split the history) |
| O-2 agent identity (board decisions 49 and 56) | Agents get their own GitHub identity, a GitHub App (machine account as fallback), created and held by the owner; the owner approves agent pull requests as an ordinary reviewer. The self-approval exception and the Codex-only rule are retired only after their replacement evidence is built and tested. | One shared identity with an owner-only approval channel (procedural trust only) |
| Key custody (board decision 65) | The owner keeps the app's private key in the owner's own password manager or store; the token helper may run under the same Windows user; agents must not read the key, the password manager, DPAPI or credential stores; the documents say this is a rule, not a technical boundary; every account, key and secret step is the owner's | A separate Windows account or service holding the key (a technical boundary) |
| O-3 pre-merge thread gate (board decision 67) | The ruleset's conversation resolution on `main`, the trunk and release branches | A readiness check with its own code and required check |
| O-4 repository-script aggregate (board decision 67) | Stays required, renamed in G2; only the CRC-worker lane leaves with F11 | Dropping it from the required checks |
| O-5 and O-6 switch-over (board decision 50) | Four steps, G0, G1-A, G1-B, G2; old protection stays until the new one works; G1-B on the owner's one-time written authorization naming base and head; before the 1.1.13 release branch if the reviews finish in time, otherwise 1.1.13 releases with the trunk rule of item 10 | An extra old-style step that seals this ADR alone first |
| Trunk catch-up and force-push means (board decision 66) | After G0 and until G1-B, a pull request with a merge commit replaces decision 23's fast-forward, only when the merge tree equals both parent trees and the history gate in force passes, otherwise stop and ask; an owner-only bypass keeps a force-push means for protected branches, never for the agent identity | Any merge without the same-tree evidence; a bypass for the agent identity |
| Bypass on `main` and its fallback (board decisions 77 and 78) | `main` also has the standing owner-only bypass, used only under the recorded procedure and never as a review or release exemption; if the admin role cannot be a bypass actor, the ruleset is paused for a bounded window instead | No bypass on `main` (the draft's recommendation); a separate break-glass app |
| O-7 waivers (board decision 52) | Old waivers frozen with the records; a new waiver is a pull request statement with every current field, approved by the owner of the waived rule; the six non-waivable areas stay | A folder of new waiver files |
| G0 setup (board decisions 80 and 82) | The owner runs reviewed, secret-free scripts for the App, its key (a DPAPI file for the helper and a Bitwarden backup), the rulesets and the token helper; the installed helper and `gh` wrapper are how agents act as the App | Agents running the setup scripts against GitHub |

O-3 and O-4 were relayed by the commander on 2026-09-26 as recommended and
confirmed by the owner as board decision 67. Board decision 66 reads the
force-push means as an owner-only bypass until the owner says otherwise.

## Owner decisions (2026-09-27)

| Decision | Chosen | Not chosen |
| --- | --- | --- |
| Priority (board decision 100) | Development speed first: ADR 0080, G1-A, G1-B and the WS-TEST stages that shorten verification come before release cleanup R-1, which pauses with its draft kept | R-1 first |
| G1-B scope (board decision 101) | G1-B keeps its designed scope, the WS-AI changes included, accepting the later retirement stated under Consequences | The second review's narrower cutover, which would retire record validation sooner |
| Governance owner (board decision 102) | The owner holds the governance owner role, mapped to the owner's GitHub account, for R3 changes to governance rules, permissions and approval policy; recorded separately from the firmware and release owner; each exact-head approval names its role | Folding governance approval into the release owner, or leaving it an unnamed "owner approval" |
| G1-A direction (board decision 105) | An ordinary pull request check that classifies paths and verifies the declared roles and evidence, failing closed; the exact-head human approval is the rulesets' code-owner review after the last push, given by the owner for every role; the remaining risks (a pull request can edit the check it runs; an earlier green result on the same head; a run that did not start) are procedural safeguards stated in this ADR, not machine guarantees | A tamper-resistant check with a default-branch bootstrap, a trusted result publisher and stale-result invalidation (option 6) |

**Owner input before G1-A admission** (only what the admission needs):

1. The principals: that the owner's GitHub account holds all three roles
   (CODEOWNERS names it today; the owner confirms its login and user ID), and
   the reviewer list of item 6: the App's bot account, whose commits show
   `nfc-agent-dennis40816[bot]`, and the owner; a machine account only if the
   fallback is ever used.
2. The firmware owner's confirmation, or amendment, of the firmware-semantic
   `src/` folders of item 4. The contract split can wait: until it is made,
   every contract takes both roles.

The other owner steps belong to later stages and are listed there: accepting
this ADR; G1-A's approval, attestation, ruleset change and D4 observation
([G1-A delivery and acceptance](#g1-a-delivery-and-acceptance)); D6 and the
one-time cutover authorization ([migration step 2](#compatibility-and-migration)).
