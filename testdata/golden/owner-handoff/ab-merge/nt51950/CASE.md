# NT51950 AB Merge Handoff

This folder accepts private R3 evidence only. The current V2 profile is a
repository-only compilable candidate and must not be promoted from a successful
process exit alone.

Provide these files under `inputs/`:

- `dp-ab.bin`, `tpa.bin`, `tpb.bin`, and `expected.bin`;
- the exact `map.txt` sidecar consumed for this case;
- `combiner-command.txt` containing the complete working-directory-relative
  invocation and command order;
- `combiner-tool.json` with tool id, exact version, executable SHA-256,
  adapter id, platform, and timeout; and
- `provenance.json` listing each original filename, SHA-256, source
  archive/ticket, expected output filename, owner, and approval date.

The Combiner alone owns AB header CRC mutation. C# must not calculate or write
the header CRC, and an unrelated map sidecar cannot be substituted.
