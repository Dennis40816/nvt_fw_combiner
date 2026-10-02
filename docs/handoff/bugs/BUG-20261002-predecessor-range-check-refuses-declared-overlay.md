# BUG-20261002-predecessor-range-check-refuses-declared-overlay: the range check refused a declared `ReplaceExisting` overlay

Status: fixed
Severity: P2
Found: 2026-10-02, Claude Code (Opus 5.5), in the R35-09 real-execution rehearsal against `v1.2.1`, at
`feature/1.2.2/executor-contract`@`2b277c0b6`
Where: `scripts/v0916_parity_certification.py`, `validate_semantic_report_ranges`
Observed: the check refused any two intersecting operation targets, whatever their address space or overlap policy.
The NT51950 and NT51951 Standard Merge maps copy the DP container and write the TP over part of it with
`ReplaceExisting`, so both scenarios, and four CtrlRAM precursors, were `PREDECESSOR_REPORT_INVALID`
(`PARITY_REPORT_RANGE_INVALID`) on both versions.
Expected: `docs/contracts/v0916-parity-certification-v1.md` forbids an intersection of two targets with the `Reject`
policy only. In the 136 reports saved from `rolling-7` and in the P-0.5 spike reports, every same-space overlap has
a later operation that declares `ReplaceExisting`.
Evidence: rehearsal run `rolling-7` in the test area (`evidence\1.2.2\p2c\rehearsal`); in `rolling-8` both
scenarios are `equal`.
Owner: Claude Code, `feature/1.2.2/executor-contract`
Resolution: fixed for the comparator in `a574021fe`: `validate_semantic_report_ranges` takes `declared_overlap`
(default off, terminal behavior unchanged); with it only an operation that declares `ReplaceExisting` may overlap
an earlier target of its address space. Two tests in `tests/scripts/test_predecessor_comparison.py`. The terminal
default still refuses the overlay; that is part of `BUG-20261002-adr0057-checks-refuse-written-processor-reports`.
