# BUG-20261002-adr0057-checks-refuse-written-processor-reports: the ADR 0057 report functions refuse every written report of an external processor or a work address space

Status: open
Severity: P1 for the predecessor comparison (24 CtrlRAM Replace and 6 AB Merge scenarios stay `invalid`)
Found: 2026-10-02, Claude Code (Opus 5.5), in the R35-09 real-execution rehearsal against `v1.2.1`, at
`feature/1.2.2/executor-contract`@`a574021fe`
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
5. `v0.9.16` only (P-0.5 spike reports): a Preview has operations with status `Skipped`, one of them a processor
   without executed commands.

Expected: the per-side safety of `docs/contracts/predecessor-comparison-v1.md` holds for the reports the CLIs write.
For items 2 and 3 the written report lacks what the contract's rule needs (a capacity; a mutation range inside an
allowed write range), so the rule itself needs a decision; it was not relaxed.
Evidence: rehearsal runs `rolling-7` and `rolling-8` in the test area (`evidence\1.2.2\p2c\rehearsal`). In
`rolling-8`, 27 scenarios stop at the reader refusal on both sides and 3 at the range refusal. Counts over the
reports saved from `rolling-7`: item 1 in all 46 CtrlRAM and NT51950 AB Previews, item 2 in all 12 AB Previews,
item 3 in the same 46. Items 4 and 5 come from reading the code and from the P-0.5 spike reports. With prototype rules for items 1 to 3 (diagnostic only), all 74 Preview and Build pairs of
`v1.1.12` and its candidate in the P-0.5 spike pass the per-side checks.
Owner: unassigned; firmware owner decision (R3) on the three rules
Resolution: not fixed.
