# Ticket Execution Admission

Status: Active for every unintegrated `0.10.x` ticket beginning with the
`0.10.x` integration branch.

This policy turns an approved issue into a bounded, reviewable implementation
slice. It supplements `AGENTS.md`,
[`development-execution-workflow.md`](development-execution-workflow.md), and
[`branch-version-and-release-governance.md`](branch-version-and-release-governance.md).
It does not change firmware evidence, IC support, release authority, or GitHub
label vocabulary.

## 0.10.x integration flow

The owner-selected `0.10.x` branch is the long-lived integration branch for the
maintainability program. It starts from a verified `origin/main` head. An
owner-bounded program stage may use a subordinate exact-version integration
branch such as `0.10.1`, created from the current `0.10.x` head. Independently
reviewable work for that stage uses `feature/<exact-version>/<topic>` and
targets the subordinate branch. When the stage is complete, its exact-version
branch uses an ordinary reviewed PR to `0.10.x`; it does not merge directly to
`main`.

A bounded program-wide slice that does not belong to a subordinate
exact-version stage may instead use `feature/0.10.x/<topic>` and a reviewed PR
directly to `0.10.x`. The execution card must name which route applies.

Those PRs retain all normal local verification, exact-head CI, independent
review, and R2/R3 human-gate requirements. They do not start tag, package
publication, GitHub Release, release-promotion work, or any release workflow.
Only the final reviewed `0.10.x` to `main` PR enters the release workflow after
the program's integration criteria are satisfied. Tagging and publication
remain separately owner-approved release actions.

Existing branches on an earlier `0.10.1` integration line are predecessor
candidates for the subordinate stage. Verify their merge base against the
current `0.10.x`, rebase or replay when necessary, re-admit them under this
policy, and review their new exact heads; they are never merged merely because
an old branch had a passing check.

## Terms

- **Ticket** — the owner-approved GitHub issue and its complete product
  outcome. A ticket is not automatically one pull request.
- **Execution slice** — one independently reviewable phase with one primary
  observable outcome, one stated owner, explicit tests, and an exact
  completion boundary.
- **Mutable surface** — a file or canonical seam owned by an active slice;
  examples include `scripts/verify.py`, `.github/workflows/ci.yml`, the
  solution/package graph, a profile/schema family, a shared Presentation
  resource, or a public Application contract.
- **Exact head** — the pushed commit SHA to which CI, local review, and the
  current external review request all refer.

## Admission before implementation

Before creating or modifying an implementation branch, the implementer records
an execution card in the issue, PR description, or approved repository audit.
It must name:

1. the authoritative remote integration base and its SHA, after fetching it;
   a stale local `main` must not be used as the base assertion;
2. the parent ticket and the one primary outcome of this slice;
3. the acceptance items owned now and the explicitly deferred items;
4. risk class, owning layers, affected workflows/ICs, non-goals, and required
   human/evidence gates;
5. narrow tests, the final verifier gate, and the exact definition of a
   successful observable result;
6. every planned mutable surface; and
7. every active branch/PR with an overlapping mutable surface, followed by an
   ordering decision or an explicit no-overlap finding.

An issue's `Blocked by` graph is necessary but not sufficient. Tickets at the
same graph depth may start together only after the mutable-surface comparison
is recorded. An overlap in a canonical verifier, CI workflow, package graph,
shared contract, shared UI resource, profile/schema family, or integration
document requires an order; it cannot be resolved by opening parallel PRs.

## Required slicing

The ticket must be decomposed into execution slices before coding when it
combines any of the following independent outcomes:

- measurement/baseline collection, policy enforcement, and a separate
  convergence or deletion objective;
- a new canonical authority, migration adapters or consumers, and a new
  user-visible surface;
- more than one independently reviewable workflow, IC family, or Presentation
  consumer;
- separately decidable R2/R3 evidence or human-review gates; or
- more than one primary observable outcome, or a non-mechanical diff expected
  to substantially exceed roughly 500 lines without a recorded cohesion
  reason.

