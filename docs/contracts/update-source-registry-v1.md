# Fixed update-source registry v1 contract

The fixed registry is one administrator-controlled logical UTF-8 JSON
publication whose wire shape is `update-source-registry-v1.schema.json` and
whose deployed filename is permanently `update-source-registry.json`. The
production publication has two fixed filesystem replicas. The Desktop host
uses one explicit `--update-source-registry-path` or
`NFC_UPDATE_SOURCE_REGISTRY_PATH` value as a diagnostic override; otherwise it
uses the ordered owner-approved primary/backup locator pair. Neither host layer
validates, normalizes, persists, logs, searches for, hashes, or merges the
values. The Registry adapter/Application policy remains the only runtime
document admission and newest-replica authority.
The repository editor performs publisher-side strict preflight only; it is not
a second runtime reader. The runtime never searches drives, shares, or
Microsoft 365.

The release-owned production locators below are compiled into Bootstrap. Their
literal bytes are therefore part of the application package and its checksums;
changing either default requires a rebuilt, newly identified package. The live
Registry JSON in those locations, and an explicit option or environment
override supplied at runtime, remain external deployment state and are not
package identity. The stable Bootstrap/Launcher process chain inherits the
environment unchanged; direct Desktop diagnostics may use the explicit
option. `NotConfigured` remains valid only when an explicit host composition
intentionally supplies no locator. Ordinary production startup uses:

1. primary:
   `G:\AUTO\projects\模組專案開發\NVT_FW_Combiner\update-source-registry.json`;
2. backup:
   `G:\AUTO\Tool\NVT_FW_Combiner\update-source-registry.json`.

The two files are replicas of one logical publication. Runtime starts both
reads concurrently with an independent 45-second user-visible deadline,
validates both, selects the newest admissible in-document publication revision,
and prefers the primary only when revision and content are identical. It never
orders replicas by filesystem timestamp or workstation clock. A same-revision
content conflict fails closed; a missing, invalid, or timed-out replica does
not prevent admission of the other. A late completion after its deadline is
ignored and cannot publish state.
The fixed replica paths are not Registry entries and are not Catalog roots.

The raw file is limited to 64 KiB and 16 entries. It distinguishes wire
`schemaVersion`, positive monotonic `registryRevision`, and the application
release summary's semantic `latestVersion`; only `registryRevision` orders
replicas. It has exactly one `latest` entry, zero or more ordered `available`
entries, and zero or more `deprecated` entries. Entry paths are absolute local
or UNC Catalog JSON files, normalized before duplicate comparison. The Catalog
source root used for package resolution is the selected file's parent
directory. Each normalized parent directory may appear only once, even when
Catalog filenames differ, because durable admission and deprecation authority
are root-scoped. Unknown fields, duplicate JSON keys, duplicate normalized
paths or parent roots, relative paths, reparse-point locators, unstable reads,
and invalid UTF-8 fail the complete registry.

The v1 document's required top-level identity and publication fields are
`schemaVersion`, `registryId`, `registryRevision`, `publishedAtUtc`,
`catalogPublication`, and `entries`. `registryId` is the constant logical
production identity `nvt-fw-combiner-production` for both replicas. The 1.x
runtime, release renderer, and hotfix publisher reject every other identity;
a higher revision can never replace that authority.
`catalogPublication` contains exactly `latestVersion`,
`catalogSchemaVersion`, and lowercase `catalogSha256`. They are consistency
assertions: the selected Catalog's first version, wire schema, and exact raw
bytes must match all three before candidate/package admission continues.
`publishedAtUtc` is audit/display metadata only. The Registry does not contain
the complete version list, package paths or hashes, release notes, or a
minimum-supported-version policy; those remain under the existing Catalog and
Application authorities.

The locator itself may be an absolute local/UNC file or HTTPS. The HTTPS reader
uses a 45-second bounded timeout, at most five redirects, and both declared and
actual 64-KiB body limits. It accepts only the complete JSON contract above.
HTML, including a Microsoft 365 login page returned with HTTP 200, is
`AuthenticationRequired`; 401/403 has the same typed result. HTTP status alone
never passes Version Self-test.

Automatic resolution tries `latest`, followed by `available` entries in their
declared array order. It never selects `deprecated`. A candidate is admitted
only after the existing update-catalog reader validates its complete non-empty
catalog and the existing package repository verifies `Versions[0]`, the newest
catalog package whether or not it is newer than the running version. After the
writer lease is acquired, the same registry is read again and the selected
catalog plus exact newest package are loaded and verified again. Only identical
registry, catalog, package identity, and verification may commit.
Complete catalog publication identity is the ordered value sequence of every
entry's Version, PublishedAt, PackagePath, PackageSize, PackageSha256,
ReleaseManifestSha256, and ReleaseNotes. A change to any non-newest entry or
release note also rejects the commit.

