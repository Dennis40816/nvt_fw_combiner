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
It is a single-version seed containing the new canonical ZIP under `packages/`
and a catalog that references only that ZIP. It is not the authoritative live
multi-version catalog and must never overwrite an existing update source by
itself. The five immutable GitHub Release assets remain unchanged.

A complete update-source publication supplies the latest aggregate
`update-catalog.v1.json` together with every ZIP referenced by it. Copying only
the new ZIP, or copying the single-version seed catalog over a live aggregate,
is incomplete publication.

1. Produce and smoke each canonical release ZIP. Do not rename a ZIP after it
   was packaged.
2. Download the matching single-version Actions handoff. In a staging copy of
   the live update-source root, retain the current catalog and every version
   that should remain available under `packages/`, then add the handoff ZIP.
3. Render `RELEASE-NOTES.md` for the new version, then rebuild the root catalog
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
4. Open the staging root in Settings and run **Check now**. Verify both rows,
   install the newer package, and confirm there is no damaged/staging entry.
5. Publish immutable ZIPs first and atomically replace the root catalog last;
   the catalog is the publication commit point. Retain every old ZIP referenced
   by the prior or current published catalog. Give ordinary users read-only
   access. Prefer a stable UNC namespace/DFS or share alias such as
   `\\novatek\firmware-tools\NvtFwCombiner`, so moving the backing server or
   volume does not change every client's configured path.
6. If the complete root must move to another local/UNC path, browse and confirm
   the new root in Settings, then run **Check now**. Until the separate fixed
   Registry contract ships, that confirmed folder remains the only source.
   The Registry may later enumerate a bounded explicit root set; unbounded
   filesystem/share search remains forbidden.

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
| Package length | 1–80,000,000 normally; v1.0.0 only: 1–134,217,728 | the same version-scoped inclusive bounds and stable-handle recheck |
| SHA-256 | lowercase 64-character hex | the same ordinal lowercase grammar |
| Release notes | required string, at most 65,536 characters | non-null plus the stricter 65,536 UTF-8-byte transport bound |
| Version count | 1–128 | the same bounds; any invalid entry rejects the complete catalog |

Runtime-only stable-read, resolved-root, reparse-point, Windows device-name,
UTF-8 byte, and filesystem checks intentionally narrow the schema-admitted set;
runtime must never admit a document that the normative schema rejects.
