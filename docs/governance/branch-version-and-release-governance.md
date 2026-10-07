# Branch, Version, and Release Governance

Status: active for `v0.9.12` and later.

This policy turns release scope, multi-agent ownership, branch cleanup, and
release notes into reviewable decisions. It supplements `AGENTS.md` and does not
waive firmware, golden, security, signing, or release-authority gates.

## Select The Version From Product Impact

Choose the version before opening implementation branches. Do not derive it from
elapsed time, commit count, changed lines, branch age, or how difficult the work
felt.

| Change set | Version decision before `1.0.0` | Admission rule |
| --- | --- | --- |
| Compatible correction | Patch increment | Fixes incorrect behavior, reliability, accessibility, performance, packaging, or presentation inside an already declared capability. No intended workflow or public contract expansion. |
| Cohesive new capability | Minor increment | Adds or materially redesigns a user-visible workflow/capability while retaining reviewed compatibility and support truth. Scope should have one primary outcome and at most two tightly coupled supporting outcomes. |
| Breaking product/support contract | Owner decision; normally defer to `1.0.0` or a later major | Requires explicit migration, support-matrix, schema/protocol, and human approval. Pre-`1.0.0` numbering does not waive breaking-change disclosure. |

Firmware risk does not choose the version automatically. A small UI patch may be
a patch release; a one-line range or processor-order change is still R3 and
cannot enter any version without its byte-level evidence and owner gate.

When the planned outcomes no longer fit one reviewable release story, keep the
version stable and defer the excess work. Do not inflate the version simply to
hide uncontrolled scope.

## Keep One Stable Version Identity

The public application version is exactly canonical `major.minor.patch`:
three non-negative decimal components, no leading-zero aliases, prefix,
prerelease/build suffix, or fourth component. `ManagedAppVersion` is the one
runtime admission owner used by Catalog, managed state, installed-version
layout, and launcher handoff. Four-component Windows file metadata is transport
metadata and is never an accepted release identity.

The repository `VERSION` file is the source release identity. A stable release
must preserve this exact mapping:

```text
VERSION X.Y.Z
  -> Git tag vX.Y.Z
  -> package NvtFwCombiner-vX.Y.Z-win-x64.zip
  -> manifest version X.Y.Z and sourceTag vX.Y.Z
  -> Catalog row version X.Y.Z bound to that exact package and manifest
```

The existing release-promotion policy validates this mapping and its hashes.
Do not add another parser or independently normalize a tag, directory, package,
manifest, or Catalog version into a different identity.

## Establish Branch Authority

