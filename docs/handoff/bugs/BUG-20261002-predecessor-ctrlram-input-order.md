# BUG-20261002-predecessor-ctrlram-input-order: the comparator expected CtrlRAM report inputs in the binding order, the CLI sorts them

Status: fixed
Severity: P2 (three scenarios; hidden behind `BUG-20261002-adr0057-checks-refuse-written-processor-reports`)
Found: 2026-10-02, Claude Code (Opus 5.5), in a diagnostic shadow run of the R35-09 rehearsal against `v1.2.1`, at
`feature/1.2.2/executor-contract`@`8d2ae66d9`
Where: `scripts/predecessor_comparison.py`, `execute_cli_stage`; the capture and capacity checks of
`scripts/predecessor_validation.py` compare by position
Observed: the CtrlRAM Replace CLI sorts its bindings by slot id, ordinal, whatever the order of its arguments
(`src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs`, `CreateBindings`), so its report lists
`reference-base`, then `replace-ctrlram-diff`, `-normal`, `-vn`. The reviewed binding of case
`nt51951-fw200-cascade2-auto-prj-599-20260731` names normal, vn, diff. The comparator staged in the binding order, so
the positional capacities and input identities belonged to other slots and the three cascade scenarios that use this
case were `PREDECESSOR_REPORT_INVALID` (`PARITY_REPORT_RANGE_INVALID`) on both versions.
Expected: the report agrees with the captured inputs, per `docs/contracts/predecessor-comparison-v1.md`
("Per-side execution safety"). The eleven other reviewed CtrlRAM bindings are already in ordinal order.
Evidence: diagnostic shadow run `shadow-9` in the test area (`evidence\1.2.2\p2c\rehearsal`): 36 scenarios `equal`
and these three `invalid`; the saved Preview reports list the inputs in ordinal order. The CtrlRAM Previews of the
P-0.5 spike (`v0.9.16`, `v1.1.12`, candidate) are in ordinal order too.
Owner: Claude Code, `feature/1.2.2/executor-contract`
Resolution: fixed in `51b06ba2b` (the shadow run `shadow-10` gives `equal` for the three scenarios): the comparator stages, passes and expects CtrlRAM Replace
inputs in ordinal slot order (`report_ordered_inputs`); the positional check is unchanged. One test in
`tests/scripts/test_predecessor_comparison.py`.
