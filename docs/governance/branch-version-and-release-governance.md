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
