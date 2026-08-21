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

All fields are required and unknown fields are rejected. Versions are stable
three-component SemVer values. Hashes are lowercase hexadecimal SHA-256.
`publishedAt` is UTC ISO-8601 ending in `Z`. `packagePath` is a forward-slash,
catalog-relative ZIP path and may not contain empty, dot, traversal, drive,
absolute, alternate-stream, device, or trailing-dot/space segments.

The entire catalog fails closed on any invalid entry; no partial publication is
allowed. The raw JSON document is limited to 1 MiB, each release-note value to
64 KiB UTF-8, and the catalog to 128 entries. A package is not `Verified` until
its exact length/hash, archive safety, inner release manifest, and closed
payload have all been checked independently of this catalog parse.
