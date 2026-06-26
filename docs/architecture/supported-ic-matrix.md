# Supported IC / Workflow Matrix Draft

This is a planning inventory, not a support claim. A row becomes supported only after profile validation, integrity disposition, golden regression, and owner sign-off. `Unknown` never means `None`.

| IC | Standard Merge | AB Merge | Replace planning | Integrity evidence | Current evidence | 1.0 status |
| --- | --- | --- | --- | --- | --- | --- |
| NT51920 | reference candidate | no evidence | Display/TP HW/TP FW/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51923 | reference candidate | no evidence | Display/TP HW/TP FW/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51926 | reference candidate | no evidence | Display/TP HW/TP FW/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51927 | reference candidate | no evidence | Display/TP HW/TP FW/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51928 | reference candidate + LD | no evidence | Display/TP HW/TP FW/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51929 | reference candidate | DP_AB + split-DP concept | region inventory TBD | TPA/TPB CRC explicitly None; TPB relocation required | verified AB sample | Priority candidate |
| NT51931 | reference candidate | no evidence | Display/TP HW/TP FW/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51932 | reference candidate | DP_AB | region inventory TBD | TPA/TPB CRC explicitly None; TPB relocation required | legacy AB reference | Priority candidate |
| NT51950 | no Standard evidence | uploaded combiner | TP HW CtrlRAM likely relevant; Display/TP FW/General TBD | TPA verify-existing; TPB recalculate/write after relocation | verified output + CRC values | Priority candidate |
| NT51951 | no Standard evidence | uploaded config | TP HW CtrlRAM likely relevant; Display/TP FW/General TBD | TPA verify-existing; TPB recalculate/write after relocation | no golden output | Candidate pending data |

## Workflow promotion gate per IC/mode

- authoritative memory map, region atomicity, and owner;
- blank/reference initializer and canonical profile;
- explicit integrity disposition for every processor stage;
- valid/invalid fixtures and expected output SHA-256;
- mutation/processor diff review;
- UI catalog visibility and terminology decision;
- release/support owner sign-off.

## Replace-specific evidence still required

- Display: DP partition map and confirmation that TP is whole-only;
- TP HW: complete named CtrlRAM regions/groups and post-processing dependencies, with DP whole-only;
- TP FW: declared non-CtrlRAM TP regions and required post-processing dependencies, with DP whole-only;
- General: globally forbidden/protected ranges, alignment, overlap, and post-processing trigger rules.
