# ADR 0066: Add per-version notification policy through existing update owners

- Status: Accepted by the product owner on 2026-09-01; the corrected minimal
  design requires a fresh independent R2 review before implementation
- Date: 2026-09-01
- Owners: Product owner, architecture owner, release owner
- Risk: R2 Catalog admission and Application selection; R3 live
  Registry/Catalog cutover
- Builds on: ADR 0051, ADR 0053, ADR 0058, and Catalog v1; this ADR amends
  ADR 0053 only for the bounded Registry/Catalog authority-publication
  admission and persistence rules below

## Context

Catalog v1 cannot suppress background notification for one version. Catalog v2
must add that policy without creating another updater or preventing the
v1.0.7 client, which reads only v1, from reaching v1.0.8.

The first design introduced identities, admission objects, install tokens,
state migration, a direct-root schema floor, a Presentation policy engine, and
self-test behavior that the repository does not need. Existing owners already
provide the required boundaries.

## Decision

Extend the existing Catalog path by one closed policy field and reuse its
current identities, request mode, Registry authority, readmission, publisher,
and release-note behavior.

### Reused authority

- `UpdateCatalogVersionSnapshot.Identity` remains the package admission
  identity. It continues to contain version, canonical package path, declared
  size, package SHA-256, and inner release-manifest SHA-256. Publication time,
  release notes, policy, Catalog identity, and Registry authority remain
  excluded.
- `UpdateCatalogContentIdentity` remains the exact Catalog identity: schema
  version plus SHA-256 of the stable Catalog bytes.
- `VersionManagementExperience.CheckAsync(bool isAutomatic, ...)` remains the
  request-mode owner. No request-mode type or second selection engine is added.
- The existing Registry writer lease, second Registry/Catalog read,
  revision/digest anti-rollback checks, launcher fence, and state store remain
  the only mutation authority.
- `VersionManagerState.CreateDurableSnapshotToken` and
  `DurableSnapshotToken.Matches` remain the durable-generation equality owner.
  Their existing comparison is extended to include `SourceRegistryState`, so a
  revision-only or digest-only Registry advancement invalidates an older
  self-test or recovery snapshot. Registry readmission reuses this token and
  removes its private duplicate durable-state comparison. No second generation
  token is added.
- Existing installation behavior remains authoritative.
  `CatalogPublicationEquals` adds the admitted policy comparison only to the
  existing first/second Registry read under the writer lease; no
  `InstallReadmissionToken` or per-field token model is added. `InstallAsync`
  does not reload a Catalog. A later policy-only publication does not revoke an
  already explicit package selection because both policies allow explicit
  installation; the existing install path still re-verifies package identity
  and bytes.
- Catalog `releaseNotes` and `scripts/create_update_catalog.py` remain the
  changelog and publisher owners. The existing Registry editor already accepts
  a positive `catalogSchemaVersion` and is not replaced.

There is no `PackageAdmissionIdentity`, `CatalogPublicationIdentity`,
`FullCatalogAdmission`, `NoEligibleNotification`, direct-root schema-floor
state, Presentation policy engine, or Environment Self-test expansion in
v1.0.8.

### Catalog contracts

Catalog v1 remains strict and byte-compatible. Its admitted entries map
explicitly to effective `notify` in Application.

Catalog v2 is a distinct strict schema and document contract. It retains the
v1 fields, sets `schemaVersion` to exact integer `2`, and requires every entry
to contain exactly one `notificationPolicy`. Only exact lowercase
`manual-only` and `notify` are admitted. Missing, null, duplicate,
case-variant, or unknown values reject the complete v2 document.

### Request behavior

`manual-only` suppresses background notification; it does not prohibit an
explicit check or explicit installation. `notify` preserves existing
notification behavior and never authorizes automatic download, install,
activation, or switch.

For `isAutomatic: true`, Application filters admitted entries before any
package I/O. The public snapshot contains at most the selected verified
`notify` candidate and contains no row, release note, policy fact, or prompt
state derived from a `manual-only` entry.

When a Registry-routed automatic check has no eligible newer `notify` entry,
it performs zero package I/O. Under the existing writer lease it repeats the
current Registry/Catalog admission and anti-rollback checks. This is
Registry/Catalog **authority-publication admission**, not package/source
candidate admission: it confirms the exact Registry and Catalog publication
and the absence of an eligible `notify` row without opening or selecting a
package. This bounded branch amends ADR 0053's earlier statement that Registry
admission always includes the newest package; it does not create a catalog-only
package candidate or effective source.

A revision/digest-only durable save is allowed only when all four facts already
hold in the reloaded v1.0.7-compatible state:

- `UpdateSource` is non-null, fully qualified, and already normalized;
- `SourceRegistryState` already exists;
- `SourceRegistryState.IsManualPin` is false; and
- the admitted Registry entry's `SourceRoot` path-equals `UpdateSource`.

Only the existing accepted Registry revision and digest may then change. The
effective source, Registry entry, manual-pin state, and every other durable
field remain byte-for-byte/equality unchanged. If any guard fails, especially
when `UpdateSource` is null, the check performs no durable save and still
returns the connected no-candidate snapshot. v1.0.8 therefore never writes the
null-source/non-null-Registry combination rejected by the released v1.0.7
reader. Before any source has been selected, Registry revision/digest
anti-rollback remains protected by the two reads within that check but is not
remembered across restart; this bounded compatibility limitation ends after an
explicit package/source admission persists the existing coupled source and
Registry state. No field, document version, migration, store, or writer is
added.

This automatic no-candidate branch is outside the shared
`InspectCandidateAsync` package-verification helper, so Environment Self-test
and fresh installation continue verifying the Registry-asserted newest package
regardless of policy.

