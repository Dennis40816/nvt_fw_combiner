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

### 2026-09-29 decision 193 correction admission

State: planned; supersedes earlier scope and stop conditions for this correction.
Source: clean `d836eaf5574610535f82e282d4ce1cc10cdb020d`, same task branch;
independent review `f115-ci-review.md`, P2-1 through P2-6 and P3-1 through P3-8.
Owner: Codex gpt-6-astra, high effort, sole writer, explicitly assigned.
Authority: local edits and commit only; no push, PR or GitHub writes.
Decision 193 authorizes the release source-CI zero-flaky policy amendment.
Offline authority classification of `scripts/release_promotion_policy.py`:
`release-approval-policy`, R3, **release-owner and governance-owner** required.
These are integration review/approval gates, not permission to publish here.

Owner search and disposition: `extend-owner` for `verify_ci_dotnet_test_shard`,
`finalize_ci_dotnet_evidence`, their existing TRX/evidence helpers, and
`validate_source_ci` / `_collect_source_ci` in the release policy. The release
collector already owns paginated exact-source run/job observations and closed
candidate evidence; extend that observation to flaky annotations across all
workflow attempts, without changing workflow callers. Reuse bug ledger files;
no second firmware or CI execution engine. No firmware bytes/ranges/order,
integrity, support or Golden expected outputs change.

Additional owned paths: `scripts/release_promotion_policy.py`, its script tests,
`docs/ci/release-package.md`, ADR 0027, and scoped bug ledger entries.
Acceptance: automatic flaky bug gate and warnings; release zero-flaky evidence;
sequence-only hang evidence; Golden retry exclusion; 20-FQN and time-budget
bounds; precise negative tests and the remaining review corrections.
Checks: affected `tests/scripts` narrow tests, `verify.py --structure-only`;
load user-level test root and all three temp variables in every test process.
No `--all` or real dotnet invocation. Do not edit the decision board or workflows.
Stop: workflows need changes, a new owner decision is required, the same test
still fails after two corrections, or the three-hour budget expires.
Open: exact-head independent re-review, applicable human approvals and first
real CI hang sequence/timing evidence remain external closure gates.


### 2026-09-29 decision 193 verified correction checkpoint

State: verified locally; not integrated or published.
Commits: this correction commit, parent `d836eaf5574610535f82e282d4ce1cc10cdb020d`.
Evidence applies to its production/test tree; exact SHA and fresh-session review
are recorded in the requested external `f115-ci-report.md` after commit.

- P2-1: fullmatch of `Sequence_<32 hex>.xml`; real filename fixture replaces
  the old suffix assumption. Sequence size/count/custody bounds remain.
- P2-2: finalizer requires a token-bounded full FQN in a checkout bug file;
  absent or prefix-only records fail before coverage. Each flaky also emits
  the exact `Flaky test` warning annotation. Matching rules are in the contract.
- P2-3: source-CI release admission requires zero flaky annotations across
  every workflow attempt, preserves a closed `flakyEvidence` observation and
  rejects incomplete/API-error/drifting observations. A new clean run is needed
  after flaky recovery; re-running jobs cannot clear it. Offline CLI fixtures
  cover a previously flaky attempt and the candidate manifest projection.
- P2-4: GoldenRegression fails directly and cannot retry; the finalizer also
  rejects forged successful retry evidence for that project.
- P2-5: `HangDumpType=None`; no memory dump collection or staging/upload.
  Sequence/TRX/log evidence remains. ADRs 0079 and 0027 and the CI contract
  agree that CI evidence still contains no firmware payloads.
- P2-6 / P3-2 / P3-3: accepted inactivity timer, first-real-hang closure gate,
  maximum 20 FQNs, and a 25-minute shard-verifier admission budget with at
  least 420 seconds remaining for retry. Exact boundary positives and refusal
  negatives pass; external setup and concurrent progress limits are explicit.
- P3-1: failed producer is chosen before artifact inspection; best-effort flaky
  diagnostics cannot replace it. Missing, malformed and failed-download cases pass.
- P3-4: documented that retry has no Coverlet instrumentation; a labelled flake
  is not a fix and cannot satisfy release admission.
- P3-5: only selected failed-method definitions/theory siblings are validated
  for retry; unrelated passing metadata cannot veto them.
- P3-6: mutation assertions match their intended errors; first and retry exit
  code 2 scenarios reject. P3-7: wording and reciprocal/entry links corrected.
- P3-8: retained real xUnit TRX locator and SHA-256 added to its existing bug.
  New review-defect bug records preserve the corresponding fixes and evidence.

