# BUG-20260926-cli-report-path-may-overwrite-output: `--report` can name the committed output

Status: suspected (code reading, not reproduced)
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
Resolution: not fixed.
