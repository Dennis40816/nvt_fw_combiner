# BUG-20260930-single-ic-region-set-includes-cascade-only-region: Single IC image maps carry the multi-chip DIFF CtrlRAM region

Status: open (R3 profile change scheduled for 1.2.5, decision 213)
Severity: P3
Found: 2026-09-30, owner decision 197 while reviewing the NT51950 single IC Memory Layout.
Where: profiles/built-in/ctrlram-postbuild-v2/catalog.json and the NT51950/NT51951 image maps
Observed: Single IC and the 2 IC cascade share one region set, including `diff-ctrlram`
(`DiffDLM.bin`, `0x33200`). Single IC never binds it; the display layer hides it as of decision 197
(BUG-20260930-memory-single-ic-diff-lane).
Expected: Each topology declares only the regions it can bind. Splitting the region set is a profile
change (R3): firmware-owner review, byte/golden evidence and an exact write-range audit.
Evidence: MemoryLayoutProjector.cs classifies by Kind regardless of topology; policy
`nt51950-nt51951-preserve-active-diffnf` is exact-2-IC.
Owner: unassigned; `1.2.5` item R42 (decision 213).
Resolution: pending. Allocated to `1.2.5` as R42 in the [1.2.x allocation](../1.1.14/1.2.x-allocation.md).
