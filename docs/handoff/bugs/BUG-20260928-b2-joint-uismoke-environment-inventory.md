# BUG-20260928-b2-joint-uismoke-environment-inventory: verifier rejects the catalog probe trace variable

Status: fixed (change-related)
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
Owner: Codex, B2 implementer, feature/1.1.13/b2a-prebuilt-catalog.
Resolution: Fixed in `4be9ca1a4`. The B2 probe introduced
`NFC_STARTUP_TRACE_PATH` without extending the source inventory. Classify it as
process-local: `ProfileCatalogProbeTests.RunAsync` sets only the child's
`ProcessStartInfo.Environment` to `workspace.PathFor("forbidden-trace.json")`.
The probe's complete workspace inventory detects unwanted writes. This is not
a caller-set external output override, so
`LOCAL_PARTITION_OVERRIDE_ENVIRONMENT_VARIABLES` remains unchanged. The source
inventory now names the variable with this rationale; every existing assertion
and the per-compiled-type fixed-directory isolation check is retained.
Verification: one targeted red run reproduced the exact missing-variable
assertion, followed by one full orchestration rerun:
`python -m unittest discover -s tests/scripts -p test_verify_orchestration.py`
passed 249/249 in 43.119 seconds. Logs are `inventory-red.log` and
`orchestration-green.log` in test-area evidence `v1113-b2-blocker-fixes`.
Scoped self-check found no firmware byte/range/order/integrity/support changes;
this is not independent review. Commander review and full joint verification
remain outstanding.
