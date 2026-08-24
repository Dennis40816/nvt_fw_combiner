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

Before adding, changing, moving, wrapping, splitting, replacing, or refactoring
production behavior, a semantic branch, or an owner contract, the active
specification, ticket, or owner-approved handoff must contain a staged,
`design-active` [capability-reuse record](capability-reuse-record.md).
The record is parsed from its real staged Git blob; intent-to-add and any
index/worktree byte mismatch fail closed. Diagnostic reads, characterization
tests, and planning may proceed while it is incomplete; new or changed
production behavior may not. Unknown, unsearched, or conflicting
ownership fails the gate. Renaming, relocating, or wrapping a duplicate does
not satisfy it. R2/R3 work records the independent
architecture/contract admission before implementation begins. The frozen exact
candidate then receives the risk-appropriate independent review. Before the
evidence commit, every admitted record becomes `final-complete`, binds the same
`implementationHead` and `reviewedHead`, records the committed path-state
digest and final-review evidence, and passes the repository validator. Each
task must still exist as `design-active` at that reviewed head; finalization
preserves all admitted design fields and changes only lifecycle/final-evidence
fields. R3 continues to require its existing external firmware-owner or
release-owner gate and authority-specific evidence; schema v2 cannot satisfy
that authority. Committed final records are immutable archives and never open
a later batch. The final evidence commit is the direct child of the reviewed
head, changes no governed path, and advances the only valid checkpoint. Every
later batch binds that checkpoint exactly, including committed changes; the
validator never substitutes a worktree-only `git diff HEAD`. Complete Git
history is a prerequisite for this validation.

An initial checkpoint is not self-authorized by a record. Until its existing
owner authority accepts the trusted initial boundary, the validator reports a
pending-checkpoint error and finalization is blocked. A `final-complete` record
also cannot satisfy R3 firmware-owner, release-owner, golden/byte, exact-range,
signing, permission, or protected-environment gates; those existing authorities
remain mandatory.

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

For governed work, create implementation commits first, review the exact frozen
implementation head, then populate and commit the `final-complete` records as a
separate evidence checkpoint. An intermediate commit that still contains a
`design-active` record intentionally fails the final repository gate; it is not
mergeable and cannot authorize follow-on development.

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

Start with one current table containing `open TODO`, `owner`, `blocker`, and
`next action`; write `none` when it is empty. Historical detail may remain as
evidence but cannot become a second queue. Report outcome, changed files, risk,
exact commands/results, firmware/profile/protocol/release impact, required
reviewers, unresolved evidence, and the repository's canonical code-size
breakdown. Separate shipped production, tests, tooling, contracts/profiles,
generated data, and deleted binary evidence, including the exact canonical
code-size command and values. PR bodies record outcomes and gates; they do not
reproduce a complete agent execution log.
