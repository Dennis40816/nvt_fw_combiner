# NFC Roadmap

Status: active owner roadmap, implementation checkpoint 2026-08-26.

2026-08-09 planning amendment: the owner approved complete removal of the
remaining legacy architecture, one production path per module, the
consolidated specification, and the LAR-00 through LAR-12 dependency graph.
The graph is part of the `v0.10.3` complete-refactoring milestone. Its `LAR-*`
planning ids receive GitHub issue numbers only after separate publication
authorization. PR #352 remains the stable predecessor; this amendment does not
tag, release, or reopen it.

2026-08-13 completion record: LAR-01 through LAR-12, LAR-00, and #197 closed
their verifier, Golden, review, package, merge, CI, and release evidence before
the stable `v0.10.3` tag. The following stable `v0.10.4` release preserved that
architecture and recorded its unachieved 700 ms target as an explicit residual.

This file owns future milestone order and release boundaries only. It does not
repeat product requirements, architecture decisions, firmware facts, skill
inventories, issue acceptance criteria, or historical release notes.

Canonical detail lives in:

- [`SPEC.md`](../../SPEC.md) for product scope and accepted requirements;
- [`CHANGELOG.md`](../../CHANGELOG.md) and dated release records for completed
  history;
- [`verification-report.md`](../references/verification-report.md) for the
  current package/release evidence state and open gates;
- the [`0.10.x maintainability design`](0.10.x-maintainability-working-design.md)
  and its linked continuations for architecture reasoning;
- [`ADR 0021`](../adr/0021-code-size-ratchet-and-convergence.md) for the
  production-code measurement, exact descending ratchets, and reviewed
  candidate-ledger completion contract;
- the [`agent skill inventory`](../governance/agent-skill-inventory.md) and
  [`routing contract`](../governance/agent-skill-routing.md) for adopted
  workflow skills; and
- the [`0.10.x ticket dependency plan`](../governance/0.10.x-ticket-dependency-plan.md)
  plus GitHub issue bodies for implementation order and acceptance criteria.

2026-08-11 milestone amendment: after the official `v0.10.3` complete-refactor
tag, the owner added a `v0.10.4` package acceptance target: the controlled
compressed single-file, self-contained `win-x64` Home launch must reach a
nonzero main-window handle at or below a 700 ms median after one unscored
warm-up and across five measured launches. The cold launch remains recorded.
This bounded first-window correction belongs beside the simplification audit;
the broader observable, cancellable, bounded, and user-controllable preload
lifecycle remains `v0.10.5` scope.

2026-08-13 milestone amendment: after the official `v0.10.4` release, the owner
approved [ADR 0049](../adr/0049-unified-preload-lifecycle.md) and the
[`v0.10.5` specification](../specs/v0.10.5-unified-preload-lifecycle.md).
`PL-01` through `PL-07` implement the bounded lifecycle and `PL-00` owns its
terminal evidence/release gate. The lifecycle owner controls scheduling and
operator actions only; catalog, report, inspection, diagnostics, and external-
runtime semantics remain with their typed owners.

## `0.10.0`: planning and governance baseline

`0.10.0` reconciles its original `v0.9.15` planning baseline with the complete
reviewed `v0.9.16` hot-fix. Its scope is M0/M1 inventory, accepted
architecture and terminology, skill/workflow governance, validation standards,
the owner-updated FlashMap evidence reference, and the approved ticket
dependency plan.

It does not allocate or implement a production Support Matrix,
release-recovery, Error, Report, or maintainability slice. The annotated-tag
newline defect and visible clean-Windows UI-smoke gap remain explicit in the
verification report; neither is represented as closed by this planning
release.

## `0.10.1` through `0.10.6`: owner-allocated implementation

The approved GitHub issues named in the
[`0.10.x` ticket dependency plan](../governance/0.10.x-ticket-dependency-plan.md)
and each issue's `Blocked by` edges control the implementation frontier. Issue
numbers are not assumed to be a contiguous range; later approved tickets such
as #207, #214, #219, and #221 remain first-class program work. Dependency depth
is not a release version. The owner allocates only dependency-ready,
reviewable slices after considering risk, evidence, file ownership, and
available reviewers. The owner release allocation recorded on 2026-08-04 is:

1. `v0.10.1` closes the headless canonical foundation.
2. `v0.10.2` publishes the reviewed desktop adoption through #208, the shallow
   shell and shared read-only Hex viewport, and the first General/Saved Rule
   compatibility deletion through #254 as a support-neutral stable checkpoint.
3. `v0.10.3` completes the remaining approved refactoring graph through #197
   and LAR-00, including LAR-01 through LAR-12, zero Workbench/renamed parallel
   owners, one production path per module, all four Core Convergence ledgers,
   reviewed line-addressed residuals, and exact descending ratchets.
