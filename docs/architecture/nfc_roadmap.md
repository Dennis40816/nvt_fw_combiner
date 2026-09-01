# NFC Roadmap

Status: active owner roadmap, release-closure and allocation checkpoint 2026-09-01.

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

## `1.1.0`: published manual-only Windows baseline

`v1.1.0` was published on 2026-09-01 as the bounded direct-run Windows x64
distribution. It preserves the frozen `v1.0.8` Application behavior and changes
no firmware semantics, profiles, ranges, processors, support decisions, output
bytes, or output naming. The observed tag/tree identity, reviewed-tree
equivalence, CI observations, exact three-asset publication, provenance,
smoke, release-note, retry, waiver, enforcement gaps, and residual facts are
recorded only in the canonical
[`verification report`](../references/verification-report.md); this roadmap does
not duplicate that release-closure authority.

Publication of `v1.1.0` does not allocate any former product-expansion,
analyzer, UI, evidence, or delivery backlog to that released version.

## `1.1.1`: verification, test, CI, and release architecture only

`v1.1.1` owns only the verification, test, CI, and release architecture review
reserved by the single-use `v1.1.0` waiver. It must restore a passing canonical
full gate or replace that gate through separately owner-approved architecture;
close the shadow-root and capability-history failure modes; add the durable
committed mutation matrix; measure and reduce test runtime by first profiling
lane and fixture cost, then reusing accepted component evidence, improving
narrow-test selection, and removing duplicated setup; and preserve exact
failure, retry, waiver, and residual evidence without hiding coverage,
weakening a release gate, or replacing the frozen-candidate full verifier with
an unproven shortcut.

The same release explicitly owns CI-flow optimization. It first measures the
PR, post-merge `main`, package-preview, and release-candidate critical paths,
then removes duplicated orchestration/setup and applies bounded concurrency
only to independent lanes with isolated logs and one aggregate verdict. The
existing Windows-only platform boundary remains in force. `scripts/verify.py`
stays the sole repository verifier, a low-memory serial fallback remains
available, and any component-evidence reuse requires a separately approved
exact-identity contract; this allocation does not pre-approve a cache or let CI
skip Golden, write-range, package-allowlist, SBOM/provenance, required-check,
branch-protection, or fresh-download gates.

No product feature, firmware behavior, support promotion, selector/Settings
change, Memory Layout review, generic Golden-evidence closure, Installer or
Launcher refinement, or former product-expansion item belongs to `v1.1.1`.

## `1.1.2`: complete former `v1.0.9` bundle

The complete bundle formerly assigned to `v1.0.9` moves together to `v1.1.2`;
none of it remains allocated to `v1.0.9` or is split into `v1.1.1`.

`v1.1.2` adds privacy-filtered current-session diagnostics/history and a user-
visible version change list sourced only from typed admitted Catalog
`releaseNotes`. It also owns the selector visual corrections recorded in the
proposed, pending-owner-approval
[`v1.1.2 CtrlRAM selector visual contract`](../ui/v1.1.2-ctrlram-selector-visual-contract.md):
increase only vertical information-row spacing at constrained viewports, give
Base firmware and every CtrlRAM group the same horizontal anchors, and replace
hover-dependent nested outlines with one stable section outline and one inset
input-card outline. A wrapped file/type title or filename must reserve its full
multi-line height and keep a clear gap before `Verified` or other badges and
before the following filename/status row.

The same indivisible `v1.1.2` bundle performs one complete-page Settings Version
proportion audit. The owner first approves a full-page reference at exact
viewports before implementation changes content width, column ratio, modal/page
scale, or minimum-size behavior. This audit is not permission for a page
redesign.

## `1.1.3`: Memory Layout review

`v1.1.3` owns a Memory Layout review of the per-section presentation contract.
It must make each canonical section and its address-space/range boundaries
explicit rather than visually flattening independent sections. This is a typed
Application-to-Presentation projection review; it cannot move firmware facts,
range authority, or memory interpretation into UI code.

## `1.3.0`: Launcher and publication-system extraction review

