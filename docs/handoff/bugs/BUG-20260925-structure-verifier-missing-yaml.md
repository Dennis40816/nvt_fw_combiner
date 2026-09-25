# BUG-20260925-structure-verifier-missing-yaml: structure verifier fails before validation without PyYAML

Status: open
Severity: P3
Found: 2026-09-25, Codex worker (GPT-6), while running the WS-IO final structure gate, at `feature/1.1.12/io-persistence`@`d4261e7ba`
Where: `python scripts/verify.py --structure-only`; `scripts/sync_derived.py`
Observed: the structure lane first failed in `sync_derived.py` with `No module named 'yaml'`, before repository validation. A temporary `PyYAML==6.0.3` installation in the test area let the lane proceed to its actual record check.
Expected: the verifier bootstrap declares or reports the missing Python dependency before starting the structure lane, per the repository's canonical verifier workflow.
Evidence: first structure-only run failed at derived synchronization; the second run completed derived synchronization with zero file changes and reached `validate_repository.py`.
Owner: unassigned; commander to assign tooling/bootstrap follow-up.
Resolution:
