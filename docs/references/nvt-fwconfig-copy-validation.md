# NVT FWConfig Backup Validation

Status: owner-approved Standard Merge golden-output evidence for the canonical runtime FWConfig Backup locator. This changes neither postbuild write ranges nor TP Overview memory-map authority.

## Locator Contract

The common FWConfig Backup is located from the unique NVT end flag bytes:

```text
00 4E 56 54
         ^ terminal T byte
```

The Backup starts at `T - 0xFFF`. The compatibility reader currently decodes the minimum half-open slice `[T - 0xFFF, T - 0xFFF + 0x07C)`. The full Backup extent is evidence/profile-declared per IC or map and is never inferred as `0x07C`. The reader rejects a BIN with no marker, more than one complete marker, or an out-of-range required decode slice; it never guesses which Backup to display.

All runtime FWConfig content is read from this Backup. Hash-pinned `flash-map.json` still declares an IC primary FWConfig address only for TP Overview and golden cross-check evidence; it is never a runtime fallback or source. Golden regression verifies the primary and Backup agree for all exposed fields: FW/FW-bar/sub-version, ChipNumber, Common FW version, PID, and exposed hardware information.

## Current Golden Evidence

| IC | Golden output | NVT terminal `T` | FWConfig Backup start |
| --- | --- | ---: | ---: |
| NT51920 | `51920/flash.bin` | `0x2FFFF` | `0x2F000` |
| NT51923 | `51923/flash.bin` | `0x3BFFF` | `0x3B000` |
| NT51926 | `51926/flash.bin` | `0x3BFFF` | `0x3B000` |
| NT51927 | `51927/flash.bin` | `0x34FFF` | `0x34000` |
| NT51928 | `51928/flash.bin` | `0x34FFF` | `0x34000` |
| NT51929 | `51929/flash.bin` | `0x2EFFF` | `0x2E000` |
| NT51930 | `51930/flash.bin` | `0x329FF` | `0x31A00` |
| NT51931 | `51931/flash.bin` | `0x3BFFF` | `0x3B000` |
| NT51932 | `51932/flash.bin` | `0x33FFF` | `0x33000` |
| NT51950 | `51950/dp-256k/flash.bin` | `0x36FFF` | `0x36000` |
| NT51951 | `51951/dp-512k/flash.bin` | `0x36FFF` | `0x36000` |

All listed outputs contain exactly one valid NVT marker. In particular, the NT51926 1.4.1 golden Backup is at `0x3B000`; its terminal flag is `0x3BFFF`.

## Residual Evidence Gates

NT51917 and NT51919 are catalog aliases without direct committed firmware artifacts. The Backup locator still applies generically to their runtime images, but their physical marker placement remains an owner-evidence gate before a direct all-IC golden claim. Additional released NT51950/NT51951 capacity or mode variants likewise require direct artifacts before they can extend this table.

## Gate For New ICs

Do not infer this table for a new IC/mode. Add the owner-approved output, verify primary/Backup field equality, record the marker/Backup row here, and obtain firmware-owner review before using its metadata for a support claim.
