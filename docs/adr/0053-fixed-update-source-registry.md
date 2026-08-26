# ADR 0053: Resolve update sources through one fixed registry

- Status: Accepted for the first distributed `v1.0.0` managed baseline
- Date: 2026-08-26
- Owners: Product owner, architecture owner, release owner
- Risk: R2 cross-tool trust, filesystem, persistence, startup, and release contract
- Builds on: accepted ADR 0051 and update catalog v1

## Context

ADR 0051 supports one explicitly confirmed local or UNC update root. Package
identity is already content-based, so identical complete source content may
move, but every client must otherwise be reconfigured when the administrative
path changes. The owner proposes one fixed registry file shared by participating
tools. It lists multiple absolute roots as `latest`, `available`, or
`deprecated` and lets the app recover safely from source relocation.

This is not another catalog or version manager. Existing SemVer, catalog
validation, package verification/install, state store, writer lease, inventory,
activation/rollback, deletion/retention, Settings projection, and release
identity remain the only owners of those behaviors.

## Approved direction

- The registry contains exactly one `latest` entry.
- Resolution tries `latest` first, then bounded `available` entries in a
  deterministic order. `deprecated` is never selected automatically.
- A failed, unavailable, malformed, or unverified candidate causes no durable
  source mutation. Exhaustion preserves the prior committed source, installed
  inventory, offline switching, and ordinary startup.
- A source path never becomes package identity and no filesystem search is
  permitted.

The owner confirmed this complete decision on 2026-08-26. New ICs and new
firmware capabilities remain outside this ADR.

## Ownership and flow

1. Contracts owns one strict bounded registry schema separate from catalog v1.
2. Infrastructure reads the fixed file through stable-read, path, reparse, and
   size/count guards and returns typed entries; it does not choose a source.
3. The existing `VersionManagementExperience` orders candidates and invokes the
   existing catalog/package verification ports.
4. Candidate admission loads the complete existing catalog and verifies its
   newest entry (`Versions[0]`, irrespective of whether it is newer than the
   running version) through the existing package port. Application then
   acquires the existing state-path-scoped writer lease, reloads durable state,
   re-reads the exact registry, re-loads the selected catalog, and re-verifies
   that exact newest package while holding the lease. Catalog publication
   identity is the complete ordered sequence of every admitted entry's Version,
   PublishedAt, PackagePath, PackageSize, PackageSha256,
   ReleaseManifestSha256, and ReleaseNotes. A change to any entry, including a
   non-newest package or release note, changes the publication. Only identical
   registry/catalog/package authority may be atomically saved with the
   effective `UpdateSource`.
5. Managed root remains inventory authority and does not create a second
   writer identity. No sidecar, second lease, second state writer, or adapter
   state write is permitted.
6. Bootstrap injects the one registry source. The stable launcher remains
   unaware of registry/catalog discovery.
7. Presentation renders typed registry/effective-source status only. It never
   probes paths, parses the registry, or chooses fallback.

## Accepted decisions

1. **Manual override.** Browse/Confirm is a durable manual pin. When there has
   been no accepted registry, the pin stores revision `0` and null digest;
   otherwise it preserves the last accepted revision/digest. Explicit Resume
   performs a complete registry/candidate admission while the pin remains
   durable and clears it only in the same successful atomic source commit.
   Failed Resume leaves pin and source unchanged.
2. **Ordering.** The strict registry contains exactly one `latest`. Application
   tries it first, followed by `available` entries in their declared array
   order. There is no priority/tie field. `deprecated` is never automatically
   selected.
3. **Trust.** Registry and candidate folders rely on administrator ACLs. V1 has
   no signature/key authority. This limitation is mandatory release/security
   evidence.
4. **Anti-rollback.** Registry revision is a positive signed 64-bit integer and
   digest is lowercase SHA-256 of the exact stable raw bytes. A lower revision
   is rejected. The same revision/same digest is idempotent; the same revision
   with another digest is rejected. Publishers must increment revision for any
   byte change. A higher revision is persisted only with a successfully
   admitted candidate.
