# Update catalog v2 contract

Catalog v2 is the strict notification-policy extension of the existing v1
contract. A direct update source publishes it as `update-catalog.v2.json`; a
Registry may select any exact absolute filename. Its normative wire shape is
`update-catalog-v2.schema.json`.

Every v1 root and entry field remains required with identical bounds and
meaning. The root `schemaVersion` is exact integer `2`, and every entry adds
one required `notificationPolicy` whose only accepted values are exact
lowercase `manual-only` and `notify`. Unknown fields and missing, null,
case-variant, numeric, duplicate, or unknown policy values reject the complete
Catalog. Runtime admission also applies the v1 stable-read, UTF-8 byte,
safe-path, resolved-root, and reparse-point checks.

Both policies preserve explicit Check and explicit installation. `notify` may
become the existing automatic prompt candidate; `manual-only` is filtered in
Application before package I/O and is not projected by an automatic check.
Policy is not part of package identity. Catalog v1 entries map explicitly to
effective `notify`.

When both direct-root files exist, v2 is authoritative. An invalid present v2
fails closed and never falls back to v1. V1 is read only when v2 is absent.
Registry revision/digest anti-rollback remains the authority for routed
publications; direct-root selection has no persisted schema floor in v1.0.8.

The v1 contract's version, timestamp, path, package-size, digest, release-note,
entry-count, and raw-document bounds apply unchanged. `releaseNotes` remains
the canonical per-version changelog field.
