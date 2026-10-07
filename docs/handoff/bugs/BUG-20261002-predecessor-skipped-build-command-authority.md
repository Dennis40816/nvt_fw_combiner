# BUG-20261002-predecessor-skipped-build-command-authority: skipped Build still compared Preview commands as compiled authority

Status: fixed on this branch (decision 272)
Severity: P1
Found: 2026-10-02, independent Codex reviewer (inherited model), while reviewing
the uncommitted executor-contract patch based on `bb620b4797167c61c0e030258ed0036822c99f34`.
Where: `scripts/v0916_parity_certification.py`, `validate_report_projection_against_compiled_authority`
Observed: a successful processor Preview carries executed commands, while an
all-Skipped Build carries none. The initial decision-272 opt-in ignored only
status during authority comparison, so it still classified the paired no-write
Build as `invalid` despite all five rejection conditions holding.
Expected: decision 272 admits this typed rejection, while retaining the exact
compiled operations, order, ranges and processor declarations from the same-side Preview.
Evidence: the paired processor assertion added to
`test_all_skipped_processors_without_commands_are_rejections_only_with_all_five_conditions`
failed before the correction (`rejected` expected, `invalid` observed).
Owner: executor-contract workstream
Resolution: only after the comparator proves all five decision-272 conditions,
the shared projection check compares the same compiled authority while allowing
the observed `Skipped` status and empty execution-only command records to differ
from a successful Preview. Compiled range or processor changes still refuse;
the ADR 0057 default remains exact and unchanged. The paired regression and
the affected predecessor/ADR 0057 Python tests pass on this branch. Real-build
rehearsal, independent final-source review and R3 owner gates remain with the commander.
