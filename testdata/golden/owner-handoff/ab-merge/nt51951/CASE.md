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

## Reference-equivalence experiment

For NT51951, the reference output starts as the complete one-mebibyte `DP_AB`
image. It verifies TPA's existing CRC and overlays TPA
`[0xA000, 0x37000)` at the same output address. It then relocates TPB ILM, DLM,
and DIFF by `0x80000`, recomputes the TPB CRC over `[0xA100, 0xA130)`, writes
that CRC at `0xA130`, and overlays TPB at `[0x8A000, 0xB7000)`.

The V2 experiment preserves those output locations but divides ownership:

1. It copies the complete `DP_AB`, stages A/B banks, and overlays TPA.
2. It clones TPB and C# relocates **only** the TPB DIFF scalar by `0x80000`.
3. Combiner receives the staged A/B artifacts with the `0x80000` argument and
   writes TPB ILM, DLM, and header CRC in its host-created output staging copy.
4. The regression compares the resulting complete BIN to Python's complete
   BIN, not merely the three header fields.

Therefore the experiment establishes that Combiner replaces Python's TPB
header/CRC work for the declared command binding. It does not imply that the
same binding is approved for a different 51951 owner case.

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
