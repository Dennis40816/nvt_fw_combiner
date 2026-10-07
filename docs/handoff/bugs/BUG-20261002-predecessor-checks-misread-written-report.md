# BUG-20261002-predecessor-checks-misread-written-report: the per-side order and capture checks refused every written 1.x report

Status: fixed
Severity: P1 (the comparator could not produce any outcome)
Found: 2026-10-02, Claude Code (Opus 5.5), in the R35-09 real-execution rehearsal against `v1.2.1`, at
`feature/1.2.2/executor-contract`@`4398c1a9a`
Where: `scripts/predecessor_validation.py`, `side_execution_verdict` and `_side_capture_failures`; the order rule is
`validate_report_sequence` in `scripts/v0916_parity_certification.py`
Observed: every scenario was `PREDECESSOR_REPORT_INVALID` at its first Preview, on the baseline and the candidate
alike. A written mutation row has no sequence, so the order check compared a list position (0, 1) with the profile
sequence of the operation (100, 200). A Preview also describes its output with `Committed: false` and leaves no
file, which the capture check read as a report that disagrees with its capture.
Expected: mutations follow the order of the Preview's operations, and the report agrees with the captured output,
per `docs/contracts/predecessor-comparison-v1.md` ("Per-side execution safety"). The test fixtures had sequence 0
and a Preview without output or mutations, so they never met the written shape.
Evidence: rehearsal runs `rolling-5` and `rolling-6` in the test area (`evidence\1.2.2\p2c\rehearsal`): 27 scenarios
`PREDECESSOR_REPORT_INVALID` with detail `PARITY_PROVENANCE_INVALID`; all 20 saved Preview reports fail
`validate_report_sequence` and pass the projection check. The P-0.5 spike reports of `v0.9.16`, `v1.1.12` and its
candidate have the same shape.
Owner: Claude Code, `feature/1.2.2/executor-contract`
Resolution: fixed in `d34e75336`: the validator gives each mutation row the sequence its own report declares for
the operation it names, and compares an output description with a capture only when a file exists or the report
commits one. Three tests in `tests/scripts/test_predecessor_comparison.py` use the written member shape. The
terminal path keeps the positional default of `validate_report_sequence`; see
`BUG-20261002-adr0057-checks-refuse-written-processor-reports`.