`v1.3.0` strengthens the Launcher and release/publication system as a bounded
delivery platform. The preferred direction is an independently versioned
repository when the shared contracts, ownership, migration, security, and
release lifecycle can be separated without duplicating NVT FW Combiner's
managed-version semantics. Extraction is not pre-approved: an ADR must first
define the public contracts, migration/deletion milestones, repository trust
boundary, and rollback path. No extracted component may own firmware facts,
composition, profiles, Golden authority, or product-specific support policy.

## Explicit owner-unallocated queue

The following work has no approved release version. Its former version labels
are provenance only, not current assignments. Nothing in this queue begins
until the owner separately allocates a bounded milestone and its gates.

1. Former `v1.0.10`: interactive architecture artifact, version-rule
   documentation, and evidence-backed dead-code/minimality review. The former
   measured test-runtime component is absorbed exclusively by `v1.1.1` and is
   not part of this queue. This unallocated slice may delete redundancy but
   cannot create a second semantic owner.
2. Former `v1.0.11`: bounded Installer and Launcher experience refinements that
   preserve the existing managed-version, custody, READY, offline, rollback,
   and recovery contracts.
3. Former `v1.0.12`: delta-download minimization. Delta transport cannot
   redefine package identity, installed bytes, full-package verification,
   recovery, or rollback authority.
4. Former `v1.0.13`: evidence-preserving documentation convergence under
   [`ADR 0021`](../adr/0021-code-size-ratchet-and-convergence.md) and the
   existing [ADR lifecycle](../adr/README.md). It may consolidate repeated
   normative text and remove only proven obsolete, evidence-free documents;
   it must retain unique decision/provenance/history evidence, validated links,
   and the smallest sufficient task-reading path without adding a duplicate
   documentation framework, generator, index, or evidence registry.
5. Former planned `v1.1.0` product-expansion/analyzer bundle: complete General
   Merge and General Replace authoring; saved/customized-rule authoring,
   persistence, import, and validation through the existing typed operation
   model; new IC and firmware-capability intake under normal profile, Golden,
   and firmware-owner gates; NT51950 AB `1 IC`/`Cascade`, selector-free NT51951
   AB, Perfect-family, and `ldc-tp-only` evidence tracks; and repository-wide
   analyzer cleanup. The published `v1.1.0` made no ordinary DP Replace
   retirement-or-reopening decision, so that decision is also owner-unallocated.
   The analyzer baseline remains 169 deferred style diagnostics: `IDE0007` 142,
   `IDE0002` 10, `IDE0001` 7, `JSON002` 5, `IDE0008` 4, and `IDE0003` 1.
6. Former generic `v1.1.x` Golden-evidence item: supply independent expected
   output for the two retained input-only canonical cases and re-review the
   three fact-scoped aliases that depend on them. Until those outputs and
   firmware-owner evidence exist, all five entries remain repository-only and
   must not be packaged, called Golden, or used to promote runtime support. One
   topology, IC, workflow, or fact-scoped alias never certifies another beyond
   its owner-approved evidence scope.
7. The
   [`post-v1.1.0 Tool development process retrospective handoff`](../governance/post-v1.1.0-tool-development-process-retrospective-handoff.md),
   which may evaluate reusable workflow changes now that publication and exact
   closure evidence exist, but cannot rewrite release evidence or change active
   governance without separate owner approval.
8. IC family/rule authoring UI after the trusted-bundle and evidence models are
   implemented and reviewed.
9. The owner-observed shared first-entry IC-selection and CtrlRAM Replace cold
   first-open problems recorded in the
   [`post-v1.1.0 navigation and CtrlRAM first-open handoff`](../ui/post-v1.1.0-navigation-and-ctrlram-first-open-handoff.md).
   The first item requires a reuse-first decision on accepted IC-context
   lifetime and safe chooser skipping; the second requires a controlled
   cold-versus-warm diagnosis before an optimization target exists. Neither is
   assigned to `v1.1.1` or another release by this record.
10. The owner-requested `v1.1.x` minor UI-delivery correction recorded in the
    [`bundle primary-output rename handoff`](../ui/v1.1.x-bundle-primary-output-rename-handoff.md):
    enabling bundle-folder output must not disable or discard the existing
    manual primary-output filename override. The folder name and generated
    primary BIN name remain independent, while the existing canonical naming,
    Windows validation, collision, atomic publication, report identity, and
    source-name owners are reused. It is excluded from `v1.1.1`; its exact
    later `v1.1.x` release remains owner-unallocated.
