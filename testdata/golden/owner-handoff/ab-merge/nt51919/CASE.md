# NT51919 AB Merge Handoff

This folder accepts private R3 evidence only. Its payloads are ignored by Git
and do not enable AB UI/CLI routing or support exposure.

The tracked NT51929 case currently names NT51919 only as a fact-scoped alias,
and the Bootstrap regression proves complete candidate-output parity through
that alias. It is not a direct NT51919 product golden, and firmware-owner
approval of the alias scope remains required before promotion.

Provide these files under `inputs/`:

- `dp-ab.bin`, `tpa.bin`, `tpb.bin`, and `expected.bin`;
- `provenance.json` listing each original filename, SHA-256, source
  archive/ticket, expected output filename, owner, and approval date; and
- an owner decision that defines this AB case as a fact-scoped alias with
  direct parity evidence. It must not rely on a Normal or whole-map alias.

The result must be independently reviewed before this candidate is promoted.
