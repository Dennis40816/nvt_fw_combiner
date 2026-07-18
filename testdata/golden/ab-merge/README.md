# AB Merge Golden Fixtures

Owner-approved AB fixtures from the `ab_code_golden_20260714-125634.7z`
HackMD CJK14 transfer. Original file names are retained in the fixture tree
and each role, size, SHA-256, and source archive identity is recorded in
`manifest.json`.

These fixtures directly prove only the named NT51929 and NT51950 products. They
do not expose AB Merge through UI or CLI and do not promote any IC to
supported. On 2026-07-18 the owner approved the NT51929 fixture as fact-scoped
family evidence for NT51919 and NT51932; both alias candidates reproduce the
complete NT51929 golden bytes, while NT51932 also retains its independent named-
configuration synthetic comparison. This records reusable family facts, not a
direct NT51919/NT51932 product golden or runtime promotion.

The NT51950 BOE and Hiway fixtures prove full-byte parity between the uploaded
Python reference and `Combiner.exe` 1.13.0 using
`NT51950BASED_MERGE_AB_MODE CRC8 A.bin B.bin output.bin 0x40000`. The profile
relocates only TPB DIFF before the tool; the tool writes TPB ILM, DLM, and
header CRC in its private staging output. The Bootstrap regression invokes the
immutable Python snapshot and the V2 + Combiner path from identical inputs,
then compares complete output bytes. A separate synthetic NT51951 regression
uses the same experiment with the `0x80000` placement; it is topology evidence
only, not a direct owner golden. On 2026-07-18 the owner approved NT51950 as the
fact-scoped NT51951 workflow-logic evidence, so NT51951 does not require a
duplicate AB product golden. That alias covers the full-DP initializer, command
family, no-`map.txt` rule, and Combiner-owned header CRC; the NT51951 synthetic
test still locks its distinct `0x80000` placement. None of these tests expose AB
Merge through UI or CLI, and firmware-owner review remains required before
runtime exposure.
