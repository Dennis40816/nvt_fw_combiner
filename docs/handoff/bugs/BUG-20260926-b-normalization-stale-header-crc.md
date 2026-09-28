# BUG-20260926-b-normalization-stale-header-crc: B-bank normalization keeps the old Header CRC

Status: root cause found (2026-09-28); change deferred to a 1.2.x decision (board decision 161)
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
Allocation (2026-09-28): Root cause (commander, 2026-09-28; reproduced byte for byte in memory from the private intake, confirmed by an independent Codex analysis): the NT51950 postbuild runs `InsertSID.py`, then `Combiner.exe NT51950BASED_NORMAL_MODE` twice. Each run first copies the Header from the file on disk into `FLASHMAP_HEADER_COPY` (`%HeaderCMD%`), then computes the ILM, DLM and Header CRCs; the copy lies inside the DLM coverage. A fresh AndeSight build carries zero ILM (`0xA10C`), DLM (`0xA11C`) and Header (`0xA130`) CRC fields, so the first run's DLM CRC covers a zero-CRC copy. NFC replays the two runs on an already post-built TP whose CRC fields are filled, so its first run covers a different copy and the four CRC words per bank differ (32 bytes for A and B). Resetting those 12 bytes to zero before the first run reproduces the owner's three TP samples and the owner's CRC words exactly. The historical single-IC CtrlRAM 16-byte allowance (four CRC words) matches the same pattern. The postbuild is therefore not idempotent: its output depends on the Header CRC fields it starts from. Resetting the fields before the first run also removes this stale-seed effect; any change is decided in 1.2.x (board decision 161).
Resolution: not fixed.