The live Registry document and its route entries are external operator routing
state. They are intentionally
excluded from the application ZIP, release manifest, SBOM/provenance payload,
outer `SHA256SUMS.txt`, immutable GitHub Release assets, catalog package hashes,
and inner package checksums. `catalogSha256` binds the selected external Catalog
publication; it is not a digest or signature of the Registry itself. The
Registry has no self-checksum or publisher signature, so changing a route never
requires rebuilding or renaming a release ZIP.

Runtime still derives the exact registry-byte SHA-256 after a stable read and
binds that observation to its `registryRevision`. This is a local anti-rollback/TOCTOU
fingerprint, not a publisher-provided release checksum. A lower revision is a
rollback; the same revision with another digest is a conflict. Publishers must
therefore advance `registryRevision` whenever any Registry byte changes. Application
commits effective source, revision, digest, and manual-pin state together under
the existing exact state-path writer lease. Registry, candidate,
stale-generation, lease, and state-save failures perform no durable mutation.
Installed versions and offline switching remain available.

For an emergency route hotfix, use the repository-owned editor. It validates
the existing and proposed documents, rejects stale/no-op/unsafe edits,
automatically increments `registryRevision`, and atomically replaces only the Registry.
Never test a proposed route by writing the live Registry first. Copy the live
Registry to an operator staging directory, prepare every proposed Catalog file,
its parent source root, and package, then run the first editor command only as a dry-run:

```powershell
python .\scripts\edit_update_source_registry.py `
  --registry 'C:\NvtFwCombiner-Registry-Staging\update-source-registry.json' `
  --expected-revision 7 `
  --latest 'G:\AUTO\projects\模組專案開發\NVT_FW_Combiner\update-catalog.v1.json' `
  --available '\\novatek\firmware-tools\NvtFwCombiner\update-catalog.v1.json' `
  --deprecated 'G:\AUTO\projects\old-NVT_FW_Combiner\update-catalog.v1.json' `
  --latest-version '1.0.1' `
  --catalog-schema-version 1 `
  --catalog-sha256 '<lowercase SHA-256 of the exact selected Catalog JSON>' `
  --published-at-utc '2026-08-27T12:00:00Z' `
  --dry-run
```

Repeat that exact command against the staged copy without `--dry-run`, then
start the diagnostic Desktop with the staged locator and run Version
**Self-test**:

Run the packaged/version-root Desktop executable directly for this diagnostic;
do not start through Bootstrap because the explicit locator option belongs to
the Desktop host:

```powershell
.\NvtFwCombiner.exe `
  --update-source-registry-path 'C:\NvtFwCombiner-Registry-Staging\update-source-registry.json'
```

Only after staged Self-test succeeds, run the same complete editor arguments,
including the same explicit `publishedAtUtc`, against **both**
administrator-controlled live replicas: dry-run both, publish the primary,
then publish the backup. A partial copy remains integrity-safe because runtime
never accepts conflicting or rolled-back authority, but failover availability
is degraded after a client accepts the newer revision: losing that replica
causes the stale copy to fail anti-rollback. Publication is not operationally
complete until Version Self-test reports both replicas readable at the same
revision. Finally,
compare both exact files with `Get-FileHash -Algorithm SHA256`; the hashes must
match. If either publication or comparison fails, repair the stale replica and
repeat Self-test before relying on failover. The editor modifies no catalog, package,
release manifest, or checksum file; clients that accepted the prior revision
admit the higher revision through normal candidate verification.

If one replica missed more than one publication, do not replay intermediate
edits. After validating the newer live replica as authoritative, copy its exact
validated bytes through the editor's repair mode while preserving the stale
destination's Windows security authority:

```powershell
python .\scripts\edit_update_source_registry.py `
  --registry 'G:\AUTO\Tool\NVT_FW_Combiner\update-source-registry.json' `
  --repair-from 'G:\AUTO\projects\模組專案開發\NVT_FW_Combiner\update-source-registry.json' `
  --expected-revision 7 `
  --dry-run

python .\scripts\edit_update_source_registry.py `
  --registry 'G:\AUTO\Tool\NVT_FW_Combiner\update-source-registry.json' `
  --repair-from 'G:\AUTO\projects\模組專案開發\NVT_FW_Combiner\update-source-registry.json' `
  --expected-revision 7
```

