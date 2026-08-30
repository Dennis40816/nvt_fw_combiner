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
  lifetime context derived injectively from the exact version-state path and
  process role. The context marker, inheritable file handle, normalized state
  path, role, and named Windows Job identity must all be absent for an
  unmanaged start or all be present and valid for the exact parsed managed
  invocation; any managed READY/options advertisement with missing, partial,
  blank, malformed, swapped-role, wrong-path, or unusable context fails closed
  at process entry. Capture verifies that the inherited handle names the exact
  role-specific lease file, then joins the derived Job before READY and holds
  both. Job active-process count, not root PID exit, is the authority for the
  complete managed tree. Before READY, last-handle close retains whole-tree
  crash/timeout cleanup. Only exact accepted READY releases that job's
  kill-on-close policy so a successful parent Launcher may exit without
  terminating the accepted Desktop tree. The OS releases remaining lifetime
  authority on tree exit or reboot. A recorded ordinary attempt may be cleared
  and retried only after the lease adapter authoritatively observes the Job
  empty; unreadable or indeterminate state remains fail-closed.
- When the distribution Launcher invokes Root Bootstrap, the complete Root
  Bootstrap/Launcher/Desktop tree additionally joins one outer Bootstrap Job.
  Its name uses the deterministic state-path prefix plus a fresh per-invocation
  identity; the exact state-path writer lease, not a reused Job name, serializes
  state mutation. Therefore a later failed invocation cannot terminate an
  already accepted Desktop tree from an earlier invocation.
  Root Bootstrap captures that lifetime before waiting on a separate one-use
  parent-to-child `START` gate and must not read or write managed state, seed an
  installation, or start a process until START is authorized. START and the
  existing child-to-parent `ADMITTED` receipt are independent authorities.
  Admission timeout permanently aborts START. An ordinary timeout is permitted
  only after the process-start worker is terminal and the outer Job is proved
  empty; otherwise the typed outcome is `TerminationUnconfirmed`. Only an
  accepted terminal READY or rollback result may release the outer Job.
- The same handoff propagates one bounded inherited identity containing the
  exact descriptor-bound Root Bootstrap filename, positive length, and
  lowercase SHA-256. Root Bootstrap captures and clears the ambient value on
  every invocation and retains it only for a complete inherited distribution-
  Launcher startup. Version Launcher clears any ambient copy and passes only
  that explicit trusted identity. Desktop consumes it only when the independent
  managed application lifetime result is `Captured`. Missing, malformed,
  legacy/direct, or manually injected context yields no stable-restart authority
  but does not turn an otherwise usable Desktop into a startup failure.
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
  composite launch lease. The repository first acquires no-follow custody of
  the complete admitted version tree, then verifies the manifest, executable,
  DLLs, and other package members through that custody. It holds the same
  handles through a final synchronous identity/topology check immediately
  before `Process.Start`. Process adapters consume the token and do not repeat
  manifest/hash policy. Any added, removed, replaced, reparse, contended, or
  changed member fails closed before process creation.
  Tree capture is Windows-only, cancellation-aware, and bounded by independent
  package ceilings: 4,097 installed files, 4,096 installed directories, and
  512 MiB plus the 4 KiB admission document. Other platforms fail closed. The
  ordinary update path and Setup reuse this same repository owner for package
  planning, extraction, admission, and verification. Ordinary update captures
  its promoted version directory directly. Setup supplies its held relative
  destination owner, verifies package content before whole-root promotion, and
  transfers immutable custody from the still-held promoted root handle only
  after proving the same root file identity; release-then-reopen by path is not
  permitted. Standard
  Windows `Process.Start` has no atomic validate-and-create primitive, so the
  synchronous topology check is the declared final observation boundary. An
  addition scheduled after that observation is a residual OS boundary, not an
  atomic verification-and-creation claim.
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
