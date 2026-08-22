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
    └── NvtFwCombiner-v1.0.0-win-x64.zip
```

The `packagePath` values in the root catalog are respectively
`packages/NvtFwCombiner-v0.10.6-win-x64.zip` and
`packages/NvtFwCombiner-v1.0.0-win-x64.zip`; the declared lengths and hashes
must describe those exact ZIP bytes and their inner `RELEASE-MANIFEST.json`.
For a relocation test, rename or move the complete `NvtFwCombiner` root,
confirm the new source path in Settings, and run Check now. This is
identity-neutral. Renaming only `packages`, an individual ZIP, or another path
below the root is not the same test: its catalog-relative `packagePath` must be
updated and therefore changes the admitted package identity.

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
| Package length | 1–80,000,000 | the same inclusive bounds and stable-handle recheck |
| SHA-256 | lowercase 64-character hex | the same ordinal lowercase grammar |
| Release notes | required string, at most 65,536 characters | non-null plus the stricter 65,536 UTF-8-byte transport bound |
| Version count | 1–128 | the same bounds; any invalid entry rejects the complete catalog |

Runtime-only stable-read, resolved-root, reparse-point, Windows device-name,
UTF-8 byte, and filesystem checks intentionally narrow the schema-admitted set;
runtime must never admit a document that the normative schema rejects.
