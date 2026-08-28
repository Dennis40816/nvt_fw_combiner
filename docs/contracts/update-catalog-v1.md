# Update catalog v1 contract

The configured update source contains one non-recursively discovered root file
named `update-catalog.v1.json`. Its normative wire shape is
`update-catalog-v1.schema.json`; runtime admission also applies UTF-8 byte,
stable-read, safe-path, resolved-root, and reparse-point checks that JSON Schema
cannot express.

Package identity is the canonical semantic version plus the declared package
relative path, length, package SHA-256, and inner `RELEASE-MANIFEST.json`
SHA-256. The configured source folder is intentionally excluded, so moving an
identical catalog and package set does not change identity.

A publishable network folder therefore has this practical shape:

```text
\\server\share\NvtFwCombiner\
├── update-catalog.v1.json
└── packages\
    ├── NvtFwCombiner-v0.10.6-win-x64.zip
    └── NvtFwCombiner-v0.10.7-win-x64.zip
```

The `packagePath` values in the root catalog are respectively
`packages/NvtFwCombiner-v0.10.6-win-x64.zip` and
`packages/NvtFwCombiner-v0.10.7-win-x64.zip`; the declared lengths and hashes
must describe those exact ZIP bytes and their inner `RELEASE-MANIFEST.json`.
For a relocation test, rename or move the complete `NvtFwCombiner` root,
confirm the new source path in Settings, and run Check now. This is
identity-neutral. Renaming only `packages`, an individual ZIP, or another path
below the root is not the same test: its catalog-relative `packagePath` must be
updated and therefore changes the admitted package identity.

## What happens when catalog and package content differ

The system fails closed without affecting an already installed version:

| Mismatch | Result |
| --- | --- |
| Invalid catalog shape, duplicate version, unsafe path, invalid size/hash/date, or unknown field | The complete candidate catalog is rejected as `InvalidManifest`; no partial version list is published. |
| Referenced ZIP is missing, unreadable, or has a different byte length | The package cannot become `Verified`; verification/install returns a typed package-unavailable result. |
| ZIP length matches but its SHA-256 differs | The package cannot become `Verified`; verification/install returns `PackageMismatch`. |
| ZIP hash matches the catalog but the inner manifest hash/identity, checksum list, or closed payload differs | The package cannot become `Verified`; verification/install returns a typed invalid-payload result. |

Background discovery therefore opens no automatic update prompt for a mismatched
newer package. A manual install also stops before admission. No active-version
reference is changed, no installed version is overwritten, and failed staging
is removed. Existing verified installed versions remain launchable/switchable
while the source is invalid or offline.

## Release/update-source checklist

After package smoke and release-note rendering, `release.yml` uploads a separate
30-day Actions artifact named `update-source-handoff-v<version>-<source-sha>`.
It is a single-version seed containing the new canonical ZIP under `packages/`,
a catalog that references only that ZIP, an exact root-level copy of the ZIP's
inner `RELEASE-MANIFEST.json`, and the reviewed
`update-source-registry.json` operator seed. The release manifest is the
version profile; its copy is for operator inspection and is not a second
runtime authority. It is not the authoritative live multi-version catalog and
must never overwrite an existing update source by itself. The five immutable
GitHub Release assets remain unchanged because the aggregate catalog is mutable
network-source state and cannot be embedded in the ZIP whose digest it records.

A complete update-source publication supplies the latest aggregate
`update-catalog.v1.json` together with every ZIP referenced by it. Copying only
the new ZIP, or copying the single-version seed catalog over a live aggregate,
is incomplete publication.

1. Produce and smoke each canonical release ZIP. Do not rename a ZIP after it
   was packaged.
2. Download the matching single-version Actions handoff. In a staging copy of
   the live update-source root, retain the current catalog and every version
   that should remain available under `packages/`, then add the handoff ZIP.
   Retain the handoff's manifest and registry JSON with the release record; do
   not copy the single-version catalog over a multi-version live catalog. The
   Registry is an editable operator seed outside all release/package checksum
   sets. Do not run the editor against the live Registry at this stage.
