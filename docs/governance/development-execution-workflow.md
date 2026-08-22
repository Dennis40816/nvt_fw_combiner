# Development Execution Workflow

Status: Active repository runbook.

## Preflight

Before a non-trivial edit, run `git status --short --branch`, preserve existing
user changes, and record risk, affected authority/layers, acceptance criteria,
human/evidence gates, narrow test, final gate, integration base, and owned
mutable surfaces. Read the relevant source, contract/profile, and test once.

Branch/version/release rules live in
[`branch-version-and-release-governance.md`](branch-version-and-release-governance.md).

## Capability-reuse gate (fail closed)

Production edits must not begin until the active specification, ticket, or
owner-approved handoff contains a capability-reuse record. Diagnostic reads,
tests that characterize existing behavior, and planning may proceed while the
record is incomplete; new or changed production logic may not.

The record must name all of the following:

1. the requested capability and every target or adjacent module/layer;
2. the search evidence used to inspect those modules: CodeGraph when the
   repository is indexed, otherwise exact symbol/text searches, project
   references, callers, ports/adapters, and existing tests;
3. the current semantic owner, typed result, complete production caller path,
   adapter/projection boundary, and test owner, or `none-found` plus the exact
   searched scope and commands;
4. the proposed owner, dependency direction, reused contracts/results, and
   affected firmware, persistence, UI, or release authority;
5. explicit checks for an existing validator, parser, normalizer, formatter,
   resolver, planner, compiler, service, cache, store/repository, processor,
   naming rule, control/style, fallback, and compatibility path; and
6. one disposition: `reuse`, `extend-owner`, `delete-then-replace`,
   `reject-duplicate`, or `approved-migration-seam`.

Unknown, unsearched, or conflicting ownership fails the gate. When an owner
already exists, the change reuses or extends it. An `approved-migration-seam`
requires the architecture owner, a complete caller inventory, a narrow test
that distinguishes old and new paths, and an executable deletion milestone;
it cannot acquire new semantic authority. R2/R3 work records the independent
architecture/contract review before implementation begins.

The gate is not satisfied by renaming a duplicate, moving it to another layer,
adding an interface around it, or demonstrating only that new tests pass.

## Narrow test selection

| Changed surface | First test |
| --- | --- |
| Documentation, agent config, governance | `python scripts/verify.py --structure-only` |
| Domain | `dotnet test tests/NvtFwCombiner.Domain.Tests/NvtFwCombiner.Domain.Tests.csproj` |
| Application | `dotnet test tests/NvtFwCombiner.Application.Tests/NvtFwCombiner.Application.Tests.csproj` |
| Profile/schema | `dotnet test tests/NvtFwCombiner.ProfileContract.Tests/NvtFwCombiner.ProfileContract.Tests.csproj` |
| Infrastructure/process adapter | `dotnet test tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj` |
| Bootstrap/CLI | `dotnet test tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj` |
| Avalonia/ViewModels | `dotnet test tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj` |
| Architecture/dependencies | `dotnet test tests/NvtFwCombiner.Architecture.Tests/NvtFwCombiner.Architecture.Tests.csproj` |
| Golden/byte semantics | `dotnet test tests/NvtFwCombiner.GoldenRegression.Tests/NvtFwCombiner.GoldenRegression.Tests.csproj` |
| CRC worker | `python -m pytest` from `tools/crc-worker` |

Add broader tests only when the change crosses that boundary. Run
`python scripts/verify.py --all` once on the frozen R1-R3 candidate. A true R0
documentation-only change may finish locally with `--structure-only`.

## Review checkpoints

Commit at stable review checkpoints. Each commit must be coherent, tested, and
recoverable; documentation, tests, and review corrections that belong to the
same outcome need not become separate ceremony commits. Stage only explicit
owned files. Never stage, reset, amend, or revert another agent's changes.

Before handoff: format changed files, run the narrow test, inspect the exact
diff, apply scoped Polytail, run the final gate, and record residual evidence.

## Recurring specification conformance audit

During the `0.10.x` restructuring program, perform a repository-wide
conformance audit after every three tickets merged into the integration branch
or at the end of a dependency block, whichever happens first. A firmware-
semantic R3 ticket also audits its affected authority before owner review even
when the global cadence has not yet been reached.

The audit is bidirectional:

1. For every current canonical requirement in `SPEC.md`, accepted ADRs,
   contracts, schemas, profiles, and owner-approved evidence, identify the
   implementing production owner and executable test or record a named,
   ticketed gap.
2. For every new or changed production behavior, profile fact, validation,
   issue code, processor range, and golden claim, identify exactly one current
   canonical authority. Historical evidence may explain a fixture but cannot
   silently become runtime policy.
3. Run repository structure/contract validation and the affected architecture,
   profile, golden, and behavior tests. Classify rather than conceal any
   deferred mismatch: `fixed-now`, `allocated-to-ticket`, `blocked-evidence`,
   or `obsolete-authority`.
4. Record the audited integration commit, authority inventory, commands,
   findings, dispositions, and next audit trigger in the corresponding
   program/ticket document or PR evidence. A green verifier is supporting
   evidence, not a substitute for the bidirectional review.

Do not broaden the current ticket merely to close an unrelated allocated gap.
Confirmed contradictions in authority or executable behavior must be fixed or
explicitly block integration.

## Retry policy

- Never rerun the same command with the same environment and inputs unchanged.
- A new hypothesis, instrumentation, input, code, or environment change permits
  another run; record what changed.
- After two or three meaningful attempts at the same failure class without
  progress, narrow the diagnostic or report the common blocker.
- Never use `python scripts/verify.py --all` as a repeated diagnostic command.
- Turn recurring multi-step diagnostics into a focused tested script or test.

## Handoff

Report outcome, changed files, risk, exact commands/results, firmware/profile/
protocol/release impact, required reviewers, and unresolved evidence. PR bodies
record outcomes and gates; they do not reproduce a complete agent execution log.