Under [ADR 0080 items 10-12](../adr/0080-governance-reset.md#branches-releases-and-ci):

1. `main` holds released code and `v*` tags. The minor-line trunk (currently
   `1.3.x`; since 2026-10-07 `1.2.x` takes only Core-free hotfixes; `1.1.x`
   retired after `v1.2.0`, board decisions 200 and 215)
   receives `feature/<version>/<topic>` pull requests.
2. Cut the release branch `X.Y.Z` from the trunk at feature freeze. It takes
   release fixes, which merge back into the trunk, and opens the release pull
   request into `main` with a merge commit. Delete it after its tag under the
   branch-cleanup authority below.
   Release and release-fix pull requests into `main` must have the exact
   `X.Y.Z` head branch matching `VERSION`. Automatic release collection skips
   a successful main push when its merged pull request has another head branch;
   manual fallback rejects that mismatch.
3. After publication, merge `main` back into the trunk through an ordinary
   merge pull request. Its approvals cite the release evidence it brings back.
   Decision 66's pre-G1-B catch-up procedure ended with G1-B.
4. Ordinary trunk merges into feature branches are allowed. Reviewed heads
   default to rebase, followed by renewed review and any required code-owner
   approval on the new head
   (board decision 117). G1-B itself only rebases under its cutover plan.
5. Follow the execution workflow's [single-writer procedure](development-execution-workflow.md#single-writer).
   A chat handoff never overrides the current Git tree.

Names and timestamps are hints, not authority. Before reconstruction or replay,
verify ancestry, tree/patch differences, open PR intent, and a recovery ref. A
branch name alone never proves the predecessor or reviewed release authority.

## Admit Work To A Version

Every pull request uses the [admission template](../../.github/pull_request_template.md)
and states its release outcome, affected workflows/ICs/modes/profiles/contracts
and address spaces, support promotion or support-neutral status, user-facing
release-note entry, and rollback or compatibility impact.

Admit it only when the completed review record binds its exact head, its target
follows the branch model above, P0/P1 findings are closed, required CI/tests are
green, and required R3 code-owner approval and role evidence are complete.
Decisions 225 W1 and 233 make owner approval R3-only on each target after its
base contains the amended CODEOWNERS/policy/checker/workflow and the owner
trials and verifies the effective rulesets. R0–R2 then merge through auto-merge
after independent exact-head review and green required CI; R2 keeps its
architecture/contract, test and scoped Polytail evidence. CODEOWNERS owns only
R3 patterns. Unswitched targets retain existing rules; there is no blanket
`main`/release exclusion from the new boundary. Release evidence and the
protected `release` environment approval remain. Follow the execution
workflow's auto-merge cancellation and re-verification before changes.
Every review thread
must be resolved, including on a release pull request. Resolution alone does
not prove that review finished; the completed exact-head review record does.
The three release boundaries retain their P0/P1 check. Check merge-tree equivalence when
the host creates a merge commit.

## Recover A Release

Recovery follows the tag and Release state:

- No tag yet: fix through a pull request from the `X.Y.Z` release branch into
  `main`. When `ci` succeeds on that merge, a new release run starts
  automatically; `workflow_dispatch` is only the fallback.
- Tag created, publication incomplete and recoverable: re-run only the failed
  promotion in the same run. The same run id, candidate artifact and digest,
  and live authority with `main` still at the workflow SHA must hold. Nothing
  merges into `main` first. Existing-tag validation applies; the
  first-publication floor that requires an absent tag does not.
- Immutable Release incomplete or conflicting: the owner decides a new
  version; the immutable Release is never repaired in place.

While a tagged release is incomplete, nothing merges into `main`. Advancing it
ends same-run recovery, and a new run cannot reuse the tagged version. A new
release checks the floor at the candidate and again at the pre-tag boundary.
History is never rewritten. The Release Closure Record lists every failed run.

## Require Complete Release Notes

Starting with `v0.9.12`, a stable Release is incomplete without human-readable
notes. Auto-generated commits may be appended, but they do not replace this
structure:

```markdown
### Summary
Who benefits and what the release changes.

### Product changes
#### Feature name
- Before → After:
- Affected: screen/workflow, IC/mode/persona
- Support status: promoted | unchanged/support-neutral | removed
- Compatibility:
- Verification:
- Limitations:

### Security
Security-relevant changes, or an explicit statement that no security boundary changed.

### Known issues
Anything intentionally deferred, support-neutral, or still requiring a human gate.

### Upgrade and rollback
Supported predecessor, upgrade path, rollback compatibility, and migration impact.

### Downloads and integrity
Portable ZIP, source archives, SBOM, provenance, hashes, and platform/runtime.
```

Do not list only commit subjects. Do not imply firmware support from authoring UI,
synthetic tests, or available profiles. Do not expose private evidence paths,
firmware names, secrets, or internal audit identifiers. The renderer requires
each canonical heading exactly once in order and requires every Product changes
feature to contain each canonical non-empty field. It permits supplemental prose
and rejects a bounded set of incomplete or secret-like tokens; it does not prove
that claims are true or detect every possible private path. Before release, an
independent human frozen-head semantic review must verify every behavior,
verification, compatibility, support, limitation, and disclosure statement
against exact evidence.

## Close PRs And Retire Branches After Release

Create an inventory containing PR/branch name, head SHA, base, last update,
ancestry to the stable tag, unique commits/patches, review state, replacement,
and proposed action.

| Classification | Action |
| --- | --- |
| `keep` | Active next-version work or independently valuable unresolved work. Route to the correct version branch. |
| `superseded` | Close the PR with a comment naming the exact replacement PR/commit/tag and retained/deferred residue. |
| `archive` | Preserve a recovery ref or bundle because intent/evidence remains valuable but active development stops. |
| `delete-candidate` | Fully merged/replaced and no unique value remains. Present the exact remote-ref list to the owner before deletion. |

Never batch-close by age or naming. Never delete a remote branch without owner
approval of the exact list. Local cleanup must also preserve other worktrees and
uncommitted user changes.

## Release Closure Record

Record the stable tag, peeled commit, reviewed-tree equivalence, workflow run,
published URL, uploaded asset names/sizes/digests, source-archive availability,
provenance identity, package smoke result, release-note review, retry history,
and every residual human gate. The next version branch may start from the stable
commit while deferred evidence remains explicit; it may not rewrite the released
tag or claim an unverified gate passed.