4. `v0.10.4` re-measured that result, audited whether ownership or code could be
   removed or expressed more simply without weakening evidence, and measured the
   exact packaged Home-window median against 700 ms on the controlled owner
   machine. The stable package did not reproduce 700 ms, so that absolute target
   remains an explicit performance residual. Shell construction does not
   synchronously publish the canonical capability catalog merely to show Home.
5. `v0.10.5` executes the approved `PL-01` through `PL-07` graph and `PL-00`
   terminal gate: one observable, cancellable, bounded, and user-controllable
   preload lifecycle, with selection-triggered inspection retaining its own
   workflow generation and typed semantic owner.
6. `v0.10.6` reserves a configured-path update screen and owns the managed
   version experience: a stable
   launcher, side-by-side content-verified payloads, explicit
   install/switch/delete, startup-readiness rollback, offline selection, and a
   unified Settings Version page. The owner-approved contract is
   `docs/specs/v0.10.6-version-management.md` and accepted ADR 0051.

The complete refactoring release `v0.10.3` closed #197 and records the applicable
architecture, firmware-owner, Golden, deterministic package/provenance,
protected-CI, and release-owner gates before publication. Its published
changelog retained visible clean-Windows smoke as an external attestation; this
roadmap does not reconstruct or independently claim that evidence. The former
fixed total and slice targets are dated planning benchmarks, not release gates.
No roadmap entry may waive the retained gates or move, overwrite, or redefine an
existing stable tag or asset.
The later audit, performance, and update-experience releases do not reopen #197
or weaken its retained gate.

## `1.0.0`: supported release and `1.0.1` upgrade validation

`1.0.0` is the first owner-approved supported distribution. It must retain the
fixed multi-path update-source registry, recoverable managed Launcher,
CtrlRAM TP/full-base routes, and the exact Standard Merge, AB Merge, and
CtrlRAM support evidence admitted by the release gate. Repository-wide analyzer
cleanup is not part of this release and cannot be used to delay or weaken its
firmware, update, package, or clean-Windows evidence.

Before publishing `1.0.0`, one isolated validation lineage produces a genuine
`1.0.1` package from different reviewed source identity. The pair must prove
catalog discovery, package and inner-manifest verification, install, READY
activation, restart, switch-back, rollback, damaged-version reporting, and
explicit deletion. Renaming the `1.0.0` ZIP, directory, executable, manifest,
or catalog entry is not a valid `1.0.1` package. The validation package is not
an official stable release unless the owner separately approves publication.

## `1.0.8` through `1.0.13`: update delivery and release-flow closure

The remaining `1.0.x` sequence is intentionally release-bounded. A later item
does not delay an earlier stable release unless it exposes a P0/P1 defect in
that earlier release's owned behavior.

1. `v1.0.8` makes Light the fresh-install shell default; confines canonical
   verification scratch work to the declared fixed test area; ships the
   owner-approved canonical evidence snapshot as 25 direct Golden cases plus
   nine self-contained fact-scoped aliases; moves CI to Windows-only; adds the
   bounded one-package deployment/publish wrapper; and introduces strict
   Catalog v2 `manual-only` / `notify` policy while retaining Catalog v1 for
   the genuine installed `v1.0.7` to `v1.0.8` canary. Live Catalog v2 Registry
   cutover remains a separate R3 release-owner operation after the v1 and v2
   canaries pass.
2. `v1.0.9` adds privacy-filtered current-session diagnostics/history and a
   user-visible version change list sourced only from typed admitted Catalog
   `releaseNotes`. It also owns the selector visual corrections recorded in
   [`v1.0.9 CtrlRAM selector visual contract`](../ui/v1.0.9-ctrlram-selector-visual-contract.md):
   increase only vertical information-row spacing at constrained viewports,
   give Base firmware and every CtrlRAM group the same horizontal anchors, and
   replace hover-dependent nested outlines with one stable section outline and
   one inset input-card outline. A wrapped file/type title or filename must
   reserve its full multi-line height and keep a clear gap before `Verified`
   or other badges and before the following filename/status row.
   `v1.0.9` also performs one complete-page Settings Version proportion audit;
   the owner first approves a full-page reference at exact viewports before
   implementation changes content width, column ratio, modal/page scale, or
   minimum-size behavior. This audit is not permission for a page redesign.
3. `v1.0.10` owns the interactive architecture artifact, version-rule
   documentation, evidence-backed dead-code/minimality review, and measured
   test-runtime reduction. Test work first profiles lane and fixture cost, then
   reuses accepted component evidence, improves narrow-test selection, and
   removes duplicated setup; it cannot hide coverage, weaken a release gate,
   or replace the frozen-candidate full verifier with an unproven shortcut.
   The slice may delete redundancy but cannot create a second semantic owner.
