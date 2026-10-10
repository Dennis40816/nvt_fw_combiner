# NT51925 CtrlRAM Replace Handoff

Status: awaiting owner evidence for the proposed NFC 1.2.2 restoration in
[ADR 0084](../../../../../docs/adr/0084-restore-nt51925-standard-merge-and-ctrlram-replace.md).
The four isolated provisional profiles are
`nt51925-ctrlram-replace-fw141-runtime-single`,
`nt51925-ctrlram-replace-fw141-runtime-cascade`,
`nt51925-ctrlram-replace-fw200-runtime-single` and
`nt51925-ctrlram-replace-fw200-runtime-cascade`, each version `0.1.0`.
They belong to the NT51925-only `nt51925-ctrlram-replace-candidate` bundle.
Geometry follows the owner TP overview and postbuild flow; it is not
independent expected-output evidence or NT51926 shared authority. Policy is `Unavailable`/`Candidate`/`ContractOnly`.
The profiles use the existing `compilable` stage; no authoring snapshot,
exact executable resolution, execution or Build is admitted. The
runtime-reference candidate exception used by other routes does not apply.

## Required cases

Supply one independent same-run case in each of:

```text
fw1.4.1/single/inputs/
fw1.4.1/cascade/inputs/
fw2.0.0/single/inputs/
fw2.0.0/cascade/inputs/
```

Declare the actual Cascade IC count in each case. The declared typed choices
are Single (one IC) and Cascade (two or three ICs). Support promotion and
independent expected-output evidence remain pending. The 1.x
contract provisionally applies to `[1.0.0,2.0.0)` and the 2.0.0 contract to
`[2.0.0,+infinity)`. Metadata and filenames do not establish topology authority.

## Files in each case

Keep the original technical filenames. Place actual physical same-run inputs
under `inputs/`, complete official final output at `expected.bin` (or identify
its original filename), and commands/provenance at `notes.txt`:

| Input / output | Provisional declared size / consumed extent |
| --- | --- |
| Actual TP input | Conventional TP-work image: 196,608 bytes (`0x30000`); full-Flash reference route: 262,144 bytes (`0x40000`). Record the actual physical input and its exact size. |
| Actual DP Initial Code input used to produce final FlashCode | Conventional address-bearing container: 262,144 bytes (`0x40000`); consumed DP window `[0x3E000,0x40000)`. |
| `Normal_Ctrlram.bin` | FW 1.x: 10,240 bytes (`0x2800`); FW 2.0.0: 11,264 bytes (`0x2C00`). |
| `MP_Ctrlram.bin` | FW 1.x: 8,192 bytes (`0x2000`); FW 2.0.0: 9,216 bytes (`0x2400`). |
| `VN_Ctrlram.bin` | FW 1.x: 824 bytes (`0x338`); FW 2.0.0: 5,728 bytes (`0x1660`). |
| `NF_Ctrlram.bin` | FW 1.x Single: 3,328 bytes (`0xD00`); FW 1.x Cascade: 7,856 bytes (`0x1EB0`); FW 2.0.0: 11,776 bytes (`0x2E00`). |
| `DiffDLM.bin` | FW 2.0.0 Cascade only: 10,240 bytes (`0x2800`); absent from every other layout. |
| Normal and MP CtrlRAM_S | FW 1.x Cascade only: 10,240 bytes (`0x2800`) and 8,192 bytes (`0x2000`); identify the actual replacement sources rather than inventing filenames. |
| Vec Table | FW 1.x Cascade only: 400 bytes (`0x190`); preserved because no replacement source or processor write contract is declared. |
| `expected.bin` | Complete official final FlashCode: 262,144 bytes (`0x40000`). |
| Independent TP-only expected output, if supplied | 196,608 bytes (`0x30000`), explicitly identified as an official TP-only output. A final FlashCode prefix is not automatically TP-only Golden evidence. |
| `notes.txt` | Text; no fixed size. Slot mapping, exact physical sizes and SHA-256, Common FW, declared topology/count, official commands and tool versions, expected name and source provenance. |

The CtrlRAM sizes above are consumed prefixes, not required physical-file
lengths. Supply the original files, including their actual tails; never slice
replacements from final output or manufacture inputs to match these sizes.
The candidate declares truncation/preserved-reference-tail behavior. Any
different official range or physical-input contract must be reviewed in the
NT51925 profile owner before promotion.

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
- Whether NT51925 matches NT51923/NT51926 or differs, and every differing fact.
- Golden cases: Standard Merge at least one single; CtrlRAM Replace one for
  each FW version and each topology listed above.

The owner TP overview and postbuild flow now supply the candidate geometry.
1.x Single: postbuild only, no real BIN. 1.x Cascade: postbuild plus two real
BINs agree (owner-reported). 2.0.0 Single: postbuild only, no real BIN.
2.0.0 Cascade: postbuild plus three real BINs agree (owner-reported). No layout
has an independent expected output (Golden). Independent full-output
comparisons, exact write-range audit and firmware-owner R3 approval remain
required before support promotion or release. The TP end flag is `0x2FFFC`
for all layouts; no `.data` or Project ID region is declared.

A later family join requires the official NT51925 map proving identical facts
and separate owner approval. NT51923/NT51926 v1.2.1 identities and Saved Rules
remain unchanged by this intake.

Incoming BINs remain ignored by `*.bin`; only empty `inputs/.keep` placeholders
are prepared. Do not add, copy or certify firmware payloads in this preparation.
Keep personal names, accounts and private paths/URLs out of provenance.
