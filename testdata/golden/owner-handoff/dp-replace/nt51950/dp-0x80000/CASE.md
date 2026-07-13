# NT51950 DP Replace, DP Size 0x80000

Requested files:

- `base.bin`
- `dp.bin`
- `expected.bin`
- optional `notes.txt`

Expected policy to verify: transiently pad DP to the selected `0x80000` base length, replace the full DP container, then restore base TP `0x0A000-0x36FFF` and customer info `0x37000-0x37FFF`.
