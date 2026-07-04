# NT51951 DP Replace, DP Size 0x80000

Requested files:

- `base.bin`
- `dp.bin`
- `expected.bin`
- optional `notes.txt`

Expected policy to verify: transiently pad DP to `0x100000`, replace full DP container, then restore base TP `0x0A000-0x36FFF`.
