# ADR 0059: Activate one audited trusted initial capability checkpoint

- Status: Accepted design — exact-head owner activation pending
- Date: 2026-08-26
- Owners: Repository owner, architecture owner
- Risk: R2 repository governance; inherited R3 authorities remain separate
- Extends: ADR 0054

## Context

The schema-v2 lifecycle was introduced after migration records had already been
committed as `design-active`. Those records bind different historical bases,
one record changed an admitted field before the immutable-history rule existed,
and none can form the direct-child final evidence sequence now required by ADR
0054. Supplying an arbitrary trusted SHA, filling final fields in place, or
deleting the files silently would hide rather than explain that history.

The repair must establish one auditable starting boundary without weakening the
ordinary post-boundary lifecycle. It must not rewrite Git history, hard-code a
moving commit into the validator, infer owner approval from passing tests, or
convert an R3 firmware/release decision into repository-local approval.

## Decision

Activation uses two commits:

1. A normal reviewed implementation commit adds this contract, validator logic,
   tests, and its design-active migration record. The exact commit and tree are
   presented to the repository owner. It is not itself the checkpoint.
2. After explicit owner approval, its direct child adds exactly
   `docs/governance/trusted-initial-capability-checkpoint.v1.json` and deletes
   exactly every legacy change record inventoried by that manifest. No other
   path may change. The activation commit becomes the trusted initial evidence
   checkpoint.

The immutable manifest binds:

- one fixed checkpoint ID;
- the reviewed implementation commit and its exact Git tree;
- a non-empty owner-decision reference;
- every direct legacy change-record path, task ID, risk, state, and SHA-256 of
  its exact reviewed Git blob; and
- every legacy R3 task plus its still-required `firmware-owner` or
  `release-owner` authority.

Validation derives the checkpoint from the first commit that adds the manifest.
It proves the sole parent is the declared reviewed head, recomputes its tree,
requires an exact inventory of the reviewed record directory, checks every
record blob and declared fact, checks that activation added only the manifest
and deleted the complete inventory, and rejects later manifest or retired-file
mutation. Retired task IDs stay reserved. Only the inventoried pre-activation
lifecycle is excluded; every record and governed change after activation uses
the complete ADR-0054 lifecycle and checkpoint coverage unchanged.

R3 remains independent. The manifest's open-R3 list is not approval. Typed
attestations are direct JSON children of
`docs/governance/external-authority-attestations` and bind an exact reviewed
head, authority type, approving reviewer, and concrete evidence. Each evidence
commit is that reviewed head's direct child and adds only its attestation batch.
For an ordinary R3 final record, the attested reviewed head must equal that
record batch's immutable final-evidence commit. Initial migrated R3 tasks bind
the later exact release head approved by their owner. Attestations remain
immutable historical evidence; a later R3 task receives a new task ID and its
own exact-head attestation. Missing, extra, altered, restored, or wrong-head
attestations fail.

## Consequences

- The reviewed implementation may be committed and tested before owner
  activation, but the pending-checkpoint gate remains red.
- Activation cannot launder a missing or changed legacy record, expand into an
  unrelated path, or create a reusable wildcard.
- Historical task IDs cannot be recycled after their records are retired.
- Ordinary future batches retain exact integration-base, path partition,
  reviewed-head, digest, direct-child evidence, and immutable-history checks.
- Firmware/support, package, signing, protected-environment, and release
  decisions remain human R3 gates even after activation.

## Alternatives rejected

- Hard-code a trusted SHA in Python: makes policy changes require code edits and
  does not prove owner approval or the retired inventory.
- Use only `checkpoint..HEAD`: ignores exact legacy bytes, record identity, and
  the activation transition.
- Delete or move old records in a cleanup commit: hides the immutable-field
  contradiction and permits later task reuse.
- Rewrite/squash shared history: unnecessary destructive mutation of reviewed
  commits.
- Mark every legacy record final in place: invents review heads, digests, and
  R3 approval that did not exist.

## Verification

Tests cover missing authority, valid activation, incomplete inventory, blob-hash
drift, extra activation paths, reviewed head/tree binding, immutable manifest,
retired-record restoration and task reuse, strict post-activation batches,
pending R3 authority, exact-head attestation completion, wrong-head evidence,
and immutable evidence retained across later unrelated commits.
The canonical repository validator remains the only enforcement entry point.
