# NT51925 Standard Merge Handoff

Status: awaiting owner evidence for the proposed NFC 1.2.2 restoration in
[ADR 0083](../../../../../docs/adr/0083-restore-nt51925-standard-merge-and-ctrlram-replace.md).
The prepared option A profile is `nt51925-standard-merge-gen-flash` version
`0.1.0`, using NT51926 shared geometry. It is a candidate, not approved support
or Golden-certified output. Declaration and inspection metadata are prepared;
Standard authoring, exact route resolution and Build admission remain closed
under the existing candidate execution policy.

## Cases and file placement

Supply at least one independent single-chip case. The prepared folders are
`fw1.4.1/single/` and `fw2.0.0/single/`; use the Common FW version actually used
by the official run and identify any additional versions in the provenance.
Standard Merge has no topology selector; single-chip is the minimum requested
evidence, not a topology inferred from a filename.

Place one same-run set in the selected folder, retaining original technical
filenames and recording the slot mapping in `notes.txt`:

| Slot / location | Prepared option A size | Purpose |
| --- | --- | --- |
| `inputs/<original DP filename>.bin` | 262,144 bytes (`0x40000`) for the conventional address-bearing Initial Code container | `dp-input`; copy its `[0x3E000,0x40000)` window. |
| `inputs/<original TP filename>.bin` | 245,760 bytes (`0x3C000`); an actual larger source may be supplied with its exact size recorded | `tp-input`; copy `[0x00000,0x3C000)`. |
| `expected.bin` (or original official final filename, identified in notes) | 262,144 bytes (`0x40000`) | Complete independent expected FlashCode. |
| `notes.txt` | Text; no fixed byte size | Exact filenames, sizes, SHA-256, Common FW, declared single-chip topology/count, official commands/tool versions, provenance and expected output filename. |

The compiler uses source-view coverage, not a Golden-file size/hash gate. Do
not pad, truncate or slice incoming files to fit this table. A shorter source
that cannot cover its required window is invalid. Report any different
official geometry for option B review.

The prepared output copies TP then DP, leaves the forbidden gap
`[0x3C000,0x3E000)` zero-filled, and uses
`NT51925_FlashCode_D{dp-version}T{tp-version}_{date}.bin`. Missing version
metadata follows the existing `xxxx` placeholder rule. The owner must confirm
the actual expected filename and full output, not accept an NFC-generated file
as its own oracle.

## Owner evidence

- The official NT51925 TP flash map or mmap.
- The postbuild `.bat` for each Common FW version, including 1.4.1 and 2.0.0.
- The FW versions and topologies in use, with explicit IC counts.
- Whether NT51925 matches NT51926 or differs, and every differing fact.
- Golden cases: Standard Merge at least one single; CtrlRAM Replace one per
  FW version and topology, collected in the corresponding CtrlRAM handoff.

The `925&926` header sheet establishes only limited normal-header evidence.
It is not complete flash-map or output-parity authority. Official evidence,
complete Golden comparisons, exact range review and the owner's R3 approval
are required before support promotion.

Follow [the owner intake contract](../../README.md). Incoming BINs remain
ignored by the repository's `*.bin` rule; `.keep` files are the only prepared
input contents. Do not add payloads or expected hashes during this preparation.
Remove personal names, accounts and private paths/URLs from provenance. Intake
and a passing candidate test do not grant support or release approval.
