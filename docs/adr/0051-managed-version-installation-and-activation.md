# ADR 0051: Manage application versions through a stable launcher and side-by-side payloads

- Status: Accepted by product and architecture owner on 2026-08-21;
  independent R2 review pending
- Date: 2026-08-21
- Owners: Product owner, architecture owner, release owner
- Risk: R2 cross-layer filesystem/process/package/UI contract; R3 distribution
  gate for the first user-managed package
- Builds on: ADR 0049 and the existing atomic filesystem/package boundaries
- Specification: `docs/specs/v0.10.6-version-management.md`

## Context

The current portable package starts one self-contained application directly.
It cannot replace its running executable safely, preserve multiple independently
verifiable versions, or recover automatically when a newly selected process
cannot start. The owner also requires a configured local/UNC update source that
continues to work after identical content moves to another location, plus
explicit rather than automatic install and deletion behavior.

These concerns cross Application policy, Infrastructure filesystem/process
adapters, Desktop/Launcher entry points, release packaging, and Presentation.
Putting them in Settings code would make UI state the package authority; putting
them only in a script would create a second untyped installation model.

## Decision

Add one stable launcher/updater outside side-by-side version directories. The
launcher and desktop consume one typed Application version-management policy and
Infrastructure adapters. Application owns discovery/install/switch/delete and
activation state transitions. Infrastructure owns stable bounded reads,
content hashing, archive/path safety, staging, atomic promotion/state writes,
managed-root inventory, guarded deletion, process launch, and ready transport.
Presentation projects typed state only. Domain, profiles, firmware execution,
and Golden authority do not participate.

Package identity is content-based and location-independent. The configured
folder is the administrative trust boundary. A bounded v1 catalog uses safe
relative package paths and exact lengths/SHA-256; admission also verifies the
inner release manifest and closed payload. `Verified` does not claim a digital
signature, and `v0.10.6` introduces no signing/key system.

Install and delete are durable two-phase mutations. Application persists an
exact prepared mutation (kind, version, and admission for install) before the
filesystem adapter changes a version directory, then commits admitted state
only after the filesystem result is known. Startup/Settings re-entry reconciles
an interrupted prepared mutation from exact inventory facts; it never guesses
from a directory name.

Every seed import, install, delete, activation, ready commit, rollback, and
recovery mutation is also covered by one OS-backed cross-process writer lease.
Its identity is the normalized exact state-file path plus normalized exact
managed root. The owner acquires the lease before reading authority, reloads
durable state after acquisition, and holds it through the complete filesystem
or supervised process transaction and its terminal state save. A contending
desktop/launcher receives a typed bounded `Busy`/`StateUnavailable` result and
does not act from an earlier in-memory snapshot. Closing or terminating the
owner process releases the operating-system file handle, so restart convergence
does not depend on deleting a stale marker file.

Installations stage and verify complete immutable payloads before same-volume
promotion into `versions/<semver>`. Activation is also a durable state machine:
`Requested` is persisted before desktop handoff, `CandidateLaunchRecorded`
before the candidate process starts, and `RollbackLaunchRecorded` before the
one automatic fallback target starts. Ready/rollback commit clears the journal
only after the corresponding persisted phase. Restart from
`CandidateLaunchRecorded` never starts the candidate again; its ready outcome is
uncertain, so recovery advances to the exact prior last-known-good target.
Restart from `RollbackLaunchRecorded` may retry only that same fallback until a
ready or terminal result is durably committed. It never returns to the candidate
or chooses another directory. Thus the protocol bounds rollback to one exact
version without claiming impossible exactly-once process start across a power
cut between process start and state commit. Later runtime crashes do not
auto-rollback.

Launcher state is a separate versioned file under per-user local application
data. It is not merged into shell preferences or report history. Absolute update
source paths are configuration, not identity; installed payload identities are
version plus admitted content and managed relative location.

The managed root contains one immutable `version-manager.seed.v1.json` beside
the launcher. It bootstraps per-user state only when that state is genuinely
missing and only after the seed's single active/last-known-good admission and
installed payload verify completely. It never repairs or replaces malformed,
unsupported, unreadable, or permission-denied existing state. Runtime writes
remain exclusively in per-user application data, so the complete managed root
can move without rewriting package identity.