The all-manual automatic snapshot has `Catalog = null`,
`VerifiedCandidate = null`, `SourceStatus = Connected`,
`CatalogIssue = None`, and `ShouldPromptForUpdate = false`, with the selected
existing Registry status and no policy-specific issue. A mixed automatic
snapshot exposes only the selected verified `notify` row. It does not
introduce a new terminal-result type or change source, package, prompt,
installation, activation, or inventory state.

For `isAutomatic: false`, the existing explicit Check path exposes all
admitted rows, including `manual-only`. Installation remains user-selected,
confirmed, verified, and readmitted through the existing path.

`CatalogPublicationEquals` compares policy in addition to its current fields
when the selected Registry publication is repeated under the writer lease.
Changing policy in either direction therefore rejects that in-flight Registry
selection while `UpdateCatalogVersionSnapshot.Identity` remains unchanged
when package identity is unchanged. It is not a post-Check install reread.

### Direct-root behavior and bounded limitation

The direct root checks fixed filenames only. If v2 is present, it is preferred
and any unreadable, unstable, unsafe, oversized, or invalid v2 fails closed;
v1 is not tried. If v2 is absent, v1 is allowed.

v1.0.8 deliberately does not persist a per-root schema floor. Consequently, a
direct root that previously served v2 can serve v1 after v2 is removed. This
bounded downgrade limitation applies only to direct/manual-pinned roots; the
Registry route retains its existing revision/digest anti-rollback authority,
not a sticky Catalog-schema floor. A release owner can deliberately publish a
higher Registry revision that asserts Catalog v1; that remains an R3 live
publication decision and must not be described as schema downgrade prevention.
Add persistent downgrade protection only if a later requirement demonstrates
that direct roots need cross-restart policy continuity, together with its
state and user-confirmation contract.

### Publication and release transition

The existing publisher keeps v1 as its default byte-compatible output. An
explicit v2 publication requires a complete policy assignment for every
aggregate entry. Missing, extra, duplicate, case-variant, or unknown
assignments fail before write. Both `manual-only` to `notify` and `notify` to
`manual-only` corrections are allowed as new complete publications.

Existing `releaseNotes` remain canonical and are preserved by aggregate
publication. On the first explicit v2 publication, when v2 is absent, valid v1
is the stable-metadata import authority. Every later v2 correction uses the
existing strict v2 aggregate as that authority even if a stale v1 remains.
Policy assignments and all metadata/package checks complete before any
Catalog, Registry, or manifest-copy write. Default invocation does not inspect
or mutate v2 and remains byte-for-byte v1 behavior. No second changelog store
is added.

The real v1.0.7-to-v1.0.8 publication and canary use Catalog v1. From that exact
installed v1.0.8 client, a staged v2 canary proves strict parsing, policy
selection, explicit installation, and both correction directions. Only then
may the release owner perform the R3 live Registry cutover to the exact v2
Catalog.

## Consequences

- One existing Application owner decides notification and installation
  visibility for both direct and Registry routes.
- Package identity, firmware bytes, profiles, composition, activation,
  rollback, and offline launch do not change.
- Exact Catalog SHA and Registry revision/digest preserve traceability without
  new identity types or state.
- Released v1.0.7 remains able to read every state v1.0.8 persists. Before the
  first explicit source admission, an all-manual automatic check deliberately
  does not retain Registry high-watermark advancement across restart.
- Direct-root removal of v2 can re-enable v1; this is accepted for v1.0.8 and
  must not be described as sticky downgrade protection.

## Rejected alternatives

- New publication/package identity types duplicate the existing snapshot and
  content identities.
- Full-admission and install-token abstractions duplicate the current strict
  snapshot and readmission path.
- Persisted direct-root schema floors, reset UI, and state migration are
  deferred until required.
- A new no-eligible result or Presentation policy engine would expose policy
  through a second semantic path.
- Environment Self-test expansion is unrelated to notification suppression.

## Verification and release gates

- Prove v1 exact regression and explicit effective-`notify` mapping.
- Prove strict v2 schema/runtime parity, including missing, case-variant, and
  unknown policy rejection.
- Prove automatic mixed/all-manual filtering occurs before package I/O and
  leaks no manual-only fact; prove the exact mixed/all-manual public snapshot
  fields and that explicit Check exposes all rows.
- Prove Registry all-manual handling re-reads under the existing lease and
  returns the existing no-candidate, no-prompt snapshot. With all four paired
  source/Registry guards satisfied it may save only revision/digest authority;
  from null-source, missing-Registry-state, manual-pin, or source-mismatch
  states it performs no durable save. Compare every durable field and prove the
  released v1.0.7 reader accepts every persisted result.
- Prove revision-only and digest-only `SourceRegistryState` changes invalidate
  the existing durable snapshot token and make Launcher self-test and managed
  setup recovery reject the stale generation.
- Prove Environment Self-test and fresh-install Registry inspection still
  verify the asserted newest package for v2/manual-only Catalogs.
- Prove direct-root v2 preference, present-invalid fail-closed behavior, and v1
  fallback only when v2 is absent, including the documented non-sticky case.
- Prove policy participates in `CatalogPublicationEquals` and both correction
  directions preserve package identity when package bytes are unchanged.
- Prove publisher v1 output remains byte-compatible; first-v2 migration uses
  valid v1, later-v2 correction ignores stale v1 in favor of strict v2, and
  every policy/metadata failure occurs before controlled atomic writes.
- Obtain a fresh independent R2 architecture/contract review before
  implementation. Live publication remains an exact R3 release-owner action
  after the v1 and staged-v2 canaries pass.