Create separate GitHub tickets when slices have independent blockers, evidence
gates, or merge timing. Otherwise retain one parent issue and record ordered
phase commits in its execution card. A parent issue may not be called complete
until every child or recorded slice is integrated.

### Headless vertical slices

A vertical slice does not require a Presentation change. A data-model slice is
vertical when one real workflow crosses the canonical IC/artifact facts,
resolution or compilation, and an Application-facing query or command, then is
proved through the same interface by contract, CLI, integration, or golden
evidence. A Domain-only type inventory with no resolved consumer is horizontal
and is not admission-ready.

During the owner-declared data-first wave, Presentation files are outside the
mutable surface unless the owner explicitly opens a UI slice. Existing UI
behavior stays on a one-way compatibility adapter or unchanged projection that
does not copy firmware facts or become a second authority. Every temporary
adapter must name its replacement interface, deletion criterion, and downstream
migration ticket.

When an integrated headless slice must unblock later data work while a sibling
UI slice remains deferred, give the headless slice its own child issue or
explicit dependency identity. Do not force downstream data work to wait for
the parent ticket's deferred Presentation outcome.

## Ownership and WIP

- Each mutable surface has exactly one implementer at a time. Reviewers and
  scouts are read-only unless ownership is explicitly transferred.
- A single writer may have only one active R2/R3 implementation slice. A
  second R1 slice is allowed only when its recorded mutable surfaces are
  disjoint from the first and its implementation does not require changing
  branches during the first slice's test or review cycle.
- An active canonical-verifier, CI, package-graph, shared-contract, or shared
  Presentation-resource slice excludes another mutable slice on that same
  surface until it is integrated or explicitly abandoned.
- New user-visible navigation or a substantial main-page surface is a separate
  Presentation slice. Confirm its entry/disclosure interaction with the owner
  before the final implementation gate; do not use an external code review as
  the first product-design check.

## Review and verification cadence

Each execution slice uses narrow tests while being developed. The canonical
full verifier runs once on the frozen R1-R3 candidate, not as a diagnostic
retry and not once per speculative patch.

Before requesting external review, the implementer must complete the slice's
acceptance ledger, inspect the diff against that ledger, run the required local
tests, apply Polytail, push the frozen head, and record residual human gates.
Request `@codex review` only for that exact head. A new request is allowed only
after a new head fixes actionable findings or changes the frozen scope; it is
not a periodic progress signal.

External review and CI are merge gates, not proof that a ticket is complete.
Review polling must inspect current-head inline comments, review summaries,
issue comments, and reactions. An earlier-head approval, an `eyes` reaction,
or a green check before the latest commit is not an admission result.

## Status vocabulary

Use these terms in PR and issue updates. Do not substitute commit count,
elapsed time, a local build, or a single green CI lane for a later state.

| State | Required evidence |
| --- | --- |
| `intake-ready` | execution card, mutable-surface comparison, and required inputs/gates are recorded |
| `active` | one owner is implementing the declared slice |
| `locally-accepted` | all slice acceptance items, narrow tests, diff review, and Polytail pass on one frozen local head |
| `review-clean` | no unresolved P0/P1/P2 applies to the exact pushed head; required reviewers have supplied their verdict |
| `integration-ready` | review-clean, exact-head CI and final gate pass, target ancestry is verified, and required human gates are satisfied |
| `integrated` | the approved exact slice is merged into its approved target integration branch |

Only `integrated` work contributes to completed-ticket progress. A parent ticket
remains active until all of its recorded execution slices are integrated.

## Current 0.10.x audit

The initial execution disposition for published `#170`–`#197` and the deferred
Presentation epics `#207`–`#208` is recorded in
[`0.10.x-ticket-slice-audit.md`](0.10.x-ticket-slice-audit.md). That audit is
an admission prerequisite, not a change to firmware behavior or support
promotion.
