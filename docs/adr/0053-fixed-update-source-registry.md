# ADR 0053: Resolve update sources through one fixed registry

- Status: Proposed — not implementation authority
- Date: 2026-08-24
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

These points constrain the future decision but do not close the open decisions
below.

## Proposed ownership and flow

1. Contracts owns one strict bounded registry schema separate from catalog v1.
2. Infrastructure reads the fixed file through stable-read, path, reparse, and
   size/count guards and returns typed entries; it does not choose a source.
3. The existing `VersionManagementExperience` orders candidates and invokes the
   existing catalog/package verification ports.
4. Only after candidate admission does Application acquire the existing
   state-path-scoped writer lease, reload durable state, revalidate the
   candidate generation, and save the effective `UpdateSource`. Managed root
   remains inventory authority and does not create a second writer identity.
   No adapter performs an independent state write.
5. Bootstrap injects the one registry source. The stable launcher remains
   unaware of registry/catalog discovery.
6. Presentation renders typed registry/effective-source status only. It never
   probes paths, parses the registry, or chooses fallback.

The registry and every candidate root expand the administrative trust boundary.
That expansion is unacceptable until the integrity, access, and rollback rules
below are decided.

## Open owner decisions

1. Whether manual Browse is a session-only diagnostic override or a durable pin,
   and when managed registry resolution resumes.
2. The exact priority/tie field and stable ordering for multiple `available`
   entries.
3. Whether the fixed registry relies on administrator ACL/integrity controls or
   requires a publisher signature/key authority.
4. The anti-rollback/revision contract and where the last accepted revision is
   persisted.
5. What candidate admission verifies: only the catalog, the newest applicable
   package, or every listed package.
6. The fixed registry path/configuration mechanism and behavior when no source
   has ever been committed.
7. Visibility and stable issue codes for registry unavailable, invalid,
   exhausted, fallback-selected, and current-source-deprecated states.
8. Compatibility/backport behavior for existing v0.10.x state and releases.

Until these decisions are recorded one at a time through the owner-decision
workflow, runtime registry code, schema publication, and durable source
switching are blocked.

## Implementation sequence after acceptance

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
