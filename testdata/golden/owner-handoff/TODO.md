# Owner Data TODO

## Already Covered

- Standard Merge golden exists for `51920`, `51923`, `51926`, `51927`, `51928`, `51929`, `51931`, `51932`.
- These match the current `refcode/gen_flash_bin_v2/ic_config.json` coverage.
- Owner confirmed `NT51917` follows `NT51927` and `NT51919` follows `NT51929`; executable alias tests reuse the corresponding golden bytes.

## Standard Merge

- [ ] `NT51950`: provide `dp.bin`, `tp.bin`, and `expected.bin`.
- [ ] `NT51951`: provide `dp.bin`, `tp.bin`, and `expected.bin`.
- [ ] `NT51930`: provide only if Standard Merge should become supported for this IC.
- [ ] Optional direct audit sample for `NT51917` if you want IC-specific files in addition to the alias regression.
- [ ] Optional direct audit sample for `NT51919` if you want IC-specific files in addition to the alias regression.

## DP Replace

- [ ] `NT51950` with DP size `0x40000`: provide `base.bin`, `dp.bin`, and `expected.bin`.
- [ ] `NT51950` with DP size `0x80000`: provide `base.bin`, `dp.bin`, and `expected.bin`.
- [ ] `NT51950` with DP size `0x100000`: provide `base.bin`, `dp.bin`, and `expected.bin`.
- [ ] `NT51951` with DP size `0x40000`: provide `base.bin`, `dp.bin`, and `expected.bin`.
- [ ] `NT51951` with DP size `0x80000`: provide `base.bin`, `dp.bin`, and `expected.bin`.
- [ ] `NT51951` with DP size `0x100000`: provide `base.bin`, `dp.bin`, and `expected.bin`.

## CtrlRAM Replace

- [ ] At least one `NT51927` single/2-chip/3-chip real postbuild case.
- [ ] At least one `NT51950` single/cascade real postbuild case.
- [ ] At least one `NT51951` single/cascade real postbuild case.
- [ ] Optional sweep cases for `NT51917`, `NT51919`, `NT51920`, `NT51923`, `NT51926`, `NT51928`, `NT51929`, `NT51930`, `NT51931`, `NT51932`.

For each CtrlRAM case, provide:

- `base.bin`
- replacement CtrlRAM BINs under `inputs/`
- `expected.bin` if postbuild final output exists
- `combiner-cmd.txt` or postbuild log if available
- IC number mode: `single`, `cascade`, `1`, `2`, or `3`
