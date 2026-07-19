# NT51950 AB Merge Handoff

No BIN upload is currently requested for NT51950.

The canonical `nt51950-ab-boe-d82t80` and
`nt51950-ab-hiway-d82t80` cases below `testdata/golden/canonical/NT51950/ab-merge`
record the supplied DP_AB, TPA, TPB, and expected output. They prove full-byte
V2/Python/Combiner parity for their named fixed-`0x80000` cases. The profile
remains a repository-only executable candidate and must not be promoted to
runtime/UI support without firmware-owner review.

For a new or otherwise unrecorded NT51950 case, provide:

- `dp-ab.bin`, `tpa.bin`, `tpb.bin`, and `expected.bin`;
- `combiner-command.txt` containing the complete working-directory-relative
  invocation and command order;
- `combiner-tool.json` with tool id, exact version, executable SHA-256,
  adapter id, platform, and timeout; and
- `provenance.json` listing each original filename, SHA-256, source
  archive/ticket, expected output filename, owner, and approval date.

The Combiner alone owns AB header CRC mutation. C# must not calculate or write
the header CRC. This AB command does not consume `map.txt`.
