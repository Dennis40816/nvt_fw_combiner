# NT51919 AB Merge Handoff

No BIN upload is currently requested for the approved fact scope. This folder
remains available only if a new direct NT51919 product case is intentionally
opened; its payloads do not enable AB UI/CLI routing or support exposure.

The firmware owner approved the tracked NT51929 case as fact-scoped NT51919
family evidence on 2026-07-18, and the Bootstrap regression proves complete
candidate-output parity through that alias. It is not a direct NT51919 product
golden; runtime promotion remains a separate firmware-owner review.

Only for a newly opened direct NT51919 case, provide these files under `inputs/`:

- `dp-ab.bin`, `tpa.bin`, `tpb.bin`, and `expected.bin`;
- `provenance.json` listing each original filename, SHA-256, source
  archive/ticket, expected output filename, owner, and approval date; and
The result must be independently reviewed before any candidate is promoted.
