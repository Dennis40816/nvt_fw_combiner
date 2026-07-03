# NT51950/NT51951 DP Length Policy

Source: `IC_FlashMap.xlsx`, sheet `51950 DP Perspective`.

The sheet shows NT51950/NT51951 DP layouts with multiple container sizes and both 950/951 perspectives:

- 1IC: 2M-bit, 4M-bit, or 8M-bit DP container variants depending on LDC/BK usage.
- 2IC: 4M-bit or 8M-bit DP container variants.
- TP FW is an overlay region in the DP perspective. The confirmed owner range is `0x0A000-0x36FFF (len 0x2D000)`. The following `0x37000-0x37FFF (len 0x1000)` range is customer info and must be preserved rather than overwritten by TP overlay. This matches the reference code convention where the exclusive TP end is `0x37000`.

The first implementation does not split the DP perspective into all named sub-regions. It treats `0x100000` as the maximum DP container length and uses it as the canonical 950/951 working length. Standard Merge accepts only the three owner-confirmed DP input sizes: `0x40000`, `0x80000`, and `0x100000`; accepted shorter DP inputs are padded to the work length before TP overlay.

## Simplest Merge Rule

Use the DP input as the base image, then overlay TP.

1. Reject `dp.bin` unless `dp.Length` is exactly `0x40000`, `0x80000`, or `0x100000`.
2. Create a transient `0x100000` work image filled with the profile padding byte.
3. Copy the supplied DP bytes to offset `0`.
4. Require the TP input to contain the declared `0x0A000-0x36FFF (len 0x2D000)` source window.
5. Overlay the TP range from the TP input into the same output range.
6. The TP overlay range is profile data, not hard-coded workflow logic. For NT51950/NT51951 it is `0x0A000-0x36FFF (len 0x2D000)`.

This avoids tying merge correctness to every DP sub-block name in the spreadsheet while still accepting shorter 950/951 DP variants.

## Simplest DP Replace Rule

Clone the base firmware as the Replace reference image, replace the DP container, then restore the original TP range from the base firmware.

1. Reject the base firmware unless `base.Length == 0x100000`. Repository policy keeps reference/base firmware exact-length.
2. Reject the replacement DP when `replacement.Length > 0x100000`.
3. Pad the replacement DP to the `0x100000` work length with the profile padding byte.
4. Replace the full output container from the padded replacement DP.
5. Copy the original base firmware TP range back into output.

This implements DP Replace without requiring CRC recalculation and without enumerating every DP-owned segment. CtrlRAM Replace remains different: it must run the Combiner postbuild sequence after replacing TP/CtrlRAM content.

## Required Tests Before 1.0 Support Claim

- Merge golden for at least one 950 and one 951 max-container case.
- Standard Merge tests showing only `0x40000`, `0x80000`, and `0x100000` DP inputs are accepted and accepted shorter DP inputs are padded to `0x100000`.
- DP Replace tests showing exact base-length enforcement, shorter replacement padding to `0x100000`, and larger replacement rejection.
- DP Replace test proving the TP range is preserved byte-for-byte after profile/model wiring.
- A map confirmation test that locks TP overlay to `0x0A000-0x36FFF (len 0x2D000)` and preserves customer info at `0x37000-0x37FFF (len 0x1000)`.
