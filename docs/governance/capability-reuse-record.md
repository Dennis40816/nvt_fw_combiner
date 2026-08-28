# Capability-reuse record

Status: Active fail-closed production-change contract (schema v2).

Create one Git-tracked JSON record under `docs/governance/change-records/`
before adding, changing, moving, wrapping, splitting, replacing, or refactoring
a production behavior, semantic branch, or owner contract. A new record starts
as `design-active` and is staged with a real Git index blob, but not committed,
with the admitted change. Intent-to-add is rejected. The validator parses the
index blob and requires the worktree bytes to match it exactly, so an unstaged
record edit cannot change the authority being validated.
Records kept only in ignored handoff or artifact directories do not open the
gate. A `design-active` record already present in `HEAD` is rejected so an
unfinished admission cannot authorize another implementation batch.

The validator computes tracked renames, copies, additions, modifications, type
changes, deletions, and non-ignored untracked files from the latest valid final
evidence checkpoint. `integrationBase` must equal that checkpoint; it is not a
self-attested ancestor. Both sides of a rename or copy are audited. Every governed
path in the candidate must occur in exactly one current record's
`mutablePaths`, and every declared path must occur in that diff. Paths are
exact, forward-slash, repository-relative names; globs and directory grants
are forbidden. A JSON record is valid only when its direct parent is exactly
`docs/governance/change-records`; nested records are rejected before parsing or
coverage and cannot later be moved into place for reuse.

## Lifecycle

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
   finalization may change only lifecycle and final-evidence fields, never the
   admitted capability, base, paths, owners, disposition, risk, or design review.
3. `blocked`: authorizes no paths. Head, digest, and final-review fields remain
   null/pending.

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
