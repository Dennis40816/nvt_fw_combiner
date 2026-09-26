# Release workflow cleanup: design (R3)

Status: **design draft for independent review and owner decisions**,
2026-09-26. Not implementation authority. Scope: the seven-item inventory the
owner accepted as board decision 29 ([1.1.13 board](../1.1.13.md), "Release
workflow inventory draft"); the owner may add items, and the proposed
additions below need that confirmation. Written for WS-GOV ([log](WS-GOV.md));
the flow rules it relies on are in the
[governance ADR draft](ADR-DRAFT-governance-reset.md).

Sources read at `8682dd269` (the files are unchanged since `v1.1.12`,
`30b17e699`): [`release.yml`](../../../.github/workflows/release.yml) (1,236
lines), [`main-package.yml`](../../../.github/workflows/main-package.yml),
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

## Summary

| # | Item | Proposal | Risk | Governance first | Batch |
| --- | --- | --- | --- | --- | --- |
| 1 | One-off 1.0.0 / 1.0.1 steps | Delete with their policy commands and `package.ps1` parameters; a release floor replaces every historical version branch | R3 | ADR 0033 amendment; `release-package.md` | R-1 |
| 2 | v0.9.16 parity jobs | Replace with one predecessor-parity job that calls the formal 1.x comparator | R3 | ADR 0057 superseded or amended (comparator item); RO-1 | R-5 |
| 3 | Maintenance `source_branch` options | Delete; the source is always `main` (RO-2) | R3 | ADR 0033; `.github/AGENTS.md`; workflows README; template | R-1 |
| 4 | Manual inputs | Derive commit, pull request and `published_at`; derive or remove the self-approval flag | R3 | ADR 0033; release-readiness skill | R-3 |
| 5 | Re-run conflict | Root fix in the governance ADR; pre-merge thread gate; rehearsal before merge; written recovery path | R2-R3 | Governance ADR G1; branch governance | R-4 |
| 6 | `main-package` | Replace with a release rehearsal on any branch (RO-6) | R3 | ADR 0033 (main-package clause); `release-package.md` | R-2 |
| 7 | Monolithic file, repeated setup | Composite setup action; collection logic into Python; reusable parity workflow; optional parallel candidate | R3 | Workflows README; `release-package.md` | R-2, R-3, R-6 |

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
branches: the candidate fails unless tag `vVERSION` is absent and `VERSION` is
greater than every existing stable tag. A new run cannot publish a historical
version today either (promotion validates the existing tag message, which
binds the original run id), but it fails only after the whole candidate; the
floor fails first. Same-run recovery of a failed promotion is unchanged.
`release-package.md` keeps the 1.0.0, 1.0.1 and v1.1.0 procedures as a short
historical note; the CHANGELOG and the Release Closure Records keep the
evidence.

## 2. v0.9.16 parity jobs and the formal 1.x comparator

Facts: `v0916-parity-compare`, `-attestation` and `-finalize`
(`:1059-1236`, 178 lines) run only for version `2.0.0`, use the protected
`firmware-parity` environment and three `NFC_FIRMWARE_OWNER_*` secrets, and
feed two promotion steps (`:631-644`). The immutable plan pins the v1.0.0
candidate executor, so `compare` cannot run for a 1.x candidate without
changing governed authority (1.1.12 WS-PARITY checkpoint). ADR 0057 defers the
terminal certification to 2.0.0. The owner decided that predecessor parity
becomes a rolling baseline in 1.1.13: each candidate is compared with the
previous stable release on every published route, and each difference is
declared in the release notes (1.1.12 WS-GOV decision 7b). The comparator
itself is the separate board item "Rolling v0.9.16 parity and the formal 1.x
comparator"; this design owns only its place in the release.

Release integration (R-5, after the comparator and its ADR are accepted):

- One reusable workflow `predecessor-parity.yml` (`workflow_call`, read-only),
  called by `release.yml`. Local reusable workflows run in the caller's run, so
  run id and artifact binding stay as they are.
- It needs only the source SHA, which equals the workflow SHA, so it starts
  with the candidate job instead of after it. It runs the comparator for that
  source against the previous stable tag. How the comparator obtains both
  executors and which routes it covers belong to the comparator item; the
  1.1.12 harness built both CLIs from source and ran every route with
  canonical input.
- It fails on any difference that the release's reviewed difference
  declaration does not list; the declaration's format belongs to the
  comparator item. Its report is an artifact whose digest the candidate
  manifest (or a sibling evidence manifest) binds. `promote` needs it.
- Deleted: the three jobs, the two promotion steps, the version-specific
  `promote` condition and its PyYAML step. After the merge, the owner deletes
  the `firmware-parity` environment and the three secrets (GitHub settings).
- Cost is not yet known. The 1.1.12 local run took 659 s for 37 routes on both
  sides plus about one minute of builds, close to the candidate's roughly 11.5
  minutes (1.1.12 measurement); its hosted-runner time is not measured.
  Starting it with the candidate keeps it off the critical path only if it
  finishes first. Decision 7b asks to measure before implementing; the
  comparator item measures it on a hosted runner.

Also found: the `promote` condition (`:607`) admits only `2.0.0` with a
successful parity chain or a `1.`-prefixed version with a skipped one. Every
other version, including `2.0.1` and any `0.9.x` maintenance release, skips
promotion and published smoke while the run still ends green
([bug](../bugs/BUG-20260926-release-promote-skips-other-versions.md)). R-1
fixes it even if R-5 is later: `promote` needs the candidate (and, until R-5,
the parity chain only when parity is required).

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

| Input | Derivation | Why it is safe |
| --- | --- | --- |
| `commit` | `github.workflow_sha` | The policy already requires the source to equal the current protected `main` head and the workflow SHA (`validate_candidate_context`). |
| `pull_request` | `GET /repos/{repo}/commits/{sha}/pulls`: exactly one merged pull request whose merge commit is the source and whose base is `main` | Zero or several matches fail closed; the policy keeps checking merge commit, base and tree. |
| `published_at` | the pull request's `mergedAt` (RO-3) | GitHub returns canonical UTC, and the value is stable across re-runs of the same release. The Catalog contract defines only the format of `publishedAt`; the Registry contract calls its `publishedAtUtc` audit and display metadata. |
| `owner_self_approval_exception` | removed under governance O-2 (owner approves agent pull requests); otherwise derived as "review decision is not `APPROVED`" | Every identity check stays: dispatcher, pull request author and repository owner are the same account. |
| `source_branch` | removed (item 3) | - |

Without inputs, the operator's binding moves to the approval: the candidate
writes version, source SHA, pull request, notes digest and difference summary
to the job summary, and the owner reads them before approving `release`.
Optional later step (RO-4): start the candidate automatically when `ci`
completes on a `main` merge of a release pull request (`workflow_run`), keeping
`workflow_dispatch` for manual runs.

Also found, for RO-5: for a self-approved release, the policy stops requiring
an exact-head Codex review only for versions from 1.0.8 up to, not including,
1.2.0 (`release_promotion_policy.py:616`, record
`RELEASE-108-OWNER-REVIEW-DEFER-01`). The planned 1.2.0 release would need the
Codex GitHub review bot again, which conflicts with the rule that no process
requires two agent products. With O-2's separate identity the exception and
this requirement disappear together.

## 5. Release re-run conflict

Causes: (a) the validator's history replay and ADR 0061's single containment
exception; (b) review-thread and approval gates are first evaluated after the
release pull request merged, so a finding leaves `main` ahead of every tag and
forces new pull requests into `main`. Main's ruleset also requires up-to-date
branches, so every release pull request must contain `main`.

Proposal:

- (a) The governance ADR's G1 removes history replay. Until it lands, board
  decision 23 applies.
- (b) A release pull request cannot merge with an unresolved P0/P1 thread
  (governance O-3; recommended: the ruleset's conversation resolution). The
  three release boundaries keep their P0/P1 check as defense in depth.
- The frozen release-branch head runs the release rehearsal (item 6) before the
  release pull request merges, so Golden, packaging, notes and catalog failures
  surface on the branch.
- Recovery, written into branch governance: after a failure following the
  merge, a fix branch from `main` goes to `main` by pull request, and a new run
  is dispatched; the Release Closure Record lists each failed run.
- Same-run re-runs stay as the contract says: re-run only a failed promotion.
  A failed candidate gets a new run. The release keeps the default
  `overwrite: false` for its candidate artifact, whose digest the tag message
  binds; the CI evidence proposal's `overwrite: true` must not be copied here.

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
  it exercises the release code. It skips the candidate manifest, which needs
  a real review snapshot, and says so. It is also the staging path for this
  cleanup.
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
- The parity job becomes the reusable workflow of item 2.
- Optional (RO-7): the candidate splits into parallel read-only jobs, Golden
  verification and packaging, with the manifest and upload after both (1.1.12
  WS-GOV decision 7; about 232 s and 332 s in 1.1.12). The contract sentence
  "missing, failed or skipped required Golden cases block packaging" becomes
  "... block the candidate manifest, upload and promotion".
- Optional (RO-8): the candidate stops waiting for the push-to-`main` CI run.
  It accepts that run as pending but not failed, and pre-tag admission
  (already fresh at promotion) requires it to have succeeded. The candidate
  then overlaps the roughly 9-minute post-merge CI instead of following it;
  no tag is created before exact-source CI passes.

Estimate: `release.yml` shrinks to roughly 600 to 650 of its 1,236 lines,
from the step spans above (about 270 lines of dead paths, 180 of inline
collection, 85 of duplicated authority, 60 of identity and input handling). The Python policy
loses the 360 version-only lines and gains the moved collection.

## Batches and order

| Batch | Content | Depends on |
| --- | --- | --- |
| R-0 | This design, owner decisions, the promote-condition bug | - |
| R-1 | Items 1 and 3, release floor, `promote` condition fix | RO-2, RO-10; ADR 0033 amendment and doc updates in the same pull request |
| R-2 | Item 6 rehearsal and the composite setup action for the release and rehearsal jobs | RO-6 |
| R-3 | Item 4 derived inputs; collection moved into the policy; RO-5 | Governance O-2 decided |
| R-4 | Item 5 thread gate and recovery text | Governance G1; O-3 setting |
| R-5 | Item 2 predecessor parity | Comparator item and its ADR; RO-1 |
| R-6 | Optional speed items (RO-7, RO-8); `ci.yml` adopts the composite action | CI failure-evidence change merged |

R-1 and R-2 fit 1.1.13 if the owner answers RO-2 and RO-6 early; R-3 and R-4
follow the governance ADR; R-5 follows the comparator.

## Verification

- **Policy tests** for every changed rule, with negatives: zero or several
  pull requests for a merge commit, a non-`main` base, a malformed `mergedAt`,
  a version at or below the floor, a present tag. The existing workflow and
  policy tests (`tests/scripts/test_release_package_policy.py`,
  `test_release_promotion_policy.py`) are updated, not bypassed.
- **Rehearsal (from R-2):** each batch branch runs `release-rehearsal.yml`
  before its pull request merges and records the run.
- **Candidate dry run:** `release.yml` can run only from protected `main`.
  Proposal for R-1: a `dry_run` input that runs the candidate (admission,
  Golden, package, smoke, notes, handoff, manifest), reports instead of failing
  on the release floor, and skips `promote` and `published-smoke`. It fills the
  dry-run gap ADR 0033 names and runs after each release batch merges, before
  the next real release.
- **Staging release (RO-9):** a scratch repository with the same rulesets and
  environments, for one end-to-end promotion with a throwaway version before
  R-3 (the batch that changes the promotion path). Otherwise the first real
  release after R-3 is the test, with the existing recovery rules.
- **First real release** after each batch: the Release Closure Record names
  the batch and every run.

## Owner decisions

- **RO-1 v0.9.16 certification.** Recommended: supersede ADR 0057's release
  gate with the rolling comparator and retire the 2.0.0 terminal
  certification. Alternative: move the three jobs unchanged into a manual
  `v0916-certification.yml` for a one-time 2.0.0 run.
- **RO-2 Maintenance releases.** Recommended: A (delete). Choose B if a
  future line such as `1.1.x` after 1.2.0 needs releases.
- **RO-3 `published_at`.** Recommended: the release pull request's `mergedAt`.
  Alternatives: the run start time; keep the input.
- **RO-4 Commit binding and start.** Recommended: derive the commit and show
  the identity before approval; automatic start later, if wanted.
- **RO-5 Self-approval exception and the 1.2.0 Codex requirement.**
  Recommended: follow governance O-2; if one identity remains, derive the flag
  and drop the Codex-only requirement in favor of the governance review rule
  (this reverses the 1.0.8 deferral's restoration at 1.2.0).
- **RO-6 `main-package`.** Recommended: A (release rehearsal).
- **RO-7 Parallel candidate.** Confirm the 1.1.12 decision and the contract
  sentence change.
- **RO-8 Source CI at pre-tag only.** Recommended: yes.
- **RO-9 Staging repository** for R-3. Recommended: yes, one run.
- **RO-10 Inventory additions:** `-ManualOnly` and the historical version
  branches (item 1), the promote condition (item 2), the 1.2.0 Codex
  requirement (item 4), the dry-run gap (verification).

## Conflicts with other work

- **CI failure evidence (R3, `ci.yml` and `scripts/verify.py`):** one writer.
  The composite action enters `ci.yml`, and the governance G2 rename and tiers
  land, only after that change merges. The release keeps `overwrite: false`.
- **Governance ADR:** the required-check rename changes
  `REQUIRED_RELEASE_CHECKS`, the ruleset and the release admission together;
  no release may run between them. O-2 decides item 4's self-approval path.
- **Pre-built catalog (ADR 0077):** likely adds a build step to packaging and
  a check to the candidate; `package.ps1` needs one writer, so R-1's parameter
  removal and the catalog snapshot step are sequenced by the commander.
- **Formal 1.x comparator:** owns the comparator, the declaration format and
  the plan fixes (`BUG-20260925-v0916-plan-nt51950-tp-work-correction-missing`,
  `BUG-20260925-nt51950-cascade-ctrlram-plan-base`); this design owns only the
  release integration.
- **1.1.13 release:** whatever lands before the release branch is cut ships in
  the workflow that releases 1.1.13; R-1 and R-2 are the realistic set.
