# BUG-20261002-adr0057-checks-refuse-written-processor-reports: the ADR 0057 report functions refuse every written report of an external processor or a work address space

Status: open (items 1 to 3 fixed for the comparator per decision 261; items 4 to 6 open)
Severity: P1 for the predecessor comparison (24 CtrlRAM Replace and 6 AB Merge scenarios stay `invalid`)
Found: 2026-10-02, Claude Code (Opus 5.5), in the R35-09 real-execution rehearsal against `v1.2.1`, at
`feature/1.2.2/executor-contract`@`8d2ae66d9`
Where: `scripts/v0916_parity_certification.py`: `_normalize_raw_operation` (executed commands),
`validate_semantic_report_ranges` (capacities and processor mutations), `validate_report_sequence` and
`_validate_raw_report` (terminal path)
Observed: written reports of `v0.9.16`, `v1.1.12`, `v1.2.1` and the candidate differ from the shape these functions
and their fixtures assume:

1. Executed commands. The executable is `external-tools/legacy-combiner/1.13.0/Combiner.exe` below the tool root,
   so its parent is not named `external-tools`; absolute arguments lie in subdirectories of the working directory
   (`output`, `BIN`), not directly in it; an operation repeats an identical command. Each is refused, and the
   reader reports `written report format invalid`.
2. Work address spaces. AB Merge operations use `tp-b-work` and `ab-combiner-work`, which are neither an input nor
   `output-image`. The report declares no capacity for them, so every range in them is refused.
3. Processor mutations. The mutation row of an external processor spans its whole operation target, while its
   allowed write ranges are 3 to 14 smaller ranges, so the row is never inside one allowed write range. The
   changed ranges are in `OutputDifferences`, which the reader does not consume.
4. Terminal path only: a Preview carries mutations and a described output, which `_validate_raw_report` refuses;
   the positional default of `validate_report_sequence` and the unqualified overlap rule refuse the same reports.
5. `v0.9.16` only: a rejected Preview lists the operations it did not run. In the v0.9.16 mode run `v0916-4` the
   baseline's NT51950 CtrlRAM cascade Preview exits 1 with a written report: one `error` issue
   (`profile.v2.compile.map-selection-invalid`), no compilation fingerprint, a described output of size 0, no
   mutation, and nine operations with status `Skipped`, two of them processors without an executed command. The
   contract defines that as a typed product rejection, and the plan expects the baseline to reject this route
   (`canonical-binding-not-applicable-to-v0916`). The reader refuses a processor without a command, and the
   compiled-authority check requires every operation to have succeeded, so the side is `invalid`, not `rejected`.
   The run reproduces the rest of the amendment's `baselineNotApplicable` row: both precursor identities, the
   rejecting stage and issue code, and the candidate output.
   What it needs: a rule for the report of a typed rejection, for example that an operation with status `Skipped`
   is a plan that did not run (no command, no mutation, no fingerprint) and is not held to the checks of an
   executed operation. Not decided; not changed.
6. `v0.9.16` only: its NT51950 AB Merge report also works in `a-bank-work` and `b-bank-work`. Decision 261 names
   `tp-b-work` and `ab-combiner-work`, so ranges in the other two are refused (`v0916-4`). What it needs: the same
   rule for those two address spaces, or an approved disposition of the route. Not decided; not changed.

Expected: the per-side safety of `docs/contracts/predecessor-comparison-v1.md` holds for the reports the CLIs write.
For items 2 and 3 the written report lacks what the contract's rule needs (a capacity; a mutation range inside an
allowed write range), so the rule itself needs a decision; it was not relaxed.
Evidence: rehearsal runs `rolling-7` and `rolling-8` in the test area (`evidence\1.2.2\p2c\rehearsal`). In
`rolling-8`, 27 scenarios stop at the reader refusal on both sides and 3 at the range refusal. Counts over the
reports saved from `rolling-7`: item 1 in all 46 CtrlRAM and NT51950 AB Previews, item 2 in all 12 AB Previews,
item 3 in the same 46. Items 4 and 5 come from reading the code and from the P-0.5 spike reports. With prototype
rules for items 1 to 3 (diagnostic only, never evidence), all 74 Preview and Build pairs of `v1.1.12` and its
candidate in the P-0.5 spike pass the per-side checks, and the shadow run `shadow-10` of the rehearsal gives `equal`
for all 39 scenarios between `v1.2.1` and the candidate, with no informational difference.
Owner: Claude Code, `feature/1.2.2/executor-contract`, for items 1 to 3; items 4 and 5 unassigned
Resolution: items 1 to 3 are fixed for the comparator in `5eb2f9fda`, as 1.2.x board decision 261 decides them. Each
is an option of the ADR 0057 function, default off, so the terminal path is unchanged (item 4 stands):
`written_commands` reads the executed commands a CLI writes, and the validator requires the executable to be a tool
file the comparator staged and hash-checked and the working directory to lie below the process's temporary
directory; `declared_work_ranges` holds a range in `tp-b-work` or `ab-combiner-work` to the ranges the same side's
Preview declares; `audited_processor_writes` leaves the processor's whole-target mutation row to an audit of the
output difference ranges against the Preview's allowed write ranges. Ten tests in
`tests/scripts/test_predecessor_comparison.py` use the written shapes. In `rolling-11`, 36 scenarios are `equal`.
The three NT51950 AB Merge scenarios stay `invalid` for another cause, recorded as
`BUG-20261002-predecessor-work-space-processor-lists-no-output-difference`. Items 5 and 6 are not decided and not
changed; in the v0.9.16 mode run `v0916-4` they leave 2 of 37 covered routes `invalid` and 35 `consistent`.

Independent-review follow-up (2026-10-02, base `a3fb247bd`): `_written_command` coerced argument members to
strings and admitted reserved device, response-file (`@`) and environment-expansion (`%`) tokens. The local
correction requires a list of strings and refuses those tokens under `written_commands` only. Two new
regression tests first failed on the base behavior and now pass, with ADR 0057 default acceptance/refusal
characterization and staged-report refusals. Disposition: `extend-owner` in `_written_command`, called by the
versioned report reader; no second command validator is added. The per-side contract now names both the shared
ADR 0057 functions and the comparator's staged-command/write-audit functions in `predecessor_validation.py`.
This uncommitted R3 follow-up still needs independent review and firmware-owner/release-owner integration gates.
