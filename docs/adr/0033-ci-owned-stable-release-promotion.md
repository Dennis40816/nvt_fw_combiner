# ADR 0033: Promote stable releases through one protected CI workflow

- Status: Accepted; amended 2026-07-28 for independent `v0.9.17` maintenance publication
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
source without merging its commits to `main`; the first and currently only
approved pair is branch `0.9.17` with `VERSION=0.9.17`. Before any tag exists,
the workflow:

1. confirms the workflow definition is current protected `main`, the selected
   product SHA is the exact source-branch head, and its tree equals the reviewed
   final-PR tree;
2. validates version and release-note consistency;
3. checks required review and CI evidence;
4. runs `python scripts/verify.py --all`;
5. builds the closed-allowlist Windows package, SBOM, provenance, and hashes;
6. smokes the package; and
7. uploads short-lived candidate artifacts bound to the run id, source SHA,
   source tree, version, and digests.

An ordinary `main` push does not automatically package. `main-package` becomes a
reusable/manual preview path or is retired once the promotion workflow provides
the same reviewed capability. It cannot create fallback prereleases.

### Protected promotion phase

The workflow then waits at the protected `release` environment. This approval
is the final tag confirmation. After approval, a narrow job receives
`contents: write` and:

1. rechecks that the prepared SHA/tree, selected release-branch identity, and
   protected-`main` workflow authority have not changed;
2. rejects any pre-existing stable tag or conflicting Release;
3. creates one annotated stable tag for the prepared SHA;
4. checks out and verifies the immutable tag identity;
5. publishes the already verified immutable candidate assets and complete notes;
6. confirms GitHub's tag-derived source ZIP and tar.gz downloads resolve; and
7. downloads the published Windows assets into a fresh directory, compares all
   digests/provenance identity.

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
- A failure after tag creation never moves or replaces the tag. A recovery run
  may attach missing assets only when tag SHA, source tree, candidate run id,
  version, and every digest match exactly; otherwise a new version decision is
  required.
- Stable assets are never clobbered.

## Migration

Until the new workflow, branch rules, environment protection, tests, and human
review are all active, the existing manual-tag `release.yml` remains the only
executable release path. The implementation phase updates `.github/AGENTS.md`,
workflow documentation/templates, branch governance, package documentation, and
repository validation together; this planning ADR alone grants no workflow
permission.

## Verification

- architecture/policy tests prove PR workflows have read-only permissions and
  never use `pull_request_target`;
- workflow tests cover invalid version, non-main SHA, tree mismatch, missing
  notes, failing candidate verification, rejected approval, existing tag,
  conflicting asset, and idempotent matching recovery;
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
