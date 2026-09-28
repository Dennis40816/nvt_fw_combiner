# Development Execution Workflow

Status: Active repository runbook.

Before choosing a path, apply the root
[task scope](../../AGENTS.md#task-scope-and-autonomy) and
[risk-adaptive gates](../../AGENTS.md#risk-adaptive-gates). Those sections own
local applicability, including the ordinary-prose R0 path and governed-document
exceptions. This runbook owns execution within those boundaries.

## Preflight

For R1-R3 work, run `git status --short --branch`, preserve existing
user changes, and put the risk, affected authority/layers, acceptance criteria,
human/evidence gates, narrow test, final gate, integration base, implementation
owner and owned mutable surfaces in the pull request admission fields. Read
the relevant source, contract/profile, and test once.

Branch/version/release rules live in
[`branch-version-and-release-governance.md`](branch-version-and-release-governance.md).

## Admission

Before changing production behavior, a semantic branch or an owner contract,
complete owner search. Record the existing semantic owner, callers and typed
contract, the search evidence and the disposition (`reuse`, `extend-owner`,
`reject-duplicate`) in the [pull request admission fields](../../.github/pull_request_template.md).
Unknown, unsearched or conflicting ownership blocks production changes.
Renaming, relocating or wrapping a duplicate does not satisfy reuse.
Diagnostic reads, characterization tests and planning may proceed while
admission is incomplete. R2/R3 retain independent architecture/contract review.

The pull request also names outcome and non-goals, declared risk and added
roles, implementation owner and owned paths, affected authority, narrow tests
and final gate, the exact-head review, R3 evidence and any waiver. These fields
carry evidence; the authority check, required CI and owner approvals enforce
their respective gates. The reviewer confirms in writing the byte, range,
order, integrity and support impact (or its absence), existing semantic owner,
callers and typed contract, and test evidence.

Implement and test the authorized scope, then independently review the frozen
exact head under the authority check below. R3 human authority and evidence
remain separate prerequisites; a review verdict cannot supply them.

## Single writer

Every pull request names its implementation owner and owned paths. The
commander assigns each mutable surface to one open workstream at a time on the
board. Another workstream that needs it waits for the merge, or takes it over
through a handoff recorded on the board and in both pull requests after the
earlier writer has stopped. An overlap found at review or rebase stops both
writers until the commander decides. CODEOWNERS routes reviews; it is not a
write lock. Rulesets do not restrict a pull request's touched paths, and Git
merge conflicts detect only textual overlap.

## Waivers

A new waiver is a pull request statement bound to its head SHA and scope. It
names the rule or tool, scope, reason, risk, owner, issue, approver, creation
and expiry dates, and removal condition. The approver is the owner of the
authority waived, approving the last push under ADR 0080 item 7 and naming the
relevant role. A new head needs renewed approval. No waiver may weaken
firmware range safety, processor write ranges, integrity order, secrets or
signing, release allowlists, or independent Golden expectations. Existing
waivers remain frozen evidence; this location grants no wider waiver power.

## Authority check

[ADR 0080](../adr/0080-governance-reset.md) (G1-A) adds the pull request check
`governance / authority` (`.github/workflows/authority.yml`,
`scripts/authority_check.py`) and its path map
[`authority-policy.json`](authority-policy.json). It binds a branch once the
owner has made the context required on that branch's ruleset.
The check proves presence and form at the time it ran; the owner judges the
evidence, and the procedures below close what the check leaves open (ADR 0080,
safeguards P1 to P9). Once the context is required, a pull request whose head
predates the workflow or checker reports a failure or no context at all, and
either blocks the merge: rebase it onto the base, then renew its review record
and approval on the new head.

**Authority block.** The description carries exactly one fenced
`nfc-authority` JSON block (template: `.github/pull_request_template.md`):
`risk`, `roles`, `implementationOwner`, `ownedPaths` and, for each declared
role, its `evidence` (`firmware-owner`: `golden`, `writeRanges`;
`release-owner`: `release`; `governance-owner`: `change` and, for each
unclassified path, a `classification`). The check takes the changed paths from
the merge base of the live base tip and the head, both sides of renames and
copies included, and applies the stricter of the base and head policies to
each path. It fails when the block is missing or malformed, the declared risk
is below the floor, a role the paths, a review record or a classification
require is not declared, a declared role lacks its entries, an R1 to R3 change
has no valid review record on the head, a policy is missing or invalid, or an
API or Git call fails. A required or declared role makes the change R3.

**Review record.** For R1-R3, another agent runtime reviews. If unavailable, a
fresh session of the same runtime reviews, preferably with another model, and
never shares the author's conversation. No process requires two agent products.
The reviewer posts a comment review on the head through the API, never in the
description:

```text
gh api repos/Dennis40816/nvt_fw_combiner/pulls/<n>/reviews -X POST \
  -f commit_id=<head> -f event=COMMENT -F body=@review.md
```

`review.md` holds one fenced `nfc-review-record` block with the JSON object
`head` (40-character SHA), `reviewer` (runtime and model, for example
`codex/gpt-6-astra`), `mode` (`other-runtime` or `same-runtime-fresh-session`),
`verdict` (`accept`, `accept-with-changes` or `reject`), `openP0P1`, `state`
(`complete` or `incomplete`) and `addedRoles`. It counts when its `commit_id`
and `head` equal the head, it is complete and not rejecting, `openP0P1` is 0,
and its author is on the reviewer list of both policies; the latest record of
each listed principal decides. Posting or editing a review starts no run, and a
new head needs a new record.

**R3 approval.** The owner approves the most recent reviewable push at its
exact head, naming every required role: `firmware-owner`, `release-owner` and
`governance-owner` as the authority policy, author or reviewer requires. The
approver must be a code owner other than the pusher. Firmware authority retains
byte and Golden evidence and the exact write-range audit; release authority
retains release-policy evidence; governance authority retains the statement
of the affected rule, permission or approval authority. A new head requires a
new review and approval, even if its tree is identical.

**Approval snapshot.** When the owner approves, the commander records in the
pull request the head SHA, the authority block as approved and every valid
review record with its review id, head, verdict and complete body.

**Pre-merge verification** (commander, before asking for the merge):

1. Base authority code. Compare the blob IDs of
   `.github/workflows/authority.yml`, `scripts/authority_check.py`,
   `docs/governance/authority-policy.schema.json` and
   `docs/governance/authority-policy.json` at the base tip
   (`git fetch origin <base>`, then `git rev-parse origin/<base>:<path>`) with
   the evaluated-head column of the latest run's job summary, whose "Checker
   that ran" line names the checker revision and blob that produced it. If the
   pull request runs older versions it does not change, rebase it (and renew
   the review record and the approval), or run the current base checker
   against its head and attach the result, whose "Checker that ran" line must
   name the base tip; a failure stops the merge:

   ```text
   git worktree add --detach <tmp>/base origin/<base>
   git worktree add --detach <tmp>/head <head>
   python <tmp>/base/scripts/authority_check.py --root <tmp>/head \
     --repository Dennis40816/nvt_fw_combiner --pull-request <n>
   ```

   (`GITHUB_TOKEN` may hold a read-only token; unset, the public API is read
   anonymously.) If the pull request changes these files on purpose, the
   self-change check below applies.
2. Re-run `governance / authority` (`gh run rerun <run-id>`) and wait for it.
3. Confirm through the API that this run reports success for the current head
   (`gh api repos/Dennis40816/nvt_fw_combiner/commits/<head>/check-runs`) and
   that the owner's approving review is on that SHA after its most recent push
   (`gh api repos/Dennis40816/nvt_fw_combiner/pulls/<n>/reviews`). For a new SHA
   with an identical tree, ask for a new approval until D4 shows GitHub does.
4. Compare the live authority block and valid review records with the
   snapshot. Any difference, a body edit under the same review id included,
   stops the merge until the owner reconfirms by a new approving review or a
   comment naming the head SHA and the change; record the new snapshot.
5. Merge with `gh pr merge <n> --merge --match-head-commit <head>` on the
   owner's go-ahead. If anything changed after step 1, start again.

The pull request records the snapshot, the blob IDs compared in step 1, and
the run id and head SHA of steps 2 and 3.

**Self-change check.** A pull request that changes the workflow, the checker,
the policy or its schema runs its own version of the check. When the base has
the checker, the commander runs the base checker against the head (step 1's
commands) and attaches the result, the pull request states which verdicts the
change alters and why, and the owner's approval states that the change was
reviewed, the base checker's result was read and this pull request's own
requirements were not lowered (or accepts the stated lowering).

**Bootstrap approval.** G1-A's introduction into the trunk used the gates then
in force on its base, with the historical authority and evidence retained in
[its delivery and acceptance](../adr/0080-governance-reset.md#g1-a-delivery-and-acceptance).
No base-checker result existed or was claimed. For the first release pull
request bringing the check into `main`, if its base has no checker, ADR 0033
and the release policy supply the base gates. An independent fixed-head review
covers the check or confirms that its authority files have the trunk's blob
IDs, citing G1-A's review. The owner's approval states that the base has no
checker, which gates stood in, and that the introduced check was reviewed at
that head. The check's own result there is informative only.

## Narrow test selection

Before each verifier or direct test, apply the root
[test-area setup](../../AGENTS.md#canonical-commands); initialization is in
[`CONTRIBUTING.md`](../../CONTRIBUTING.md). These requirements apply to every
bare command below. Windows verifier custody of the validated root, sessions
root, session, and marker remains live through scratch creation, descendant
handoff, workload completion, and exact-session cleanup.

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

Before R1-R3 handoff: format changed files, run the affected narrow test, inspect
the exact diff, apply scoped Polytail, and record residual evidence. The full
final gate belongs to the frozen integration/release checkpoint above, not each
interim status update or document handoff.

## Recurring specification conformance audit

The historical `0.10.x` restructuring cadence was a whole-repository audit
after three merged tickets or a dependency block, whichever came first (see
[the dated audit](0.10.x-spec-implementation-conformance-audit-20260729.md)).
That program-specific cadence does not apply to ordinary 1.x tasks; historical
records and their outstanding evidence statements remain unchanged.

For current work, trace the affected authority in both directions: each
requirement to its production owner and executable evidence (or named gap),
and each changed behavior to its canonical requirement. A firmware-semantic
R3 change still requires this audit before owner review. Expand the scope for
a demonstrated dependency or contradiction, or an explicitly requested global
audit. Historical evidence cannot silently become runtime policy.

Use the risk-appropriate tests and existing task/PR record to report the fixed
source, scope, commands, findings and dispositions (`fixed-now`,
`allocated-to-ticket`, `blocked-evidence`, `obsolete-authority`). A green test
does not replace this trace. Confirmed authority/behavior contradictions must
be corrected or explicitly block integration; unrelated allocated gaps do not
expand the current implementation scope.

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
