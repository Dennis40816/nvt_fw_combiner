# WS-CIRERUN: decision 191 CI rerun and hang evidence

## Dispatch and admission

- Outcome: one evidence-preserving retry of failed .NET test identities in a
  CI shard; five-minute VSTest hang detection with dump evidence; visible flaky
  identities in the shard and aggregate, with a commander bug-record merge gate.
- Non-goals: fixing the underlying flakes, local/release verifier retries,
  release evidence policy, workflow changes, GitHub permissions or writes.
- Authority: local edits and commit only; no push or PR. Decision 191
  ([board](../1.1.12.md)) explicitly amends ADR 0079. Risk R3 for changing
  acceptance of a required CI check (ADR 0079 T3); release-owner review remains
  an integration gate. No firmware byte, range, order, integrity or support change.
- Branch: `feature/1.1.15/ci-rerun`; worktree `<worktrees>/f115-ci`;
  base `aba286bae924234397cc561831d65e37f919655b` from `origin/1.1.x`.
- Implementation owner: `codex/gpt-6-astra`, high reasoning effort, explicitly
  selected by the owner; sole writer. Independent review is read-only.
- Owned paths: `scripts/verify.py`, affected `tests/scripts/` tests,
  `docs/adr/0079-test-architecture.md`, `docs/ci/pull-request-ci.md`, this log.
  Review corrections additionally own the two `BUG-20260929-ci-rerun-*` ledger
  files required by the repository's found-bug rule.
- Existing semantic owner: `verify_ci_dotnet_test_shard` produces the closed
  project inventory and schema-3 evidence; `finalize_ci_dotnet_evidence` owns
  aggregate acceptance. `local_dotnet_vstest_command`, TRX/discovery parsers,
  coverage normalization, regular-file custody, hashing and artifact staging
  are existing capabilities. Workflow callers are the existing CLI switches.
- Owner search: inspected those functions, their tests in
  `test_verify_orchestration.py`, ADRs 0027/0079, the CI runbook, and the dated
  release-workflow cleanup design. The active failure-evidence contract is
  `docs/ci/pull-request-ci.md`; the design is historical coordination evidence.
  Disposition: `extend-owner`; reuse the command/evidence primitives and keep
  filters and verdicts out of workflow YAML. No second execution path.
- Acceptance: fake-runner tests for green/no retry, recovery, repeated failure,
  hang attachments, both attempts and aggregate flaky validation; negative
  evidence tests; affected script tests and `verify.py --structure-only`.
  No real dotnet run or `--all` in this task.
- Stop: need to change workflows or the CI mirror, release workflow/evidence
  rules, the same test still failing after two corrections, or four-hour budget.
- Known bug evidence: `BUG-20260929-repository-lease-test-hang`,
  `BUG-20260929-vstest-discovery-foreground-thread-json`,
  `BUG-20260928-launcher-admission-deadline-test-flake`. A retry is not a fix.

### 2026-09-29 admission

State: planned
Evidence: clean task branch at the stated base; existing workflow uploads the
whole allowlisted staging directory and already calls the canonical finalizer.
Open: independent fixed-head review and release-owner integration approval;
actual VSTest hang/dump CI evidence cannot be claimed from fake-runner tests.
Next: implement the bounded CI-only contract and tests. Build, discovery and
hang failures are not retried: they cannot establish a complete failed-test
inventory. Coverage remains the initial complete run's evidence.

### 2026-09-29 local implementation checkpoint

State: verified (local candidate; not integrated or published)
Commits: this checkpoint commit, based on `aba286bae924234397cc561831d65e37f919655b`.
Evidence:

- `python -m unittest tests.scripts.test_ci_dotnet_retry tests.scripts.test_ci_structure_contract tests.scripts.test_coverage_ci_contract tests.scripts.test_governance_retirement -q`
  passed 44 tests; retry cases use the existing fake-runner/TRX fixtures.
- `python -m unittest tests.scripts.test_verify_orchestration -k ci_ -k vstest -k trx -k discovered -k partition -q`
  passed 60 tests. CI-only command/manifest changes preserve local/release defaults.
