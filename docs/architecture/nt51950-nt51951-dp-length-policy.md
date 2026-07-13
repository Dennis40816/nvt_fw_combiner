# NT51950/NT51951 DP Length Policy

Source: `IC_FlashMap.xlsx`, sheet `51950 DP Perspective`.

The sheet shows NT51950/NT51951 DP layouts with multiple container sizes and both 950/951 perspectives:

- 1IC: 2M-bit, 4M-bit, or 8M-bit DP container variants depending on LDC/BK usage.
- 2IC: 4M-bit or 8M-bit DP container variants.
- TP FW is an overlay region in the DP perspective. The confirmed owner range is `0x0A000-0x36FFF (len 0x2D000)`. The following `0x37000-0x37FFF (len 0x1000)` range is customer info and remains part of the DP image because the exclusive TP end is `0x37000`.

The first implementation does not split the DP perspective into all named sub-regions. Standard Merge accepts only the three owner-confirmed DP input sizes: `0x40000`, `0x80000`, and `0x100000`; the Standard Merge output length follows the selected DP input length. Owner-supplied `merge_bin.7z` golden outputs on 2026-07-03 confirm `0x40000` and `0x80000` outputs are not padded to `0x100000`.

## Simplest Merge Rule

Use the DP input as the base image, then overlay TP.

1. Reject `dp.bin` unless `dp.Length` is exactly `0x40000`, `0x80000`, or `0x100000`.
2. Create a blank output image whose length equals `dp.Length`.
3. Copy the supplied DP bytes to offset `0`.
4. Require the TP input to contain the declared `0x0A000-0x36FFF (len 0x2D000)` source window.
5. Overlay the TP range from the TP input into the same output range.
6. The TP overlay range is profile data, not hard-coded workflow logic. For NT51950/NT51951 it is `0x0A000-0x36FFF (len 0x2D000)`.

This avoids tying merge correctness to every DP sub-block name in the spreadsheet while preserving the selected DP container length.

## Canonical V2 Map Selection

NT51950 and NT51951 Standard Merge are compiled from the hash-anchored
`nt51950-nt51951-standard-merge` V2 bundle. The family declares one exact
canonical map for each permitted DP capacity. Runtime derives the available
capacities from those maps and selects exactly the map whose capacity equals
the submitted DP BIN length; it does not keep a second 950/951 length table in
UI or CLI code. A missing DP BIN length leaves Standard Merge pending, and an
unlisted length is rejected with the stable Standard Merge DP-length issue.

General Merge is an authoring workflow, not a Standard Merge map selection.
Its default output capacity for these ICs is the largest declared V2 map
(`0x100000`) only until an author supplies an explicit General Merge mapping.
That default must not be interpreted as silently selecting a `0x100000`
Standard Merge map.

The same physical maps also bind `dp-replace`. Customer information remains an
`explicit-range` physical region so the full DP-container write is traceable,
but DP Replace does not create a separate base-restore view for it. Standard
Merge and DP Replace both retain customer-information bytes from their DP
source image; only the TP overlay range is copied from a different source.

## Simplest DP Replace Rule

Clone the base firmware as the Replace reference image, replace the DP container at the selected base length, then restore only the original TP range from the base firmware. Customer information follows the replacement DP image.

1. Reject the base firmware unless `base.Length` is exactly `0x40000`, `0x80000`, or `0x100000`. Repository policy keeps reference/base firmware exact-length to the selected container.
2. Reject the replacement DP when `replacement.Length > base.Length`.
3. Pad the replacement DP to the selected `base.Length` work length with the profile padding byte.
4. Replace the full output container from the padded replacement DP.
5. Copy the original base firmware TP range back into output.
6. Leave customer-information `0x37000-0x37FFF (len 0x1000)` from the replacement DP image, or from its declared `0x00` padding when the replacement ends before that range.

This implements DP Replace without requiring CRC recalculation and without enumerating every DP-owned segment. CtrlRAM Replace remains different: it must run the Combiner postbuild sequence after replacing TP/CtrlRAM content.

## Required Tests Before 1.0 Support Claim

- V2 Merge golden for the recorded NT51950 `0x40000` and NT51951 `0x80000` owner DP Perspective cases.
- Deterministic six-case public oracle hashes for NT51950/NT51951 and all declared capacities, plus customer-padding boundary cases and legacy/V2 plan parity.
- Standard Merge tests showing only `0x40000`, `0x80000`, and `0x100000` DP inputs are accepted and accepted outputs keep the selected DP input length.
- DP Replace tests showing approved base-length enforcement, shorter replacement padding to the selected base length, and larger replacement rejection.
- DP Replace test proving the TP range is restored byte-for-byte from base while customer information follows replacement DP.
- A map confirmation test that locks TP overlay to `0x0A000-0x36FFF (len 0x2D000)` and keeps customer info at `0x37000-0x37FFF (len 0x1000)` outside that overlay.
- Firmware-owner decision recorded on 2026-07-13: legacy workflow is the migration baseline and DP Replace customer information follows replacement DP. The public synthetic oracle is behavior regression evidence, not hardware validation; independent owner/reference output parity and firmware-owner promotion review remain required.
