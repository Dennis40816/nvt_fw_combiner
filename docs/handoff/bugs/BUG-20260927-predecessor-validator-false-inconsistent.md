# BUG-20260927-predecessor-validator-false-inconsistent: an exact-output route with identical bytes can be reported inconsistent

Status: open (accepted for the next batch by the owner on 2026-09-27; merged with pull request #461)
Severity: P2
Found: 2026-09-27, the automated Codex review of pull request #461; confirmed by the commander in the source
Where: `scripts/predecessor_validation.py`, the v0.9.16 route check near the `exact-output` branch (`PREDECESSOR-VALIDATION-1113-01`)
Observed: for an `exact-output` route the validator rejects only a `consistent` result whose output bytes differ. A route
whose baseline and candidate outputs and computed range evidence are identical can still be reported `inconsistent`, with the
summary and overall result recomputed to match, and the validator reports no failure. The contract defines `exact-output`
consistency by complete output equality, so the result must follow the evidence in both directions.
Expected: an `exact-output` route with identical outputs and no differing ranges must be `consistent`; any other result is a
report failure. A mutation test covers both directions and joins the rule-removal check.
Impact: the error only makes a report stricter (a false inconsistency blocks a release; it cannot pass a bad one), and the
validator is not yet wired into any release gate.
Evidence: the review thread on pull request #461; the source at batch 2c head `e0e330325`.
Owner: rolling parity P-2 (a small follow-up record in the next batch; the finalized record is not reopened).
Resolution:
