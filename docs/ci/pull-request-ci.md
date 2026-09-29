# Pull Request CI

The executable workflow is [`.github/workflows/ci.yml`](../../.github/workflows/ci.yml). Every external action is pinned to a reviewed full commit SHA.

## Required public checks

1. **`policy / polytail`** validates repository structure, forbidden tracked
   files, schemas, Markdown links, layered AGENTS, the exact skill inventory,
   skill frontmatter and Codex invocation metadata, immutable reference hashes,
   frozen evidence pins,
   action pins, version/license consistency, dependency direction, and
   canonical architecture fields.
2. **`python-worker / verify`** runs Ruff format/check, Pyright strict, Pylint, pytest, branch coverage, protocol/process tests, plus the structure gate.
3. **`dotnet / build-test`** is the stable final verdict over one complete
   Release-build producer and three closed test shards. The canonical
   verifier validates the exact eight-project/TRX/coverage manifest, reconciles
   every execution with compiled test discovery and the declared platform skips,
   and requires complete GoldenRegression execution, coverage policy, and
   CtrlRAM fixture evidence before the check passes. Test totals and the
   GoldenRegression summary come from the validated execution evidence. The
   [failure-evidence contract](#failure-evidence-and-in-job-retry) defines the
   bounded failed-FQN retry and mandatory flaky bug-record gate.

Private firmware golden regression remains an approved-runner gate once private vectors exist. It must publish reports/hashes only, never firmware payloads.

## Security rules

- Default permission is `contents: read`.
- Checkout credentials are not persisted.
- `pull_request_target` is forbidden.
- Pull-request jobs receive no release/signing secrets.
- Action references use full 40-character SHAs; mutable tags are rejected by repository validation.
- The Polytail semantic review remains required in the PR record in addition to deterministic CI checks.
- Producer artifacts contain short-lived logs/TRX/coverage and the bounded CI
  blame-hang diagnostics specified below (decisions 191 and 193). The finalizer
  rejects missing, failed, duplicate, unknown, wrong-SHA, wrong-SDK,
  path-escaping, symlinked, hash-mismatched, counter-drifted, or extra evidence.
  Producers publish from a clean allowlisted staging root, and the finalizer
  preserves each artifact name/root until ownership and collision checks pass.
  A failed test shard still uploads each failed project's discovery list and
  TRX unchanged (owner decision 41: the TRX keeps the failure messages and
  output that VSTest also writes to its `attempt.log`), and its coverage pair only
  after the same pairing and path normalization as passing evidence; coverage
  that fails them, and files outside the diagnostic allowlist, stay out. The shard's failed-test
  report in the job log and the step summary names the failed tests (from the
  TRX, without their messages) and gives a fixed reason for any omitted
  evidence; the raw omission diagnostic, which can hold runner or source
  paths, goes to `shard.log`, an allowlisted public artifact, not a
  confidential location. Two existing channels can still put raw exception
  text in the job log: the shard's combined failure message for a failed
  project (for example its failed command line) and the stderr line printed
  when the step summary cannot be written. Each producer names its artifact
  with the run attempt (`-attempt-<n>`) and records the run id and attempt in
  its manifest; the finalizer requires a complete download of every attempt
  and verifies each producer's newest one, so producers that "Re-run failed
  jobs" did not re-run keep their earlier evidence. This is pending
  verification by a real re-run; until then, after a failed run start a new
  workflow run instead of re-running failed jobs.
  Coverage paths are normalized to verified repository-relative identities
  before hashing so the finalizer never trusts runner roots;
  missing, outside, ambiguous, or normalization-colliding identities fail at
  the producer.

## Failure evidence and in-job retry

Revised 2026-09-29 by owner decisions 191 and 193 in the
[1.1.12 board](../handoff/1.1.12.md), amending
[ADR 0079 item 8](../adr/0079-test-architecture.md#8-stability-rules).
This is the active CI failure-evidence contract. The release-workflow cleanup
design is a dated dependency record, not a second owner. Implementation remains
in `scripts/verify.py`: `--ci-dotnet-test-shard` and `--ci-dotnet-finalize`.
Workflow YAML, local `--all` and release Golden verification gain no retry path.
Decision 193 additionally requires zero flaky tests in release source CI, enforced
by `scripts/release_promotion_policy.py` and the
[release contract](release-package.md#source-ci-zero-flaky-gate).

### Selection and verdict

1. Each project builds and discovers once, then executes unfiltered with
   coverage. Empty/invalid discovery fails before execution. A build or discovery
   failure is not retried: it does not identify a complete set of failed tests.
2. One job-local retry is allowed only after VSTest exits 1 and a complete,
   non-aborted TRX, unchanged assembly snapshot, full discovery reconciliation,
   admitted skips and normalized coverage pair validate. `Failed` test outcomes
   alone do not authorize retrying a test-platform error, hang or crash.
   xUnit's `RunInfo outcome="Error"` notification is allowed only when it has
   the exact timestamped `[xUnit.net ...] <case identity> [FAIL]` form and names
   an actually failed TRX case. Unknown Error diagnostics and nonzero terminal
   error/abort/timeout counters fail closed; diagnostic severity alone does not
   distinguish assertion failures from platform failures.
3. The filter ORs exact `FullyQualifiedName=<class>.<method>` equalities from
   unique TRX `TestMethod` definitions of failed tests. Display names are not
   interpolated into filters. Unsupported filter syntax, missing/ambiguous
   metadata and identity disagreement fail closed. VSTest filters methods: all
   theory rows sharing a failed FQN are selected, including passing sibling rows.
   Only definitions belonging to failed FQNs (including their passing theory rows)
   require retry metadata validation; unrelated passing definitions do not veto
   selection. No other method is rerun. At most 20 distinct FQNs may be retried;
   more fails with `too many failures for a flaky retry`, without attempt 2.
   Retry evidence must contain exactly those original
   case identities, with no additional, missing, skipped or failed rows.
4. A validated passing retry makes the project/shard successful, with each
   recovered FQN recorded as flaky. A second failure, nonzero retry exit, host
   error, timeout, hang/crash attachment, malformed evidence or snapshot drift
   fails the shard; there is no third attempt. Other ordinary project failures
   still allow later projects to run. Runner timeout/cancellation still stops
   the runner through the existing process owner.
5. The finalizer independently checks both attempts and recomputes the flaky
   list before admitting a successful shard. Effective counters count each
   originally discovered case once, replacing a failed initial outcome only
   with its validated passing retry. `NvtFwCombiner.GoldenRegression.Tests` is
   always excluded from retry: any initial failure fails the shard and finalizer.
   Its totals remain first-attempt execution counters. Coverage policy uses the
   first complete run's paired reports only; attempt 2 has **no Coverlet collector
   or instrumentation**. An instrumentation-dependent failure may therefore be
   labelled flaky, not fixed; the bug gate still applies and release is blocked.
   Its smaller selection cannot replace or inflate the full-run denominator.
6. Retry admission uses a monotonic 25-minute budget from the shard verifier's
   entry (including restore/build/discovery and preceding projects), reserving
   five minutes of the 30-minute job for external setup, cleanup and upload.
   Any active verifier lane deadline further reduces that budget. Less than
   420 seconds remaining (five-minute inactivity timer plus two-minute margin)
   refuses retry with `insufficient shard time for a flaky retry: requires 5
   minutes plus 2 minutes margin`. The original failure stays failed. This is
   admission protection, not a per-test deadline or a guarantee when external
   setup exceeds the reserved time or concurrent tests keep resetting the timer.

### Attempt and artifact contract

Schema version 4 retains run id, workflow `runAttempt`, source SHA, SDK,
producer platform, project inventory and the exact `files` SHA-256 map.
Workflow-attempt artifact names and latest-producer selection are unchanged.
Version 3 manifests are rejected by the version-4 finalizer; producers and
finalizer must run the same candidate contract. Job-local retry does not resolve
the pending real-workflow-re-run gate described above.

Each project writes under `shards/<shard>/results/<project>/attempt-1/`:
`discovered-tests.txt`, unchanged `test-results.trx`, `attempt.log`, and its
normalized JSON/Cobertura pair. A retry writes a distinct `attempt-2/` containing
unchanged `test-results.trx` and `attempt.log`. Both attempts are kept even when
the retry fails. Build, restore, SDK and cleanup diagnostics stay in `shard.log`.

The project row retains its initial counters and `discovery`, `trx`,
`coverageJson`, `coverageCobertura`, `testAssemblySha256` and `relativePath`.
It adds `attemptLog` and `retry`. `retry` is `null` without a retry, or the exact
object `{ "filter": "FullyQualifiedName=...", "trx": ".../attempt-2/test-results.trx",
"log": ".../attempt-2/attempt.log" }`. The shard adds `flakyTests`, an ordered
array of `{ "project": "tests/...csproj", "fullyQualifiedName": "Namespace.Type.Method" }`,
in project order and sorted FQN order within each project. An empty array means
no admitted recovered test. Failed projects may lack an admitted project row;
their available evidence remains in `files` and their diagnostics in the summary.

Successful aggregation rejects altered filters, counters, flaky lists, missing
attempt logs/TRX, unexpected files, invalid hashes, path escapes, duplicate
ownership and non-regular entries. A successful artifact never contains hang
attachments. The existing always-upload step carries both attempts in the same
producer artifact with three-day retention. No workflow, permission or separate
artifact channel is required.

### Hang diagnostics

Every CI test execution enables VSTest's
`--Blame:CollectHangDump;TestTimeout=5m;HangDumpType=None`. `CollectHangDump`
activates hang detection; `HangDumpType=None` disables memory-dump collection.
A detected hang ends the host and fails the project without retry. Available
sequence, TRX and attempt log enter the existing producer artifact. The five-minute
timer is VSTest's test-progress/inactivity timer, not a separate wall-clock timer
for each concurrent test; progress resets it and termination adds time. Decision
193 accepts this distinction. The **first real Windows CI hang** remains a closure
gate: retain the sequence file, stalled test identity, attempt log with observed
elapsed time, timer/termination behavior, and proof that no dump was collected or
uploaded. Fake-runner tests certify command and allowlist behavior only. Sequence
identities and log timing are evidence, not an invented per-test duration field.

Only nonempty regular filenames matching exactly
`re.fullmatch(r"Sequence_[0-9a-fA-F]{32}\.xml", name)` are admitted, each at most
1 MiB and at most eight per attempt. For example,
`Sequence_af6bc426faab481fa504c42e3d52afd2.xml` is valid; `host_Sequence.xml` is not.
Symlinks, junctions and other non-regular entries reject the diagnostic tree.
Empty, oversized or excessive sequences are omitted with the fixed
`hang attachments not uploaded: attachment validation failed` reason and a detailed
diagnostic in `shard.log`; independently valid TRX/log/discovery/coverage remain.
Attachment rejection never makes a failed project pass. Memory dumps, including
unexpected `.dmp` files, are never staged or uploaded. No sequence is fabricated
when VSTest produces none; available logs/TRX remain failed-run evidence.

Decision 191 implied a CI-only exception to ADR 0027's single-execution rule;
decision 193 defines its bounds and supersedes the interim dump choice. CI evidence
**still never contains firmware payloads**. Only sequence diagnostics extend the
logs/TRX/coverage allowlist. Any necessary memory dump must be reproduced locally;
there is no private-Golden or release-artifact upload exception.

### Flaky bug records and merge gate

Both shard and aggregate emit every recovered identity in the log/summary and as
`::warning title=Flaky test::<project> <FQN>` (workflow-command data is escaped).
GitHub's runner retains at most ten warnings per step, so emitting all commands
is not proof that all twenty possible FQNs receive separate PR annotations.
The complete list remains in the log and job summary. The successful aggregate
has no preceding warning producer; its first `Flaky test` annotation is sufficient
to block release. This platform display limit and the pinned runner evidence are
tracked in [the annotation-limit bug](../handoff/bugs/BUG-20260929-ci-rerun-annotation-render-limit.md).
On producer failure, the finalizer chooses the producer-failed error first, attempts
flaky diagnostics best-effort as **unverified**, then always raises that original
producer failure. Missing artifacts, invalid JSON or a failed download cannot mask it.

Before coverage acceptance, the finalizer requires each recomputed flaky FQN to
appear in at least one checkout file matching `docs/handoff/bugs/BUG-*.md`.
The rule is **case-sensitive full FQN only**, bounded on both sides by the absence
of `[A-Za-z0-9_.+]`; Markdown backticks and punctuation other than a dot delimit
it. Thus `Probe.Tests.Case1` does not match `Probe.Tests.Case10` or
`OtherProbe.Tests.Case1`. Separate class/method mentions do not qualify. Missing
records fail with `flaky test has no bug record` naming the project and FQN.
A bug added only in a remote issue, another branch or after checkout cannot pass
this gate. The verifier never writes bug records or calls GitHub.

The commander still links each identity, workflow run and job-local attempts to
its bug record in the review evidence. A passing retry does not fix or close the
bug. Independent exact-head review and applicable owner approval remain required.
Release source CI rejects any flaky observation even if its bug file exists.
