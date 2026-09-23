# ADR 0071: Preserve change provenance with unique final integration ownership

- Status: Accepted design; checkpoint amendment approved 2026-09-23; implementation and candidate verification pending
- Date: 2026-09-10
- Owner: Repository owner, explicitly approved preserving historical records and unique final ownership
- Risk: R2 governance
- Amends: [ADR 0054](0054-finalize-capability-reuse-records.md) final path coverage and
  [ADR 0070](0070-bounded-local-r1-continuation.md) integration of successive local corrections

## Decision

Keep immutable `mutablePaths` as change provenance. Allow the existing schema-v2
final record to add optional `integrationPaths` as its final responsibility.
The [record contract](../governance/capability-reuse-record.md#lifecycle) owns the
exact validation rules. This uses the existing validator and evidence lifecycle,
not a new checkpoint mechanism or a second source of firmware semantics.

Final ownership is a strict governed subset of the record's original paths.
The admitted union must equal the governed checkpoint diff; final ownership must
cover that same diff exactly once. All records finalize together against the
same reviewed source; every original path remains in its record's digest.
Empty ownership never removes review, minimum risk or R3 owner obligations.
Historical records without the optional field retain their prior semantics.
Exact auxiliary evidence paths, including the status-only 1.1.10 delivery
document admitted by the record contract, remain in their original admissions,
reviewed diffs and complete digests; they never enter `integrationPaths` or
become a second product authority. Current canonical specification and
architecture owners are governed paths, even when their Markdown syntax
resembles an ordinary handoff. This classification does not rewrite earlier
admissions or change the final checkpoint lifecycle.

Owner amendment, 2026-09-23: a committed active admission may have named an
intermediate product commit as `integrationBase` rather than the last sealed
final evidence checkpoint. Preserve that erroneous field and the original
first-active blob. Permit only a final-only, independently reviewed
`checkpointReconciliation` under the [record contract](../governance/capability-reuse-record.md#lifecycle).
The existing validator derives the checkpoint from historical replay and
checks Git ancestry through the original base and first-active commit; the
new evidence cannot reset the checkpoint. Final review still covers the full
checkpoint-to-reviewed diff and every original mutable path, with unique
ownership, digest, direct-child evidence, external R3 and release gates.
Unneeded or premature reconciliation and changes to sealed final records fail.
This does not claim the original admission met the checkpoint rule when filed.
The record contract fixes the document-classification cutover at the last
sealed final evidence checkpoint before those exact paths became governed.
Earlier final batches are verified under their original classifier, while
current and later batches use the new one. This preserves historical admission
meaning without exempting any batch from its applicable coverage or ownership
checks.

## Why and migration

Successive authorized UI corrections legitimately touch the same file. Treating
every historical modification as exclusive final ownership made that workflow
impossible to integrate without rewriting immutable history. Instead reviewers
assign final responsibility explicitly while retaining all contributing records.

For the current 1.1.4 batch, review original admissions and actual changes,
admit missing local R1 paths with an explicit post-implementation integration
review, freeze source, review the partition, then finalize the complete batch.
Never claim that missing historical admission existed before implementation.
No task-ID exception, record retirement or trusted-initial reactivation is used.

## Alternatives and verification

Rejected: rewriting old paths, dropping duplicate checks, ignoring unrecorded
files, resetting the trust root, or replacing Golden/CI with metadata checks.
A separate integration manifest adds a second lifecycle without a demonstrated
need; the existing final record already binds source, digest and reviewer.

Tests use real Git lifecycle transitions to cover overlap and empty ownership,
legacy defaults, gaps, duplicates, malformed/subset violations, stale admitted
paths, forbidden active use, immutable final partitions and retained R3 authority.
Full candidate Golden, protected CI, package validation and release approval
remain required; this decision alone neither publishes nor certifies 1.1.4.