Repair requires the same production `registryId`, a strictly newer
authoritative revision, stable authoritative bytes, the exact expected stale
revision, and the same atomic replacement and post-write verification as a
normal edit. The destination bytes must then hash exactly like the
authoritative replica.

Every non-dry-run publication requires `--expected-revision`. The editor takes
one adjacent exclusive publisher lock, then re-reads and revalidates the exact
Registry bytes, revision, file identity, complete reparse-free locator chain,
and Windows owner/group/effective-DACL/protection identity before using native
`ReplaceFile`. The staged file must carry that same security authority. Windows
may re-mark inherited-ACE provenance during native replacement; this metadata
does not change the effective ACEs or DACL protection and is not treated as an
authority change.
A competing publisher, stale revision, ACL mismatch, or handled publication
failure fails closed, removes staging/lock state, and leaves or restores the
original Registry bytes and security authority. The publisher holds its
delete-on-close Windows lock handle for the complete operation, so forced
process termination releases the lock. On the next invocation, while holding a
new lock, it removes only a recognized strict-JSON staging residue whose
security identity matches the live Registry; unknown or unsafe residue blocks
publication for operator review. Direct manual writes that bypass this single
publisher are unsupported.

Manual Browse/Confirm creates a durable pin and suspends automatic registry
selection. Explicit Resume attempts registry admission; it clears the pin only
as part of a successful atomic candidate commit. A failed resume leaves the
prior pin and source unchanged.

Registry state requires one non-empty, fully qualified, already normalized
effective source. Revision zero/null digest is valid only for a manual pin.
ADR 0056 classifies manual source, registry selection, and Resume commits as
application-state mutations; any pending launcher transaction fences them even
while the OS lease is temporarily free.

If the prior source is declared deprecated and no latest/available candidate
passes, that source remains durable for recovery and offline inventory but is
not contacted automatically. Application reports `CurrentSourceDeprecated`.

The registry relies on administrator ACLs for v1 and has no publisher
signature. This limitation is part of release/security review.

The reader denies writer sharing on its stable handle, rejects same-length
rewrite/replacement, checks every traversed locator component for reparse
points, and rejects device, extended, alternate-data-stream, relative, or
non-normalized locator and entry forms.

For the current operations layout, both Registry replicas contain the same one
`latest` entry. That entry's exact `catalogPath` is
`G:\AUTO\projects\模組專案開發\NVT_FW_Combiner\update-catalog.v1.json`.
Its parent directory is the Catalog source root and package ZIPs are under that
parent's exact `packages` child. This is not either fixed Registry locator. The
backup Registry directory is not an `available` Catalog entry. A future Catalog
relocation or filename change changes the logical Registry publication and
advances `registryRevision`; it does not rename the fixed deployed Registry
files.

For a local deployment test, set the external locator for only the current
process before starting the stable Bootstrap, then use Version **Self-test**:

```powershell
$env:NFC_UPDATE_SOURCE_REGISTRY_PATH = 'C:\NvtFwCombiner-UpgradeLab\update-source-registry.json'
.\NvtFwCombiner.Bootstrap.exe
```

Production administrators may provision the same variable at User or Machine
scope as a diagnostic/emergency override. Removing, mistyping, or changing the
runtime override never rewrites managed-version state; without an override the
compiled primary/backup filesystem pair is restored. Changing either compiled locator
does require a new package identity. Self-test reports the typed missing, unsafe, permission,
authentication, timeout, or invalid Registry result only after actual document,
catalog, and newest-package admission. Production replica discovery also
reports Primary/Backup issue, revision, and selection. A successful source with
a missing, timed-out, or lower-revision peer is explicitly degraded rather
than a false redundancy all-clear. Self-test reads durable authority before and
after the potentially slow package admission and applies the same
revision/digest anti-rollback rule as Check without taking a writer lease or
mutating state. Concurrent authority advancement therefore rejects the stale
Self-test result. Per-replica reads are single-flight, so repeated timeouts do
not leave an unbounded set of physical filesystem reads behind.

## Durable compatibility

`version-manager.v1.json` schema version 1 remains readable. A null registry
state continues to serialize as version 1 during ordinary non-registry saves.
The first otherwise-successful registry selection, Resume, or manual pin creates
registry state and writes schema version 2 in the same state file; later writes
remain version 2. A first manual pin uses revision zero/null digest; a later pin
preserves the accepted revision/digest. No sidecar exists. This keeps one atomic
file, one state-path lease, and one writer. Reverse reading a version-2 state
with internal `0.10.x` builds is unsupported; `1.0.0` is the first distributed
managed baseline.
