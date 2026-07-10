# NVT FWConfig Copy Validation

Status: owner-approved Standard Merge golden-output evidence used by the metadata reader. This is display/selection evidence, not a new postbuild or memory-map authority.

## Locator Contract

The common FWConfig copy is located from the unique NVT end flag bytes:

```text
00 4E 56 54
         ^ terminal T byte
```

The copy starts at inclusive address `T - 0xFFF`. The reader rejects a BIN with no valid marker or more than one valid marker; it never guesses which copy to display.

`TpFlashMapCatalog` still declares the IC primary FWConfig location. Golden regression verifies the primary FWConfig and the NVT copy agree for all display fields: FW/FW-bar/sub-version, ChipNumber, Common FW version, PID, and exposed hardware information.

## Current Golden Evidence

| IC | Golden output | NVT terminal `T` | FWConfig copy start |
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

All listed outputs contain exactly one valid NVT marker. In particular, the NT51926 1.4.1 golden copy is at `0x3B000`; its terminal flag is `0x3BFFF`.

## Gate For New ICs

Do not infer this table for a new IC/mode. Add the owner-approved output, verify primary/copy field equality, record the marker/copy row here, and obtain firmware-owner review before using its metadata for a support claim.
