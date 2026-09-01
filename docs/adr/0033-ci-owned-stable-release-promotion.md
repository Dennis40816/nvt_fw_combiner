# ADR 0033: Promote stable releases through one protected CI workflow

- Status: Accepted; amended 2026-09-02 for multi-suite exact-check admission
- Date: 2026-07-22
- Owners: Product owner, release owner, security/repository owner

## Context

The `v0.9.13` path requires a human to create the stable tag before dispatching
the release workflow. The same candidate is then verified repeatedly by PR CI,
`main` push CI, `main-package`, and `release`. `main-package` also packages every
ordinary `main` push and can publish a fallback prerelease. The stable Release
body relies on generated PR/commit notes, which do not satisfy the repository's
complete release-note contract.

This consumes unnecessary GitHub Actions time, makes the tag step depend on a
local toolchain, and can leave a tag before the exact package has passed its
candidate gates. The release must still run from an immutable stable tag
reachable from protected `main`, use least privilege, and retain an explicit
human release-authority decision.

## Decision

`v0.9.14` will introduce one stable promotion workflow with pre-approval and
post-approval phases.

### Development and final PR timing

Feature PRs target the exact version branch. They open when a coherent slice is
reviewable, not merely when work begins. Superseded runs use per-PR concurrency
cancellation. Draft/early changes run lightweight policy checks; the full
required matrix runs for review-ready slices and always for the frozen final
version PR to `main`.

Release-note fragments are version-controlled with their feature slices. Before
the final PR opens, CI assembles and validates a complete human-readable version
section. Generated commit lists may be appended but cannot replace Before/After,
affected workflow, support state, compatibility, evidence, limitations, upgrade,
and integrity guidance.

### Candidate phase

The promotion run is always dispatched from the exact current protected `main`
SHA. The product source is normally that same SHA. An explicitly
owner-approved maintenance branch/version pair may instead provide the product
source without merging its commits to `main`; the approved pairs are branch
`0.9.17` with `VERSION=0.9.17`, branch `0.9.18` with `VERSION=0.9.18`, and
branch `0.9.19` with `VERSION=0.9.19`.
Before any tag exists,
the workflow:

1. queries fresh GitHub evidence and confirms the workflow definition is the
   exact current remote `main`, GitHub reports `main` as protected, its applied
   rules require exactly `policy / polytail`, `python-worker / verify`, and
   `dotnet / build-test`, the selected product SHA is the exact source-branch
   head, and its tree equals the reviewed final-PR tree;
2. validates version and release-note consistency;
3. proves check-run and review-thread pagination is complete, requires one or
   more GitHub Actions runs for each closed required-check name and requires
   every same-name run to bind the exact reviewed head with completed/success
   status, and classifies every unresolved review thread from
   one recognized P0-P3 marker; unresolved P0/P1 or unclassifiable evidence
   blocks, while independently scoped P2/P3 may remain visible and proceed in
   parallel;
4. runs `python scripts/verify.py --all`;
5. builds the closed-allowlist Windows package, SBOM, provenance, and hashes;
6. smokes the package; and
7. uploads short-lived candidate artifacts bound to the run id, source SHA,
   source tree, version, and digests.

For `v1.1.1` and later, the same admission also requires an active tag ruleset
covering `refs/tags/v*` that prevents update and deletion. Missing, malformed,
truncated, duplicated, or contradictory machine-readable GitHub evidence fails
closed. One bounded read-only collector in `release_promotion_policy.py` owns
main-rule, exact check-run, review-thread/comment, and stable-tag-ruleset
collection and validation; the workflow remains the only mutation owner. The
collector follows every review-thread page and every unresolved thread's comment
pages, rejects incomplete/non-advancing pagination, and permits only GitHub GET
and GraphQL reads. GitHub omits `bypass_actors` from ruleset detail responses when
the caller lacks ruleset write access, so the least-privilege workflow does not
claim that an omitted field proves an empty bypass list. When the field is
visible it must be exactly `[]`; otherwise admission fails. The protected
environment's release owner must inspect and attest the omitted no-bypass
setting.
These requirements begin at `v1.1.1`; historical Release evidence is not
relabeled.

