# NT51950 CtrlRAM Replace Handoff

The direct AUTO_PRJ-676/PID `0x4A06` Common FW 2.0.0 single package remains
canonical under
`testdata/golden/canonical/NT51950/ctrlram-replace/fw2.0.0/single/nt51950-fw200-single-auto-prj-676-20260717/`.

Issue #188 adds the owner-approved, fact-scoped Cascade alias under
`testdata/golden/canonical/NT51950/ctrlram-replace/fw2.0.0/cascade-2/nt51950-cascade2-geometry-nt51951-auto-prj-599-alias/`.
It reuses only the exact 2-IC TP geometry and postbuild facts proven by the
direct NT51951 AUTO_PRJ-599 case: one `0x1400` record at `0x33200`, writable
Diff CtrlRAM `[+0x000,+0x910)`, reference-preserved Diff NF
`[+0x910,+0x1400)`, and the `0x0780`-byte FWConfig copy from `0x22200` to the
fixed Backup at `0x36000`.

The NT51951 `0x80000` expected output is not an NT51950 direct golden.
NT51950 retains its separately declared `0x40000` capacity, and the alias does
not authorize wider IC counts, support promotion, or release publication. No
additional owner input is requested for #188; independent R3 review remains.
