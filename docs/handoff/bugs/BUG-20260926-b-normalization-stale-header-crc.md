# BUG-20260926-b-normalization-stale-header-crc: B-bank normalization keeps the old Header CRC

Status: suspected (reproduced in memory by the read-only investigation; not yet pinned by a test)
Severity: P2 pending firmware-owner review (R3 firmware semantics)
Found: 2026-09-26, Codex `gpt-6-astra` read-only Header backup CRC investigation (1.1.13 wave 2), on
`feature/1.1.13/wave2`
Where: B-bank address normalization before the CtrlRAM postbuild processors
(`src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.RuntimeReferenceReplace.BankOperations.cs`)
Observed: normalization restores the bank-local addresses at Header offsets `0xA100`, `0xA110` and `0xA120`
but keeps the Header CRC computed for the relocated values. The first postbuild pass then copies that stale
CRC into `FLASHMAP_HEADER_COPY` and into the DLM CRC coverage, so the A and B results diverge for the same TP.
Refreshing the Header CRC after normalization makes the B-local TP result equal the A result, but still not
equal the owner's expected output (see the investigation summary on the 1.1.13 board).
Expected: to be decided by the firmware owner together with the Header copy contract (whether the CRC is
refreshed after normalization and before the copy).
Owner: 1.1.13 Header backup CRC investigation (R3, firmware-owner review).
Resolution: not fixed.
