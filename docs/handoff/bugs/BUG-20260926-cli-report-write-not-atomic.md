# BUG-20260926-cli-report-write-not-atomic: a failed `--report` write can leave a partial report

Status: fixing (on `feature/1.1.13/cli-hardening`, not yet integrated)
Severity: P2
Found: 2026-09-26, automated review of pull request #457 (thread on
`src/NvtFwCombiner.Cli/CliCompositionRunSupport.cs`), also noted by the implementer of
`CLI-REPORT-RECEIPT-1113-01`
Where: CLI report writing after a committed Build (`CliCompositionRunSupport`)
Observed: the report is written with `File.WriteAllTextAsync` directly to the destination. When
cancellation, a full disk or another `IOException` happens after the file was created or truncated, the
destination keeps partial JSON (or a damaged earlier report), while the CLI prints `cli.report.failed`
("was not written") and keeps exit code 0.
Expected: write to a staging file in the same directory and replace the destination atomically; clean
up the staging file on every handled failure, so a failure never changes the destination.
Owner: 1.1.13 wave 2, with the CLI items after #457 merges.
Resolution: fixed on `feature/1.1.13/cli-hardening` by `CLI-REPORT-ATOMIC-1113-01` (R1): the report is written to a same-directory staging file and replaces the destination with one rename; a handled failure before the rename deletes the staging file. Final review pending; not yet integrated.
