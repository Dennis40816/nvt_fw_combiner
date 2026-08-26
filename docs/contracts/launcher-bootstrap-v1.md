# Launcher Bootstrap State Contract v1

The strict mutable per-user journal is derived as
`<exact-version-manager-state-path>.launcher-bootstrap.v1.json`. This injective
derivation prevents two custom version-state paths in one directory from
sharing launcher state under different leases. It is not stored under the
managed root and is not an application preference or seed template.

The existing version-manager writer lease is the only writer identity. Its key
is the normalized exact `version-manager.v1.json` path. A caller must own that
lease while reading or atomically writing this document. The launcher-state
adapter does not expose another lock.

Rules:

- UTF-8 without BOM, no duplicate or unknown properties, maximum 64 KiB and
  maximum JSON depth 16.
- `schemaVersion` is integer `1`.
- `managedRootIdentity` is the normalized full managed-root path and must equal
  the application state binding.
- A launcher identity is content identity, not a discovery hint. It binds one
  owning application version, its exact admission identity and release-manifest
  SHA-256, an independent launcher SemVer, protocol integer `1`, the exact forward-slash path
  `launcher/NvtFwCombiner.Launcher.exe`, positive byte length, and lowercase
  SHA-256.
- `active` and `lastKnownGood` are null together only before the first launcher
  has passed readiness. Otherwise both are complete identities.
- `pending` contains the exact candidate and the exact prior active and
  last-known-good identities captured when the transaction began. They are not
  recomputed from current directories.
- `requested` may start the exact candidate.
  `candidateLaunchRecorded` never starts it again after restart.
  `rollbackLaunchRecorded` may start only the recorded previous
  last-known-good identity. `activeLaunchRecorded` records one ordinary active
  target attempt before process creation and prevents another start while the
  prior child-owned lifetime lease remains active or cannot be inspected.
- Each managed Launcher and Desktop child inherits an exclusive OS-owned
  lifetime context derived injectively from the exact version-state path. A
  context marker, inheritable file handle, and named Windows Job identity must
  all be absent for an unmanaged start or all be present and valid for a
  managed start; any partial, blank, malformed, or unusable advertised context
  fails closed at process entry. The child captures the file handle and joins
  the Job before READY, then holds both. Job active-process count, not root PID
  exit, is the authority for the complete managed tree. The OS releases the
  authority on tree exit or reboot. A recorded ordinary attempt may be cleared
  and retried only after the lease adapter authoritatively observes the Job
  empty; unreadable or indeterminate state remains fail-closed.
- Both journals are loaded as one raw observed pair before recovery. An
  application `activeLaunchRecorded` phase is recoverable only with no launcher
  pending phase. A launcher `activeLaunchRecorded` phase is recoverable with no
  application pending phase or with an already recorded application candidate
  or rollback phase. Every other overlap, dual active guard, and active phase
  overlapping an application mutation fails closed without rewriting either
  journal. A power cut while clearing a confirmed-exited active phase leaves
  the prior recorded phase authoritative.
- A rejected or timed-out process permits automatic fallback in the same
  invocation only after the process adapter confirms exit. Unconfirmed
  termination returns a distinct fail-closed outcome and preserves the current
  recoverable journal phase without starting another process.
- Installed-file verification and process creation share one repository-owned
  executable launch lease. The repository opens and hashes the exact
  manifest-admitted executable while denying write/delete sharing, and holds
  that token through `Process.Start`. Process adapters consume the token's
  stable path and do not repeat manifest/hash policy. Lease acquisition failure
  occurs before a new launch phase is recorded and starts no process.
- `failed` is diagnostic history and never a fallback selector.
- Launcher activation is forbidden while application `pendingActivation` or
  `pendingMutation` exists, except that the active-attempt phase is first
  recovered through the child-owned application lifetime lease. A candidate
  pending activation runs only the already admitted active launcher; a pending
  mutation starts no process.
- While launcher `pending` exists, protocol-aware application mutation policy
  blocks every app-state writer. Read-only state/inventory remains available.
- Outer READY includes the complete expected launcher identity and exact
  committed application admission/release-manifest identity. Bootstrap commits
  only after reacquiring the lease and reloading both unchanged authorities.
- Every write is full-snapshot, write-through temporary file plus same-volume
  atomic replacement. State write failure leaves the prior committed document
  authoritative.

The coupled release manifest declares `versionManagementProtocolVersion: 1`
and contains a required `launcher` object. Its owner version equals the enclosing
release `version`; the runtime identity adds the exact admission identity and
that manifest's SHA-256. The launcher executable also appears exactly once in
`files` with role `launcher`. Package admission and installed verification must
agree on both representations and the actual file bytes.
