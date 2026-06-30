# Supported IC / Workflow Matrix Draft

This is a planning inventory, not a support claim. A row becomes supported only after profile validation, integrity disposition, golden regression, and owner sign-off. `Unknown` never means `None`.

Current owner priority as of 2026-06-30:

- focus on normal Merge and normal Replace for DP Replace and CtrlRAM Replace workflows;
- defer AB Code Merge implementation for now;
- include NT51950 and NT51951 normal Merge after owner memory maps are provided;
- require Replace UI to collect IC num before profile-specific regions are shown, using `single` or `cascade` initially while reserving `numeric`;
- expect Replace CRC/header recalculation through legacy `combiner.exe`, with exact invocation still owner-supplied.

| IC | Standard Merge | AB Merge | Replace planning | Integrity evidence | Current evidence | 1.0 status |
| --- | --- | --- | --- | --- | --- | --- |
| NT51920 | reference candidate | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51923 | reference candidate | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51926 | reference candidate | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51927 | reference candidate | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51928 | reference candidate + LD | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51929 | reference candidate | DP_AB + split-DP concept | region inventory TBD | TPA/TPB CRC explicitly None; TPB relocation required | verified AB sample | Priority candidate |
| NT51931 | reference candidate | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51932 | reference candidate | DP_AB | region inventory TBD | TPA/TPB CRC explicitly None; TPB relocation required | legacy AB reference | Priority candidate |
| NT51950 | normal merge requested; memory map pending owner | uploaded combiner; deferred | DP and CtrlRAM priority | AB: TPA verify existing CRC; TPB relocate/recalculate/write CRC. Replace: CRC/header expected through legacy combiner.exe; exact invocation/ranges pending | AB output + CRC values; normal merge map pending | Priority candidate pending map |
| NT51951 | normal merge requested; memory map pending owner | uploaded config; deferred | DP and CtrlRAM priority | AB: TPA verify existing CRC; TPB relocate/recalculate/write CRC. Replace: CRC/header expected through legacy combiner.exe; exact invocation/ranges pending | AB config evidence; no normal merge golden output; map pending | Candidate pending map |

## Workflow promotion gate per IC/mode

- authoritative memory map, region atomicity, and owner;
- blank/reference initializer and canonical profile;
- explicit integrity disposition for every processor stage;
- valid/invalid fixtures and expected output SHA-256;
- mutation/processor diff review;
- UI catalog visibility and terminology decision;
- release/support owner sign-off.

## Replace-specific evidence still required

- DP Replace: DP partition map and allowed atomicity;
- CtrlRAM Replace: complete named CtrlRAM regions/groups and post-processing dependencies;
- IC num: UI and request model must bind Replace to the selected IC before presenting region choices; initial modes are `single` and `cascade`, with `numeric` reserved;
- CRC/header: exact legacy `combiner.exe` version, invocation, read/write ranges, execution order, and golden evidence;
- General: globally forbidden/protected ranges, alignment, overlap, and post-processing trigger rules.
