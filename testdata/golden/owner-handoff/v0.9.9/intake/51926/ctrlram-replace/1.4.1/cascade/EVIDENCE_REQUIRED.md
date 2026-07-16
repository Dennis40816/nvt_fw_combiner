# NT51926 CtrlRAM Replace 1.4.1 Cascade

## Already provided and verified in the repository

- `base.bin` equivalent: tracked 262,144-byte owner fixture.
- Normal/DIFF/MP/VN/NF replacement inputs with manifested sizes and SHA-256.
- `external-tools/legacy-combiner/1.13.0/Combiner.exe`, size 41,472 bytes.
- tool SHA-256:
  `ed6b58289cc780f73d36b831f5424cef44ad93187ba7518d36df6a77ad0c76bf`.
- exact `PostbuildSetup_51926_1.4.1.bat` cascade commands: the first
  `CRC_Enable` command stages Normal/DIFF/MP/VN/NF, FWConfig backup, and header
  copy; the second refreshes the copied header CRC.
- PR #138 regression `24c01c2e` runs both the Workbench route and V2 candidate
  through the actual Combiner 1.13 runner and requires identical full output
  bytes and SHA-256.

Do not upload another Combiner executable or command trace unless it differs
from the pinned tool/BAT above.

## Still required

- `expected.bin`: independently owner-approved 262,144-byte final result for
  the existing base and five replacement inputs.
- non-personal provenance label and approval role/date for that expected output.
- owner confirmation that the statement below is correctly scoped to the
  post-Combiner byte diff, not a broader tool write-authority grant.

## Owner fact recorded 2026-07-16

The allowed post-Combiner differences are the ranges marked Header CRC and
Header Copy CRC. The current candidate translates those labels to these
half-open validation ranges:

```text
[0x0001C, 0x00020)  Header CRC
[0x0003C, 0x00040)  Header CRC
[0x000FC, 0x00100)  Header CRC
[0x32F50, 0x33050)  Header Copy / copied-header CRC block
```

CtrlRAM bytes intentionally changed by the supplied replacement inputs are
compared as declared replacement operations, not reclassified as postbuild CRC
drift. If the independently supplied `expected.bin` changes any other byte, the
case fails closed; the allowed range must not be widened merely to match it.

The BAT also performs a FWConfig backup copy to `[0x3B000,0x3B800)`. For the
current self-replacement case that copy must remain byte-identical if the owner
allowed-diff statement above is complete. It remains declared tool write
authority because the actual command performs the copy, but it is not an
allowed unexplained byte difference for this golden.
