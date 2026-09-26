# BUG-20260926-cli-report-path-may-overwrite-output: `--report` can name the committed output

Status: reproduced and fixed on `feature/1.1.13/wave1` (not yet integrated into `1.1.x`)
Severity: P2
Found: 2026-09-26, Claude sub-agent, while fixing CLI-REPORT-RECEIPT-1113-01 on
`feature/1.1.13/cli-report-receipt`
Where: CLI Replace (CtrlRAM and General) and General Merge report writing
Observed: the post-commit report alias check protects only an explicit `--output`; for an
automatically named output, and for General Merge, no post-commit check compares the report
path with the committed output, and the report path is not handed to Application as a
protected path. A `--report` equal to the generated output name could overwrite the
committed BIN.
Expected: a report path that resolves to the committed output (or any protected path) is
rejected before anything is written.
Owner: 1.1.13 (triage with the CLI work).
Reproduction (2026-09-26): with a loose automatic name the run is rejected before it starts,
because every built-in output name template equals the CLI default name. In bundle mode the
committed BIN sits in a folder the commit creates, no check compares it with the report path,
and a `--report` naming it overwrote the BIN after its SHA-256 receipt was printed.
Resolution: the shared committed-run report owner compares the report path with the run's
actual committed output (`ProtectedPathGuard`) before writing; a match leaves the BIN
unchanged and prints `cli.report.failed` (record `CLI-REPORT-RECEIPT-1113-01`, `0f080dccc`;
regressions for General Merge, CtrlRAM Replace and General Replace).