3. Render `RELEASE-NOTES.md` for the new version, then rebuild the staged root catalog
   from the actual ZIP bytes. Existing versions retain their previously
   published date and notes; every new version supplies both:

   ```powershell
   python .\scripts\create_update_catalog.py `
     --source-root '\\server\share\NvtFwCombiner-staging' `
     --published-at '0.10.7=2026-08-24T00:00:00Z' `
     --release-notes-file '0.10.7=.\artifacts\release\RELEASE-NOTES.md'
   ```

   For a brand-new two-ZIP source, repeat `--published-at` and
   `--release-notes-file` once for both `0.10.6` and `0.10.7`. The helper
   refuses a new ZIP without metadata, reads the version/product/RID from its
   inner manifest, recalculates the exact size and both hashes, and atomically
   replaces only the root catalog. If an existing SemVer's package size,
   package SHA-256, or inner-manifest SHA-256 differs, generation fails and the
   previous catalog bytes remain unchanged; publish a new version instead of
   replacing stable package identity.
4. Copy the live Registry into a separate operator staging directory. Use the
   repository editor first with `--dry-run` and then without `--dry-run`
   against that **staged Registry only**, with the proposed roots/statuses.
   Start the diagnostic Desktop with
   `--update-source-registry-path <staged-registry>` and run Version
   **Self-test**. Open the staged root in Settings, run **Check now**, verify
   both rows, install the newer package, and confirm there is no damaged or
   staging entry. Any failure stops publication and leaves the live Registry
   untouched.
5. Publish immutable ZIPs first and atomically replace the root catalog last;
   the catalog is the publication commit point. Retain every old ZIP referenced
   by the prior or current published catalog. Give ordinary users read-only
   access. Prefer a stable UNC namespace/DFS or share alias such as
   `\\novatek\firmware-tools\NvtFwCombiner`, so moving the backing server or
   volume does not change every client's configured path.
6. After the complete proposed root and staged Registry pass step 4, run the
   same full editor arguments against the **live Registry** with `--dry-run`.
   Reconfirm its exact current `--expected-revision`, then run once without
   `--dry-run`. That atomic replacement is the route-publication commit point:
   promote the verified root to `latest`, retain bounded fallbacks as
   `available`, and move retired roots to `deprecated`. This hotfix changes
   neither ZIP identity nor any package/catalog checksum. Unbounded
   filesystem/share search remains forbidden.

## Required 1.0.0 to 1.0.1 validation source

The pre-release local lab uses the repository-independent root
`C:\NvtFwCombiner-UpgradeLab` so moving or deleting a Git worktree cannot make
the result pass accidentally. Populate it only with genuinely rebuilt packages:

```text
C:\NvtFwCombiner-UpgradeLab\
├── RELEASE-MANIFEST.json
├── update-catalog.v1.json
├── update-source-registry.json
└── packages\
    ├── NvtFwCombiner-v1.0.0-win-x64.zip
    └── NvtFwCombiner-v1.0.1-win-x64.zip
```

The 1.0.0 and 1.0.1 ZIPs must name different reviewed source commits and carry
their own matching executable, launcher, manifest, SBOM, provenance, and hash
identities. After copying both ZIPs and preparing the two release-note files,
build the aggregate catalog and expose the latest version profile:

```powershell
python .\scripts\create_update_catalog.py `
  --source-root 'C:\NvtFwCombiner-UpgradeLab' `
  --published-at '1.0.0=2026-08-26T00:00:00Z' `
  --published-at '1.0.1=2026-08-26T00:01:00Z' `
  --release-notes-file '1.0.0=.\artifacts\upgrade-validation\1.0.0-notes.md' `
  --release-notes-file '1.0.1=.\artifacts\upgrade-validation\1.0.1-notes.md' `
  --manifest-copy '1.0.1=C:\NvtFwCombiner-UpgradeLab\RELEASE-MANIFEST.json' `
  --registry-template '.\docs\ci\update-source-registry.json.in' `
  --registry-output 'C:\NvtFwCombiner-UpgradeLab\update-source-registry.json' `
  --registry-revision 1 `
  --registry-published-at '2026-08-26T00:01:00Z'
```

The rendered local Registry points its sole `latest` entry at the exact
`C:\NvtFwCombiner-UpgradeLab\update-catalog.v1.json` file. Start the packaged
Desktop with `--update-source-registry-path` set to that rendered Registry,
then run Version **Self-test** and **Check now**, install and
activates 1.0.1, restarts through Bootstrap, switches back to 1.0.0, exercises
rollback, damages a non-active copied version to confirm the damaged count,
and deletes that version explicitly. Repeat the same sequence after copying the
unchanged complete root to a test UNC share. A folder/ZIP rename is never a
substitute for either package build.

All fields are required and unknown fields are rejected. Versions are stable
three-component SemVer values. Hashes are lowercase hexadecimal SHA-256.
`publishedAt` is canonical UTC ISO-8601 using
`yyyy-MM-ddTHH:mm:ssZ` or one through seven fractional-second digits before
`Z`; offsets, spaces, omitted seconds, and more than seven fractional digits
are rejected. `packagePath` is a forward-slash,
catalog-relative ZIP path and may not contain empty, dot, traversal, drive,
absolute, alternate-stream, device, or trailing-dot/space segments.

The entire catalog fails closed on any invalid entry; no partial publication is
allowed. The raw JSON document is limited to 1 MiB, each release-note value to
64 KiB UTF-8, and the catalog to 128 entries. A package is not `Verified` until
its exact length/hash, archive safety, inner release manifest, and closed
payload have all been checked independently of this catalog parse.

## Schema/runtime parity

| Contract fact | JSON Schema | Runtime admission |
| --- | --- | --- |
| Required fields and unknown fields | `required`; `additionalProperties: false` | strict source-generated JSON deserialization; null required values fail closed |
| Stable SemVer | exact three-component pattern | `ManagedAppVersion.TryParse` |
| Publication time | canonical UTC pattern plus `date-time` | invariant `TryParseExact` with the same zero-to-seven fractional-second forms |
| Package path | 5–512 characters, forward-slash ZIP pattern | the same minimum/maximum and extension plus Windows device/control/traversal guards |
| Package length | temporary version-independent range 1–134,217,728 | the same inclusive bound and stable-handle recheck; executable ceilings remain separate |
| SHA-256 | lowercase 64-character hex | the same ordinal lowercase grammar |
| Release notes | required string, at most 65,536 characters | non-null plus the stricter 65,536 UTF-8-byte transport bound |
| Version count | 1–128 | the same bounds; any invalid entry rejects the complete catalog |

Runtime-only stable-read, resolved-root, reparse-point, Windows device-name,
UTF-8 byte, and filesystem checks intentionally narrow the schema-admitted set;
runtime must never admit a document that the normative schema rejects.
