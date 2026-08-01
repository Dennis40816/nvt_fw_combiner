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

## Dynamic DiffDLM Runtime Postcondition

For the NT51919/NT51929/NT51932 family only, a Cascade DiffDLM run has a
count-derived expected Backup start:

```text
activeRecordCount = IC Count - 1
activeRecordEnd = 0x2D100 + activeRecordCount * 0x1400
expectedBackupStart = AlignUp(activeRecordEnd, 0x1000)
```

The Replace scatter plan does not copy FWConfig and does not choose the actual
Backup address. The declared postbuild processor places it. After an attempted
Preview/Build, runtime locates the actual Backup through the unique NVT marker
and compares it with the expected address. An actual address that differs but
remains within the profile-declared bounded Backup-placement write authority is
reported as a typed warning. Missing or ambiguous markers, out-of-bounds
placement, or mutation outside processor authority still fail closed.

The declared bounded range is a placement-candidate range, not blanket write
permission. After the processor returns, the host derives both the original
Reference Backup envelope and the unique actual output Backup envelope. Those
two envelopes are the only candidate bytes allowed to differ; this permits
clearing the old marker and writing the relocated Backup while rejecting any
unrelated inactive DLM, Diff NF, or padding mutation.

The maximum seven-record Dynamic DiffDLM layout envelope is
`[0x2D100,0x35D00)`. Only the first `IC Count - 1` records are active for
Replace; the remainder is inactive capacity that postbuild may reuse for the
FWConfig Backup. Consequently, 4 IC places the expected Backup at
`[0x31000,0x32000)`, inside the maximum record envelope.
`[0x35D00,0x37000)` is the static upper authority extension required for the
8-IC expected Backup `[0x36000,0x37000)`, not a fixed Backup allocation.
Replace leaves inactive bytes unchanged from Reference before postbuild.

Dynamic DiffDLM input requires every active `0x1400` AE record in full. For
4 IC this is exactly three records, or `0x3C00` bytes. A source ending at the
last writable DLM byte (`0x3390`) is still truncated because it omits the
preserved NF tail of that active record. Additional AE records are inactive
dummy content and are ignored.

If the canonical Backup reports `Chip_Num = 0`, Dynamic DiffDLM cannot resolve
active records or expected Backup placement and blocks with
`firmware-config.chip-count-required`; it must never silently select 2 IC.
Routes that do not consume IC Count emit `firmware-config.chip-count-zero` as a
warning and may continue.

The direct NT51932 Cascade-3 fixture confirms this relationship:
`0x2D100 + 2 * 0x1400 = 0x2F900`, which aligns to Backup start `0x30000`;
its unique NVT marker ends at `0x30FFF`. The golden table below records each
artifact's observed location and is not a count-independent placement table.
The owner-provided NT51932 4-IC golden must additionally confirm three active
records, active end `0x30D00`, aligned Backup start `0x31000`, preserved active
NF tails, and unchanged inactive records.

NT51950/NT51951 are a separate fixed-layout contract. Their flash map declares
the End Flag at `0x36FFC`, so its terminal `T` fixes the Backup start at
`0x36000`. Postbuild copies the primary FWConfig at `0x22200` to that fixed
destination. A different location is a fixed-map/postbuild failure, not the
Dynamic DiffDLM in-authority warning case.

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
| NT51951 | `51951/dp-512k/flash.bin`; direct #188 AUTO_PRJ-599 Cascade expected | `0x36FFF` | `0x36000` |

All listed outputs contain exactly one valid NVT marker. In particular, the NT51926 1.4.1 golden Backup is at `0x3B000`; its terminal flag is `0x3BFFF`. The direct NT51951 #188 Cascade output independently proves that the primary and fixed Backup match for the declared `0x0780`-byte copy envelope.

## Residual Evidence Gates

NT51917 and NT51919 are catalog aliases without direct committed firmware artifacts. The Backup locator still applies generically to their runtime images, but their physical marker placement remains an owner-evidence gate before a direct all-IC golden claim. Additional released NT51950/NT51951 capacity or mode variants likewise require direct artifacts before they can extend this table.

## Gate For New ICs

Do not infer this table for a new IC/mode. Add the owner-approved output, verify primary/Backup field equality, record the marker/Backup row here, and obtain firmware-owner review before using its metadata for a support claim.
