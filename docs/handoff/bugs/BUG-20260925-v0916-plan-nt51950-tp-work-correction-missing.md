# BUG-20260925-v0916-plan-nt51950-tp-work-correction-missing: the v0.9.16 plan expects exact output where NT51950 cascade TP-work intentionally differs

Status: open
Severity: P2
Found: 2026-09-25, Claude Code (Opus 5.5, WS-PARITY sub-agent), while running the non-certifying 1.1.12 v0.9.16 comparison with the approved predecessor build, at `feature/1.1.12/v0916-alignment`@`ef5798030` (candidate product source `1c37bd718`)
Where: `docs/contracts/v0916-parity-certification-v1.json`: route `route-7-nt51950-15-ctrlram-replace-4-2-ic-36-nt51950-ctrlram-fw1x-cascade-tp-work` is an `exact-output` route with a canonical input binding, and `approvedSemanticCorrections` names only the NT51951 FW1.x cascade full-flash route
Observed: the two executors differ in exactly 2,816 bytes over five half-open file-offset ranges, `[0xA11C,0xA120)`, `[0xA130,0xA134)`, `[0x2D428,0x2D42C)`, `[0x2D43C,0x2D440)` and `[0x33B10,0x34600)`, both outputs 225,280 bytes (v0.9.16 `cfae1591...e47e`, candidate `a239645d...c643`). The reports show the cause: v0.9.16 writes the Diff CtrlRAM operation over `[0x33200,0x34600)`, the candidate over `[0x33200,0x33B10)` and preserves the Diff NF tail. These are the same ranges and count as the plan's owner-approved NT51951 correction.
Expected: under ADR 0057 and the plan, an `exact-output` route without an approved correction must be byte-identical to v0.9.16; the formal comparator would fail this route with `PARITY_EXACT_MISMATCH`. The difference itself matches intended behavior (CHANGELOG 0.10.1, `99766df75`, #188, and the NT51950 alias case's owner-approved fact scope: writable Diff CtrlRAM prefix `[0x0000,0x0910)`, reference-preserved Diff NF tail `[0x0910,0x1400)`), so the plan, not the product, appears incomplete.
Evidence: harness run `run-bc1` in the test area; row in `docs/handoff/1.1.12/parity/v0916-local-comparison.json`. ADR 0057 states the v1.0.0 lab closed eleven of twelve full-base CtrlRAM cases; this TP-work route uses its TP input directly, so that batch plausibly never compared it (not verified).
Owner: unassigned; the owner disposes the difference; the 1.1.13 formal comparator work owns any plan change
Resolution:
