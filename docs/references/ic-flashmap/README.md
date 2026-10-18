# IC FlashMap Reference Evidence

This directory retains the public provenance inventory and explanatory notes
for reviewed IC flash-map evidence. The confidential flash maps, postbuild
scripts, memory-map headers, and firmware source are identified by stable IDs,
size, and SHA-256 in the
[confidential reference manifest](../confidential-references.json). That
manifest contains no original source paths. In `nvt-private-assets`, each
SHA-256 resolves to the original evidence through
`nfc/references/SHA256SUMS`.

The evidence supports human review of flash geometry, postbuild command
selection, and firmware metadata layout. Executable production facts remain
in approved profiles and catalogs. Intake records new confidential evidence in
the private repository layout and updates the public manifest after review.
Release packages include only public reference notes and manifests.

## Desay workbook intake — 2026-09-08

This intake identified unresolved IC identity, allocation, and protected-range
questions. The reviewed source remains private. No new production geometry was
admitted from this intake; the profile and owner decisions govern execution.

## Public DP Perspective refresh — 2026-09-23

The reviewed DP perspective update informed human layout review for NT51950
and NT51951. Its public evidence ID is `flashmap-2026-09-22`. The source did
not expand TP write authority or change the protected customer-information
range. Accepted production behavior remains in the profiles.
