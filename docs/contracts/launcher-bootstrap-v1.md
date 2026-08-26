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
  last-known-good identity.
- A rejected or timed-out process permits automatic fallback in the same
  invocation only after the process adapter confirms exit. Unconfirmed
  termination returns a distinct fail-closed outcome and preserves the current
  recoverable journal phase without starting another process.
- `failed` is diagnostic history and never a fallback selector.
- Launcher activation is forbidden while application `pendingActivation` or
  `pendingMutation` exists. A pending activation runs only the already admitted
  active launcher; a pending mutation starts no process.
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
