# BUG-20261002-predecessor-work-space-processor-lists-no-output-difference: the write-range audit has nothing to read for a processor that writes a work address space

Status: open (needs a firmware owner decision)
Severity: P1 for the predecessor comparison (the three NT51950 AB Merge scenarios stay `invalid`)
Found: 2026-10-02, Claude Code (Opus 5.5), in the R35-09 real-execution rehearsal against `v1.2.1`, at
`feature/1.2.2/executor-contract`@`5eb2f9fda`
Where: `scripts/predecessor_validation.py`, `_processor_write_audit_failures` (decision 261, third rule)
Observed: the NT51950 AB Merge map runs the external combiner on the work address space `ab-combiner-work`
(operation `run-nt51950-ab-combiner`, target the whole space, three allowed write ranges of four bytes each). Its
mutation row reports changed bytes (6 in the report inspected). Three later `CopyRange` operations (`import-postbuild-b-ilm`, `-dlm`, `-crc`)
copy exactly those three allowed write ranges from the work space into `output-image`. The report lists no output
difference at all: `OutputDifferences` is empty in Preview and Build, on `v0.9.16`, `v1.1.12`, `v1.2.1` and the
candidate. Decision 261 audits the ranges listed under the output differences, so the audit has nothing to read, and
the comparator refuses the report with `processor changed bytes without a listed output difference`
(`PREDECESSOR_REPORT_INVALID`). A CtrlRAM Replace report lists its processor's changed ranges and passes the audit.
Expected: every external processor's writes are held to the write ranges the Preview allows, per
`docs/contracts/predecessor-comparison-v1.md` ("Per-side execution safety", item 5). For a processor that writes a
work address space the contract names no source for that audit, so the rule needs a decision; the refusal was kept.
Evidence: rehearsal run `rolling-11` in the test area (`evidence\1.2.2\p2c\rehearsal`): 36 scenarios `equal`, the
three NT51950 AB Merge scenarios `invalid` at Preview on both sides. A diagnostic shadow run with prototype rules
and without this audit (`shadow-10`, never evidence) gave `equal` for the three.
Options for the owner:
1. Audit the operations that carry the result into the output: every later operation that reads the processor's
   work address space must read inside one of its allowed write ranges. The compiled operations are bound to the
   Preview, so nothing the processor wrote outside those ranges can reach the output. Recommended: it needs no
   product change, holds for the `v1.2.1` baseline, and is checked from the report alone.
2. Leave the three routes as an approved coverage gap until a release lists work-space differences in its report.
3. Change the product report; the `v1.2.1` baseline cannot change, so the gap of option 2 stays for one release.
Owner: unassigned; firmware owner decision (R3)
Resolution: not fixed.
