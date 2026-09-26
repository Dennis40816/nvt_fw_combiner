# Release workflow cleanup: design (R3)

Status: **design draft, revised after the independent design review**,
2026-09-26. Not implementation authority. Scope: the seven-item inventory the
owner accepted as board decision 29 ([1.1.13 board](../1.1.13.md), "Release
workflow inventory draft"); the owner may add items, and the proposed
additions below need that confirmation. Written for WS-GOV ([log](WS-GOV.md));
the flow rules it relies on are in the
[governance ADR draft](ADR-DRAFT-governance-reset.md). The review
(`codex/gpt-6-astra`, ACCEPT-WITH-CHANGES) and how each finding was taken in
are recorded in the [log](WS-GOV.md#design-review-2026-09-26); the owner
decisions are listed in risk order at the end of the log.

Sources read at `8682dd269`, identical in content to the rebuilt `e60ba0062`
(the files are unchanged since `v1.1.12`, `30b17e699`):
[`release.yml`](../../../.github/workflows/release.yml) (1,236 lines),
[`main-package.yml`](../../../.github/workflows/main-package.yml),
[`release_promotion_policy.py`](../../../scripts/release_promotion_policy.py),
`scripts/package.ps1`, [ADR 0033](../../adr/0033-ci-owned-stable-release-promotion.md),
[ADR 0057](../../adr/0057-v0916-black-box-parity-certification.md),
[`release-package.md`](../../ci/release-package.md),
[`.github/AGENTS.md`](../../../.github/AGENTS.md) and
[`.github/workflows/README.md`](../../../.github/workflows/README.md). Line
numbers below refer to that source.

## What stays

Every item keeps these ADR 0033 properties: the workflow runs only from the
exact current protected `main`; exact-source CI and fresh Golden execution
against the candidate (`verify.py --release-golden`); one closed-allowlist
package, smoked before upload; three separate admission boundaries (candidate,
pre-tag, pre-Release) with fresh GitHub evidence; the protected `release`
environment as the human gate; the `contents: write` job never runs candidate
code; immutable annotated tag and Release, no clobbering; the published
download smoked without a token; same-run recovery of a failed promotion.
ADR 0057's terminal v0.9.16 certification obligation also stays until the
owners decide RO-1.

## Principles

1. **One current policy.** The workflow publishes only a version newer than
   every stable tag. Code for historical versions is deleted, not skipped.
2. **Derive what GitHub records;** ask the operator only for decisions.
3. **Move gates earlier when they can:** pull-request findings before the
   release pull request merges, packaging problems before the release branch
   is frozen.
4. **The workflow orchestrates;** `release_promotion_policy.py` owns GitHub
   reads and their validation, as `release-package.md` already states. The
   workflow stays the only owner of tag and Release mutations.
5. **A cleanup does not change certification or approval policy.** Those
   changes (RO-1, RO-5) need their own owner decisions and evidence.

## Summary

| # | Item | Proposal | Risk | Governance first | Batch |
| --- | --- | --- | --- | --- | --- |
| 1 | One-off 1.0.0 / 1.0.1 steps | Delete with their policy commands and `package.ps1` parameters; a release floor, checked at the candidate and pre-tag, replaces every historical version branch | R3 | ADR 0033 amendment; `release-package.md` | R-1 |
| 2 | v0.9.16 parity jobs | Keep the terminal obligation until RO-1; fix the promote gate for every version from 2.0.0; add the rolling comparator as a separate gate | R3 | RO-1 with firmware-owner and release-owner approval; ADR 0057 amendment if it changes | R-1 (gate), R-5 |
| 3 | Maintenance `source_branch` options | Delete; the source is always `main` (RO-2) | R3 | ADR 0033; `.github/AGENTS.md`; workflows README; template | R-1 |
| 4 | Manual inputs | Derive commit, pull request and `published_at`; the self-approval exception stays an explicit consent or goes with O-2; product-neutral exact-head reviewer evidence | R3 | ADR 0033; release-readiness skill; O-2 and RO-5 | R-3 |
| 5 | Re-run conflict | Root fix in the governance ADR; recovery by tag and Release state; pre-merge thread gate; rehearsal before merge | R2-R3 | Governance ADR G1-B; branch governance | R-4 |
| 6 | `main-package` | Replace with a release rehearsal on any branch (RO-6) | R3 | ADR 0033 (main-package clause); `release-package.md` | R-2 |
| 7 | Monolithic file, repeated setup | Composite setup action; collection logic into Python; reusable comparator workflow; optional parallel candidate | R3 | Workflows README; `release-package.md` | R-2, R-3, R-6 |

All workflow, release-policy and packaging changes are R3 (release owner).
`release-package.md` and ADR text are R2 by path but change the release
contract, so the owner approves them with the batch that needs them.

## 1. One-off 1.0.0 and 1.0.1 steps

Facts: `release.yml:420-428` (1.0.0 package-source authority, only for
`1.0.0`), `:429-454` (download the published 1.0.0 package, only `1.0.1`),
`:465-469` (1.0.1 package arguments), `:471-485` (1.0.1 version-only proof)
and `:159-165` (1.0.1 lineage) run for no future version. Behind them:
`release_promotion_policy.py` subcommands `validate-version-only-lineage`,
`validate-version-only-package` and `extract-version-only-stable-payload` with
about 360 lines of helpers used by nothing else (`:1404-1765`), the
`VERSION_ONLY_*` constants (`:44-60`), `package.ps1` parameters
`-VersionOnlyBasePackage` and `-VersionOnlyBasePackageSha256` (`:9-11`,
`:83-97`, `:124-130`, `:1455-1466`), and
`v0916_parity_certification.py validate-package-source`.

The same class, proposed as additions: `package.ps1 -ManualOnly` and the
policy's `1.1.0` rejection (v1.1.0 only); and every version branch below the
current release: `verify.py --all` for versions below 1.1.3 (`:413-418`),
"strict admission starts at v1.1.1" (`:739-755`, `:849-866`),
`STRICT_RELEASE_POLICY_VERSION` and `SOURCE_CI_POLICY_VERSION` (`:35-36`),
`_publishes_installer_assets` (`:705-707`) and `package.ps1:1801`.

Proposal: delete all of them. A **release floor** replaces the version
branches for a first publication: tag `vVERSION` must be absent and `VERSION`
greater than every existing stable tag. The candidate checks it, and the
pre-tag boundary checks it again in the same run block as the tag mutation,
so a tag created between the two cannot be published over. The floor does not
apply to same-run recovery after the tag exists (item 5), which keeps its
existing-tag validation. `release-package.md` keeps the 1.0.0, 1.0.1 and
v1.1.0 procedures as a short historical note; the CHANGELOG and the Release
Closure Records keep the evidence.

## 2. v0.9.16 parity jobs and the formal 1.x comparator

Facts: `v0916-parity-compare`, `-attestation` and `-finalize`
(`:1059-1236`, 178 lines) run only for version `2.0.0`, use the protected
`firmware-parity` environment and three `NFC_FIRMWARE_OWNER_*` secrets, and
feed two promotion steps (`:631-644`). ADR 0057 requires a complete 64-route
terminal certification against the fixed v0.9.16 baseline, with an
independent firmware-owner verification bound to the plan, the candidate
package and the complete evidence; the release owner deferred it to 2.0.0. The
immutable plan pins the v1.0.0 candidate executor, so it must be rebound to
the 2.0.0 candidate anyway (1.1.12 WS-PARITY checkpoint). The owner decided
that 1.1.13 adds a rolling comparison with the previous stable release, with
every difference declared in the release notes (1.1.12 WS-GOV decision 7b);
the comparator itself is the board item "Rolling v0.9.16 parity and the formal
1.x comparator".

The two answer different questions. The rolling comparison asks whether
anything changed since the previous release; it does not prove conformance to
the fixed v0.9.16 baseline and does not replace the independent
firmware-owner verification. This design therefore never presents it as a
replacement for the terminal certification.

**Evidence debt.** The plan lists 27 of the 64 selected routes without
canonical input (`canonicalInputAuthority.currentlyMissingRouteIds`):

| Workflow | IC | Routes (plan route suffix) | Count |
| --- | --- | --- | --- |
| CtrlRAM Replace | NT51917 | `fw132-twochip` full flash and TP work 212k; `fw140-threechip` full flash and TP work 212k | 4 |
| CtrlRAM Replace | NT51919 | `fw1x-cascade` full flash (NT51929 map) | 1 |
| CtrlRAM Replace | NT51927 | `fw132-twochip` full flash and TP work 212k; `fw140-threechip` full flash and TP work 212k | 4 |
| CtrlRAM Replace | NT51928 | `fw141-single`, `fw132-twochip` and `fw140-threechip`, each full flash and TP work 212k | 6 |
| CtrlRAM Replace | NT51929 | `fw1x-cascade` full flash | 1 |
| CtrlRAM Replace | NT51932 | `fw1x-single` full flash | 1 |
| CtrlRAM Replace | NT51950 | `fw200-single` TP work | 1 |
| CtrlRAM Replace | NT51951 | `fw200-single` TP work; `fw1x-cascade` TP work | 2 |
| Standard Merge | NT51928 | dual capacity 256k/512k | 1 |
| Standard Merge | NT51950 | 512k; 1024k | 2 |
| Standard Merge | NT51951 | 256k; 1024k | 2 |
| AB Merge | NT51950 | 1024k | 1 |
| AB Merge | NT51951 | 1024k | 1 |

**Claims at stake.** The publication status of the 64 selected routes (all
`supported` in the capability policy, resting on their current evidence kinds
`direct-golden`, `approved-alias`, `contract-only` or `synthetic-oracle`), ADR
0057's condition that the 11 shortened TP-work routes stay first-class
Supported only when their transitive proof passes, the planned 2.0.0
"terminal pass", and the rule that the 27 routes are not relabelled Verified.

**RO-1 options** (firmware owner and release owner decide together):

- **A. Keep the terminal certification for 2.0.0.** ADR 0057 stands. The
  three jobs cannot simply move to a manual workflow: they consume the same
  run's candidate artifacts and a pinned workflow contract, so a standalone
  certification must be redesigned and rebound to the 2.0.0 candidate. The
  27-route debt must get canonical inputs, or an explicit owner disposition,
  before 2.0.0.
- **B. Retire the terminal certification.** One owner decision record, approved
  by the firmware owner and the release owner, states which claims are
  retained, withdrawn or deferred, disposes of each of the 27 routes, and
  defines the new owner evidence binding for the rolling comparison (report
  digest, candidate source SHA, previous tag, declared-difference list, and
  who approves it). ADR 0057 is amended or superseded by that record. The
  `firmware-parity` environment and the three secrets are deleted only after
  the replacement gate has passed on a real release.
- **C. Defer.** Keep the jobs; decide A or B before 2.0.0 work starts.

**Promote gate (R-1, whatever RO-1 becomes).** The `promote` condition (`:607`)
admits only `2.0.0` with a successful parity chain or a `1.`-prefixed version
with a skipped one; every other version skips promotion and published smoke
while the run still ends green
([bug](../bugs/BUG-20260926-release-promote-skips-other-versions.md)). R-1
replaces it with a release-eligibility job (no environment, no write
permission): until RO-1 is decided, every version from 2.0.0 on requires the
terminal parity chain, so skipping to 2.0.1 cannot avoid it; a version that
requires a gate which failed, was cancelled or was skipped fails the run
instead of ending green. `promote` needs that job.

**Rolling comparison (R-5, after the comparator and its ADR are accepted).**
It is an additional gate in every mode. A reusable workflow
`predecessor-parity.yml` (`workflow_call`, read-only) runs in the caller's run,
so run id and artifact binding stay. It needs only the source SHA, which
equals the workflow SHA, so it starts with the candidate job. How the
comparator obtains both executors and which routes it covers belong to the
comparator item; the 1.1.12 harness built both CLIs from source and ran every
route with canonical input. It fails on a difference that the reviewed
declaration does not list; its report digest is bound into the candidate
manifest or a sibling evidence manifest; `promote` needs it. Cost is not yet
known: the 1.1.12 local run took 659 s for 37 routes on both sides plus about
one minute of builds, close to the candidate's roughly 11.5 minutes; decision
7b asks to measure on a hosted runner before implementing.

## 3. Old maintenance-line options

Facts: `source_branch` offers `0.9.17`, `0.9.18` and `0.9.19` (`:13-15`,
`:92-94`, `:150-158`; policy `MAINTENANCE_RELEASES`, `:39-43`), and ADR 0033,
`.github/AGENTS.md`, the workflows README, `release-package.md` and the release
template still list them as approved. The path is dead twice: since
`ae932e245` (2026-08-28) the `promote` condition skips every `0.9.x` version,
after the last maintenance release `v0.9.19` (2026-08-08); and no
`origin/0.9.17` to `origin/0.9.19` branch appears in the local remote-tracking
refs (to confirm with `git ls-remote` before the change). The owner decided on
2026-09-25 that no further 0.9.x maintenance release follows (1.1.12 WS-GOV
decision 7c).

Options (RO-2):

- **A, recommended:** delete maintenance support. The source is always `main`.
  The candidate then runs protected `main` itself, so the copy of the policy
  scripts to `RUNNER_TEMP` and `git checkout --detach` (`:133-145`) go too.
  A future maintenance line gets its own decision, including a per-line
  release floor.
- B: a generic mechanism (for example a reviewed `maintenance-lines` list) for
  a future line such as `1.1.x` fixes after 1.2.0. Choose it only if the owner
  expects such releases.

## 4. Manual inputs the workflow can derive

| Input | Proposal | Why it is safe |
| --- | --- | --- |
| `commit` | derive: `github.workflow_sha` | The policy already requires the source to equal the current protected `main` head and the workflow SHA (`validate_candidate_context`). |
| `pull_request` | derive: `GET /repos/{repo}/commits/{sha}/pulls`, exactly one merged pull request whose merge commit is the source and whose base is `main` | Zero or several matches fail closed; the policy keeps checking merge commit, base and tree. |
| `published_at` | derive: the pull request's `mergedAt` (RO-3) | Canonical UTC and stable across re-runs. It is merge-derived metadata, not the actual publication time, and the notes and catalog documentation say so. The Catalog contract defines only the format of `publishedAt`; the Registry contract calls its `publishedAtUtc` audit and display metadata. |
| `owner_self_approval_exception` | removed if O-2 separates the agent identity (the owner then approves the release pull request); otherwise kept as an explicit owner consent | A review decision other than `APPROVED` only selects the policy branch; it does not show that the owner chose the exception, so the flag is never derived from it. |
| `source_branch` | removed (item 3) | - |

Without the derived inputs, the operator's binding moves to the approval: the
candidate writes version, full source SHA, pull request, notes digest and
difference summary to the job summary, which the owner reads before approving
`release`. Optional later step (RO-4): start the candidate automatically when
`ci` completes on a `main` merge of a release pull request (`workflow_run`),
keeping `workflow_dispatch`.

**Reviewer evidence for a self-approved release (RO-5).** Today the policy
requires an exact-head Codex review for such a release except for versions
from 1.0.8 up to, not including, 1.2.0 (`release_promotion_policy.py:616`,
record `RELEASE-108-OWNER-REVIEW-DEFER-01`), so the planned 1.2.0 release would
need the Codex GitHub review bot again, against the rule that no process
requires two agent products. The replacement is product-neutral but just as
verifiable:

- a completed review on the exact head (a pull review or inline comment whose
  `commit_id` is the head, or an issue comment naming the full head SHA),
  posted by a principal on a reviewer allowlist held in the protected-`main`
  policy, not in the pull request, and different from the pull request author;
- the record keeps the runtime identifier (for example `codex/gpt-6-astra`)
  apart from the GitHub principal that posted it;
- any `CHANGES_REQUESTED` stays fail-closed, and the author, dispatcher and
  repository-owner constraints of the exception stay;
- the owner's consent to the exception is recorded separately (item row
  above), never inferred.

The Codex-only check and its version window are removed only after this
replacement is implemented and tested; until then the current rule, including
its 1.2.0 restoration, stands.

## 5. Release re-run conflict and recovery

Causes: (a) the validator's history replay and ADR 0061's single containment
exception, which on 2026-09-26 also failed wave 2 after a trunk merge
([bug](../bugs/BUG-20260926-trunk-merge-flags-sealed-record.md)); (b)
review-thread and approval gates are first evaluated after the release pull
request merged, so a finding leaves `main` ahead of every tag. Main's ruleset
also requires up-to-date branches, so every release pull request must contain
`main`.

Proposal:

- (a) The governance ADR's G1-B removes history replay. Until it lands, board
  decision 23 applies.
- (b) A pull request cannot merge with an unresolved review thread (governance
  O-3; the ruleset's conversation resolution), and the completed exact-head
  review record shows that review finished. The three release boundaries keep
  their P0/P1 check.
- The frozen release-branch head runs the release rehearsal (item 6) before the
  release pull request merges, so Golden, packaging, notes and catalog failures
  surface on the branch.
- **Recovery by tag and Release state**, written into branch governance:

  | State | Path |
  | --- | --- |
  | No tag yet | Fix through a pull request into `main`; dispatch a new run. |
  | Tag created by this run; publication incomplete and recoverable | Re-run only the failed promotion in the same run. Same run id, same candidate artifact and digest, and live authority (`main` still at the workflow SHA) must hold, so nothing merges into `main` first. The first-publication floor does not apply; the existing-tag validation does. |
  | Immutable Release incomplete or conflicting | Never repaired in place; the owner decides a new version. |

  While a tagged release is incomplete, nothing merges into `main`: advancing
  `main` ends same-run recovery, and a new run cannot reuse the tagged version.
  The Release Closure Record lists every failed run.
- Same-run re-runs otherwise stay as the contract says: a failed candidate gets
  a new run. The release keeps the default `overwrite: false` for its
  candidate artifact, whose digest the tag message binds; the CI evidence
  change's `overwrite: true` must not be copied here.

## 6. `main-package` and the release

Facts: `main-package.yml` is manual only. It runs `verify.py --all` (not the
release's `--release-golden`), builds a preview package with `-AllowPrerelease`
(release-manifest schema 1.1, no managed Launcher), smokes it and keeps a
3-day artifact. The release never uses it. ADR 0033 says it "becomes a
reusable/manual preview path or is retired". ADR 0033's verification also
promises "a dry-run path [that] produces candidate artifacts without tag or
Release authority"; `release.yml` has none, and it runs only for a merged
release pull request at the `main` head.

Options (RO-6):

- **A, recommended:** replace it with `release-rehearsal.yml`: dispatch on any
  branch (normally the frozen release branch); run `verify.py
  --release-golden`, the stable package build (`package.ps1` without
  `-AllowPrerelease` works on any clean checkout whose `VERSION` matches), the
  smoke, the notes render and the update-source handoff; upload with 3-day
  retention. No GitHub admission, environment, secret or write permission. It
  shares the candidate's build steps through the composite action (item 7), so
  it exercises the release code. Its artifacts and summary state that it
  proves the build path only: no admission, approval, promotion, tag or
  Release path runs, and it skips the candidate manifest, which needs a real
  review snapshot. It is also a staging aid for this cleanup.
- B: retire `main-package` and build previews locally with `package.ps1`.
- C: keep it and document that it is unrelated to the release.

## 7. One monolithic file; repeated setup

Facts: Python setup repeats in five release jobs and in `main-package`, .NET
setup in the candidate and in `main-package`. The admission step inlines 205
lines of GitHub collection (`:176-380`), which repeat the policy's reviewer
normalizer (`:231-238` against `_normalize_reviewer`) and its required-check
list (`:323-327` against `REQUIRED_RELEASE_CHECKS`). The live-authority block
appears twice (`:756-800` in the tag step, `:867-907` in the Release step).

Proposal:

- A composite action `.github/actions/setup-toolchain` (pinned Python,
  `install-dotnet.ps1`, PATH, the pip extras as an input) for the candidate,
  the rehearsal and, after the CI evidence change, `ci.yml`. A local action
  loads from the checked-out ref. After item 3 every release job checks out
  protected `main`, so the action adds no source-code execution to a release
  job; the rehearsal runs branch code by design, without secrets or write
  permission.
- Policy subcommands replace the inline blocks: `collect-review-snapshot` and
  `collect-live-authority`. The workflow keeps every mutation.
- The comparator job becomes the reusable workflow of item 2.
- Optional (RO-7): the candidate splits into parallel read-only jobs, Golden
  verification and packaging (1.1.12 WS-GOV decision 7; about 232 s and 332 s
  in 1.1.12). Both bind the same source SHA and tree. The packaging job
  uploads only an intermediate artifact; the formal candidate (manifest,
  checksums and the candidate upload the tag binds) is created only after the
  Golden job succeeded, so no releasable candidate exists without Golden. The
  contract sentence "missing, failed or skipped required Golden cases block
  packaging" becomes "... block the candidate manifest, upload and promotion".
- Optional (RO-8): the candidate stops waiting for the push-to-`main` CI run.
  It records that run as pending (a failed run still stops it), and a pending
  snapshot is never final evidence: pre-tag and pre-Release admission keep
  requiring a fresh, same-source, successful run, with negative tests for
  pending, failed and different-SHA runs at those boundaries. The candidate
  then overlaps the roughly 9-minute post-merge CI instead of following it.

Estimate: `release.yml` shrinks to roughly 600 to 650 of its 1,236 lines,
from the step spans above (about 270 lines of dead paths, 180 of inline
collection, 85 of duplicated authority, 60 of identity and input handling),
before the eligibility job and the comparator call are added back. The Python
policy loses the 360 version-only lines and gains the moved collection.

## Batches and order

| Batch | Content | Depends on |
| --- | --- | --- |
| R-0 | This design, the owner decisions, the promote-condition bug | - |
| R-1 | Items 1 and 3; release floor at candidate and pre-tag; release-eligibility job (terminal parity required from 2.0.0 until RO-1); `dry_run` input with non-promotable artifacts; staging runs before merge | RO-2, RO-9, RO-10; ADR 0033 amendment and doc updates in the same pull request |
| R-2 | Item 6 rehearsal and the composite setup action for the release and rehearsal jobs | RO-6 |
| R-3 | Item 4 derivations; self-approval per O-2; product-neutral reviewer evidence; collection moved into the policy; staging again for the changed promotion path | O-2, RO-5 |
| R-4 | Item 5 recovery text and pre-merge gate | Governance G1-B; O-3 setting |
| R-5 | Rolling comparison as an additional gate; terminal certification per RO-1 | Comparator item and its ADR; RO-1 with both owners' approval |
| R-6 | Optional speed items (RO-7, RO-8); `ci.yml` adopts the composite action | CI failure-evidence change merged |

R-1 and R-2 fit 1.1.13 if the owner answers RO-2, RO-6, RO-9 and RO-10 early;
R-3 and R-4 follow the governance ADR; R-5 follows the comparator and RO-1.

## Verification

- **Policy tests** for every changed rule, with negatives: zero or several
  pull requests for a merge commit, a non-`main` base, a malformed `mergedAt`,
  a version at or below the floor at candidate and at pre-tag, a present tag,
  reviewer evidence on an older head or from the author's principal, a
  `CHANGES_REQUESTED` review. The existing workflow and policy tests
  (`tests/scripts/test_release_package_policy.py`,
  `test_release_promotion_policy.py`) are updated, not bypassed.
- **Staging repository, from R-1 (RO-9):** a scratch repository with the same
  rulesets and environments runs, before R-1 merges: a new version publishing;
  an existing, equal or lower version rejected at the candidate and at
  pre-tag; a promotion failing after the tag and recovering in the same run;
  recovery refused after `main` advanced; an immutable Release conflict
  refused, with a new version required; a required parity chain that failed,
  was cancelled or was skipped failing the run instead of ending green; and a
  dry-run artifact refused by promotion. R-3 repeats the promotion-path cases.
- **Dry run on `main`:** after each release batch merges, a `dry_run` run
  exercises admission, Golden, package, smoke, notes, handoff and manifest.
  Its artifacts are marked non-promotable (manifest flag and name prefix), the
  promotion refuses them, `promote` and `published-smoke` do not run, and its
  floor report never makes a run eligible.
- **Rehearsal (from R-2):** each batch branch runs `release-rehearsal.yml`
  before its pull request merges and records the run.
- **First real release** after each batch: the Release Closure Record names
  the batch and every run.

## Owner decisions

The WS-GOV log lists them in risk order with plain options and consequences
([owner decisions](WS-GOV.md#owner-decisions-in-risk-order)).

- **RO-1 v0.9.16 terminal certification.** A keep for 2.0.0 (rebuilt and
  rebound), B retire with a claims and debt record approved by the firmware
  owner and the release owner, or C defer. The rolling comparison is added in
  every case and is never presented as a replacement.
- **RO-2 Maintenance releases.** Recommended: A (delete).
- **RO-3 `published_at`.** Recommended: the release pull request's `mergedAt`,
  labeled merge-derived metadata.
- **RO-4 Commit binding and start.** Recommended: derive the commit and show
  the full identity before approval; automatic start later, if wanted.
- **RO-5 Self-approval exception and reviewer evidence.** Recommended: follow
  O-2; replace the Codex-only check with the product-neutral exact-head
  reviewer evidence of item 4, and keep an explicit owner consent while one
  identity remains.
- **RO-6 `main-package`.** Recommended: A (release rehearsal), labeled as not
  proving promotion authority.
- **RO-7 Parallel candidate**, under the conditions of item 7.
- **RO-8 Source CI at pre-tag only**, under the conditions of item 7.
- **RO-9 Staging** from R-1, with the failure and recovery cases above.
- **RO-10 Inventory additions**, each approved on its own: `-ManualOnly` and
  the historical version branches (item 1), the promote gate (item 2), the
  1.2.0 Codex requirement (item 4), the dry-run gap (verification).

## Conflicts with other work

- **CI failure evidence (R3, `ci.yml` and `scripts/verify.py`):** one writer.
  The composite action enters `ci.yml`, and the governance G2 rename and tiers
  land, only after that change merges. G2 and R-6 keep its failing-project
  evidence (board decision 41) and the attempt-to-artifact correspondence, so
  stale-attempt evidence cannot return. The release keeps `overwrite: false`.
- **Governance ADR:** the required-check rename changes
  `REQUIRED_RELEASE_CHECKS`, the ruleset and the release admission together;
  no release may run between them. O-2 decides item 4's self-approval path.
- **Pre-built catalog (ADR 0077):** its text is not on this branch. Before
  integration, fix the ADR 0077 version and its package diff and check catalog
  generation time, allowlist, provenance, manifest and smoke together with
  R-1; `package.ps1` needs one writer.
- **Formal 1.x comparator:** owns the comparator, the declaration format and
  the plan fixes (`BUG-20260925-v0916-plan-nt51950-tp-work-correction-missing`,
  `BUG-20260925-nt51950-cascade-ctrlram-plan-base`); this design owns only the
  release integration.
- **1.1.13 release:** whatever lands before the release branch is cut ships in
  the workflow that releases 1.1.13; R-1 and R-2 are the realistic set.
