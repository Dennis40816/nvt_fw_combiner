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
   GoldenRegression summary come from the validated execution evidence.

Private firmware golden regression remains an approved-runner gate once private vectors exist. It must publish reports/hashes only, never firmware payloads.

## Security rules

- Default permission is `contents: read`.
- Checkout credentials are not persisted.
- `pull_request_target` is forbidden.
- Pull-request jobs receive no release/signing secrets.
- Action references use full 40-character SHAs; mutable tags are rejected by repository validation.
- The Polytail semantic review remains required in the PR record in addition to deterministic CI checks.
- Producer artifacts contain short-lived logs/TRX/coverage and the bounded CI
  blame-hang diagnostics specified below (decision 191). The finalizer
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

Revised 2026-09-29 by owner decision 191 in the
[1.1.12 board](../handoff/1.1.12.md), amending
[ADR 0079 item 8](../adr/0079-test-architecture.md#8-stability-rules).
This is the active CI failure-evidence contract. The release-workflow cleanup
design is a dated dependency record, not a second owner. Implementation remains
in `scripts/verify.py`: `--ci-dotnet-test-shard` and `--ci-dotnet-finalize`.
Neither workflow YAML, local `--all`, nor release Golden verification gains a
retry path or changed release evidence rules.

### Selection and verdict

1. Each project builds and discovers once, then executes unfiltered with
   coverage. Empty/invalid discovery fails before execution. A build or discovery
   failure is not retried: it does not identify a complete set of failed tests.
2. One job-local retry is allowed only after VSTest exits 1 and a complete,
   non-aborted TRX, unchanged assembly snapshot, full discovery reconciliation,
   admitted skips and normalized coverage pair validate. `Failed` test outcomes
   alone do not authorize retrying a test-platform error, hang or crash.
3. The filter ORs exact `FullyQualifiedName=<class>.<method>` equalities from
   unique TRX `TestMethod` definitions of failed tests. Display names are not
   interpolated into filters. Unsupported filter syntax, missing/ambiguous
   metadata and identity disagreement fail closed. VSTest filters methods: all
   theory rows sharing a failed FQN are selected, including passing sibling rows.
   No other method is rerun. Retry evidence must contain exactly those original
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
   with its validated passing retry. GoldenRegression totals follow these same
   CI execution counters. Coverage policy uses the first complete run's paired
   reports only; attempt 2 collects no coverage. Its smaller selection must not
   replace or inflate the full-run coverage denominator.

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
`--Blame:CollectHangDump;TestTimeout=5m;HangDumpType=Mini`. A detected hang ends
the host and fails the project without retry; available dump, sequence, TRX and
attempt log enter the existing producer artifact. The five-minute timer is
VSTest's test-progress/inactivity timer, not a separate wall-clock timer for
each concurrent test. Dump collection and process termination take additional
time. Command-level tests cannot certify the actual Windows adapter's hang
latency or dump creation; that remains real CI evidence to obtain before
claiming the strict five-minute per-test target is achieved.

Only nonempty regular `.dmp` files of at most 256 MiB and `*_Sequence.xml` files
of at most 1 MiB are admitted, at most eight attachments per attempt. Symlinks,
junctions and other non-regular entries reject the attempt's diagnostic tree.
Empty, oversized or excessive attachments reject its collection and add the
existing fixed omission reason plus a detailed diagnostic in `shard.log`;
they never make a failed project pass. Other files are not copied. No dump is
fabricated when VSTest fails before creating one: that remains a failed run
with the available log/TRX evidence.

Decision 191 explicitly replaces ADR 0079 U1's previous no-dump choice and the
CI-only logs/TRX/coverage allowlist of ADR 0027. Mini dumps reduce collection;
they are not sanitized and may contain process memory. These are public,
short-lived CI diagnostics: private fixtures and secrets must not enter these
jobs. This grants no release artifact or private-Golden upload exception.

### Flaky bug records and merge gate

Both shard and aggregate emit every recovered identity with an explicit
**bug record required before merge** warning. On a failed producer job, the
finalizer reports available latest-producer flaky declarations as **unverified**
diagnostics and still fails; they are not a validated aggregate success.

Minimum enforcement uses the existing commander PR procedure, without GitHub
writes from the verifier: before merge, the commander lists each shard/FQN,
its workflow run and job-local attempts, and its matching
`docs/handoff/bugs/BUG-*.md` file in the PR evidence. Reuse a matching known bug
or create a record for the newly observed failure. Missing mappings block merge.
The summary is a warning, not proof that a file exists; CI success alone cannot
satisfy this procedural gate. A passing retry never fixes or closes that bug.
Required failing checks still block merge, and independent exact-head review
and applicable owner approval remain required.
