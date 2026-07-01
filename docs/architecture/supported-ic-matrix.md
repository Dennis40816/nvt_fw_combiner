# Supported IC / Workflow Matrix Draft

This is a planning inventory, not a support claim. A row becomes supported only after profile validation, integrity disposition, golden regression, and owner sign-off. `Unknown` never means `None`.

Current owner priority as of 2026-06-30:

- focus on normal Merge and normal Replace for DP Replace and CtrlRAM Replace workflows;
- defer AB Code Merge implementation for now;
- include NT51950 and NT51951 normal Merge with the confirmed DP Perspective TP overlay range `0xA000..0x36FFF`; golden cases remain required;
- require Replace UI to collect IC num before profile-specific regions are shown. ICs with only single/cascade choices use text labels; ICs with three or more concrete choices such as NT51917/NT51927/NT51928 use numeric count selection, optionally with an Other/custom path later;
- expect CtrlRAM Replace CRC/header recalculation through approved legacy `combiner.exe` postbuild sequences.

The per-IC Merge/Replace flowchart reference is [`ic-workflow-flowcharts.md`](ic-workflow-flowcharts.md). Update both documents together when IC workflow status changes.

| IC | Standard Merge | AB Merge | Replace planning | Integrity evidence | Current evidence | 1.0 status |
| --- | --- | --- | --- | --- | --- | --- |
| NT51917 | follows NT51927 | no evidence | DP/CtrlRAM priority | CtrlRAM Replace uses the NT51927 reference flow | owner alias confirmation | Candidate; postbuild core implemented |
| NT51919 | follows NT51929 | no evidence | DP/CtrlRAM priority | CtrlRAM Replace uses the NT51929/NT51932 reference flow | owner alias confirmation | Candidate; postbuild core implemented |
| NT51920 | reference candidate | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51923 | reference candidate | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51926 | reference candidate | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51927 | reference candidate | no evidence | DP/CtrlRAM priority | CtrlRAM Replace uses `MERGE_MODE` plus `NT51927BASED_GEN_CRC_MODE CRC32` | `gen_flash_bin_v2` config + IC FlashMap postbuild | Candidate; postbuild core implemented |
| NT51928 | reference candidate + LD | no evidence | DP/CtrlRAM priority | CtrlRAM Replace uses the NT51927 reference flow for non-NB only; NT51928 NB is a separate IC and is not covered | `gen_flash_bin_v2` config + owner alias confirmation | Candidate; postbuild core implemented |
| NT51929 | reference candidate | DP_AB + split-DP concept | DP/CtrlRAM priority | CtrlRAM Replace uses the NT51932 reference flow; TPA/TPB CRC explicitly None for AB evidence; TPB relocation required | verified AB sample + owner alias confirmation | Priority candidate; postbuild core implemented |
| NT51930 | no built-in Standard Merge profile yet | no evidence | CtrlRAM priority; DP/General TBD | CtrlRAM Replace uses `NT51930BASED_NORMAL_MODE CRC8`; current cascade maps to `<=13 IC` DiffDLM branch | IC FlashMap postbuild + owner IC-count confirmation | Candidate; postbuild core implemented |
| NT51931 | reference candidate | no evidence | DP/CtrlRAM/General TBD | Unknown | `gen_flash_bin_v2` config | Candidate |
| NT51932 | reference candidate | DP_AB | region inventory TBD | TPA/TPB CRC explicitly None; TPB relocation required | legacy AB reference | Priority candidate |
| NT51950 | normal merge requested; DP Perspective available | uploaded combiner; deferred | DP and CtrlRAM priority | CtrlRAM Replace postbuild uses `NT51950BASED_NORMAL_MODE CRC8`; DP Merge/Replace uses DP-as-base plus confirmed TP overlay/preserve range `0xA000..0x36FFF` | IC FlashMap postbuild + DP Perspective; normal merge golden pending | Priority candidate pending golden |
| NT51951 | normal merge requested; DP Perspective shared with 950 | uploaded config; deferred | DP and CtrlRAM priority | CtrlRAM Replace uses the NT51950 reference flow; DP Merge/Replace uses the same confirmed 950/951 DP length and TP range policy | DP Perspective evidence + owner alias confirmation; no normal merge golden output | Candidate pending golden |

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
- IC num: UI and request model must bind Replace to the selected IC before presenting region choices; two-option ICs use text choices such as `single`/`cascade`, while three-or-more concrete count ICs use numeric count selection with future room for Other/custom exceptions;
- CRC/header: exact legacy `combiner.exe` version, invocation, read/write ranges, execution order, and golden evidence;
- General: globally forbidden/protected ranges, alignment, overlap, and post-processing trigger rules.
