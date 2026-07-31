# NT51951 CtrlRAM Replace Handoff

The direct AUTO_PRJ-695 Common FW 2.0.0 single case remains canonical under
`testdata/golden/canonical/NT51951/ctrlram-replace/fw2.0.0/single/nt51951-fw200-single-auto-prj-695-20260718/`.

Issue #188 adds the direct AUTO_PRJ-599 exact-2-IC Cascade case under
`testdata/golden/canonical/NT51951/ctrlram-replace/fw2.0.0/cascade-2/nt51951-fw200-cascade2-auto-prj-599-20260731/`.
The complete `0x80000` owner expected output is reconstructed byte-for-byte by
the immutable Initial-Code plus TP-Firmware base recipe. Cascade writes only
the active `0x0910` Diff CtrlRAM prefix, preserves the following `0x0AF0` Diff
NF bytes and every inactive target byte, and copies the primary `0x0780`-byte
FWConfig from `0x22200` to the fixed Backup at `0x36000`.

Registered Combiner 1.13.0 differs from the owner expected output in exactly
16 bytes confined to the four previously approved 1.11-versus-1.13 CRC words.
The supplied inactive source record is all `0xFF`; that uniform-fill check is
golden-only and is not production admission or runtime validation. No
independent NF selector or DiffNFMerge execution is authorized. Independent
R3 review and explicit support promotion remain separate gates.
