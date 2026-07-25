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

## Establish Branch Authority

1. Keep `main` stable and release-capable.
2. Create the owner-selected integration branch named exactly for the version,
   such as `0.9.12`, from the peeled predecessor tag/current reviewed `main`.
3. Record predecessor tag, peeled SHA, main SHA, and tree identity.
4. Create independently reviewable work as `feature/<version>/<topic>` from that
   version branch. Merge it back to that version branch, never directly to
   `main`.
5. Use one writer for each mutable surface, a read-only reviewer/supervisor, and
   a separate integrator for final admission. A chat handoff never overrides the
   current Git tree.

Names and timestamps are hints, not authority. Before reconstruction or replay,
verify ancestry, tree/patch differences, open PR intent, and a recovery ref. A
branch that started before the final predecessor tag is not a valid release base
merely because its name contains the new version.

### 0.10.x maintainability-program integration exception

Owner decision, 2026-07-25, recorded in
[ADR 0038](../adr/0038-0.10.x-program-integration-branch.md): the `0.10.x`
maintainability program may use one
long-lived integration branch named `0.10.x`, created from a verified
`origin/main` SHA. This is a program-integration exception, not an exact release
version and not a release branch.

- An owner-bounded stage may use a subordinate exact-version integration branch
  such as `0.10.1`, created from the current `0.10.x`. Its independent features
  use `feature/<exact-version>/<topic>` and target that subordinate branch.
  The completed exact-version branch then uses a reviewed PR to `0.10.x`.
- A bounded program-wide slice may use `feature/0.10.x/<topic>` and target
  `0.10.x` directly. Both routes retain normal CI, exact-head review, evidence,
  and R2/R3 admission rules.
- Slice and subordinate-version PRs into `0.10.x` cannot start a release
  workflow or create a tag, package publication, GitHub Release, or
  release-promotion run.
- A final `0.10.x` to `main` integration PR is permitted only after the
  maintainability integration gate, protected CI, required human gates, and an
  explicit owner approval. That final boundary enters the release workflow;
  tagging and publication remain separate owner-approved actions.
- Existing feature branches based on an earlier `0.10.1` line are candidates,
  not merge authority. Rebase or replay them onto `0.10.x`, re-run admission,
  and obtain review for their new exact heads.

## Admit Work To A Version

Every feature PR must state:

```text
primary release outcome and non-goals
risk class and affected layers
workflows, ICs, modes, profiles, contracts, and address spaces affected
support promotion or explicit support-neutral status
narrow tests, final verification, and human gates
user-facing release-note entry
rollback or compatibility impact
```

Admit the PR only when its exact head is reviewed, its target is the exact version
branch, P0/P1 findings are closed, required CI/tests are green, and R3 evidence is
not hidden behind a TODO. Merge-tree equivalence must be checked when the host
creates a merge commit.

## Require Complete Release Notes

Starting with `v0.9.12`, a stable Release is incomplete without human-readable
notes. Auto-generated commits may be appended, but they do not replace this
structure:

```markdown
## Summary
Who benefits and what the release changes.

## Feature changes
### Feature name
- Before:
- After:
- Affects: screen/workflow, IC/mode/persona
- Support status: promoted | unchanged/support-neutral | removed
- Compatibility or migration:
- Verification:
- Limitations/deferred:

## Fixes
User-observable corrections and their impact.

## Performance and package
Only measured startup, responsiveness, working-set, and size claims.

## Known issues and human gates
Anything not verified or intentionally support-neutral.

## Downloads and integrity
Portable ZIP, source archives, SBOM, provenance, hashes, and platform/runtime.
```

Do not list only commit subjects. Do not imply firmware support from authoring UI,
synthetic tests, or available profiles. Do not expose private evidence paths,
firmware names, secrets, or internal audit identifiers.

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
