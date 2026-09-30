# BUG-20260928-b2-joint-recovery-entry-health-unavailable: Bootstrap recovery entry gate fails

Status: deferred to `1.2.1` C01-1 (policy) and `1.2.6` C01-2 (bounded response with cold/warm evidence), decision 188 Q2 (pre-existing cold-start health deadline; board decision 160)
Severity: P2
Found: 2026-09-28, Codex (codex/gpt-6-astra), while running ADR 0077 B2 joint gates,
at feature/1.1.13/b2a-prebuilt-catalog@8e74ece67.
Where: tests/NvtFwCombiner.Bootstrap.Tests/ManagedDistributionLauncherHostServicesTests.cs:95
Observed: RecoveryEntryExposesSessionBoundToTheExactEntryRoot returns
HealthUnavailable instead of RecoveryRequired. The complete Bootstrap run had
2,083 passing tests and this one failure; the isolated test also failed.
Expected: The existing test contract requires RecoveryRequired and a recovery
session bound to the entry's exact managed root.
Evidence: Release `dotnet test` with `--no-restore` failed; selecting the exact
test with `--no-build --no-restore --filter` reproduced the same assertion.
Logs and TRX are retained under the test-area evidence directory
`v1113-b2-joint-8e74ece67` (`Bootstrap` and `Bootstrap-isolated-recovery`).
Baseline evidence (2026-09-28): a throwaway detached worktree at `8f5223860`
was restored in locked mode using only installed local packages. The command
`dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj
-c Release --no-restore --disable-build-servers -p:BuildInParallel=false
--filter FullyQualifiedName~RecoveryEntryExposesSessionBoundToTheExactEntryRoot`
failed 1/1 with the same expected/actual result (655 ms reported test time).
The test-area evidence directory `v1113-b2-blocker-fixes` retains
`base-restore.log`, `base-recovery.log` and `base-recovery.trx`.
Owner: the Launcher tranche (board decision 160, 2026-09-28): not pursued in 1.1.13 or 1.1.14 because `1.2.0` is expected to ship as a single executable. Local runs of this test can fail on a cold start; CI passes.
Update, 2026-09-30: that premise did not hold, since v1.2.0 ships the Launcher as its own five-asset set
(`CHANGELOG.md` [1.2.0], Downloads and integrity). Decision 188 Q2 schedules the cold-health work: `1.2.1` C01-1
decouples the health cutoff from `ProgressDelay` and `1.2.6` C01-2 delivers the bounded response with cold and warm
evidence ([1.2.x allocation](../1.1.14/1.2.x-allocation.md)).
Initial disposition: Classified pre-existing relative to B2; no product or assertion
change, per the owner's base-failure stop rule. One baseline test run; no B2
bisect. The detached baseline worktree was removed after collecting evidence.
The host uses the coordinator's default 250 ms health-observation deadline.
This is plausibly another deadline-sensitive test, but the exact failing
health path is not established. It is not proven identical to
`BUG-20260928-launcher-admission-deadline-test-flake`, whose different test
uses a 25 ms admission deadline and expects `TerminationUnconfirmed` after
process creation. The Bootstrap joint gate remains blocked. Consequently the
optional VERSION=1.1.13 package/smoke run was not run.

## Focused diagnosis, 2026-09-28

Scope: owner-authorized diagnosis at `da72473d8`, with at most three detached
historical test runs; test-only/local correction permitted only if justified.
No product fix, deadline change, assertion change, or independent review is
claimed. No GitHub access or full verifier was used.

### Confirmed branch and timing

A temporary `NFC-DIAG` exception at `ManagedLauncherEntryCoordinator.Result`
recorded the outcome, elapsed time, observation stages, and caller stack. At
`47e01ebab`, it reached the health-observation cancellation catch at original
`ManagedLauncherEntry.cs:543-550`: `healthDeadline.IsCancellationRequested`
was true while the admission deadline and caller token were not cancelled.
The result was `HealthUnavailable`, with no managed root, at **262.0998 ms**.
This was not the start-issue, admission-outcome, or post-admission cleanup path.

The stage trace was:

- payload admission started at 4.0881 ms;
- payload admission succeeded (`Issue=None`) at 253.0419 ms;
- the explicit state store returned `Missing` at 255.1551 ms;
- root observation had been entered, but no root result was recorded before
  cancellation won.

The coordinator source blob is identical at `47e01ebab` and `da72473d8`
(`4c4e30ff22a00674eb8f1b118c33318a337729d4`). The embedded schema implementation
is also identical at both sources
(`354efd8df9477cafcb84fbc0e3c84b2f9c96b1fc`). The exact branch was observed
directly at the historical source, not retroactively inferred from the old
B2 assertion log.

### Machine-dependent input and limits

The selecting input is elapsed wall-clock time for first-use payload
projection within the real **250 ms** budget. On this machine the observed
payload work alone consumed about 249 ms in the traced failure. The payload
descriptor and Bootstrap resource are in-memory test streams; state is an
explicit missing scratch file. No Bootstrap process was started.

