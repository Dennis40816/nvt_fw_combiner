# NT51932 AB Merge Handoff

This folder accepts private R3 evidence only. Existing candidate evidence does
not replace a product golden or firmware-owner review.

The current Bootstrap regression compares the NT51932 candidate byte-for-byte
with the immutable Python snapshot's directly named `51932` configuration over
an address-sensitive synthetic vector (`cd54e124...7de10ce`). This confirms the
candidate/reference geometry only; it is not a product golden and does not
make the NT51929 owner fixture applicable to NT51932.

Provide these files under `inputs/`:

- `dp-ab.bin`, `tpa.bin`, `tpb.bin`, and `expected.bin`; and
- `provenance.json` listing each original filename, SHA-256, source
  archive/ticket, expected output filename, owner, and approval date.

Do not treat NT51929 evidence as this IC's product golden without an explicit
owner fact-scoped parity decision. Promotion therefore still requires either
the direct package above or that reviewed fact-scoped alias evidence, plus
firmware-owner approval.
