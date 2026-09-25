# BUG-20260925-persistence-failure-not-actionable: local-state save failure is silent

Status: open
Severity: P2
Found: 2026-09-25, Codex worker (GPT-6), while revalidating F08, at `feature/1.1.12/io-persistence`@`5882d7a57`
Where: `src/NvtFwCombiner.Presentation.Avalonia/LatestSnapshotPersistenceCoordinator.cs:84-123`; `src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs:560-565,627-632`
Observed: the coordinator catches save exceptions into `LastFailure`; the window queues snapshots but has no failure presentation or retry action, so a failed save can be invisible.
Expected: persistence failures are visible and retryable, per `docs/architecture/nfc_roadmap.md` 1.1.12 and `docs/architecture/post-v1.1.8-audit-handoff.md` AUD-01.
Evidence: `LastFailure` has no consumer outside coordinator tests; window queue calls have no failure callback.
Owner: unassigned; `MainWindow.axaml.cs` is in WS-WINDOW's write lock under the WS-IO Amendment.
Resolution:
