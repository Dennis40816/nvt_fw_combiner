# ADR 0054: Finalize capability-reuse records against a reviewed Git state

- Status: Accepted mechanics — trusted initial checkpoint authority pending
- Date: 2026-08-25
- Owners: Architecture owner, repository governance owner
- Risk: R2 repository admission and review evidence
- Supersedes: capability-reuse record schema v1 lifecycle

## Context

Schema v1 admitted exact mutable paths against one integration-base diff, but
left every record active and its final review pending. After a candidate was
reviewed, nothing bound the admission to the reviewed commit or its Git path
states. A surviving active record could be mistaken for authority in a later
batch, while editing or deleting historical review evidence was not detected.

The remedy must stay inside the canonical repository validator. A database,
signature service, second verifier, or speculative multi-stage workflow would
add failure modes without improving the local Git evidence contract.

## Decision

Capability-reuse records use schema v2 and one bounded lifecycle:

- `design-active` is an uncommitted, Git-tracked admission for the current exact
  integration base and governed diff. If that state appears in `HEAD`, the
  validator rejects it as a final candidate; it cannot authorize a later
  batch. Its first committed blob persists the admitted fields, which every
  later active or final record must preserve.
- `final-complete` binds `implementationHead` and `reviewedHead` to the same
  frozen commit, records an independent final review, and stores a SHA-256
  digest of each declared path's committed blob bytes and tree mode or deletion
  marker.
  That reviewed commit must contain the same task as `design-active`;
  finalization preserves admitted design fields and changes only lifecycle and
  final-review evidence.
- `blocked` authorizes no path and carries no implementation/final evidence.

The first committed `final-complete` record is immutable. Validation walks the
current branch history to reject every later commit-level record mutation,
deletion, type change, or rename, including change-then-restore. A committed
final record remains auditable but is archived for coverage purposes, so a new
batch requires a new task ID and base. A not-yet-committed final record covers
the candidate while its evidence commit is prepared.

The evidence commit is the direct child of `reviewedHead`, changes no governed
path, and finalizes the complete batch. The batch records exactly partition the
governed diff from the preceding valid evidence checkpoint to `reviewedHead`.
Clean committed changes after that checkpoint remain visible and fail when no
new record covers them. A new active record must name the checkpoint exactly;
choosing an arbitrary older ancestor is rejected.

The record parser reads the staged Git index blob, rejects intent-to-add, and
requires byte-identical index/worktree content. Record history comparisons use
exact Git blob bytes and never normalize line endings.
Only direct children of `docs/governance/change-records` are records. Nested
JSON paths are rejected before parsing or coverage, including a committed
nested record later moved to the direct parent.

Path-state calculation reads Git objects, not checkout files. It is therefore
independent of platform CRLF conversion and includes exact repository modes.
All Git invocations use fixed argument arrays. Complete history is mandatory;
shallow clones fail closed, and CI uses `fetch-depth: 0`.

The repository has no mechanically trusted initial checkpoint yet. A record
cannot nominate its own trust root. Until the existing owner authority accepts
an initial boundary through a separate policy decision, the validator reports
a typed pending-checkpoint error and the migrated records remain unfinalizable.
Likewise, schema v2 does not create or replace R3 human authority: firmware and
release owner approval, byte/golden and exact-range evidence, signing,
permission, and protected-environment gates remain authoritative outside this
record.

## Consequences

- The implementation commit is intentionally not mergeable while its records
  remain `design-active`. Review occurs at that exact commit, then a separate
  evidence commit finalizes all records.
- Later batches do not recompute coverage from archived mutable paths.
- Rewriting a reviewed commit, changing bytes or modes, deleting/renaming a
  declared path without the matching digest, or tampering with its final record
  is detected.
- History traversal is proportional to the small change-record directory and
  happens only in repository policy validation.
- Existing schema v1 records migrate to `design-active` or `blocked`; no
  implementation or review evidence is invented during migration.
- The current migration is intentionally non-mergeable until the trusted
  initial checkpoint and applicable R3 owner gates are provided.

## Alternatives rejected

- Keep v1 and rely on PR prose: does not bind evidence or prevent reuse.
- Store approvals in a service/database: unnecessary authority and operational
  complexity for repository-local evidence.
- Sign each record: signature/key lifecycle is outside this R2 contract.
- Replay a general state machine over every commit: more machinery than the
  three required states and first-final immutability need.

## Verification

Behavioral tests cover staged/index equality, intent-to-add, pending initial
authority, active-record reuse/deletion, exact checkpoint coverage, clean
committed changes, final head/digest binding, direct-child evidence, forbidden
governed evidence changes, later batches after archival, blob-byte and mode
changes, deletion and rename states, change-then-restore tampering,
shallow-history failure, and CI full-history checkout. The canonical structure
verifier remains the only repository gate.
