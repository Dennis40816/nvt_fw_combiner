# NT51950 Standard Merge Handoff

Requested files:

- `dp.bin`
- `tp.bin`
- `expected.bin`
- optional `notes.txt` with source/provenance and expected filename.

Current expected policy to verify: DP initializes the work image, then TP overlays `0x0A000-0x36FFF`.
