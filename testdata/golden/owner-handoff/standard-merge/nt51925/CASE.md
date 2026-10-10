# NT51925 Standard Merge Handoff

Status: awaiting owner evidence for the proposed NFC 1.2.2 restoration in
[ADR 0084](../../../../../docs/adr/0084-restore-nt51925-standard-merge-and-ctrlram-replace.md).
The isolated profile `nt51925-standard-merge-gen-flash` version `0.1.0`
belongs to the NT51925-only `nt51925-standard-merge-candidate` bundle.
Its geometry follows the owner TP overview and postbuild flow; it supplies
no NT51926 shared authority or independent expected-output evidence. Policy is
`Unavailable`/`Candidate`/`ContractOnly`; authoring snapshot, exact
executable resolution, execution and Build remain blocked. It has no approved
support or Golden-certified output.

## Cases and file placement

Supply at least one independent single-chip case. The prepared folders are
`fw1.4.1/single/` and `fw2.0.0/single/`; use the Common FW version actually used
by the official run and identify any additional versions in the provenance.
Standard Merge has no topology selector; single-chip is the minimum requested
evidence, not a topology inferred from a filename.

Place one same-run set in the selected folder, retaining original technical
filenames and recording the slot mapping in `notes.txt`:

| Slot / location | Provisional declared size | Purpose |
| --- | --- | --- |
| `inputs/<original DP filename>.bin` | 262,144 bytes (`0x40000`) for the conventional address-bearing Initial Code container | `dp-input`; copy its `[0x3E000,0x40000)` window. |
| `inputs/<original TP filename>.bin` | 196,608 bytes (`0x30000`); an actual larger source may be supplied with its exact size recorded | `tp-input`; copy `[0x00000,0x30000)`. |
| `expected.bin` (or original official final filename, identified in notes) | 262,144 bytes (`0x40000`) | Complete independent expected FlashCode. |
| `notes.txt` | Text; no fixed byte size | Exact filenames, sizes, SHA-256, Common FW, declared single-chip topology/count, official commands/tool versions, provenance and expected output filename. |

The declared source-view coverage is not a Golden-file size/hash gate. Do not
pad, truncate or slice incoming files to fit this table. A shorter source
that cannot cover its required window is invalid. Report every official
NT51925 geometry difference for profile-owner review.

The provisional operation declaration copies TP then DP, leaves the forbidden gap
`[0x30000,0x3E000)` zero-filled, and uses
`NT51925_FlashCode_D{dp-version}T{tp-version}_{date}.bin`. Missing version
metadata follows the existing `xxxx` placeholder rule. The owner must confirm
the actual expected filename and full output, not accept an NFC-generated file
as its own oracle.

## Owner evidence

- The official NT51925 TP flash map or mmap.
- The postbuild `.bat` for each Common FW version, including 1.4.1 and 2.0.0.
- The FW versions and topologies in use, with explicit IC counts.
- Whether NT51925 matches NT51923/NT51926 or differs, and every differing fact.
- Golden cases: Standard Merge at least one single; CtrlRAM Replace one per
  FW version and topology, collected in the corresponding CtrlRAM handoff.

The owner TP overview and postbuild flow declare the 196,608-byte TP prefix
and the end flag at `0x2FFFC`. The 53,248-byte gap (`0x30000` to `0x3CFFF`)
stays unmapped and zero-filled in this Merge candidate. The production-test
customer-data window (4,096 bytes at `0x3D000`) is declared as a forbidden
customer region, as in the CtrlRAM Replace full-flash map, and also stays
zero-filled. The TP header metadata and TP SVN at
`0x24` are unchanged. No `.data` or Project ID region is declared.

The five bytes `[0x2EA80, 0x2EA8B)` of the FW 2.0.0 output come from an owner
insertion script that is not yet available. They are temporarily excluded from
the Merge candidate claim until the owner provides that script.

1.x Single: postbuild only, no real BIN. 1.x Cascade: postbuild plus two real
BINs agree (owner-reported). 2.0.0 Single: postbuild only, no real BIN.
2.0.0 Cascade: postbuild plus three real BINs agree (owner-reported). No layout
has an independent expected output (Golden). Complete Golden comparisons,
exact range review and the owner's R3 approval remain required before support
promotion.

A later family join requires the official NT51925 map proving identical facts
and separate owner approval. NT51923/NT51926 v1.2.1 identities and Saved Rules
remain unchanged by this intake.

Follow [the owner intake contract](../../README.md). Incoming BINs remain
ignored by the repository's `*.bin` rule; `.keep` files are the only prepared
input contents. Do not add payloads or expected hashes during this preparation.
Remove personal names, accounts and private paths/URLs from provenance. Intake
and a passing candidate test do not grant support or release approval.
