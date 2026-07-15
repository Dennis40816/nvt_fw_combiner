# NT51951 AB Merge Handoff

This folder accepts private R3 evidence only. The candidate profile and its
regression already pin the following non-promoted binding:

```text
Combiner.exe 1.13.0
NT51950BASED_MERGE_AB_MODE CRC8 A.bin B.bin output.bin 0x80000
```

The regression runs the immutable Python snapshot and V2 + Combiner from the
same inputs, asserts complete-byte equality, and permits Combiner writes only
to the B ILM, B DLM, and B header-CRC fields. It is topology evidence, not a
direct owner product golden or runtime approval.

Provide these files under `inputs/`:

- `dp-ab.bin`, `tpa.bin`, `tpb.bin`, and `expected.bin`;
- `provenance.json` listing each original filename, SHA-256, source
  archive/ticket, expected output filename, owner, approval date, and approval
  of the binding above.

Do not provide `map.txt`, a duplicate Combiner executable, or duplicate
command/tool sidecars when this binding is used. If the owner case uses a
different command, tool version, executable hash, or staging order, provide
`combiner-command.txt` and `combiner-tool.json` instead; it is a distinct R3
candidate and cannot be compared to this one by assumption.

The Combiner alone owns AB header CRC mutation, and this AB command does not
consume `map.txt`. Do not copy NT51950's input/output result into this case
without direct owner evidence and review.