The same exact commit may carry both final-PR and protected-main-push check
suites. Duplicate same-name evidence is admitted only when at least one match
exists and every match has the exact reviewed head, `appSlug=github-actions`,
`status=completed`, and `conclusion=success`. A mixed failed, pending,
wrong-head, or wrong-app duplicate fails closed; the workflow never selects one
latest or successful run to hide another result. Non-required in-progress runs
may retain GitHub's null conclusion in the collected snapshot, but a completed
run with a null conclusion is malformed and a pending required run cannot pass.

An ordinary `main` push does not automatically package. `main-package` becomes a
reusable/manual preview path or is retired once the promotion workflow provides
the same reviewed capability. It cannot create fallback prereleases.

### Protected promotion phase

The workflow then waits at the protected `release` environment. This approval
is the final tag confirmation. After approval, a narrow job receives
`contents: write` and:

1. checks out policy bytes from the exact prepared candidate workflow SHA, not
   a moving `main`, and rechecks through a fresh GitHub snapshot that the
   prepared SHA/tree, selected release-branch identity, protected-`main`
   authority, closed checks, review-thread disposition, and tag ruleset have not
   changed;
2. rejects any conflicting, moved, or non-exact stable tag or Release; an
   explicitly allowed historical recovery may validate an exact existing
   annotated tag without replacing it;
3. creates one annotated stable tag for the prepared SHA only when that tag is
   absent;
4. checks out and verifies the immutable tag identity;
5. publishes the already verified candidate assets and complete notes;
6. confirms GitHub's tag-derived source ZIP and tar.gz downloads resolve; and
7. requires the REST Release record to report `immutable=true` and the exact
   closed asset set with `state=uploaded`, exact byte size, and
   `digest=sha256:<candidate SHA-256>`; and
8. downloads the published Windows assets into a fresh directory and compares
   every byte digest/provenance identity independently of REST metadata.

The selected branch head and remote protected-main SHA/flag are read again
immediately before every tag or Release mutation. A new tag requires the source
to remain the exact current branch head. Existing-tag recovery may observe an
advanced branch only when fresh comparison evidence proves the candidate is
still its ancestor. Any protected-main drift, source divergence, or protection
loss fails closed.

Candidate admission, pre-tag admission, and pre-Release admission are three
distinct temporal boundaries. Each invokes the same collector for fresh
evidence; no boundary reuses another boundary's snapshot. Pre-tag collection,
validation, and tag mutation remain in one workflow run block so no later step
can mutate from a stale successful result.

A separate `contents: read` job then downloads the published package and runs
the protected-main smoke tool. Download and execution are separate steps; the
package execution step receives neither `GH_TOKEN` nor `GITHUB_TOKEN`.

The stable publishing steps therefore run only after an immutable tag exists,
even though CI rather than a local operator creates that tag.

## Security and failure behavior

- Workflow permissions default to `contents: read`; only the approved promotion
  job receives `contents: write`.
- Pull-request code never receives release secrets, a write token, or access to
  the protected release environment.
- The `contents: write` job never checks out or executes product-source code.
  It observes the source commit/tree through GitHub and invokes only the
  protected-main policy. Candidate binaries run only in the later token-free,
  read-only smoke job.
- A maintenance source is accepted only through an explicit branch/version
  allowlist, an exact merged PR targeting that branch, exact-head required
  checks/review evidence, and the same protected release approval. It gains no
  authority to modify or merge into `main`.
