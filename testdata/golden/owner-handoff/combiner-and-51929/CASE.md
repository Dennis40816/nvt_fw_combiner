# NT51929 Combiner And CMI Handoff

This handoff is private evidence only. Its payloads remain under the ignored `intake/` directory and do not promote Standard Merge or AB Merge support.

## Provenance

- Source: owner-provided CJK14 transfer retrieved on 2026-07-10.
- Outer archive: `929_golden_combiner_source_code.7z`, SHA-256 `006324782aa52487ffba8dfc9a0a38145b21f51a99965972f0b3d5fa92095309`.
- Nested 51929 archive: SHA-256 `2e6751a10a79cd9f3106b83dc4cc9bc29b122a1da9f136adaf066942b8818209`.
- Nested Combiner 1.13 source archive: SHA-256 `0c86ad1d292db279c613b0f23a2e4cef8c2422950c56d1c22fdd1130660b47b8`.

## Observed CMI Evidence

- Initial DP image: `NT51929_initial code_TM149_2344x880_D01_20260114.bin`, 256 KiB, SHA-256 `91ce8204d7dc6103a015eba59f9ddb41ef5d1a64c101aa62a4fe7c4517f5cebf`.
- CMI registers: `0x401A..0x401C = 52 01 02`.
- Decoded values: Jira `594` (`AUTO_PRJ-594`), DP major `01`, DP minor `0`.
- Legacy gen_flash DP version rule reads major `01` at `0x67`; it matches the CMI major. The legacy minor remains a separate byte-format concern and is not conflated with the CMI minor nibble.

## Gate

The accompanying FlashCode/TP firmware and Combiner source are archive-only investigation evidence. Promoting any payload to a tracked golden, enabling AB Merge, or changing merge ranges still requires a reviewed manifest, byte-level parity evidence, and firmware-owner approval.
