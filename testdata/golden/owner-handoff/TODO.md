# Owner Data TODO

The complete versioned path list and per-case contract are in
[`v0.9.9/README.md`](v0.9.9/README.md). This file is the short priority view.

## Already covered

- Standard Merge golden exists for NT51920, NT51923, NT51926, NT51927,
  NT51928, NT51929, NT51931, and NT51932.
- Owner-approved Standard Merge evidence also covers NT51930, NT51950 at DP
  `0x40000`, and NT51951 at DP `0x80000`.
- Owner-approved Standard Merge aliases are NT51917 -> NT51927 and
  NT51919 -> NT51929. Direct audit files for those two aliases are optional.
- NT51950 AB has two direct owner-approved cases and Python-vs-Legacy Combiner
  1.13 full-byte parity. It still needs firmware-owner runtime-promotion review
  before support exposure.
- NT51926 CtrlRAM 1.4.1 cascade has a direct base and five sliced inputs. The
  independent owner-approved final output and authority review remain open.

## P0: blocks v1 authority retirement

- [ ] NT51926 1.4.1 cascade: independent `expected.bin`, non-personal
  provenance, and owner review. Combiner 1.13.0, its hash, and exact BAT command
  are already tracked; the owner confirmed Header CRC/Header Copy CRC as the
  allowed post-Combiner diff scope on 2026-07-16.
- [ ] Remaining selected CtrlRAM IC/version/count cases: direct expected output
  or exact fact-scoped alias, allowed diff, and owner review.
- [ ] NT51931: owner decision on correct Combiner tool/mode before any parity
  claim.
- [ ] CtrlRAM base-artifact contract and TP firmware-version edit authority.
- [ ] General Replace protected ranges, mapping envelope, overlap/alignment,
  TP-postbuild trigger, and selected release scope.

## P1: blocks selected candidate promotion

- [ ] NT51919 AB: firmware-owner approval of the existing manifest-declared
  NT51929 AB-specific fact alias; a direct golden is optional if approved.
- [ ] NT51929 AB: firmware-owner review of the existing direct golden/parity.
- [ ] NT51932 AB: direct golden or owner-approved AB-specific fact alias, plus
  firmware-owner review.
- [ ] NT51950 AB: firmware-owner runtime-promotion review only; do not
  regenerate already accepted evidence without a new case.
- [ ] NT51951 AB: direct product golden, exact Combiner trace, and owner review.
- [ ] Selected General Merge rows: legacy/current byte-and-report parity plus
  an explicit support decision. Unselected rows remain candidates.

## P2: optional audits

- [ ] Additional Standard Merge capacities for NT51950/NT51951 only when
  selected for release.
- [ ] Direct DP Replace product/hardware goldens for the six NT51950/NT51951
  capacities when desired for audit.
- [ ] Direct Standard Merge audit samples for NT51917/NT51919 aliases.
