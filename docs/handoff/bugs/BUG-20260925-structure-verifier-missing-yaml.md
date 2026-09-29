# BUG-20260925-structure-verifier-missing-yaml: structure verifier fails before validation without PyYAML

Status: resolved locally for 1.1.14; pending integration review
Severity: P3
Found: 2026-09-25, Codex worker (GPT-6), while running the WS-IO final structure gate, at `feature/1.1.12/io-persistence`@`d4261e7ba`
Where: `python scripts/verify.py --structure-only`; `scripts/sync_derived.py`
Observed: the structure lane first failed in `sync_derived.py` with `No module named 'yaml'`, before repository validation. A temporary `PyYAML==6.0.3` installation in the test area let the lane proceed to its actual record check.
Expected: the verifier bootstrap declares or reports the missing Python dependency before starting the structure lane, per the repository's canonical verifier workflow.
Evidence: first structure-only run failed at derived synchronization; the second run completed derived synchronization with zero file changes and reached `validate_repository.py`.
Owner: Codex (1.1.14 tooling implementation).
Resolution: `scripts/verify.py` now checks for `yaml` before admitting the structure lane and reports the pinned `PyYAML==6.0.3` install command if it is missing. The pin was already declared in `tools/crc-worker/pyproject.toml` (`dev`) and installed by the CI structure job, so no dependency or CI workflow change was needed. The verifier does not install packages.
Verification: the new missing-module test failed before the fix on 2026-09-28 (`1 != 0`, lane returned success), then passed after the fix. On 2026-09-29, `python -m unittest tests.scripts.test_verify_orchestration` passed (251 tests), and `python scripts/verify.py --structure-only` passed, including `sync_derived.py` (0 files changed), repository validation, and Polytail. Both commands used the user-level `NFC_TEST_AREA_ROOT` with `TEMP`, `TMP`, and `TMPDIR` set to its existing `temp` child.
