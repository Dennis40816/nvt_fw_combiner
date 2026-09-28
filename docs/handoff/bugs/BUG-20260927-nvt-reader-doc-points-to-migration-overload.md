# BUG-20260927-nvt-reader-doc-points-to-migration-overload: runtime reader docs still point to the migration-only overload

Status: fixed (merged into `1.1.x` by #459)
Severity: P2
Found: 2026-09-27, automated review of pull request #458 (thread on `FirmwareConfigMetadataReader.cs`)
Where: `src/NvtFwCombiner.Application/FlashMaps/FirmwareConfigMetadataReader.cs` (XML documentation near line 16) and
`src/NvtFwCombiner.Application/FlashMaps/FirmwareConfigVersionWritePlan.cs:67`
Observed: `NVT-END-FLAG-1113-01` made the overload without a `FirmwareNvtEndFlagResolution` migration-only, but the
public XML documentation still tells runtime consumers to call it. A new consumer following IntelliSense would rescan
the whole image for markers and skip the declared-position and `Unresolved` handling the change requires.
Expected: the documentation points runtime consumers to the declaration overload and marks the other as migration-only.
Evidence: code reading by the automated review; documentation only, no behavior defect in current consumers.
Owner: 1.1.13 batch 2b, a small follow-up record (the finalized NVT record is not reopened).
Resolution: fixed by `NVT-READER-DOC-1113-01` (R1), merged into `1.1.x` by #459.
