# BUG-20260929-memory-tiny-slice-hit: Subpixel memory slices cannot be selected accurately by mouse

Status: resolved
Severity: P2
Found: 2026-09-29, Codex gpt-6-astra, decision 189 implementation,
at feature/1.1.15/memory-layout@99e3efd7e.
Where: ProportionalStackPanel.cs / MemoryCoverageBar.cs
Observed: F3 of the revised analysis; a three-byte slice rounds into adjacent target geometry.
Expected: Decision 189 O5 adds visible non-overlapping minimum targets or collision lists.
Evidence: revised analysis under `evidence/1.1.15/` and scoped source inspection.
Owner: Codex, feature/1.1.15/memory-layout.
Resolution: the shared bar adds visible 24-DIP targets under the unchanged
proportional rail. Overlapping targets open a scrollable list of original
typed slices; card ranges are unchanged. Actual pointer selection passes at
240/420 DIP, 100/150/200% render scaling, Light/Dark and English/Traditional
Chinese (24 cases). Keyboard/teardown also passes. Evidence:
`evidence/1.1.15/test-results/stage3-ui.trx`; screenshots under
`evidence/1.1.15/screens/after/`. Owner visual acceptance remains external;
see `../1.1.15/WS-MEMLAYOUT.md`.
