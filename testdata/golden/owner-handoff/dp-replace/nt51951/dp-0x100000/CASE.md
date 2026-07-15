# NT51951 DP Replace, DP Size 0x100000

Requested files:

- `base.bin`
- `dp.bin`
- `expected.bin`
- optional `notes.txt`

Expected policy to verify: replace the full `0x100000` DP container, restore base TP `0x0A000-0x36FFF`, and retain customer info `0x37000-0x37FFF` from DP. Public synthetic oracle coverage is recorded in `testdata/public-synthetic/dp-replace/nt51950-nt51951-dp-replace-oracle-v1.json`.