5. **Candidate admission.** Every attempted source must have a valid non-empty
   catalog and a fully verified newest catalog package. The exact registry,
   catalog identity, newest package identity, and package verification are
   repeated under the existing writer lease before commit. There is no
   catalog-only candidate admission.
6. **Locator.** Bootstrap injects one exact absolute filesystem file locator.
   It may be local, UNC, or a locally synchronized Microsoft 365 path readable
   by `FileStream`; HTTPS and filesystem/share search are forbidden. The final
   production value is a release input. A missing locator or first source is a
   typed non-blocking unavailable state and ordinary/offline startup continues.
7. **Status and issues.** Stable Application issues cover NotConfigured,
   Unavailable, PermissionDenied, Invalid, RevisionRollback,
   RevisionConflict, CandidatesExhausted, RegistryChanged, StateUnavailable,
   Superseded, and CurrentSourceDeprecated. Visible status distinguishes manual
   pin, latest selected, fallback selected, unavailable, exhausted, rejected,
   and deprecated retained. If every permitted candidate fails while the prior
   committed source is declared deprecated, the source is preserved but is not
   contacted automatically; the typed deprecated-retained state is shown.
8. **Compatibility.** The same `version-manager.v1.json` path and exact writer
   lease remain authority. A state with null `SourceRegistryState` reads and
   writes schema version 1 during ordinary non-registry mutations. The first
   successful manual pin or registry/resume source commit creates non-null
   registry state and atomically writes schema version 2; every later save
   remains version 2. Readers accept v1 and v2. No sidecar exists. Reverse
   reading v2 state with internal `0.10.x` builds is unsupported because
   `1.0.0` is the first distributed managed baseline.

Registry state is valid only with a non-empty, fully qualified, already
normalized effective `UpdateSource`. Revision zero/null digest is valid only
when `IsManualPin` is true; an automatic registry state always has a positive
revision and lowercase digest.

ADR 0056's launcher logical mutation fence covers manual source commits,
registry selection, and Resume because each changes application durable state.
While launcher state has any pending transaction, these operations fail with
typed `StateUnavailable` before registry/source state mutation even when the OS
lease is temporarily free. Read-only registry/catalog/package admission may be
performed, but it cannot commit after the fence changes.

The filesystem adapter opens the registry without writer sharing, binds the
read to one stable handle and unchanged exact bytes, checks every traversed
locator component for reparse points, and rejects device, extended, alternate-
data-stream, relative, and non-normalized locator/entry forms. Same-length
concurrent replacement or rewrite must not publish a mixed or changed snapshot.

Every registry read, candidate admission, stale generation, changed durable
state, lease, re-admission, and save failure performs zero durable source,
registry-state, or inventory mutation.

## Deployment path input

The current operational update root is
`G:\AUTO\projects\模組專案開發\NVT_FW_Combiner`; it contains
`update-catalog.v1.json`, while its packages live under the exact child
`G:\AUTO\projects\模組專案開發\NVT_FW_Combiner\packages`. This is source-root
documentation, not the fixed registry locator. The production fixed registry
remains a separately owner-supplied absolute synced-local or UNC file path.

## Implementation sequence

1. Freeze schema, stable issue codes, bounds, and compatibility rules.
2. Add the Application registry-source port and policy tests; add the strict
   Infrastructure reader independently.
3. Add atomic candidate admission/commit tests proving every failed candidate
   causes zero state/repository mutation.
4. Wire startup discovery and notification without blocking ordinary startup.
5. Add release publication and local/UNC relocation/failover evidence.
6. Complete Architecture, scoped Polytail, independent R2 review, and the full
   canonical verifier before enabling the feature.

## Required evidence

- Strict schema/bounds/stable-read/path/reparse tests.
- Deterministic ordering and complete failure-matrix tests.
- Cross-process writer-lease and stale-generation zero-mutation tests.
- Local and UNC failover, relocation, outage, and last-known-good evidence.
- Existing install/switch/rollback/damage/delete/retention regressions unchanged.
- No firmware, profile, output-byte, CRC/header, naming, or Golden change.
- Exact v1/v2 migration and ordinary non-registry-save coverage.
