# NT51929 AB Merge Handoff

This folder accepts private R3 evidence only. Existing candidate/reference
parity does not replace a product golden or firmware-owner review.

The existing 256 KiB `initial code` / `TPFW` / `FlashCode` Combiner archive is
a Normal case, not AB evidence. Do not substitute it for the required AB
container and both TP bank inputs.

Provide these files under `inputs/`:

- `dp-ab.bin`, `tpa.bin`, `tpb.bin`, and `expected.bin`; and
- `provenance.json` listing each original filename, SHA-256, source
  archive/ticket, expected output filename, owner, and approval date.

No new CRC, range, or alias fact is inferred from a filename or from this
handoff layout.
