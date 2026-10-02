# BUG-20261002-exclusive-atomic-writer-race: `write_json_exclusive_atomic` is not exclusive between concurrent callers

Status: open
Severity: P3 (no concurrent caller exists today)
Found: 2026-10-02, Codex (`gpt-6-astra`) review of the comparator's rolling mode, which writes its report through
this helper.
Where: `scripts/v0916_parity_certification.py`, `write_json_exclusive_atomic`.
Observed: the function checks that the target does not exist, writes a temporary file opened exclusively, and
publishes it with `os.replace`. Two callers that both pass the existence check can each publish, and the second
replaces the first report without a failure. Only the temporary name (`<target>.tmp`) is exclusive.
Expected: publication fails with `PARITY_WRITE_CONFLICT` when the target exists at publication time, and a caller
removes only the temporary file it created.
Evidence: the review of commit `01e655df6` (by reading; no concurrency experiment was run).
Impact: the terminal certification and the predecessor comparator each run as one process and write each report
once, so no overwrite is known. The comparator's subcommands also refuse an existing output path before they
start.
Owner: the terminal parity script (firmware owner and release owner path); fix with the next change to that file.
Resolution: not fixed.
