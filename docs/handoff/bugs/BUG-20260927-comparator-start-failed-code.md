# BUG-20260927-comparator-start-failed-code: the predecessor comparator does not know the new process start-failed issue code

Status: fixed
Severity: P3
Found: 2026-09-27, Codex (gpt-6-sol) review of W6-B (PROCESS-START-TYPED-1113-01) at `d75a4b585`
Where: `docs/contracts/predecessor-comparison-v1.json` and its report schema (process-failure issue codes)
Observed: W6-B adds the issue code `external-tool.process.start-failed`; the comparator contract lists process-failure codes
and does not include it, so a run that fails this way would not be classified as a process failure.
Expected: the comparator contract and its classification recognize the new code, under the parity P-2 contract owner.
Evidence: the W6-B review.
Owner: rolling parity P-2 (contract change, R2).
Allocation (2026-09-28): 1.1.14 with rolling parity P-2 (board decision 154).
Allocation (2026-09-29): moved to 1.2.x with the rest of parity P-2 (board decision 167); the comparator work
lands in `1.2.2` as part of R35 per the accepted [1.2.x allocation](../1.1.14/1.2.x-allocation.md) (board decision
175). This supersedes the 1.1.14 target above; the exact-output false-inconsistent fix is the one P-2 item that
still shipped in 1.1.14 (`BUG-20260927-predecessor-validator-false-inconsistent`).
Resolution: R35-07 classifies `external-tool.process.start-failed` as a non-approvable process failure in the contract, schemas and validator, with negative regression coverage.