4. `v1.0.11` owns bounded Installer and Launcher experience refinements that
   preserve the existing managed-version, custody, READY, offline, rollback,
   and recovery contracts.
5. `v1.0.12` owns delta-download minimization. Delta transport cannot redefine
   package identity, installed bytes, full-package verification, recovery, or
   rollback authority.
6. `v1.0.13` applies the existing documentation-convergence rule in
   [`ADR 0021`](../adr/0021-code-size-ratchet-and-convergence.md) and the
   existing [ADR lifecycle](../adr/README.md) to consolidate repository
   documentation for minimum correct agent reading. It inventories one
   canonical owner for each normative product,
   architecture, firmware, governance, test, and release fact; merges repeated
   normative text; deletes only documents proven obsolete and evidence-free;
   and marks retained historical release/evidence material archived or
   superseded so it leaves the default task-reading path. AGENTS and skill
   routing point each task to the smallest sufficient document set. Before and
   after inventories for representative architecture, firmware, Golden,
   release, and UI tasks must show which mandatory reads were removed or
   redirected without losing an applicable authority; those inventories stay
   in the existing change or release evidence rather than becoming a new
   registry. The slice must preserve unique decision/provenance/history
   evidence and validated links, and must not add a second documentation
   framework, speculative generator, or duplicate index merely to organize the
   cleanup.

## `1.1.0`: deferred product expansion and analyzer cleanup

`1.1.0` starts only after the release-bounded `1.0.x` sequence is closed and
from its latest official reviewed stable predecessor. It cannot bypass, drop,
or reconstruct behavior or evidence already published by any retained
`1.0.x` release. It owns:

1. complete General Merge and General Replace authoring;
2. saved/customized-rule authoring, persistence, import, and validation through
   the existing typed operation model;
3. new IC and new firmware capability intake under normal profile, Golden, and
   firmware-owner gates;
4. the owner decision to retire or reopen ordinary DP Replace authoring;
5. NT51950 AB `1 IC`/`Cascade`, selector-free NT51951 AB, Perfect-family, and
   `ldc-tp-only` evidence tracks; and
6. repository-wide analyzer cleanup.

The analyzer task starts from the measured 169 deferred style diagnostics:
`IDE0007` 142, `IDE0002` 10, `IDE0001` 7, `JSON002` 5, `IDE0008` 4, and
`IDE0003` 1. It is performed in project-bounded commits with narrow tests and
zero new diagnostics in touched files. It must simplify the existing owners,
not add suppressions, mass semantic rewrites, duplicate implementations, or
firmware behavior.

One topology, IC, workflow, or fact-scoped alias never certifies another beyond
its owner-approved evidence scope.

Only after `v1.1.0` is formally published and its exact release evidence is
closed, start the
[`post-v1.1.0 Tool development process retrospective handoff`](../governance/post-v1.1.0-tool-development-process-retrospective-handoff.md).
That discussion evaluates which decisions produced good outcomes, which caused
avoidable delay or rework, how this process compares with mature large-company
engineering systems, and which changes should become a reusable workflow for
future Tool projects. It is not a `v1.1.0` release gate and cannot retroactively
rewrite release evidence.

## `1.1.x`: Golden evidence closure and `1.1.1` Memory Layout review

The first suitable `1.1.x` evidence slice supplies independent expected output
for the two retained input-only canonical cases and then re-reviews the three
fact-scoped aliases that currently depend on them. Until those expected outputs
and firmware-owner evidence exist, all five entries remain repository-only and
must not be packaged, called Golden, or used to promote runtime support.

`v1.1.1` owns a Memory Layout review of the per-section presentation contract.
It must make each canonical section and its address-space/range boundaries
explicit rather than visually flattening independent sections. This is a
typed Application-to-Presentation projection review; it cannot move firmware
facts, range authority, or memory interpretation into UI code.

## `1.3.0`: Launcher and publication-system extraction review

`v1.3.0` strengthens the Launcher and release/publication system as a bounded
delivery platform. The preferred direction is an independently versioned
repository when the shared contracts, ownership, migration, security, and
release lifecycle can be separated without duplicating NVT FW Combiner's
managed-version semantics. Extraction is not pre-approved: an ADR must first
define the public contracts, migration/deletion milestones, repository trust
boundary, and rollback path. No extracted component may own firmware facts,
composition, profiles, Golden authority, or product-specific support policy.

## Later owner queue

Only work not already owned by the current ticket dependency plan remains here:

- IC family/rule authoring UI beyond the `1.1.0` admitted capability set after
  the trusted-bundle and evidence models are implemented and reviewed.

## Update rule

Dated `0.9.x` roadmaps are historical evidence. New milestone ordering or
resequencing is recorded here, while implementation detail is changed only in
its canonical specification, ADR, contract, profile, evidence record, or issue.
