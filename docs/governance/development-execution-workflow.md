# Development Execution Workflow

Status: Active repository runbook.

For ordinary non-normative, non-classifier-governed documentation, use the root
R0 short path: preserve existing edits, update the existing owner, review the
diff and affected links, then report the result briefly. Structure or consumer
checks apply when document layout or parsed inputs change. This path needs no
new issue, capability record, subagent, code-size census or handoff artifact.
`AGENTS.md`, governance and other classifier-governed documents still follow
their record/integration contract. Normative, permission and release changes
are not R0 merely because their file is Markdown. Required CI is unchanged.

## Preflight

For R1-R3 work, run `git status --short --branch`, preserve existing
user changes, and record risk, affected authority/layers, acceptance criteria,
human/evidence gates, narrow test, final gate, integration base, and owned
mutable surfaces. Read the relevant source, contract/profile, and test once.

Branch/version/release rules live in
[`branch-version-and-release-governance.md`](branch-version-and-release-governance.md).

## Capability-reuse gate (fail closed)

### Bounded local R1 continuation

Under [ADR 0070](../adr/0070-bounded-local-r1-continuation.md), an explicitly
authorized local R1 correction may proceed while prior integration records are
unfinished. Before editing, the owner's authorization, existing semantic owner,
source base, exact paths, acceptance criteria, narrow tests and residual gates
must be recorded in the owner task discussion or existing handoff. A later
commit may restate or link that evidence, not establish it retroactively.
Resolve unknown or conflicting ownership before changing behavior.

This path applies only to an existing capability's bounded implementation or
presentation correction. It excludes changes to architecture/public contracts,
governance, ADRs/schemas, profiles, firmware bytes/ranges/order/integrity,
support/evidence admission, permissions, CI and release policy. R2/R3 keep the
recorded design admission and their authority-specific reviews below. A failing
affected test or correctness finding must still be resolved for the local unit.

Complete the narrow tests and scoped Polytail, then commit each coherent unit
when the owner has authorized commits. A local R1 unit does not need a new
overlapping JSON record or closure of all prior integration records merely to
continue. Preserve existing record blobs; they are neither reusable permission
nor something to rewrite to fit the new scope. Ask only for genuinely missing
authority or a material unresolved decision, not a repeated sequencing waiver.

Local continuation is not an integration batch or a validator pass. Before
integration, the complete candidate still needs valid, uniquely covering
records and the checkpoint/review/evidence contract below. Missing or
unresolved final ownership remain explicit integration blockers. Overlapping
historical modifications use the final-only ownership partition in
[ADR 0071](../adr/0071-final-integration-path-ownership.md); this local path
supplies no activation, history rewrite or automatic finalization.
Do not run a candidate gate merely to reopen a known record-only local blocker.
When required at its actual stage, run it unchanged and report every failure.

### Recorded design admission and integration evidence

Before changing behavior outside the bounded local R1 path (including R2/R3),
and before every formal integration admission, read and follow the complete
[capability-reuse record contract](capability-reuse-record.md). It owns the
staged-blob, schema, path coverage, immutable lifecycle, checkpoint/activation,
merge normalization and external-attestation requirements; this runbook owns
the execution sequence:

1. Complete owner search and stage the `design-active` admission before
   implementation; R2/R3 require independent architecture/contract admission.
   Unknown, unsearched or conflicting ownership blocks production changes.
   Renaming, relocating or wrapping a duplicate does not satisfy reuse.
   Diagnostic reads, characterization tests and planning may proceed while
   admission is incomplete.
2. Implement and test the admitted scope, then commit and independently review
   the frozen exact head as required by risk.
3. Finalize the admitted records against that reviewed head under the contract,
   stage them, and pass the repository validator before the evidence commit.
4. Commit the final evidence as the reviewed head's direct child. R3 external
   authority and evidence remain separate prerequisites, never supplied by a
   record or reviewer verdict.

## Narrow test selection

Local verification and every direct narrow test run only after one fixed,
absolute, existing `NFC_TEST_AREA_ROOT` outside the repository is loaded into
the current process. That process also explicitly sets `TEMP`, `TMP`, and
`TMPDIR` to the root's existing `temp` child. The user-level declaration is
initialized once; every shell repeats the process-level assignments. GitHub
Actions derives `RUNNER_TEMP/NvtFwCombiner-TestArea` and rejects any conflicting
declared root. These requirements apply to the bare commands below without
changing their command text. Windows verifier custody of the validated root,
sessions root, session, and marker remains live through scratch creation,
descendant handoff, workload completion, and exact-session cleanup.

| Changed surface | First test |
| --- | --- |
| Ordinary non-normative, non-classifier-governed prose (R0) | Diff and affected-link review; structure/consumer checks only for layout or parsed-input changes. |
| Agent config, governance, normative or classifier-governed documents | `python scripts/verify.py --structure-only`, plus tests of any affected executable/contract behavior. |
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
`python scripts/verify.py --all` once on the frozen R1-R3 integration/release
candidate. Ordinary R0 prose may finish locally after the short path above;
it does not disable any protected CI or release gate.

## Review checkpoints

For R1-R3 implementation, use three checkpoints so review runs with development
instead of becoming a serial tail after implementation:

1. **Development loop.** After each coherent bounded correction, run its narrow
   test and analyzer. Independent reviewers may inspect security, semantics,
   architecture, and evidence in parallel, but each reviewer must audit one
   recorded snapshot completely and return one severity-ranked findings batch.
   Mark findings from an older snapshot as stale; do not drip already-visible
   findings across repeated rounds.
2. **Pre-freeze.** Freeze the approved scope and acceptance criteria, aggregate
   all review lanes once, and close every P0/P1. Give each P2 a recorded
   `fix-now` or owner-approved follow-up disposition. Review findings do not
   expand the current task: allocate unrelated improvements to a named later
   ticket.
3. **Fixed head.** Commit the coherent candidate, review that exact head once,
   then run the final gate once. A correction invalidates only the affected
   review lane and evidence: rerun its narrow gates and exact-head review, not
   unrelated lanes. Completion requires one exact head with no open P0/P1,
   every P2 disposition recorded, applicable tests green, and required human or
   external evidence still explicit.

Each checkpoint commit must be coherent, tested, and recoverable;
documentation, tests, and review corrections that belong to the same outcome
need not become separate ceremony commits. Stage only explicit owned files.
Never stage, reset, amend, or revert another agent's changes.

For governed work, create implementation commits first, perform the fixed-head
review, then populate and commit the `final-complete` records as a separate
evidence checkpoint. An intermediate commit that still contains a
`design-active` record intentionally fails the final repository gate; it is not
mergeable and cannot authorize another integration batch. It does not prohibit
an independently authorized correction under the bounded local R1 path above.

Before R1-R3 handoff: format changed files, run the affected narrow test, inspect
the exact diff, apply scoped Polytail, and record residual evidence. The full
final gate belongs to the frozen integration/release checkpoint above, not each
interim status update or document handoff.

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

For R0 documentation, a brief outcome and verification note is sufficient.
For R1-R3 handoff, report outcome, changed scope, actual checks and remaining
gates. Include a TODO/owner/blocker/next-action table only when transferring
unfinished work; use the existing canonical queue, not another report. Include
the canonical code-size breakdown only when source-size policy is affected or
the owner requests it. PR bodies record outcomes and gates, not execution logs.
