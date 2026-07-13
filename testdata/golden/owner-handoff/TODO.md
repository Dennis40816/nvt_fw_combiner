# Owner Data TODO

## Already Covered

- Standard Merge golden exists for `51920`, `51923`, `51926`, `51927`, `51928`, `51929`, `51931`, `51932`.
- These match the current `refcode/gen_flash_bin_v2/ic_config.json` coverage.
- Owner confirmed `NT51917` follows `NT51927` and `NT51919` follows `NT51929`; executable alias tests reuse the corresponding golden bytes.
- Owner-approved `merge_bin.7z` golden fixtures are recorded for `NT51930`, `NT51950` DP size `0x40000`, and `NT51951` DP size `0x80000`.

## Standard Merge

- [ ] Optional `NT51950` direct audit sample for DP size `0x80000` or `0x100000` if those variants are selected for release exposure.
- [ ] Optional `NT51951` direct audit sample for DP size `0x40000` or `0x100000` if those variants are selected for release exposure.
- [ ] Optional additional `NT51930` sample only if a new product variant or memory map needs promotion.
- [ ] Optional direct audit sample for `NT51917` if you want IC-specific files in addition to the alias regression.
- [ ] Optional direct audit sample for `NT51919` if you want IC-specific files in addition to the alias regression.

## DP Replace

- [ ] `NT51950` with DP size `0x40000`: provide `base.bin`, `dp.bin`, and `expected.bin`.
- [ ] `NT51950` with DP size `0x80000`: provide `base.bin`, `dp.bin`, and `expected.bin`.
- [ ] `NT51950` with DP size `0x100000`: provide `base.bin`, `dp.bin`, and `expected.bin`.
- [ ] `NT51951` with DP size `0x40000`: provide `base.bin`, `dp.bin`, and `expected.bin`.
- [ ] `NT51951` with DP size `0x80000`: provide `base.bin`, `dp.bin`, and `expected.bin`.
- [ ] `NT51951` with DP size `0x100000`: provide `base.bin`, `dp.bin`, and `expected.bin`.

## AB Merge

- [ ] `NT51919`: provide direct AB inputs/output and an owner-approved fact-scoped alias/parity decision; a Normal or whole-map alias is not enough.
- [ ] `NT51929`: provide product AB input/output and firmware-owner review in addition to the existing candidate/reference evidence.
- [ ] `NT51932`: provide product AB input/output and firmware-owner review in addition to the existing candidate evidence.
- [ ] `NT51950`: provide AB inputs/output, exact `Combiner.exe` identity, full command trace, and the exact `map.txt` sidecar used by the golden.
- [ ] `NT51951`: provide AB inputs/output, exact `Combiner.exe` identity, full command trace, and the exact `map.txt` sidecar used by the golden.

For every AB case, retain original source filenames and record their SHA-256,
source archive/ticket, output filename, and firmware-owner approval. AB header
CRC is performed only by the owner-approved Combiner stage; C# does not
calculate or write it.

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
