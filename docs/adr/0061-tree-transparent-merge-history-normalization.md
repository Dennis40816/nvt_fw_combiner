# ADR 0061: Normalize tree-transparent containment merges in evidence history

- Status: Proposed for exact-head release-owner approval
- Date: 2026-08-29
- Owners: Repository-governance owner, release owner
- Risk: R3 immutable release-governance evidence

## Context

The canonical repository validator audits every commit after an immutable
capability record or external-authority attestation with `diff-tree -m`.
That is normally fail-closed: each parent edge of a merge remains visible, so a
merge cannot conceal a mutation.

GitHub's v1.0.2 merge commit `017d5390` is a redundant containment node. It has
two parents, its complete tree equals only reviewed parent `298fec58`, and its
other parent `0328f550` is already an ancestor of `298fec58`. The reviewed
parent passes repository validation and the merge adds no tree change, but
`diff-tree -m` repeats the reviewed branch's changes against the older parent.
The validator therefore reports valid immutable records and attestations as if
the merge had modified them again.

## Decision

When commit-level immutable-history auditing first observes that its target
path changed on a candidate merge node, it may suppress that candidate node
only when all of these facts are proven from Git:

1. the candidate has exactly two parents;
2. its complete tree equals exactly one parent tree; and
3. the other parent is an ancestor of the tree-equivalent parent.

The validator still traverses and audits every ancestral commit. Any Git
parent, tree, or ancestry lookup error fails closed. A one-parent commit,
octopus merge, tree different from both parents, tree equal to both parents,
non-contained parent, real mutation, or changed-then-restored path is not
normalized.

This is a semantic normalization of one duplicate merge observation. It is not
a trusted-history exception, a repeatable recovery checkpoint, or authority to
rewrite, retire, replace, or re-attest existing evidence.

## Consequences

- Tree-transparent GitHub containment merges do not make already-reviewed
  immutable evidence appear modified a second time.
- Genuine mutations on either ancestry remain visible and fail validation.
- Capability checkpoints, change-record lifecycle, external authority,
  firmware evidence, release evidence, and finalization rules do not change.
- The implementation stays inside the canonical repository validator; no
  second verifier or trusted-SHA bypass is introduced.

## Rejected options

- Removing `-m` globally could hide a real merge mutation.
- Hard-coding the v1.0.2 commit would create a one-off bypass.
- Rewriting release history would destroy published provenance.
- Creating a second recovery checkpoint or retiring valid records would alter
  correct evidence to compensate for a validator interpretation defect.

## Verification

- Synthetic Git-history tests reproduce the exact two-parent containment
  topology for both final records and external-authority attestations.
- Negative tests cover distinct merge trees, non-contained parents, underlying
  mutations and restoration, genuine attestation mutation, and Git inspection
  failure.
- The exact v1.0.2 merge head must pass the complete repository validator after
  finalization without changing any firmware, package, or evidence bytes.
