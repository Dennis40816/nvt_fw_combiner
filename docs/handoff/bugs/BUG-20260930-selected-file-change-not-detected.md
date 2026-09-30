# BUG-20260930-selected-file-change-not-detected: a selected input file that changes on disk is not noticed before Build

Status: open (owner decision 203; scheduled for 1.2.7, decision 212)
Severity: P2
Found: 2026-09-30, owner question "if a user loads a file and then changes its path, when does Verified reload?",
answered from code at `1.1.x`@`9d5783b4e` (v1.1.15 content); not reproduced in the running app.
Where: `src/NvtFwCombiner.Infrastructure/Files/FileContentSnapshotInspector.cs` (reads and hashes the file once),
`src/NvtFwCombiner.Application/Composition/AcceptedSessionCompositionExecution.cs` (`AcceptedArtifactReader` serves
the accepted bytes; the stale checks cover only a catalog reload and the external tool generation),
`src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.FirmwareInspection*.cs`
(re-inspection re-reads `slot.FilePath`).
Observed: NFC does not watch selected files. Selecting a file reads it into memory with its length and SHA-256;
Build and Preview use those bytes. A file renamed, moved, modified or deleted afterwards keeps showing Verified
with its old path, and Build uses the old content, until a re-inspection runs: another file selected in the same
workflow (the loaded Merge or Replace slots of that workflow are re-read), an IC, Number or mode change, the AB same-TP or dummy-DP switch, or a
catalog republication. A re-inspection of a missing path fails the slot; a replaced file is inspected again.
Expected: owner decision 203: when a selected file changes on disk, NFC shows a toast and unselects the file; at
minimum the check runs when Build starts (the path still resolves to the same length and SHA-256 as the accepted
snapshot), and it may also run earlier (a watcher or on focus).
Evidence: the code paths above; no test covers a file changed after selection.
Owner: unassigned; `1.2.7` item R43 (decision 212).
Resolution: pending.
