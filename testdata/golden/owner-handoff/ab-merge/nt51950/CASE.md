# NT51950 AB Merge Handoff

The tracked `nt51950-ab-boe-d82t80` and `nt51950-ab-hiway-d82t80` fixtures in
`testdata/golden/ab-merge/manifest.json` record the supplied DP_AB, TPA, TPB,
and expected output. They prove the V2 pre-Combiner staging boundary and the
uploaded Python reference result, but not legacy `Combiner.exe` equivalence.
The profile remains a repository-only compilable candidate and must not be
promoted from a successful process exit alone.

To close the external-processor evidence for either fixture, provide:

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
