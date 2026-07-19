# NT51950 Standard Merge Handoff

Current owner-approved NT51950 Standard Merge golden data covers DP size `0x40000` in direct case `51950-dp-256k` under `testdata/golden/canonical/` from `merge_bin.7z`.

Use this directory only for optional additional DP-size audit samples, such as `0x80000` or `0x100000`, if selected for release exposure.

Requested files:

- `dp.bin`
- `tp.bin`
- `expected.bin`
- optional `notes.txt` with source/provenance and expected filename.

Current expected policy to verify: DP initializes the work image, then TP overlays `0x0A000-0x36FFF`.
