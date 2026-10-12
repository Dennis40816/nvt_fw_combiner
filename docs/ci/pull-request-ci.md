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
   producer reconciles every execution with compiled discovery and the declared
   platform skips. The finalizer validates the exact eight-project summary
   inventory, the three shard floors and paired product coverage,
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
- Test-shard publication follows the public artifact contract below. Build
  evidence and other CI lanes retain their existing contracts.

## Public artifact contract

The 2026-10-11 owner decision supersedes public raw-evidence upload. Each test
shard stages only a schema-5 summary manifest, a schema-1 shard summary, and
validated Coverlet JSON/Cobertura pairs for public product code. Project rows
contain the project identity, assembly hash, final counters, a boolean retry
receipt with recovered-case count, and parameter-free failed/flaky methods.
The closed file inventory carries SHA-256 hashes. Old schemas, extra files,
invalid hashes, inconsistent counters, failed/skipped outcomes and below-floor
shards are refused. All three shard summaries must identify the same source,
run and current workflow attempt. Re-run all shard producers or start a new
complete workflow run; earlier shard attempts cannot supply current evidence.

Raw results, discovery, logs, retry filters, assertion messages, captured output
and hang/crash attachments stay on the producer runner within its existing
local evidence lifecycle. They never enter the public upload tree. Coverage
that can carry test names or messages is refused. Failed/flaky reports contain
counters and parameter-free methods only. Reproduce detailed failures locally.
Test-process console output is scrubbed whenever `GITHUB_ACTIONS=true`, which a
GitHub-hosted runner always sets, and on a local run that sets
`NFC_CI_SCRUB_TEST_OUTPUT=1`. A local run without the flag keeps its console
output. The workflow may also set the flag on the producer and finalizer steps;
the result is the same. Artifact enforcement is
unconditional. Build artifacts and other lanes are
outside this contract. Existing job-result and download-outcome precedence,
coverage policy and flaky bug-record gates remain in force.

## Failure evidence and in-job retry

