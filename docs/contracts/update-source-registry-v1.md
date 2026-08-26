# Fixed update-source registry v1 contract

The fixed registry is one administrator-controlled UTF-8 JSON file whose wire
shape is `update-source-registry-v1.schema.json`. Bootstrap supplies its exact
absolute locator; the runtime never searches drives, shares, or Microsoft 365.
The production locator is a release input and is intentionally not embedded
until the owner supplies it.

The raw file is limited to 64 KiB and 16 entries. It has one positive monotonic
`revision`, exactly one `latest` entry, zero or more ordered `available`
entries, and zero or more `deprecated` entries. Paths are absolute local or UNC
directories, normalized before duplicate comparison. Unknown fields, duplicate
JSON keys, duplicate normalized paths, relative paths, reparse-point locators,
unstable reads, and invalid UTF-8 fail the complete registry.

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

The exact registry-byte SHA-256 is bound to its revision. A lower revision is a
rollback; the same revision with another digest is a conflict. Both fail
closed. Application commits effective source, revision, digest, and manual-pin
state together under the existing exact state-path writer lease. Registry,
candidate, stale-generation, lease, and state-save failures perform no durable
mutation. Installed versions and offline switching remain available.

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

For the current operations layout, the source root is
`G:\AUTO\projects\模組專案開發\NVT_FW_Combiner`, the catalog is directly under
that root, and package ZIPs are under its exact `packages` child. This is not
the fixed registry locator. The latter remains a separately supplied absolute
synced-local or UNC file path.

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