- The normal path requires an exact-head GitHub approval. When GitHub forbids
  the repository owner's sole identity from approving its own PR, an explicit,
  default-off dispatch exception may be used only when that same repository
  owner authored the merged PR and dispatched the workflow. The exception also
  requires a completed exact-head Codex response and permits only the
  absent-review or GitHub `REVIEW_REQUIRED` self-approval state; it is rejected
  on an ordinarily approved PR. Codex is excluded from ordinary approvals, so
  its review cannot satisfy that path. Evidence may be a pull review, an inline
  pull-review comment bound to the head SHA, or a Codex issue comment that
  explicitly names the reviewed head prefix. The snapshot records its source
  and identity; the exception cannot bypass a `CHANGES_REQUESTED` decision, CI
  checks, immutable-source checks, or the protected `release` environment's
  final human approval.
- `pull_request_target` is forbidden.
- Release notes are passed as a validated file, not interpolated as executable
  PowerShell or shell source.
- A pre-approval failure creates no tag or stable Release.
- The workflow reads branch/ruleset evidence but never creates, weakens, or
  repairs repository rules. Release immutability configuration remains an
  external repository/release-owner prerequisite; the workflow verifies only
  the published Release record's REST `immutable` value after publication.
- For `v1.1.1` and later, `immutable` missing, false, or non-boolean, a non-
  uploaded asset, or any missing, extra, duplicate, size-drifted, or digest-
  drifted asset is a release failure. REST digests never replace the fresh
  download-and-hash gate.
- A failure after tag creation never moves or replaces the tag. An eligible
  historical pre-v1.1.1 recovery run may attach missing assets only when tag
  SHA, source tree, candidate run id, version, and every digest match exactly.
  v1.1.0 is excluded: it remains read-only manual-only operator evidence and CI
  rejects both rebuild and recovery; any defect requires a new version. An
  incomplete immutable v1.1.1-or-later Release likewise requires a new version
  decision.
- Stable assets are never clobbered.

## Operational prerequisites

Before `v1.1.1` publication, the repository owner must enable protection for
`main`, the exact three required checks, an active update/deletion restriction
for stable `v*` tags with no bypass actors, and GitHub immutable Releases. The
protected `release` environment and its human release-owner approval provide
the no-bypass attestation that a least-privilege token cannot read. Workflow
code cannot configure or waive these controls, and this ADR alone grants no
workflow permission.

## Verification

- architecture/policy tests prove PR workflows have read-only permissions and
  never use `pull_request_target`;
- workflow tests cover invalid version, non-main SHA, tree mismatch, missing
  notes, unprotected main, missing/stale required checks, all-green duplicate
  suites, mixed failed/pending/wrong-head/wrong-app duplicates,
  truncated pagination, unresolved P0/P1, unclassifiable review threads,
  inactive tag rules, failing candidate verification, rejected approval,
  existing tag, immutable=false, asset state/size/digest drift, and eligible
  historical matching recovery;
- a dry-run path produces candidate artifacts without tag or Release authority;
- release evidence records final PR tree, main SHA/tree, tag object and peeled
  SHA, workflow run, asset names/sizes/digests, source downloads, provenance,
  smoke result, retry history, and residual human gates; and
- required status-check names remain stable while path-aware orchestration is
  introduced beneath them.

## Alternatives rejected

- **Keep creating tags locally:** depends on the caller's local toolchain and
  can tag before the package gate.
- **Release on every tag push:** development or accidental tags would gain
  publishing authority and bypass the protected confirmation.
- **Package every main push:** repeats expensive work unrelated to a stable
  candidate.
- **Trust only the final PR artifact:** the exact merged `main` identity and
  release environment still require verification.
- **Merge every maintenance hotfix into `main` before release:** this would mix
  a compatibility-only maintenance line into the active development line. The
  protected workflow remains the release authority while the exact reviewed
  maintenance head remains the product source.
- **Regenerate notes from commit subjects:** cannot communicate support,
  compatibility, verification, limitations, or rollback accurately.

## Consequences

- The owner performs one protected promotion decision instead of a local tag
  command plus a separate workflow dispatch.
- Exact-source verification and packaging happen once in the promotion run,
  after the final PR gate and before the tag mutation.
- Ordinary development pushes consume fewer Windows minutes and less temporary
  artifact storage.
- Workflow, environment, permission, tag, and release changes remain subject to
  independent review and explicit human release authority.