At `da72473d8`, instrumented isolated observations also completed within the
budget (215.9514 ms and 179.5141 ms). A controlled same-host cold/warm probe
measured 149.2413 ms on the first observation, then 0.7701 ms and 0.4481 ms.
The first schema load/evaluation measured 26.9066/8.0845 ms; subsequent schema
loads were below 0.001 ms and evaluations about 0.12 ms. These values establish
a large first-use cost in the payload path; they do **not** attribute all of it
to schema loading, JIT, CPU contention, disk, or antivirus. Those contributions
were not separately profiled.

The original claim of deterministic local failure did not hold throughout
this session: the initial instrumented isolated test and the entire host test
class passed (1/1 and 18/18). Later timing probes deliberately throw even for
`RecoveryRequired`; their failed test status is diagnostic output, not another
product failure. The retained historical unmodified tests did fail normally.

The test runtime retained
`NvtFwCombiner.LocalState.CurrentUserFolderForbidden=true`. Inspection confirms
that the internal host factory uses the supplied `statePath`; `GetDefaultPath`
is not on this route. The factory's environment reader is explicitly `_ => null`.
No process-start/executable-policy branch was reached. The inspected
`DOTNET_TieredCompilation`, `DOTNET_TieredPGO`, `DOTNET_TC_QuickJit`,
`DOTNET_PROCESSOR_COUNT`, `COMPlus_TieredCompilation`, `COMPlus_TieredPGO`,
`COMPlus_GCStress`, and `DOTNET_gcServer` overrides were unset. This is not a
claim that every environment variable was audited.

The owner reports a CI pass on PR #476 at `86acc35c7` (base `4689ddf22`).
Process-wide schema/JIT warm-up and different execution speed can explain why
the same threshold-sensitive test passes in a shard, but CI stage timings and
test order were not retrieved, so that specific explanation remains unproven.

### Historical bracket and retained evidence

Exactly three test invocations ran in detached test-area worktrees:

| Source | Change | Result |
| --- | --- | --- |
| `47e01ebab` (1.1.13 batch 3) | None | Failed 1/1: expected `RecoveryRequired`, actual `HealthUnavailable`; 624 ms test duration. |
| `47e01ebab` | Temporary branch/stage probe only | Health deadline catch confirmed; 262.0998 ms coordinator elapsed. |
| `30b17e699` (v1.1.12 release source) | None | Built offline; failed 1/1 with the same expected/actual values; 981 ms test duration. |

Both worktrees were removed after evidence capture. Restore used only the
already installed local package directory, an external NuGet configuration
with empty package/audit sources, `--locked-mode`, and `-p:NuGetAudit=false`.
Production project restores included their locked `win-x64` runtime identifier;
test project restores did not. No packages were downloaded or installed, and
no lock files changed. An initial longer checkout path exceeded Windows path
limits; the successful detached checkouts used shorter test-area paths with
command-local `core.longpaths=true`.

All builds/tests set `NFC_TEST_AREA_ROOT` from its user-level value and set
`TEMP`, `TMP`, and `TMPDIR` to its existing `temp` child. The test command was:

```text
dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj
  -c Release --no-restore --disable-build-servers -p:BuildInParallel=false
  --filter FullyQualifiedName~RecoveryEntryExposesSessionBoundToTheExactEntryRoot
```

TRX/logger options wrote evidence under
`<NFC_TEST_AREA_ROOT>/v1113-recovery-health-diagnosis/`: `batch3-test.log`,
`batch3-probe.log`, `v1112-test.log` and their TRX files; `branch-probe`,
`class-probe`, `timing-probe`, `timing-minimal`, and `cold-warm-probe` logs/TRX;
offline restore logs/configuration; and the two temporary probe patches.
The timing-minimal probe used `--no-build`; the class probe selected
`ManagedDistributionLauncherHostServicesTests`. All temporary source and test
instrumentation was restored before committing this record.
The restored, uninstrumented `da72473d8` source was rebuilt and the same
isolated test passed 1/1 (`restored-head.log` / `restored-head.trx`, 464 ms
runner-reported duration). That pass neither fixes nor disproves the traced
deadline failure. `git diff --check` and the affected ADR link check passed;
only this bug record remains changed.

### Classification and disposition

**Pre-existing product-path cold-start timing risk; no fix, product decision
needed.** [ADR 0062](../../adr/0062-first-run-managed-setup.md) explicitly places
descriptor projection, state load, and root observation inside one hard
250 ms budget. The real production coordinator and payload adapter can consume
that budget before routing to recovery. Warming the schema in this test or
relaxing its deadline would conceal that behavior without resolving it.
Packaged Launcher performance on a real installation was not tested here;
this finding does not claim that every shipped startup fails.

The commander/launcher owner must decide the authorized response to this
production-path timing evidence. The existing Bootstrap failure remains an
open integration gate. This diagnosis does not supply an independent review,
a full-suite pass, or release approval.
