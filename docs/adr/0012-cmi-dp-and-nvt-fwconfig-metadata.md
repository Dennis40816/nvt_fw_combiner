# ADR 0012: CMI DP Metadata and NVT Backup FWConfig Selection

- Status: Partially superseded by ADR 0015 only for the retired C# catalog
  ownership; canonical DPCMI/FWConfig meanings and firmware-owner evidence
  gates remain accepted
- Date: 2026-07-10
- Last amended: 2026-08-01 for canonical DPCMI convergence and output-name correction
- Owners: Product owner + firmware owner + architecture owner
- Partially superseded by: ADR 0015 after #194 parity-adapter retirement

## Context

Each supported CMI DP payload encodes three adjacent bytes: Reg16h stores Jira bits `[7:0]`, Reg17h is DP major, and Reg18h stores DP minor in bits `[7:4]` plus Jira bits `[11:8]` in bits `[3:0]`. These are metadata facts; they are not Standard Merge operations.

FWConfig is copied to the common driver-readable Backup location ending at the NVT End Flag. The Backup begins at the terminal `T` byte of `00 4E 56 54` minus `0xFFF`. FWConfig `ChipNumber` is at Backup offset `0x017`.

## Decision

The canonical `firmware-family-v1` DPCMI metadata structure and the exact
`composition-profile-v2` metadata binding own CMI locations and evidence.
`GenFlashVersionCatalog` was a parity adapter and is retired by #194. Current
owner-confirmed rules are:

| IC | CMI Reg16h offset | Selection |
| --- | ---: | --- |
| NT51923, NT51926 | `0x3E014` | fixed |
| NT51927 | `0x3C01C` | fixed |
| NT51929, NT51932 | `0x401A` | fixed |
| NT51950 | `0x3B016` / `0x05016` | 1IC / cascade; select from TP NVT Backup `ChipNumber` |
| NT51951 | `0x05016` | fixed for all IC counts |

For a rule with a cascade location, missing or zero ChipNumber makes CMI metadata unavailable. The implementation never guesses the 1IC location.

The DPCMI decoder treats Reg17h as the major byte and only Reg18h bits `[7:4]`
as the minor value. The low nibble remains Jira bits `[11:8]` and cannot enter
the version. Output naming renders the decoded version as two hexadecimal
bytes, `{major:X2}{minor:X2}`. This deliberately corrects the retired adapter's
raw-adjacent-byte projection: examples such as `8001`, `8202`, and `0102`
become `8000`, `8200`, and `0100` when the decoded minor nibble is zero. The
underlying DP/TP/output firmware bytes are unchanged.

FWConfig display, TP-driven CMI selection, postbuild category selection, and output naming read the NVT Backup exclusively. A flash-map primary FWConfig address is retained only for TP Overview and golden cross-check evidence. It is never a runtime source or prerequisite, and a primary/Backup mismatch cannot substitute or suppress the canonical Backup facts.

## Consequences

- CMI creates only display/report facts such as `AUTO_PRJ-xxx`; Jira zero creates no badge.
- No merge/replace range, operation order, checksum, processor authority, or
  firmware byte is changed. Output filenames intentionally use the corrected
  decoded DPCMI token described above.
- NT51950 2IC selection has unit coverage but still requires a real TP+DP 2IC golden before promotion of its payload-size expectation or workflow support.

## Verification

- NT51923 and NT51932 Standard Merge golden outputs cross-check their CMI major byte against the legacy DP major rule.
- NT51950 1IC golden reads ChipNumber from TP NVT Backup and resolves `0x3B016`; unit coverage verifies cascade resolution to `0x05016` without a missing-chip fallback.
- All current direct Standard Merge golden outputs compare every exposed FWConfig field between the primary address and the NVT Backup; this is evidence, not a runtime gate.
- Output-name vectors pin the decoded high-nibble minor projection and the
  intentional `8001`/`8202`/`0102` to `8000`/`8200`/`0100` correction.
