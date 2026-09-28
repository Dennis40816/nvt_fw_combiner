# BUG-20260926-terminal-parity-rejects-ctrlram-issues: the terminal parity report check may reject every CtrlRAM result

Status: suspected (found by reading the ADR 0057 comparator rules against 1.1.12 run evidence; not run)
Severity: P2 (affects the deferred 2.0.0 terminal certification, not 1.x releases)
Found: 2026-09-26, rolling-parity design on `feature/1.1.13/rolling-parity`
Where: the ADR 0057 terminal report checks in `scripts/v0916_parity_certification.py`
Observed: the terminal comparison requires an empty `Issues` list and a fixed report field set, but all 86
successful CtrlRAM runs of the 1.1.12 comparison (both versions) carry one to three issues, so the terminal
comparison would likely reject every CtrlRAM result.
Expected: the terminal comparator distinguishes informational issues from failures in a declared way, or the
plan states why CtrlRAM results must carry no issues.
Owner: the 2.0.0 terminal certification decision (decision 47); the 1.1.13 rolling comparison does not reuse
these report checks.
Resolution: not fixed.
