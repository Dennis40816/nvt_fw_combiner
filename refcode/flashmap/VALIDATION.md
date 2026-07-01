# IC FlashMap Validation

Validated on 2026-07-01 from the owner-provided `IC FlashMap.7z` source archive. The repository stores the extracted workbook, postbuild scripts, mmap headers, hsi guide, and approved Combiner evidence copy as normal files rather than as a `.7z`.

## Trust Model

Postbuild scripts are treated as the behavioral truth because CtrlRAM Replace must reproduce their Combiner command order. mmap headers are used to explain why offsets and sizes exist. TP Overview is the desired human-facing documentation baseline, but it currently has discrepancies that should be corrected before being treated as complete product documentation.

## Supported By Postbuild Evidence

The postbuild-backed ICs verified in this snapshot are:

- NT51917: owner-confirmed alias of the NT51927 reference flow.
- NT51920: `CRC_Enable` normal mode.
- NT51923: `CRC_Enable` normal mode, including split `DiffDLM.bin` cascade source offsets `0x0` and `0x1400`.
- NT51926: `CRC_Enable` normal mode.
- NT51927: special flow using `MERGE_MODE` plus `NT51927BASED_GEN_CRC_MODE CRC32`; normalized as explicit single, 2IC, and 3IC command sequences.
- NT51928: owner-confirmed alias of the NT51927 reference flow for non-NB only. NT51928 NB is a separate IC and is not covered by this profile.
- NT51919: owner-confirmed alias of the NT51929/NT51932 reference flow.
- NT51929: owner-confirmed alias of the NT51932 reference flow.
- NT51930: `NT51930BASED_NORMAL_MODE CRC8`.
- NT51931: `NT51930BASED_NORMAL_MODE CRC8` per postbuild.
- NT51932: `NT51932BASED_NORMAL_MODE CRC8`.
- NT51950: `NT51950BASED_NORMAL_MODE CRC8`.
- NT51951: owner-confirmed alias of the NT51950 reference flow.

Alias profiles are represented explicitly so reports keep the selected NT-prefixed IC id even when the command family follows another IC reference.

## Documentation Warnings

These are not production-blocking for the postbuild catalog, but they mean TP Overview should be corrected:

- NT51929/NT51932: TP Overview appears to place NF at `0x1F200` and show a smaller header copy, while postbuild and mmap identify `FW_REGISTER = 0x1F200`, `NF_TABLE = 0x1FC00`, and header copy size `512`.
- NT51930: the current product target has no `>13 IC` case. Runtime maps cascade to the `<=13 IC` branch: `DiffDLM = 0x2F200`, size `65024`.
- NT51927: runtime follows the hard-coded postbuild command sequence. If mmap and TP Overview disagree, postbuild is authoritative and TP Overview should be updated to match it.
- NT51923: postbuild copies FW Config as one `2048` byte block, while TP Overview splits FW Config/FW Register details. This is explainable but should be documented explicitly.

## Implementation Decision

The first normalized CtrlRAM postbuild catalog includes NT51917, NT51919, NT51920, NT51923, NT51926, NT51927, NT51928, NT51929, NT51930, NT51931, NT51932, NT51950, and NT51951. NT51926 is intentionally `CRC_Enable` normal mode. NT51927 is intentionally represented as multiple `MERGE_MODE` commands followed by `NT51927BASED_GEN_CRC_MODE CRC32`. NT51917 and NT51928 follow the approved NT51927 command sequence; NT51919 and NT51929 follow the approved NT51932 command family; NT51951 follows the approved NT51950 command family.
