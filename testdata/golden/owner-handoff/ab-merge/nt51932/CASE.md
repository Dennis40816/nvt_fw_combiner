# NT51932 AB Merge Handoff

This folder accepts private R3 evidence only. Existing candidate evidence does
not replace a product golden or firmware-owner review.

Provide these files under `inputs/`:

- `dp-ab.bin`, `tpa.bin`, `tpb.bin`, and `expected.bin`; and
- `provenance.json` listing each original filename, SHA-256, source
  archive/ticket, expected output filename, owner, and approval date.

Do not treat NT51929 evidence as this IC's product golden without an explicit
owner parity decision.
