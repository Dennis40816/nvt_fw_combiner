# ADR 0078: Predecessor comparison for 1.x releases

- Status: Proposed
- Date: 2026-09-26
- Owners: Repository owner, as firmware owner and release owner
- Amends: ADR 0057 (adds a non-terminal 1.x mode; the terminal certification
  is unchanged)

## Context

The owner decided on 2026-09-25 that every release candidate is compared with
the previous stable release across all published routes, that any difference
is declared in the release notes, and that the cost is measured before
implementation (1.1.12 WS-GOV decision 7b). Golden stays the certified oracle;
the comparison catches unintended changes, including on routes whose evidence
is not direct Golden. 1.1.12 first aligned once with v0.9.16 through a
non-certifying local comparison of the 37 plan routes that have canonical
inputs (1.1.12 board decision 10): 34 equal, 2 differences the owner approved
(board decision 12), and 1 route v0.9.16 cannot run. The formal 1.x comparator
was moved to 1.1.13.

[ADR 0057](0057-v0916-black-box-parity-certification.md) owns the terminal
64-route v0.9.16 certification. The release owner deferred it to 2.0.0, and on
2026-09-26 the owner kept that deferral (board decision 47, RO-1 option C): the
rolling comparison is an extra check, never a replacement. The terminal
machinery cannot serve 1.x releases as it stands:

- the plan pins the v1.0.0 candidate executor, policy catalog 1.10.0 and the
  Golden snapshot at `1d1d1cfc`; every plan route still in the current policy
  has a new capability fingerprint, and two have left it;
- its schema and loader admit exactly one approved correction, so the
  NT51950 TP-work difference the owner approved fails it;
- its v0.9.16 executor contract no longer restores (`NU1004`);
- its report check requires an empty `Issues` list and a fixed report member
  set, while every successful CtrlRAM run of 1.1.12 reported issues
  (suspected defect `BUG-20260926-terminal-parity-rejects-ctrlram-issues`);
- the 1.1.12 comparison ran an intermediate source, not the exact `v1.1.12`
  release (`BUG-20260926-changelog-1112-product-source`).

The active Golden binds a case to 33 of the 63 supported routes. The four AB
routes the 1.1.12 alignment compared became contract-only when Dummy DP was
added, but their Normal-mode Golden cases remain. The CLI reads two per-user
settings files that an environment variable cannot reliably redirect.

The owner decided the design questions on 2026-09-26 (1.1.12 board decisions
57 to 64) and the compiler host of the executors after the P-0.5 spike
(decision 79). The design, its independent reviews and the mapping of each
decision are in the 1.1.13 WS-PARITY handoff.

## Decision

1. **Two modes, one comparator.** The comparator has a rolling mode and a
   v0.9.16 1.x mode. Each defines its own baseline, input authority, compared
   set, proof, difference disposition and result; they share the execution
   primitives of the ADR 0057 comparator and add no byte semantics of their
   own.
2. **Rolling gate.** Every 1.x release candidate is compared with the
   previous published stable release: the highest annotated `vX.Y.Z` tag
   below the candidate version with a complete published Release, whose
   peeled commit is an ancestor of the candidate. Inputs are the active
   Golden at the candidate commit, through the coverage ledger. A difference
   is acceptable only through the release's declaration; an undeclared one
   blocks the release (decision 57).
3. **v0.9.16 1.x mode at milestones only.** At the 1.1.13 final candidate, at
   the 1.2.0 release approval and before RO-1 is decided (decision 64), the
   comparator runs as a historical consumer of the ADR 0057 plan: the plan's
   own canonical input authority, its 37 routes with canonical input, and its
   proof kinds (exact output, approved semantic correction, TP-prefix
   transitive), with the 27 unbound routes reported as not covered. A
   difference or a baseline rejection is acceptable only when an exact plan or
   [1.x amendment](../contracts/v0916-parity-1x-amendment-v1.md) row is
   reproduced exactly; no declaration is read. The result is `consistent`,
   `inconsistent` or `invalid`, never `pass`.
4. **Coverage ledger.** A committed
   [ledger](../contracts/predecessor-comparison-v1.md#universe-and-coverage-ledger)
   declares every rolling scenario with its pinned inputs, CLI selection and
   CtrlRAM base. The universe is every `available` route published as
   `supported` or `candidate` (decision 64). Every universe route is compared,
   or is one of decision 12's 27 exact route ids, or is an accepted gap.
   Retiring a scenario, replacing its inputs and leaving a published route
   without comparable inputs are declared like byte differences; a new gap
   never inherits the exemption of the 27 (decision 60). The initial ledger
   has 39 scenarios over 37 routes: the 37 of the 1.1.12 alignment and the
   NT51950 Hiway and Display-OSD cases (decision 64).
5. **Execution.** The comparator builds every CLI itself from exact Git
   authority in fresh detached worktrees, records their identities (the
   runtime-closure digest, never the apphost hash alone), and feeds both
   sides the same read-only staged inputs. The compiler host is pinned
   (decision 79). A formal run needs both per-user settings files absent;
   runs with custom settings are diagnostic only (decision 59).
6. **Per-side safety.** Each side passes the ADR 0057 checks unchanged,
   including the independent compiled-authority check: the Build report must
   match the operations that a typed Preview of the same executor compiled
   for the same inputs (`validate_report_projection_against_compiled_authority`),
   so a report cannot widen its own ranges. The versioned report reader only
   converts formats and may not relax these checks. A crash, timeout, tool or
   report failure is `invalid` and cannot be declared or approved.
7. **Comparison and attribution.** The primary output is compared completely
   (size, every byte, SHA-256), with half-open difference ranges. Each
   difference is attributed range by range (writes different bytes, stopped
   write with preserved bytes, carried by the precursor, derived field), and
   each attribution says whether its cause is supported by cited evidence or
   not independently verified; the union of write ranges is not evidence.
   Differences in operations, issue codes, map id or capability fingerprint
   are information, never a gate of their own.
8. **Declaration and report of record.** Each release commits its
   declaration; each entry carries the owner's approval as firmware owner by
   exact board-decision citation, and its id appears in the CHANGELOG under
   the product change that caused it (decision 64). Then the report of record
   is produced from the exact release source and binds the source, both
   executors, the comparator, the contracts, the ledger and the declaration
   (decision 59). There is no automatic fallback to the 1.1.12 harness or any
   reduced comparison (decision 58). The release decision stays with the
   owner as release owner. A newly rejected input ships only as a known
   non-blocking issue approved for that release; a declared issue never
   overrides a P0 or P1 bug, a required Golden case or a protected check
   (decision 57).
9. **Enforcement.** Until the release workflow runs the comparison (WS-GOV
   batch R-5, an R3 release change that builds on decision 47 option C), the
   gate is procedural: the commander runs the formal comparator, and the owner
   disposes of every change and approves the release with the report of record
   (decision 61). R-5 binds the report into its run-scoped evidence.
10. **v0.9.16 amendment.** The NT51950 FW1.x cascade TP-work Diff NF
    difference becomes an exact approved correction (decision 12). The NT51950
    FW1.x cascade full-flash canonical binding is recorded as not applicable
    to v0.9.16 and is revisited at 2.0.0 (decision 62). A second v0.9.16
    executor contract is approved (decisions 63 and 79). The amendment rows
    are firmware-owner authority: they are admitted by their own R3 pull request
    with the owner's exact-head firmware-owner approval (ADR 0080 item 7),
    which the declaration's citation form does not replace.
11. **ADR 0057 unchanged.** Its plan, schemas, workflow contract, three
    parity jobs, protected environment, terminal parser and 2.0.0 gate stay as
    they are. The comparator never emits ADR 0057 evidence or reads its
    candidate, package, authority-transfer or attestation authority, and its
    documents state `certification: none`.

The contracts are the
[predecessor comparison contract](../contracts/predecessor-comparison-v1.md)
with its ledger and its proposed declaration and report schemas, and the
[v0.9.16 1.x amendment](../contracts/v0916-parity-1x-amendment-v1.md).

## Consequences

- Every release carries a committed declaration, possibly empty, and its
  notes name each declared difference and coverage change.
- The rolling mode compares 37 of the 74 universe routes today. The other 37
  are decision 12's 26 published routes and 11 candidate routes without
  inputs, which need the owner's approval as accepted gaps before a report of
  record can exist.
- Golden changes that add or replace inputs update the ledger in the same
  pull request; a coverage reduction is visible and approved, never silent.
- The chain back to v0.9.16 holds only for scenarios and input revisions
  compared at every release since the 1.1.12 anchor; the milestone runs
  re-anchor it.
- A release costs one more CI-sized job once R-5 lands; the cost is measured
  before that.

## Rejected options

- **Comparing with v0.9.16 at every release.** Each intended change would be
  approved twice, and the exact correction rows would have to be renewed
  whenever such a route changed again.
- **One mode with shared disposition rules.** Applying the rolling inputs,
  direct TP-work comparison and declarations to v0.9.16 would change what the
  historical plan proves.
- **Rolling inputs from the plan's historical Golden snapshot.** It conflicts
  with the rolling active Golden of ADR 0057 and freezes coverage.
- **Editing the ADR 0057 plan in place.** It is the immutable certification
  plan, its schema admits one correction, and its files are resynchronized by
  release-workflow changes.
- **Only route-evidence scenarios.** AB coverage that 1.1.12 compared would
  disappear silently.
- **The 1.1.12 harness as a fallback.** It compares TP-work routes only
  against the baseline's full-output prefix and has no declaration, severity
  or coverage gate (decision 58).
- **Local reports with user settings.** A report of record must not depend on
  one machine's profile (decision 59).
- **A hand-written schema subset in the comparator.** Schemas are checked by
  a Draft 2020-12 engine; the comparator owns the semantic checks.

## Non-goals

- Terminal 64-route certification, or any change to it.
- Covering decision 12's 27 routes, or using unbound Golden cases for them.
- Golden, profile, capability-policy or product changes; evidence ranks and
  publication values.
- The release-workflow integration itself (WS-GOV R-5).
- Package certification, which the release workflow keeps.

## Verification

- The .NET contract tests validate every schema against the Draft 2020-12
  meta-schema with the repository's JsonSchema.Net engine, the committed
  contract, ledger and amendment against their schemas, and a counterexample
  for each conditional relation of the report and declaration schemas.
- The Python contract tests check the ledger against the active Golden
  manifest, the capability policy and the ADR 0057 plan (scenario inputs,
  alias resolution, CtrlRAM bindings, the exact debt set, the universe
  partition and completeness), the contract against the report schema's
  identity members, and that the shared compiled-authority check rejects a
  self-widened report. The amendment's tests check its rows against the plan,
  the case manifests and the recorded 1.1.12 evidence, with a mutation test
  for each bound identity.
- The comparator batch adds behavioral tests for every outcome, proof kind
  and gate rule with synthetic payload-free byte fixtures, and keeps the ADR
  0057 tests unchanged.
- The 1.1.13 final runs, rolling against `v1.1.12` and the v0.9.16 1.x mode,
  are the acceptance evidence.

## Open items

- Closed in `1.2.2`: the report reader rules and the revised report and
  declaration schemas are in effect
  ([contract](../contracts/predecessor-comparison-v1.md#report-reader-v1)).
- The second v0.9.16 executor contract and the compiler-host pinning of
  decision 79 (a separate executor pull request after the P-0.5 spike).
- A Draft 2020-12 validator for the comparator's Python runtime, or keeping
  schema validation in the .NET contract tests (P-2).
- The owner's 1.1.13 approval of the 11 candidate routes as accepted gaps.
