# ADR 0012: CMI DP Metadata and NVT Backup FWConfig Selection

- Status: Accepted with firmware-owner review gate
- Date: 2026-07-10
- Owners: Product owner + firmware owner + architecture owner

## Context

Each supported CMI DP payload encodes three adjacent bytes: Reg16h stores Jira bits `[7:0]`, Reg17h is DP major, and Reg18h stores DP minor in bits `[7:4]` plus Jira bits `[11:8]` in bits `[3:0]`. These are metadata facts; they are not Standard Merge operations.

FWConfig is copied to the common driver-readable Backup location ending at the NVT End Flag. The Backup begins at the terminal `T` byte of `00 4E 56 54` minus `0xFFF`. FWConfig `ChipNumber` is at Backup offset `0x017`.

## Decision

`GenFlashVersionCatalog` owns CMI locations and evidence. Current owner-confirmed rules are:

| IC | CMI Reg16h offset | Selection |
| --- | ---: | --- |
| NT51923, NT51926 | `0x3E014` | fixed |
| NT51927 | `0x3C01C` | fixed |
| NT51929, NT51932 | `0x401A` | fixed |
| NT51950 | `0x3B016` / `0x05016` | 1IC / cascade; select from TP NVT Backup `ChipNumber` |
| NT51951 | `0x05016` | fixed for all IC counts |

For a rule with a cascade location, missing or zero ChipNumber makes CMI metadata unavailable. The implementation never guesses the 1IC location.

FWConfig display, TP-driven CMI selection, postbuild category selection, and output naming read the NVT Backup exclusively. A flash-map primary FWConfig address is retained only for TP Overview and golden cross-check evidence. It is never a runtime source or prerequisite, and a primary/Backup mismatch cannot substitute or suppress the canonical Backup facts.

## Consequences

- CMI creates only display/report facts such as `AUTO_PRJ-xxx`; Jira zero creates no badge.
- No merge/replace range, operation order, checksum, processor, or output filename is changed.
- NT51950 2IC selection has unit coverage but still requires a real TP+DP 2IC golden before promotion of its payload-size expectation or workflow support.

## Verification

- NT51923 and NT51932 Standard Merge golden outputs cross-check their CMI major byte against the legacy DP major rule.
- NT51950 1IC golden reads ChipNumber from TP NVT Backup and resolves `0x3B016`; unit coverage verifies cascade resolution to `0x05016` without a missing-chip fallback.
- All current direct Standard Merge golden outputs compare every exposed FWConfig field between the primary address and the NVT Backup; this is evidence, not a runtime gate.
