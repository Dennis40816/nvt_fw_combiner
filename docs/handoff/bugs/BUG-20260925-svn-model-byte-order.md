# BUG-20260925-svn-model-byte-order: TP SVN field is modeled as a little-endian integer

Status: open
Severity: P2
Found: 2026-09-25, Claude Code (Opus 5.5), during the owner SVN interview, at `1.1.12`@`d69b6e54a`
Where: `profiles/built-in/nt51923-standard-merge/families/nt51923-nt51926.json:100,196`; `profiles/built-in/nt51927-standard-merge/families/nt51927-nt51928.json:262` (and the matching v1.5 family)
Observed: field `svn-auto-build-version` at offset 36 (`0x24`) is `unsigned-integer`, `byteOrder: little`, so a value such as `C0 20 45 78` decodes to 2,017,796,288.
Expected: 4 bytes big-endian at TP start + `0x24`; byte 0 is a build-origin flag (`0xC0` = local build), bytes 1-3 are six BCD revision digits (`C0 20 45 78` = local build, rev 204578), per the owner facts recorded under board item P0-3.
Evidence: diff of the owner's `nt51950_fw_T02.bin` and `nt51950_fw_expected_diff_svn_revision.bin` (only `0xA026-0xA027` change in the field); bytes 1-3 are valid BCD in 20 of 20 unique Golden TP inputs.
Owner: Codex, WS-HDR (board C-7), `feature/1.1.12/header-integrity`
Resolution:
