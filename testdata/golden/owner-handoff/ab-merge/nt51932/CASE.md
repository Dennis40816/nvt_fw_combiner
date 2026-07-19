# NT51932 AB Merge Handoff

No BIN upload is currently requested for the approved fact scope. This folder
remains available only if a new direct NT51932 product case is intentionally
opened; existing candidate evidence does not replace a product golden or
firmware-owner runtime-promotion review.

The current Bootstrap regression compares the NT51932 candidate byte-for-byte
with the immutable Python snapshot's directly named `51932` configuration over
an address-sensitive synthetic vector (`cd54e124...7de10ce`). This confirms the
candidate/reference geometry only; it is not a product golden. Separately, the
firmware owner approved the NT51929 direct case as fact-scoped NT51932 family
evidence on 2026-07-18. The canonical alias records only that reviewed scope.

Only for a newly opened direct NT51932 case, provide these files under `inputs/`:

- `dp-ab.bin`, `tpa.bin`, `tpb.bin`, and `expected.bin`; and
- `provenance.json` listing each original filename, SHA-256, source
  archive/ticket, expected output filename, owner, and approval date.

Do not present NT51929 bytes as a direct NT51932 product golden. Runtime
promotion remains separate from the approved family-fact alias and still
requires firmware-owner review.
