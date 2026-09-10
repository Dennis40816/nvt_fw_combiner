# ADR 0070: Separate bounded local R1 work from integration admission

- Status: Accepted
- Date: 2026-09-09
- Owner: Repository owner; requested fixing the restriction before further work
- Risk: R2 governance applicability
- Amends: [ADR 0054](0054-finalize-capability-reuse-records.md), local-start scope only
- Amended by: [ADR 0071](0071-final-integration-path-ownership.md), final integration
  ownership for successive corrections; bounded local eligibility remains unchanged.

## Decision

An unfinished integration record is not, by itself, grounds to stop an
explicitly authorized bounded local R1 correction or repeatedly request a
sequencing waiver. The [development workflow](../governance/development-execution-workflow.md#bounded-local-r1-continuation)
owns the eligibility conditions and required owner/scope/base/path/test/review
evidence. A commit boundary does not broaden authorization.

This is not record-free integration. R2/R3 keep staged design admission and
independent/authority-specific review. Formal integration still requires the
unchanged unique checkpoint diff, record lifecycle, frozen-head review, hashes,
attestations and CI; releases still execute all required certified Golden cases.
Preserve existing immutable records and disclose outstanding admission problems.

No validator, schema, protected check, permission, external authority or release
workflow changes. The repository validator runs through `verify_structure`
and CI and may still fail an interim candidate. Local work must not report that
failure as a pass or treat it as authority to change the validator.

## Rationale and verification

Repeated small UI corrections were stopped by the broad follow-on prohibition,
even after the owner authorized each fix and commit. Closing unrelated R3
evidence or requesting another waiver does not establish those UI fixes' quality.
Targeted behavior tests and scoped review do. Conversely, weakening the shared
validator or rewriting old admissions would damage integration evidence.

Independent R2 design review admitted only the bounded R1 distinction. Review
scenarios cover authorized UI continuation, a firmware-range change (R3 gates
still required), unresolved ownership (stop), and a failed formal candidate
(still blocked). Existing missing/duplicate-record, immutable-active-record,
independent-R2-review and external-R3-authority tests remain applicable unchanged.
