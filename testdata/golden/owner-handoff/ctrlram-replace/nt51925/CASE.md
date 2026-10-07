# NT51925 CtrlRAM Replace Handoff

Status: awaiting owner evidence for the proposed NFC 1.2.2 restoration in
[ADR 0083](../../../../../docs/adr/0083-restore-nt51925-standard-merge-and-ctrlram-replace.md).
The four prepared option A profiles are
`nt51925-ctrlram-replace-fw141-runtime-single`,
`nt51925-ctrlram-replace-fw141-runtime-cascade`,
`nt51925-ctrlram-replace-fw200-runtime-single` and
`nt51925-ctrlram-replace-fw200-runtime-cascade`, each version `0.1.0`.
They reuse NT51926 canonical geometry as executable candidates with
Contract Only evidence. Existing runtime-reference candidate admission can
execute them; it does not certify NT51925 or establish support approval.

## Required cases

Supply one independent same-run case in each of:

```text
fw1.4.1/single/inputs/
fw1.4.1/cascade/inputs/
fw2.0.0/single/inputs/
fw2.0.0/cascade/inputs/
```

Declare the actual Cascade IC count in each case. The prepared typed choices
are Single and generic Cascade, as in the borrowed NT51926 contract; exact
counts and supported FW versions are awaiting owner confirmation. The 1.4.1
contract provisionally applies to `[1.0.0,2.0.0)` and the 2.0.0 contract to
`[2.0.0,+infinity)`. Metadata and filenames do not establish topology authority.

## Files in each case

Keep the original technical filenames. Place actual physical same-run inputs
under `inputs/`, complete official final output at `expected.bin` (or identify
its original filename), and commands/provenance at `notes.txt`:

| Input / output | Prepared option A size / consumed extent |
| --- | --- |
| Actual TP input | Conventional TP-work image: 245,760 bytes (`0x3C000`); full-Flash reference route: 262,144 bytes (`0x40000`). Record the actual physical input and its exact size. |
| Actual DP Initial Code input used to produce final FlashCode | Conventional address-bearing container: 262,144 bytes (`0x40000`); consumed DP window `[0x3E000,0x40000)`. |
| `Normal_Ctrlram.bin` | Consumed prefix 11,264 bytes (`0x2C00`). |
| `MP_Ctrlram.bin` | Consumed prefix 9,216 bytes (`0x2400`). |
| `VN_Ctrlram.bin` | FW 1.4.1: 5,728 bytes (`0x1660`); FW 2.0.0: 5,278 bytes (`0x149E`). |
| `NF_Ctrlram.bin` | Consumed prefix 11,728 bytes (`0x2DD0`). |
| `DiffDLM.bin` | Cascade only: consumed prefix 10,240 bytes (`0x2800`); absent from the Single plan. |
| `expected.bin` | Complete official final FlashCode: 262,144 bytes (`0x40000`). |
| Independent TP-only expected output, if supplied | 245,760 bytes (`0x3C000`), explicitly identified as an official TP-only output. A final FlashCode prefix is not automatically TP-only Golden evidence. |
| `notes.txt` | Text; no fixed size. Slot mapping, exact physical sizes and SHA-256, Common FW, declared topology/count, official commands and tool versions, expected name and source provenance. |

The CtrlRAM sizes above are consumed prefixes, not required physical-file
lengths. Supply the original files, including their actual tails; never slice
replacements from final output or manufacture inputs to match these sizes.
Runtime replacement uses the existing truncation/preserved-reference-tail
contract. Any different official range or physical-input contract must be
reviewed before option A promotion.

Follow [the current owner intake contract](../../README.md): do not create
`base.bin` or ask the owner to export a pre-replacement base. The replay must
use physical Postbuild inputs from the same official run, complete final
expected FlashCode and exact command/provenance. Identify how the TP input and
any required firmware-config material were produced. If the physical input
set cannot reconstruct an immutable replay reference, record the missing
material; do not substitute the expected output or invent a reconstruction.

Compare the complete image under an owner-approved NT51925 contract, including
DP preservation, exact processor write ranges, CRC/header order and naming.
Existing NT51926 allowed-byte-difference bounds are not NT51925 authority.
The normal FlashCode naming contract uses
`NT51925_FlashCode_D{dp-version}T{tp-version}_{date}.bin`; record the official
name and any difference. TP-only and full-Flash routes need independently
appropriate expected outputs before claiming parity for both capacities.

## Owner evidence

- The official NT51925 TP flash map or mmap.
- The postbuild `.bat` for each Common FW version, including 1.4.1 and 2.0.0.
- The FW versions and topologies in use, including actual Cascade counts.
- Whether NT51925 matches NT51926 or differs, and every differing fact.
- Golden cases: Standard Merge at least one single; CtrlRAM Replace one for
  each FW version and each topology listed above.

No official NT51925 map/postbuild or Golden is currently supplied. The
`925&926` workbook is normal-header evidence only. Official evidence,
independent full-output comparisons, exact write-range audit and firmware-owner
R3 approval remain required before support promotion or release.

Incoming BINs remain ignored by `*.bin`; only empty `inputs/.keep` placeholders
are prepared. Do not add, copy or certify firmware payloads in this preparation.
Keep personal names, accounts and private paths/URLs out of provenance.
