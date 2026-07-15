# NT51951 AB Merge Handoff

This folder accepts private R3 evidence only. The reference configuration is
not a product golden, an exact Combiner binding, or an approved runtime map.

Provide these files under `inputs/`:

- `dp-ab.bin`, `tpa.bin`, `tpb.bin`, and `expected.bin`;
- `combiner-command.txt` containing the complete working-directory-relative
  invocation and command order;
- `combiner-tool.json` with tool id, exact version, executable SHA-256,
  adapter id, platform, and timeout; and
- `provenance.json` listing each original filename, SHA-256, source
  archive/ticket, expected output filename, owner, and approval date.

The Combiner alone owns AB header CRC mutation, and this AB command does not
consume `map.txt`. Do not copy NT51950's input/output result into this case
without direct owner evidence and review.