- `python scripts/verify.py --structure-only` passed, including derived-file sync
  (zero changes), repository validation, Polytail fast checks and sentinel dry-run.
  The existing advisory code-size report is not a failure.
- Ruff syntax/error checks and `git diff --check` passed. Every test/verifier
  process explicitly loaded the user-level test root and bound all three temp
  variables to its existing `temp` child. No actual dotnet test or `--all` ran.
- Initial red: new retry tests exposed the missing retry, flaky field and blame
  argument. Final fake-runner cases also reject filter/metadata tampering,
  incomplete theory rows, changed hashes/logs, hang attachments on a passing
  artifact and a third attempt. They preserve first evidence if attempt 2 fails
  to launch or produces an invalid attachment. Partial recovery stays recorded
  while a twice-failing sibling keeps the job failed.
- ADR 0079 and the active CI contract now distinguish workflow attempts from
  job-local attempts, define schema 4 and the bug-record merge gate, and document
  no retry for build/discovery/hang. No workflow, mirror or release rule changed.
- Scoped self-review traced one shard/finalizer owner, fail-closed evidence,
  unchanged coverage input, and no firmware semantic impact. Independent
  fixed-head review is the next gate; self-review does not substitute for it.

Open:

- VSTest's five-minute progress/inactivity timer is not a strict independent
  wall-clock deadline for each concurrent test; progress may reset it and dump
  collection adds time. The strict per-test target remains unproven. Real Windows
  CI hang/dump and actual adapter/FQN evidence require commander follow-up; this
  task was explicitly limited to fake runners and no GitHub writes.
- Independent fixed-head review and release-owner integration approval remain
  required. The final review is reported against the actual commit in the local
  handoff report. No push, PR, integration or publication is authorized here.
- Commander must map every reported flaky FQN to a bug file before merge. Summary
  warnings are procedural enforcement, not an automatic bug-existence check.
- Existing underlying stability bugs remain open unless their separate owner
  resolves them. The workflow-level re-run provenance gate remains pending.
- No stop condition was triggered: no workflow/mirror or release-rule edit was
  needed, no test remained failing after two corrections, and budget remained.

Next: independently review this exact commit; the commander obtains real CI
evidence and applicable owner approval before any separately authorized merge.

### 2026-09-29 independent review corrections

State: local, narrow-verified; not integrated or published
Commits: first candidate `3253841cffc6a6581d732327393b7887cbecd94d`; corrections
are in this follow-up checkpoint commit.
Evidence:

- Fresh-session, read-only `codex/gpt-6-astra`, high effort, reviewed the first
  fixed head against the base; verdict `FAIL`, one P1 and one P2, both fix-now.
- `BUG-20260929-ci-rerun-xunit-error-runinfo`: retained real xUnit TRX proved an
  ordinary failure emits `RunInfo outcome="Error"`. The reviewer replayed it
  read-only, with no dotnet invocation. Sanitized structural fixtures reproduced
  the rejection. The fix admits only the exact timestamped xUnit `[FAIL]`
  notification of a failed TRX case, with zero fatal terminal counters; unknown
  Error messages, unmatched identities and platform counters still fail closed.
- `BUG-20260929-ci-rerun-dump-rejection-drops-attempt`: the reviewer reproduced
  valid attempt-2 TRX/log being discarded for an empty dump. Attachment bounds
  now produce an omission diagnostic while retaining independently valid files;
  whole-tree regular-file/reparse rejection remains unchanged. The regression
  asserts retention of both attempts, not just the first.
- After correction, `python -m unittest tests.scripts.test_ci_dotnet_retry -q`
  passed 16 tests, and orchestration `-k ci_` passed 43 tests. Both findings
  passed after one correction; the repeated-failure stop condition did not fire.

Open: fixed-head re-review and final structure confirmation for the correction;
all earlier external CI, timer, commander bug mapping and human gates remain.
No workflow/mirror or release-rule change is needed.
Next: independently re-review the correction commit and report its exact SHA,
checks and verdict in the local handoff report; do not push.
