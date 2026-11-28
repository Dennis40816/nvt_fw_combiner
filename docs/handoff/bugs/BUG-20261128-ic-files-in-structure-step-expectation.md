# BUG-20261128-ic-files-in-structure-step-expectation: existing test inventory excludes the two new structure tools

Status: fixed
Severity: P2
Found: 2026-11-28, primary Codex session, while integrating IC authoring checks at `feature/1.3.0/ic-files-in` based on `c3cc65875552ffa6b869b8f92e14f47722bedab0`.
Where: `tests/scripts/test_authority_policy.py`, `test_r3_is_limited_to_owner_approved_groups_and_ci_is_r2`.
Observed: The test asserts an exact set of four structure subprocess scripts. The authorized verifier registration adds `scripts/ic_files.py` and `scripts/check_ic_files.py`, so its existing exact-set assertion no longer matches. No old structure step was removed.
Expected: The structural step inventory and its test agree, while honoring the owner's instruction not to change an existing expected test value.
Evidence: Read-only extraction of the original assertion and execution of `verify_structure` with its process runner mocked produced exactly those two added paths and no removed paths. The new registration regression independently passes. The existing test module itself was not executed because PyYAML is unavailable.
Owner: primary Codex session, pending owner clarification of the expected-value restriction.
Resolution: No existing expected value was changed. A scoped clarification asks whether adding only these two script names is allowed; this test remains an integration blocker until resolved.

Host fix (2026-11-28 11:1x, PC-NFC): the two IC file checks are not registered in `verify_structure_postchecks`. CI already runs every `tests/scripts/test_*.py` module through the repository-script pytest shards. `test_ic_files_fmt.py` runs `fmt --check` on the repository files, and `test_ic_files_structure.py` runs the checker on them with both schema engines. So CI enforces the IC files without a new structure step, and the governance-owned inventory in `test_authority_policy.py` stays unchanged. The registration test was removed with the registration. Host evidence in the external test area: the two modules plus `test_authority_policy.py` passed (105 tests), and `python scripts/verify.py --structure-only` passed.
