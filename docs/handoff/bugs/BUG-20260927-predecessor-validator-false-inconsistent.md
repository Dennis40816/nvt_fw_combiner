# BUG-20260927-predecessor-validator-false-inconsistent: an exact-output route with identical bytes can be reported inconsistent

Status: locally fixed for 1.1.14; independent review and commander integration pending
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
Allocation (2026-09-28): 1.1.14 (board decision 154).
Resolution:

- The exact-output check now rejects an inconsistent result when both output
  identities are equal and computed output differences are absent, with
  `PREDECESSOR_REPORT_INVALID`. The existing differing-bytes rule and its
  `PREDECESSOR_UNAPPROVED_DIFFERENCE` failure remain intact. Existing execution
  failure, rejection, precursor and other proof-kind handling is unchanged.
- Admission: R2; base `8ee916a3b`, branch `feature/1.1.14/predecessor-validator`.
  Disposition `extend-owner`: `_v0916_route_failures`, called by
  `v0916_report_failures`, remains the semantic owner for the contract's
  v0.9.16 1.x exact-output rule. Owner search covered these functions,
  `scope_evidence_failures`, the predecessor tests and the ADR 0078 contract
  consumers. Owned paths are this bug record, `scripts/predecessor_validation.py`
  and `tests/scripts/test_predecessor_validation.py`. No firmware bytes,
  ranges, ordering, integrity, support, schema or contract are changed.
- Regression: the bidirectional test starts from accepted equal and differing
  output reports, flips only the result and failure code, then recomputes the
  summary and overall result. It uses a route without transitive dependents
  so another proof cannot mask the missing exact-output check. The equal-output
  mutation also joins `test_each_route_rule_fails_on_its_own_change`; every
  existing case remains.
- Red: before the production edit, `python -m unittest
  tests.scripts.test_predecessor_validation` ran 22 tests with 2 failures,
  both showing that the equal-output mutation returned no validation failure.
- Rule removal: separately disabled each exact-output condition in memory
  and ran the route-rule and bidirectional tests; both mutants were killed,
  each with 2 regression failures (2/2 killed). No source mutation was retained.
- Green (2026-09-28): `python -m unittest
  tests.scripts.test_predecessor_validation
  tests.scripts.test_predecessor_comparison_contracts
  tests.scripts.test_v0916_parity_contracts
  tests.scripts.test_v0916_parity_1x_amendment` passed all 91 tests. This covers
  the 22 predecessor-validation tests, parity contracts and ADR 0078 consumers.
  `python scripts/verify.py --structure-only` and
  `python scripts/polytail_check.py` passed; derived synchronization changed
  no files. Each test/check process used the user-level `NFC_TEST_AREA_ROOT`
  and its existing `temp` child for `TEMP`, `TMP` and `TMPDIR`.
- Implementation commit: `4f35b23a3` (`fix(predecessor): reject false exact-output
  inconsistency`). Local `nfc-review` self-check of this commit against
  `8ee916a3b` found no additional scoped defect; local Polytail verdict is
  `PASS-WITH-HUMAN-GATE`. The checks above ran on this implementation's source
  before commit; the follow-up commit changes only this bug record.
- Remaining gates: self-checks are not independent review. The commander owns
  exact-head independent review, full verification and integration; no push,
  GitHub operation or release certification is performed by this workstream.
