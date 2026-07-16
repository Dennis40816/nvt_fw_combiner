# NT51926 CtrlRAM Replace Handoff

The committed base is owner-provided. The committed `normal`, `diff`, `mp`,
`vn`, and `nf` files were sliced from that base by repository tooling; they are
derived replay fixtures, not separately supplied replacement files.

For the quickest reference-parity check, put only the complete official output
at `cascade/expected.bin` and confirm the official legacy flow used the exact
committed fixture hashes.

For a direct product golden, put one same-run set under `cascade/`:

```text
base.bin                   262,144 bytes
inputs/normal.bin           11,264 bytes
inputs/diff.bin             10,240 bytes
inputs/mp.bin                9,216 bytes
inputs/vn.bin                5,728 bytes
inputs/nf.bin               11,728 bytes
expected.bin               262,144 bytes
```

The direct product inputs must be the actual files used by the official run,
not new slices from `base.bin`. The BAT command and Legacy Combiner 1.13 tool
are already pinned; provide a command/log only if the official run differs.

NT51926 single is not requested in this batch.
