# Development Execution Workflow

Status: Active repository workflow.

This runbook defines the development cadence around the canonical verifier. It does not replace `AGENTS.md`, ADRs, firmware evidence, or PR review.

## 1. Preflight

Run this once before editing a non-trivial change:

```text
git status --short --branch
```

Record existing user changes, then state the risk class, affected layers, acceptance criteria, required human gate, narrow test, and final gate. Preserve unrelated changes; do not stage or revert them.

Also record the owner-selected version integration branch. Work directly on that exact-version branch when it is tightly coupled to the release. For an independently reviewable feature, create `feature/<version>/<topic>` from it and merge back to the same version branch before the version branch opens its final PR to `main`.

## 2. Test Selection

Run the narrowest relevant command first. Add another row only when the change crosses that boundary.

| Changed surface | First test | Add when applicable |
| --- | --- | --- |
| Documentation/governance only | `python scripts/verify.py --structure-only` | Full gate if a command, fixture, schema, or executable contract changes. |
| Domain composition | `dotnet test tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj` | Application or architecture tests when a public composition contract changes. |
| Application, flash-map, profile planning, or postbuild catalog | `dotnet test tests/NvtFwCombiner.Application.Tests/NvtFwCombiner.Application.Tests.csproj` | Profile contract and golden regression tests for profile/range/output effects. |
| Profile/schema contract | `dotnet test tests/NvtFwCombiner.ProfileContract.Tests/NvtFwCombiner.ProfileContract.Tests.csproj` | Golden regression and owner review for firmware semantics. |
| Infrastructure or external Combiner adapter | `dotnet test tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj` | Approved real-tool smoke or golden test only when its evidence is in scope. |
| Bootstrap or CLI | `dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj` | Application or infrastructure tests when the request/execution boundary changes. |
| Avalonia UI/ViewModels | `dotnet test tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj` | Bootstrap tests when binding or request projection changes. |
| Architecture/dependency boundary | `dotnet test tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj` | Full gate for shared-layer or project-reference changes. |
| Golden outputs or byte semantics | `dotnet test tests/NvtFwCombiner.GoldenRegression.Tests/NvtFwCombiner.GoldenRegression.Tests.csproj` | Required firmware-owner review and full gate. |
| Python CRC worker | `python -m pytest` from `tools/crc-worker` | The C# contract tests and full gate for protocol/transform changes. |

For `R1`-`R3`, after the selected tests pass, run this once before handoff or commit readiness:

```text
python scripts/verify.py --all
```

`R0` documentation/governance changes may use `--structure-only` only when they do not alter an executable command, schema, contract, fixture, or firmware claim. Pull-request CI still executes the complete policy, Python, and .NET gates.

## 3. Autonomous Phase Commits

Every completed, independently verifiable editing phase is committed autonomously before another editing phase begins. A phase is one coherent code-and-test slice, documentation decision, evidence inventory, UI slice, or release change that can be reviewed independently.

Create the phase commit only after all of these are true:

1. The change is one reviewable concern; unrelated UI, core, release, and documentation work is separated.
2. The phase-local narrow test passes, or every skipped test has an explicit reason. The final `--all` gate remains required before `R1`-`R3` PR handoff, not before each intermediate phase commit.
3. `git diff --check` passes and the reviewed diff excludes generated outputs,
   temporary staging data, unapproved firmware/evidence, and credentials.
   Owner-approved golden replay artifacts are allowed only under
   `testdata/golden/` with path/size/SHA-256/provenance, privacy review, and the
   required R3 firmware-owner gate defined by
   `golden-fixture-retention-and-privacy.md`.
4. Risk class, human-review requirements, and residual evidence gaps are recorded.

Stage only explicit files belonging to the phase; never use `git add -A` or `git add -u`. Do not amend, reset, revert, or stage pre-existing changes from another agent. If overlapping uncommitted changes make the boundary unclear, stop and request direction rather than combining work.

Use a Conventional Commit message that identifies the phase outcome. Do not commit exploratory investigation or temporary artifacts. `R3` commits remain review-only on a non-`main` branch until the required human gate passes.

## 4. Retry Policy

The first failed command is evidence, not a reason to rerun it unchanged.

1. Record the command, exit/result, and failure class: invocation, input/evidence, assertion, or environment.
2. Retry only after changing the command, input, code, or environment; record what changed.
3. One materially changed retry is the limit. After a second failure, use a smaller diagnostic or report the blocker.
4. Do not use `python scripts/verify.py --all` as a diagnostic retry.
5. When a multi-step diagnostic repeats, replace manual shell composition with a focused tested script or test case.

## 5. Handoff

For a feature branch, provide the changed files, risk class, exact test commands/results, firmware/profile/protocol/release impact, human-review requirements, and unresolved evidence gaps for merge into its version integration branch.

Only when the version branch is complete should the final handoff target `main`. Use the pull-request template to record the version branch, source branch, target branch, phase commits, verification evidence, and unresolved gates.
