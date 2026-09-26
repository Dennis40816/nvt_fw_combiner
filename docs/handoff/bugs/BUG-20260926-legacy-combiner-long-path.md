# BUG-20260926-legacy-combiner-long-path: the legacy Combiner fails when its argument paths reach 260 characters

Status: open
Severity: P3
Found: 2026-09-26, Claude Code (Opus 5.5), in the rolling-parity P-0.5 spike (test area only)
Where: the external processor `Combiner.exe` as run by the CtrlRAM workflows; the CLI's temporary staging layers
Observed: with a TEMP root of 111 characters, the CLI's temporary layers added about 149 more, and the
NT51917 and NT51927 FW1.4.1 single scenarios failed with `external-tool.process.failed` on v0.9.16,
`v1.1.12` and the candidate alike; the tool reported a file open failure for an argument path of 260
characters. The same runs passed with a shorter TEMP root.
Expected: either the host keeps the paths it passes to the legacy tool below the Windows MAX_PATH limit
(for example a short staging root), or it refuses before the run with a typed issue that names the path
length, so an environment limit is not reported as a processor failure.
Evidence: payload-free P-0.5 evidence in the test area (`parity-p05\evidence`). Typical user TEMP paths
are much shorter, so ordinary installations are unlikely to hit this.
Owner: unassigned. The rolling-parity comparator bounds its TEMP root meanwhile (P-1/P-2).
Resolution:
