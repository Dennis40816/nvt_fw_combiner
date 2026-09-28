# BUG-20260926-osd-tail-nvt-marker-blocks-base: an NVT marker inside Display OSD content blocks the Base

Status: fixed in 1.1.13 (merged into `1.1.x` by #458)
Severity: P2 (P1 in the pull request #449 automated review)
Found: 2026-09-26, automated review of pull request #449; confirmed by tests on
`feature/1.1.12/review-fixes`
Where: CtrlRAM Base classification (AB-TWO-NVT-1112-01) and the FWConfig Backup
validations of the CtrlRAM Display-OSD envelope (CTRLRAM-OSD-ENVELOPE-1112-01)
Observed: when Display OSD content contains a complete `00 4E 56 54` marker,
(a) a 512 KiB NT51950 Standard Base has one marker in each AB bank and is
classified as an invalid AB Base, so the session is rejected; (b) a nonstandard
envelope Base passes classification, but the FWConfig Backup checks, which require
exactly one marker in the complete image, fail the Build without writing an output.
Expected: markers outside the canonical FWConfig authority (the layout template,
or the canonical Backup position inside each AB bank) never count.
Evidence: tests `Nt51950StandardOsdBaseWithTailNvtMarkerFailsClosed` and
`Nt51950CascadeEnvelopeTailNvtMarkerFailsClosedAsync` pin both fail-closed outcomes.
Owner: 1.1.13 (needs an R3 design: AB evidence at the canonical Backup position and
FWConfig marker cardinality within the template).
Resolution: fixed by `NVT-END-FLAG-1113-01` (R3, decisions 30, 35 and 37), merged into `1.1.x` by #458:
only the marker at each bank's layout-declared end flag counts, so a Display OSD marker elsewhere no longer
blocks the Base or the envelope Build. The two pinning tests now assert the accepted outcomes
(`Nt51950StandardOsdBaseWithTailNvtMarkerIsStandard`, `Nt51950CascadeEnvelopeTailNvtMarkerIsIgnoredAsync`).
In 1.1.12 it was a known issue (owner decision 19). Owner direction for the
1.1.13 fix (decision 20, draft): take AB evidence from the NVT marker after each bank's FWConfig Backup
region, at least one each in the A and B code.
