# AB Merge Golden Fixtures

Owner-approved AB fixtures from the `ab_code_golden_20260714-125634.7z`
HackMD CJK14 transfer. Original file names are retained in the fixture tree
and each role, size, SHA-256, and source archive identity is recorded in
`manifest.json`.

These fixtures prove only the cases named in the manifest. They do not expose
AB Merge through UI or CLI, do not promote any IC to supported, and do not
establish NT51951 behavior. The NT51950 BOE and Hiway fixtures prove full-byte
parity between the uploaded Python reference and `Combiner.exe` 1.13.0 using
`NT51950BASED_MERGE_AB_MODE CRC8 A.bin B.bin output.bin 0x40000`. The profile
relocates only TPB DIFF before the tool; the tool writes TPB ILM, DLM, and
header CRC in its private staging output. This does not expose AB Merge through
UI or CLI, and firmware-owner review remains required before runtime exposure.