11. The owner-observed Report History interaction failures and small visual
    corrections recorded in the
    [`v1.1.x Report History usability handoff`](../ui/v1.1.x-report-history-usability-handoff.md):
    restore actual single-entry delete and one-click individual-report open,
    match the selector trash-can delete treatment, and add a top-right exact
    raw-JSON copy action. The existing global Load report action also moves
    into an always-reachable Report header or empty Report hub without creating
    a second picker/loader. Existing report commands, queued persistence,
    selector styling, bounded file loader, report projection, and clipboard
    pattern must be reused.
    It is excluded from `v1.1.1`; its exact later `v1.1.x` release remains
    owner-unallocated.
12. The owner-observed AB Code Merge DP metadata truncation recorded in the
    [`v1.1.x AB DP metadata layout handoff`](../ui/v1.1.x-ab-dp-metadata-layout-handoff.md):
    the DP_AB card currently joins each bank's version and `AUTO_PRJ` project
    into one trimmable value while the shared desktop selector reserves four
    equal fact columns. Reuse the existing typed input observations, Standard DP
    Version/Jira Index pattern, AB Presentation projection, firmware fact
    model/template, and four-/two-column card response to show ordered DP1 and
    DP2 Version facts plus Jira Index only when the corresponding tracker exists;
    unknown banks keep an Unknown Version and do not fabricate Jira data. It is
    excluded from `v1.1.1`; its exact later `v1.1.x` release remains
    owner-unallocated.
13. The owner-observed AB Code IC selector scope defect recorded in the
    [`v1.1.x AB IC selector scope handoff`](../ui/v1.1.x-ab-ic-selector-scope-handoff.md):
    the canonical workflow projection already contains only AB-authorable ICs,
    but an in-page mode switch deliberately omits the `IcChoices` publication
    that would replace the broader Merge-page `ItemsSource` during the first
    selector event. Reuse the canonical publication, workflow projection, and
    existing guarded canonical-choice publisher to synchronously republish the
    AB list after the accepted mode state is assigned, then restore the page
    projection when leaving AB; do not add a dispatcher, hard-code ICs, or
    change profiles to repair the stale binding. It is excluded from `v1.1.1`;
    its exact later `v1.1.x` release remains owner-unallocated.
14. The owner-observed Report Changes comparison defects recorded in the
    [`v1.1.x Report Changes compare handoff`](../ui/v1.1.x-report-changes-compare-handoff.md):
    keep the right-side scrollbar outside the byte content by reusing the
    existing non-overlay scroll treatment without nesting scroll viewers;
    present one physical CtrlRAM section as the primary navigator group while
    retaining every raw contiguous range, hash, replay, and acceptance fact;
    restore an independent pale Original semantic palette instead of the
    controller/reference purple mapping; and reserve a measured address-label
    gutter so the Original label cannot cover bytes. It is excluded from
    `v1.1.1`; its exact later `v1.1.x` release remains owner-unallocated.
15. The owner-requested explanation of Standard Merge DP/TP card verification
    and uniform-content warnings recorded in the
    [`v1.1.x Standard Merge input verification feedback handoff`](../ui/v1.1.x-standard-merge-input-verification-feedback-handoff.md):
    retain the canonical compiled input inspection as the sole health owner,
    explain that Verified means a stable readable `.bin` satisfied its compiled
    length/coverage contract with no warning, and replace raw
    `DP_UNIFORM_CONTENT_WARNING`/`TP_UNIFORM_CONTENT_WARNING`-first help with
    localized wording that says the declared range contains one repeated byte,
    why that may indicate blank/placeholder input, what to review, and that
    Build remains available. Reuse the existing focusable status disclosure and
    tooltip; Presentation must not reread firmware or invent another validator.
    It is excluded from `v1.1.1`; its exact later `v1.1.x` release remains
    owner-unallocated.

## Update rule

Dated `0.9.x` roadmaps are historical evidence. New milestone ordering or
resequencing is recorded here, while implementation detail is changed only in
its canonical specification, ADR, contract, profile, evidence record, or issue.