Revised 2026-09-29 by owner decisions 191 and 193 in the
[1.1.12 board](../handoff/1.1.12.md), amending
[ADR 0079 item 8](../adr/0079-test-architecture.md#8-stability-rules).
The public artifact contract above amends the evidence transport; the producer
retry and hang rules below remain active. The release-workflow cleanup
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
5. The producer checks both attempts and projects the final outcome and flaky
   methods. The finalizer validates the closed receipt and shard summary before
   admitting a successful shard. Effective counters count each
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

The raw attempt layout, full discovery reconciliation and exact retry selection
remain on the producer. A passing retry changes only previously validated
outcomes in the summary view and never inflates the complete-run denominator.
The public manifest retains source/run/attempt/SDK/platform identity and the
closed project inventory; it carries no raw-result path, log path, filter or
message. Each public product coverage pair projects the canonical normalized
first-attempt pair: non-production modules/sources and generated `obj`/`bin`
sources are removed, including empty Cobertura packages. The retained identities
and numeric payload are validated; root line totals and package rates describe
the retained classes, and report-wide branch totals describe the retained JSON
branches. The production aggregate equals the base finalizer's
aggregate of the raw pair. The finalizer aggregates the projected pair into the
existing coverage output and runs the existing coverage policy.

As the accepted summary-only transport trade-off, the finalizer no longer
recomputes flaky methods or discovery reconciliation from raw files. It validates
the producer receipt instead, including nonempty recovery/flaky declarations
for a successful retry and the unconditional GoldenRegression no-retry rule.
Names outside the public method grammar are omitted from failed/flaky lists;
the corresponding cases remain in outcome and recovered-case counters.

The runner-local schema-4 manifest records test execution before public
projection. If execution passed but projection fails, it retains `success: true`
while a produced public manifest reports `success: false`. If no valid public
projection can be produced, the upload tree is cleared. Late local diagnostics
refresh the raw file hashes without changing that original execution receipt.
If that refresh fails, the producer keeps the primary failure type, reports a
fixed diagnostic and clears public staging; the unavailable local hash refresh
cannot be represented as successful evidence publication.

Publication refuses unexpected staged files and clears a refused staging tree,
including when the always-upload workflow would otherwise run after failure.
The existing upload root and three-day retention need no change to enforce
summary-only artifacts. Workflow and template upload paths must retain this
summary-only shard artifact contract.

### Hang diagnostics

Every CI test execution enables VSTest's
`--Blame:CollectHangDump;TestTimeout=5m;HangDumpType=None`. `CollectHangDump`
activates hang detection; `HangDumpType=None` disables memory-dump collection.
A detected hang ends the host and fails the project without retry. Available
sequence, raw results and attempt logs stay on the producer runner. The five-minute
timer is VSTest's test-progress/inactivity timer, not a separate wall-clock timer
for each concurrent test; progress resets it and termination adds time. Decision
193 accepts this distinction. The **first real Windows CI hang** remains a closure
gate: retain the sequence file, stalled test identity, attempt log with observed
elapsed time, timer/termination behavior, and proof that no dump was collected or
uploaded. A GitHub-hosted runner is deleted after the job and the public artifact
carries none of this evidence, so it comes from a local reproduction or from a run
on a runner that the owner controls. Fake-runner tests certify command and
allowlist behavior only. Sequence identities and log timing are evidence, not an
invented per-test duration field.

Two further closure gates need the first real CI runs. First, the release
zero-flaky gate reads flaky annotations and has no positive count signal yet, so
a missing annotation cannot be told from zero flaky tests; a count notice on
`dotnet / build-test` is deferred until real annotation behavior is observed.
Second, if real VSTest writes a Sequence file on ordinary failures, the hang rule
refuses every retry (fail-closed); the first real ordinary-failure-plus-retry run
must show which case applies. A flaky test is accepted only when an open bug
record names it: a record whose `Status:` starts with fixed, closed, wontfix or
duplicate must be reopened.

The existing bounded regular sequence admission still applies on the runner:
nonempty attachments are at most 1 MiB each and at most eight per attempt.
Invalid attachments remain omitted with a fixed reason and a runner-local
raw diagnostic. Reparse/non-regular entries still reject the diagnostic tree.
Only a nonempty regular file named exactly
`re.fullmatch(r"Sequence_[0-9a-fA-F]{32}\.xml", name)` is admitted; symlinks,
junctions and other non-regular entries reject the diagnostic tree. A rejected
attachment never makes a failed project pass. Neither sequence attachments nor
memory dumps are staged for public upload. The first real Windows hang and
ordinary-failure/retry observations remain human evidence gates.

Decision 191 implied a CI-only exception to ADR 0027's single-execution rule;
decision 193 defines its bounds and supersedes the interim dump choice. CI evidence
**still never contains firmware payloads**. Any necessary memory dump must be
reproduced locally. There is no private-Golden or release-artifact upload
exception.

### Flaky bug records and merge gate

Both shard and aggregate emit every recovered identity in the log/summary and as
`::warning title=Flaky test::<FQN>` using parameter-free method identities. A
recovered test whose name is outside the public set (a generic class, a non-ASCII
identifier) is not listed. The shard checks such a name against the open bug
records itself and fails when none names it. If that name is the only recovered
name of its project, the closed receipt would have an empty flaky list, and the
shard fails as well.
GitHub's runner retains at most ten warnings per step, so emitting all commands
is not proof that all twenty possible FQNs receive separate PR annotations.
The complete list remains in the log and job summary. The successful aggregate
has no preceding warning producer; its first `Flaky test` annotation is sufficient
to block release. This platform display limit and the pinned runner evidence are
tracked in [the annotation-limit bug](../handoff/bugs/BUG-20260929-ci-rerun-annotation-render-limit.md).
On producer failure, the finalizer chooses the producer-failed error first, attempts
flaky diagnostics best-effort as **unverified**, then always raises that original
producer failure. Missing artifacts, invalid JSON or a failed download cannot mask it.

Before coverage acceptance, the finalizer requires each validated summary flaky FQN to
appear in at least one checkout file matching `docs/handoff/bugs/BUG-*.md`.
The rule is **case-sensitive full FQN only**, bounded on both sides by the absence
of `[A-Za-z0-9_.+]`; Markdown backticks and punctuation other than a dot delimit
it. Thus `Probe.Tests.Case1` does not match `Probe.Tests.Case10` or
`OtherProbe.Tests.Case1`. Separate class/method mentions do not qualify. Missing
records fail with the fixed sentence `Flaky test has no open bug record.`
A bug added only in a remote issue, another branch or after checkout cannot pass
this gate. The verifier never writes bug records or calls GitHub.

The commander still links each identity, workflow run and job-local attempts to
its bug record in the review evidence. A passing retry does not fix or close the
bug. Independent exact-head review and applicable owner approval remain required.
Release source CI rejects any flaky observation even if its bug file exists.
