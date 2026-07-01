# NT51950/NT51951 DP Length Policy

Source: `IC_FlashMap.xlsx`, sheet `51950 DP Perspective`.

The sheet shows NT51950/NT51951 DP layouts with multiple container sizes and both 950/951 perspectives:

- 1IC: 2M-bit, 4M-bit, or 8M-bit DP container variants depending on LDC/BK usage.
- 2IC: 4M-bit or 8M-bit DP container variants.
- TP FW is an overlay region in the DP perspective. The confirmed owner range is `0xA000..0x36FFF`. The following `0x37000..0x37FFF` range is customer info and must be preserved rather than overwritten by TP overlay. This matches the reference code convention where the exclusive TP end is `0x37000`.

The first implementation does not split the DP perspective into all named sub-regions. It treats `0x100000` as the maximum DP container length and uses it as the canonical 950/951 working length. Real DP inputs can be shorter, such as 256 KB or 512 KB variants; they are padded to the work length before TP overlay.

## Simplest Merge Rule

Use the DP input as the base image, then overlay TP.

1. Reject `dp.bin` when `dp.Length > 0x100000`.
2. Create a transient `0x100000` work image filled with the profile padding byte.
3. Copy the supplied DP bytes to offset `0`.
4. Overlay the TP range from the TP input into the same output range.
5. The TP overlay range is profile data, not hard-coded workflow logic. For NT51950/NT51951 it is `0xA000..0x36FFF` inclusive.

This avoids tying merge correctness to every DP sub-block name in the spreadsheet while still accepting shorter 950/951 DP variants.

## Simplest DP Replace Rule

Use the replacement DP as the new base image, then restore the original TP range from the base firmware.

1. Reject the base firmware when `base.Length > 0x100000`.
2. Reject the replacement DP when `replacement.Length > 0x100000`.
3. Create a transient `0x100000` work image filled with the profile padding byte.
4. Copy the replacement DP bytes to offset `0`.
5. Copy the original base firmware TP range back into output. If the base is shorter than `0x100000`, missing bytes outside the supplied base remain padding.

This implements DP Replace without requiring CRC recalculation and without enumerating every DP-owned segment. CtrlRAM Replace remains different: it must run the Combiner postbuild sequence after replacing TP/CtrlRAM content.

## Required Tests Before 1.0 Support Claim

- Merge golden for at least one 950 and one 951 max-container case.
- Standard Merge tests showing shorter DP inputs are padded to `0x100000` and larger DP inputs are rejected.
- DP Replace tests showing shorter replacement padding to `0x100000` and larger replacement rejection after the replace model supports this initialization shape.
- DP Replace test proving the TP range is preserved byte-for-byte after profile/model wiring.
- A map confirmation test that locks TP overlay to `0xA000..0x36FFF` and preserves customer info at `0x37000..0x37FFF`.
