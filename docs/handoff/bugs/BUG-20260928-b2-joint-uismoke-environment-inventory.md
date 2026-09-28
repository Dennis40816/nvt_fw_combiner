# BUG-20260928-b2-joint-uismoke-environment-inventory: verifier rejects the catalog probe trace variable

Status: open
Severity: P2
Found: 2026-09-28, Codex (codex/gpt-6-astra), during ADR 0077 B2 joint gates,
at feature/1.1.13/b2a-prebuilt-catalog@8e74ece67.
Where: tests/scripts/test_verify_orchestration.py:6207;
tests/NvtFwCombiner.UiSmoke.Tests/ProfileCatalogProbeTests.cs:105
Observed: test_uismoke_writers_outside_the_session_are_isolated_per_compiled_type
fails because NFC_STARTUP_TRACE_PATH is not in its permitted variable inventory.
The new probe test sets that variable on ProcessStartInfo.Environment to detect
unwanted trace writes in the child process. The script suite had 248 passing
tests and this one failure.
Expected: The verifier's source inventory must account for the actual probe test
without weakening the existing partition and external-writer isolation rules.
Evidence: `python -m unittest discover -s tests/scripts -p test_verify_orchestration.py`
failed at the variable inventory assertion. The test-area evidence directory
`v1113-b2-joint-8e74ece67` retains the complete log.
Owner: unassigned; commander to route to the verifier/test isolation owner.
Resolution: Pending. The requested project-count correction is complete; changing
the environment-variable isolation contract was not included in that correction.
