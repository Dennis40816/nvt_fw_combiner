# Capability-reuse record

Status: Active fail-closed production-change contract (schema v2).

Use the [workflow's bounded local R1 path](development-execution-workflow.md#bounded-local-r1-continuation)
for eligible local corrections; it changes no record or validator semantics.
R2/R3 design admission and every formal integration candidate require a
Git-tracked JSON record under `docs/governance/change-records/` before their
admission. A new record starts
as `design-active` and is staged with a real Git index blob, but not committed,
with the admitted change. Intent-to-add is rejected. The validator parses the
index blob and requires the worktree bytes to match it exactly, so an unstaged
record edit cannot change the authority being validated.
Records kept only in ignored handoff or artifact directories do not open the
gate. A `design-active` record already present in `HEAD` is rejected as an
integration candidate; it cannot authorize another integration batch. This
rejection is not a prohibition on separately authorized bounded local R1 work.

The validator computes tracked renames, copies, additions, modifications, type
changes, deletions, and non-ignored untracked files from the latest valid final
evidence checkpoint. For ordinary admission, `integrationBase` must equal that
checkpoint; it is not a self-attested ancestor. A mistaken committed base may
be reconciled only with the final-only evidence in [Lifecycle](#lifecycle),
while the effective checkpoint still comes from replay. Both sides of a rename
or copy are audited. Every governed path in the candidate must have exactly
one effective integration owner,
and every declared `mutablePaths` path must occur in that diff. Paths are
exact, forward-slash, repository-relative names; globs and directory grants
are forbidden. A JSON record is valid only when its direct parent is exactly
`docs/governance/change-records`; nested records are rejected before parsing or
coverage and cannot later be moved into place for reuse.

The filename stem must equal its uppercase `taskId` using ASCII letter
case-insensitive comparison; other spelling differences fail. Record paths and
task IDs that collide after ASCII case folding fail, even if Git can store both
names. The validator retains each record's actual Git path for historical,
predecessor, and `HEAD` checks; it does not reconstruct a path from `taskId`.
This spelling tolerance does not weaken the staged blob, immutable admission,
committed `design-active`, or exact `mutablePaths` checks.

Exact non-governed files under `tests/`, and only the exact
`docs/ui/v1.1.10-delivery.md` status/evidence document, may accompany governed
paths in `mutablePaths` as auxiliary evidence. They do not grant production
authority or contribute to exactly-once governed coverage; an auxiliary-only
record cannot authorize a batch. Existing governed classification takes
precedence (for example, `tests/AGENTS.md` remains governed). Auxiliary paths
ordinarily must occur in the current checkpoint diff and, at finalization, the
checkpoint-to-reviewed diff. The single final-only reconciliation below handles
one admitted unchanged test path. They remain included in the complete path-state
digest and immutable admission fields, but cannot appear in `integrationPaths`.
The delivery document may cite accepted decisions and retain test/status
evidence; it cannot itself decide firmware, support, release, or version rules.
This exception does not admit other non-governed paths or relax index/worktree
matching, review, Golden, or external release authority.

The validator classifies `SPEC.md`,
`docs/architecture/experience-and-access-policy.md`,
`docs/architecture/nfc_roadmap.md`,
`docs/architecture/supported-ic-matrix.md`, and
`docs/architecture/ic-workflow-flowcharts.md` as exact governed paths with
minimum R2 risk. Nearby files do not inherit that classification. The
flowcharts remain a synchronized architecture projection, not an independent
firmware or support authority. Committed admission fields remain immutable;
this classification does not waive historical or final coverage checks.

The exact-document and delivery-evidence classifications start after the sealed
final evidence checkpoint `b9a94a2bab1a7bc05129b3438f0c0afeaaf45ad4`.
Final batches at or before that commit in Git ancestry retain the preceding
path, risk, auxiliary, ownership, and evidence-commit classification. The
validator verifies the fixed cutover is an ancestor and a sealed final batch;
it still replays every earlier final batch with its original coverage, digest,
immutability, checkpoint, and external-authority checks. Current active records
and the current checkpoint diff always use the new classification. The cutover
is not a new trust root or permission to omit old governed paths.

The exact root `VERSION` file is governed with minimum R3 risk for current and
future batches after the separately sealed final evidence checkpoint
`3db701b70e0eec66889a4b030fcbb84db0db5693`. The validator verifies this
cutover is an ancestor and a sealed final batch, then replays batches through
that commit with their original `VERSION` classification. In particular, the
historical 1.1.10 `VERSION` edit is not retroactively covered by a new record.
The earlier exact-document classification remains in effect for batches between
its own cutover and this one. This adds current authority coverage for the
previously declared `RELEASE-IDENTITY-1111-01` `VERSION` path without altering
its admitted record fields or granting release approval.

## Lifecycle

[ADR 0071](../adr/0071-final-integration-path-ownership.md) separates original
change provenance from final integration ownership. Schema v2 optionally accepts
`integrationPaths` **only** on `final-complete` records. It is a unique list of
exact governed paths drawn from that record's unchanged `mutablePaths`.
Omitting it retains legacy ownership of all governed `mutablePaths`; an explicit
empty list owns no final paths. Active/blocked records cannot use this field.

All admitted governed paths must still equal the checkpoint diff as a set.
Effective integration ownership must cover every changed governed path exactly
once across the entire batch. Thus overlapping historical modifications may be
partitioned at final review without hiding a stale path or leaving a gap.
Auxiliary evidence cannot be an integration owner. Final review evidence must cover
the chosen partition and all original mutable paths. Risk, independent review,
full path-state digest and R3 attestations remain attached to each original
record even when its `integrationPaths` is empty. The partition is a finalization
field, not a change to admitted fields; first-final whole-blob immutability
also protects its presence and contents. No old record is deleted or rewritten
to invent prior approval. A missing local R1 path receives an honest current
integration admission and review, not a retroactive design approval.

1. `design-active`: owner search, disposition, exact base, exact paths, risk,
   and design review are admitted. Implementation/review heads, path digest,
   and final review remain null/pending. The first committed `design-active`
   blob is the immutable admission; every later active or final form must
   preserve all admitted fields.
2. `final-complete`: after the implementation is committed and the exact
   candidate is reviewed, `implementationHead` and `reviewedHead` both name
   that frozen commit. `pathStateDigest` binds the declared paths at that
   commit, and `finalReview` records the independent result and evidence.
   The same task must exist as `design-active` at that reviewed commit;
   finalization may change only lifecycle and final-evidence fields (including
   optional `integrationPaths`, `checkpointReconciliation`, and the bounded
   `auxiliaryPathReconciliation`), never the
   admitted capability, base, paths, owners, disposition, risk, or design review.
3. `blocked`: authorizes no paths. Head, digest, and final-review fields remain
   null/pending.

For a committed `design-active` admission whose immutable `integrationBase`
mistakenly names an intermediate product commit rather than the last sealed
evidence checkpoint, `final-complete` may add exactly this final-only object:

```json
{
  "checkpointReconciliation": {
    "expectedCheckpoint": "0123456789abcdef0123456789abcdef01234567",
    "reviewer": "independent-reviewer",
    "evidence": "Original base named an intermediate product commit; the full checkpoint diff was reviewed."
  }
}
```

The validator derives the effective checkpoint from its unchanged historical
replay, then requires `expectedCheckpoint` to equal that value. The field
cannot choose a checkpoint. The original base must differ from the derived
checkpoint and lie on the Git ancestry path from that checkpoint to the
record's first committed `design-active` revision; that revision must also be
an ancestor of `reviewedHead`. Missing history, malformed or extra fields,
Git failures, an unnecessary reconciliation, or a reviewer equal to the
implementation owner fail closed. The original first-active blob, admitted
fields and independent `finalReview` remain mandatory. Full diff, path digest,
unique `integrationPaths` ownership, direct-child final evidence, R3 authority
and release checks still use the derived checkpoint and remain unchanged.
Active or blocked records cannot carry this field, and a sealed final record
cannot add or alter it later. This records the admission mistake honestly; it
does not retroactively certify the original base as valid.

Owner amendment, 2026-09-24: only `PARTIAL-AB-BANK-110-01` may reconcile its
unchanged admitted auxiliary test path
`tests/NvtFwCombiner.Bootstrap.Tests/AbDummyDpOutputTests.cs` at finalization.
Its immutable first-active admission still lists that path. It is absent from
the checkpoint-to-reviewed diff, so it must not be changed merely to satisfy
the ordinary diff requirement. The final record may add exactly:

```json
{
  "auxiliaryPathReconciliation": {
    "path": "tests/NvtFwCombiner.Bootstrap.Tests/AbDummyDpOutputTests.cs",
    "expectedCheckpoint": "b9a94a2bab1a7bc05129b3438f0c0afeaaf45ad4",
    "reviewer": "independent-reviewer",
    "evidence": "The admitted test is an unchanged regular blob throughout the reviewed ancestry."
  }
}
```

The validator derives the actual checkpoint by historical replay, requires the
field to bind it, verifies the original first-active path and its auxiliary
test classification, and requires the same regular Git blob and mode at both
ends with no intervening ancestry commit changing, deleting, renaming or
restoring that path. A changed path cannot use this field. Other task/path
pairs, malformed or extra fields, absent Git evidence, a reviewer equal to the
implementation owner (including `root`/`/root` agent aliases), and active/blocked
use fail. The path stays in the full
path-state digest and final review; all governed unique ownership, other
auxiliary diff checks, direct-child final evidence, R3 owner authority and
release gates remain in force. Sealed final records cannot be amended later.

The final record is committed as evidence immediately after the reviewed
implementation commit. That evidence commit must have `reviewedHead` as its
only parent and cannot change a governed path. Every record in the batch must
be finalized together and must exactly cover the governed checkpoint-to-review
diff. Its first committed `final-complete` Git blob is immutable: any later
commit-level modification, deletion, type change, or rename fails even if a
subsequent commit restores identical bytes. Final records whose evidence commit is
already in history are archived automatically. They are still checked for
head ancestry, digest, tampering, and deletion, but are excluded from current-
batch path coverage. A later batch therefore needs a new task ID, a new exact
base, and a new `design-active` record.

Validation requires complete Git history. Shallow repositories fail closed;
CI checkout uses `fetch-depth: 0`. When no prior evidence checkpoint exists,
the validator requires the separately authorized trusted-initial activation
defined by ADR 0059. A record cannot establish its own trust root. The reviewed
governance implementation is followed by one owner-approved direct-child
activation that adds the immutable
`trusted-initial-capability-checkpoint.v1.json` manifest and deletes exactly its
blob-hashed legacy-record inventory. Until that commit exists, the gate reports
the pending-checkpoint error.

Commit-level immutability auditing follows every ancestry commit and every
merge-parent edge. ADR 0061 permits only one normalization: a two-parent merge
whose complete tree equals exactly one parent while the other parent is already
an ancestor of that tree-equivalent parent is a duplicate observation of the
reviewed ancestry. The merge node itself is skipped, but every ancestral commit
is still audited. Distinct or ambiguous trees, octopus or non-contained merges,
real mutations, restoration commits, and Git inspection errors remain
fail-closed.

The manifest records, but does not close, inherited R3 authorities. Typed
firmware-owner and release-owner approvals live as exact-head JSON attestations
under `docs/governance/external-authority-attestations/`. Each immutable
attestation is added by its reviewed head's direct-child evidence commit; an R3
final record requires an attestation for its exact final-evidence head. Passing
tests, activation, or a repository reviewer cannot substitute for those owners.

## Schema v2 example

```json
{
  "schemaVersion": 2,
  "taskId": "ISSUE-01",
  "capability": "One bounded behavior",
  "integrationBase": "0123456789abcdef0123456789abcdef01234567",
  "risk": "R2",
  "kind": "behavior",
  "state": "design-active",
  "mutablePaths": ["src/Project/Owner.cs"],
  "implementationOwner": "implementer",
  "searchEvidence": ["rg -n 'Owner|TerminalResult' src tests"],
  "semanticOwner": "Application.Owner",
  "terminalContract": "Typed terminal result consumed by adapters",
  "disposition": "extend-owner",
  "designReview": {
    "reviewer": "independent-reviewer",
    "outcome": "approved",
    "evidence": "Reviewed dependency direction and caller inventory."
  },
  "implementationHead": null,
  "reviewedHead": null,
  "pathStateDigest": null,
  "finalReview": {
    "reviewer": null,
    "outcome": "pending",
    "evidence": ""
  }
}
```

Allowed values are:

- `risk`: `R0`, `R1`, `R2`, or `R3`.
- `kind`: `behavior`, `refactor`, `ui`, `release`, or `governance`.
- `state`: `design-active`, `final-complete`, or `blocked`.
- `disposition`: `reuse`, `extend-owner`, or `reject-duplicate`.
- `designReview.outcome`: `not-required`, `approved`,
  `findings-incorporated`, or `blocked`.
- `finalReview.outcome`: `pending`, `approved`, or
  `findings-incorporated`.

`reject-duplicate` requires `blocked`. R2/R3 design records name an
independent reviewer and concrete evidence; R0/R1 use a null design reviewer
and `not-required`. Every `final-complete` record names a reviewer independent
of the implementation owner and concrete final evidence. These repository
fields do not replace any existing R3 firmware-owner, release-owner, byte/
golden, write-range, signing, permission, or protected-environment gate. Those
authorities remain external prerequisites and cannot be satisfied by a
capability-reuse record alone.

The path-state digest is SHA-256 over sorted path names and their committed Git
state at `reviewedHead`: deletion marker, or exact blob bytes and tree mode.
Reading Git objects rather than checkout files makes the result independent of
platform CRLF conversion. Record evidence itself is compared as exact committed
and indexed Git blob bytes; it is never CRLF-normalized. Directories are not
valid mutable-path entries.

The validator derives minimum risk from mutable paths and applies the highest
result. Governed paths are at least R1; agent/skill/governance/ADR/spec/
contract and canonical script authority is at least R2; profile, golden,
CRC-worker, workflow, golden-promotion/generation, packaged-executable
allowlist, and release/signing authority is R3.