Installed versions are reverified before launch/switch and through Version-page
inventory. Inventory distinguishes state-admitted versions from exact
self-admitted recovery candidates and unknown/unadmitted directories. Only
Application may classify a recovery candidate, and only when the healthy
self-admission exactly equals the durable prepared-install admission. A valid
self-admission with no matching prepared install remains unadmitted. Only
state-admitted versions are healthy/damaged installed rows or ordinary delete/
switch targets; other directories are shown as recovery-required and cannot
enter normal actions. Damaged versions cannot launch. Every non-active admitted
version can be explicitly deleted through a guarded destructive action. Three healthy versions
is a soft review threshold; exceeding it prompts review but never prunes
automatically.

The main window is usable before background discovery. Only a newer fully
verified package may trigger one consent dialog per session. Update is never
automatic. Settings uses the approved shared rail and control system and keeps
all firmware semantics outside Presentation.

The `0.10.x` line is the internal managed-upgrade proving ground. `1.0.0` is the
first initial managed package supplied to end users; there is no unmanaged
`0.10.5` in-place adoption tool. Launcher self-update is not part of this ADR.

## Rejected options

- **Overwrite the current portable directory in place.** Windows may lock the
  running executable, failure exposes a mixed package, and rollback has no
  atomic boundary.
- **Let the app install/switch without a stable launcher.** The running process
  cannot supervise its own exit, replacement, readiness, and fallback safely.
- **Use the configured absolute path as trust identity.** Moving identical
  content would invalidate it and path text proves neither content nor safety.
- **Put updater state in `preferences.v1.json`.** Older versions can erase or
  reinterpret fields needed by the launcher and recovery journal.
- **Automatically delete the oldest version.** The owner requires individual
  user choice and permits Keep all beyond the default reminder threshold.
- **Add signing/key management in `v0.10.6`.** Existing distribution has no
  approved signing authority; this slice uses the configured-folder trust
  boundary and records its limitation.
- **Build a one-time unmanaged `0.10.5` migration executable.** End users first
  receive `1.0.0`; the `0.10.x` line needs only isolated managed lab evidence.

## Consequences

- `v0.10.6` proves the managed layout as an internal relocatable folder without
  changing the existing end-user portable ZIP contract. The first managed
  end-user `v1.0.0` package must update package layout, manifests, smoke tooling,
  SBOM/provenance, and size evidence together under release-owner review.
- The stable launcher becomes a new critical executable/protocol boundary and
  must remain small, deterministic, package-allowlisted, and independently
  tested. Changing it later requires an explicit migration decision.
- Offline switching and startup recovery remain available because installed
  identity and state do not depend on the update source.
- A party able to replace both catalog and package in the configured trusted
  folder can defeat unsigned integrity checks. Release/security evidence must
  state that limitation honestly.
- Source unavailability never blocks ordinary app startup. Package/install and
  delete mutations remain explicit, typed, and fail-closed.
- The release packager validates its generated manifest against the canonical
  schema after generation and again after the closed allowlist check, before
  archive creation. Managed verification and installed inventory enforce the
  same contract with actual expanded-byte counting; ZIP metadata is only an
  early rejection hint and is compared to the remaining byte budget before
  aggregate addition so ZIP64 metadata cannot overflow the typed boundary.
- No firmware, profile, support, report-wire, output, or Golden contract changes.

## Migration and release path

Implement and verify against an ignored local managed lab seeded with generated
`0.10.x` packages and an immutable bootstrap seed. Advance `VERSION` only after
real discovery, install, activation, managed-root relocation, offline switching,
damaged cleanup, and rollback evidence passes. For internal `v0.10.6`, the
evidence artifact is the verified folder contents, not a newly distributed ZIP.
The final `1.0.0` managed ZIP becomes the first end-user baseline. No claim is
made that a published unmanaged portable package self-adopts.

## Verification

- Contract, Application state-machine, Infrastructure path/archive/hash, and
  process-ready tests described by the specification.
- UI smoke/accessibility in Light/Dark and both languages.
- Real local-folder managed `0.10.5` to `0.10.6` evidence plus relocation,
  offline, tamper, start-failure, timeout, rollback, retention, and delete cases.
- Architecture tests prevent UI/package-policy leakage, direct filesystem or
  process access outside adapters, and a second launcher policy owner.
- Production-adapter tests cover forged underreported and ZIP64 `long.MaxValue`
  entry metadata through both Verify and Install, and seed-specific tests pin
  busy-lease rejection plus post-acquisition durable-state reload.
- Scoped Polytail, independent R2 review, canonical full verification, unchanged
  Goldens, existing portable-package regression smoke, managed-folder content/
  relocation/size evidence, and clean-machine evidence before the internal
  identity advances. Managed-package SBOM/provenance and distribution approval
  remain required before the `1.0.0` end-user release.