Commands/results (all test/verifier processes loaded the user-level
`NFC_TEST_AREA_ROOT` and set `TEMP`, `TMP`, `TMPDIR` to its existing `temp`):

- `python -m pytest tests/scripts/test_ci_dotnet_retry.py tests/scripts/test_verify_orchestration.py tests/scripts/test_ci_structure_contract.py tests/scripts/test_coverage_ci_contract.py tests/scripts/test_release_promotion_policy.py tests/scripts/test_governance_retirement.py -q --tb=short`
  -> **367 passed in 108.07s**.
- `python scripts/verify.py --structure-only` -> **PASS**, structure 30.2s;
  derived synchronization changed zero files, repository structure and Polytail
  fast checks passed. Existing code-size advisory only.
- Ruff formatting on changed regions and `ruff check --select E9,F63,F7,F82`
  on the five changed Python files -> PASS; `git diff --check` -> PASS.
- New tests first reproduced missing bug/release gates, Golden retry, sequence
  loss, error masking and unrelated-definition veto. The release fake API and
  old dump-based orchestration fixture were each updated once for the new
  contract; no test remained failing after two corrections.
- Prohibited surfaces (`.github/workflows`, workflow mirror and decision board)
  have no diff. Added-line machine-path/credential scan passed. No `--all`,
  actual dotnet, push, PR, GitHub write, fetch/prune/gc or rebase occurred.

Scoped self-review: one CI acceptance owner and one release source-CI owner;
no firmware bytes, ranges, order, integrity, support, Golden expectations,
approval principals or workflow permissions changed. Script path classification
is **R3 / release-approval-policy / release-owner + governance-owner**; the
release contract is R3/release-owner, verifier/tests/ADRs have R2 path floors.
The behavior changes required-check/release acceptance and remains R3 overall.
Local Polytail state: PASS-WITH-HUMAN-GATE, subject to exact-head independent
review and the explicit external gates below; not an integration-ready claim.

Open:

- Independent exact-head review is the next local read-only step; its final
  result belongs to the external report and does not authorize publication.
- First real Windows CI hang: actual sequence and stalled test name, measured
  elapsed/termination timing, no dump collection/upload. Owner/commander closes
  this gate; the accepted inactivity timer is not a strict per-test deadline.
- Real source-run annotation transport across workflow attempts and actual
  adapter/FQN evidence remain to be observed; offline fixtures do not prove them.
- Before integration, independent review evidence and last-push owner approval
  must name **release-owner and governance-owner**. Required CI and release
  Golden execution are not replaced by these narrow local checks.
- Known underlying test flakes remain owned by their separate bugs. No new owner
  decision or workflow change was needed, and no stop condition was triggered.

Next: inspect this committed head independently and deliver the local report;
leave push, PR, integration and publication to separately authorized work.


### 2026-09-29 fixed-head independent review and presentation limit

State: locally committed and independently reviewed; not integrated/published.
Reviewed head: `51b4caa119d18384b0d4d2d8dc7f2f0061a917ea` against the admission base.
Reviewer: fresh-session, read-only Codex gpt-6-astra, high effort, role reviewer;
no inherited author conversation. No second writer. Verdict:
**PASS-WITH-HUMAN-GATE**, no new P0/P1/P2 findings; P2-1 through P2-6 satisfied
within their recorded external evidence limits.

Fresh evidence at that head:

- Reviewer: `python -B -m unittest tests.scripts.test_ci_dotnet_retry tests.scripts.test_release_promotion_policy -q`
  -> **78 tests PASS in 51.812s**, with required user-level test-root/temp setup.
- Primary: post-commit `python scripts/verify.py --structure-only` -> **PASS**,
  structure 14.9s, including tracked bug records/checkpoint; zero derived changes.
- Reviewer independently confirmed the retained TRX hash, clean worktree and
  no workflow/decision-board change; no actual dotnet/live CI or `--all`.
- P3 platform observation: `BUG-20260929-ci-rerun-annotation-render-limit`.
  The reporter emits every requested warning command, but GitHub retains at
  most ten per step. All FQNs remain in the log/summary. The first aggregate
  warning suffices for release rejection; no concrete all-warning loss path
  in a successful aggregate was found. Contract disclosure is in this docs-only
  follow-up; it changes no production/test blob or accepted verdict.

Open: platform annotation presentation after ten warnings is documented for
commander follow-up; real CI hang/annotation/adapter evidence and both R3 owner
roles remain as above. Final docs-only exact-head review is reported externally.
No workflow change or new owner decision is necessary to complete this correction.
Next: confirm the documentation delta and deliver the report; do not push.
