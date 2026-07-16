# NT51950 CtrlRAM Replace Handoff

No direct product CtrlRAM golden is currently committed. Provide one same-run
set for each selected mode:

- `single/`: complete `base.bin`, actual `inputs/normal.bin` (23,552),
  `inputs/vn.bin` (8,444), `inputs/nf.bin` (10,768), and complete
  `expected.bin`;
- `cascade/`: the single files plus actual `inputs/diff.bin` (5,120).

Base and expected output must have the same exact complete-firmware size.
Record Common FW version and mode. The Postbuild 2.0.0 reference command is
already tracked; provide a command/log only if the official run differs.
