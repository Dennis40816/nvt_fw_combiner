# NFC Roadmap

Status: active owner roadmap; release-closure checkpoint 2026-09-01;
subsequent owner allocation amendments are recorded below.

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

## `1.1.2`: repository document convergence planning

`v1.1.2` owns physical whole-repository document inventory, topology, retention,
and cleanup planning only. The detailed classification/manifest gate is the
[`v1.1.2 repository document convergence handoff`](v1.1.2-repository-document-convergence-handoff.md).
Its Phase 1 [manifest](v1.1.2-repository-document-convergence-manifest.md)
records the owner-approved D1-D7 deletion batch as final with frozen review and
final verifier evidence. Lane C is final plus attested. Lane B is the
owner-approved implementation candidate for exactly four standalone boilerplate
README deletions; its profile-catalog digest projection, exact-path audit,
focused validation, independent fixed-head review, and one R3 release-owner
attestation remain required. Packaged payload inventory, profile-file projection,
and package scripts remain unchanged, while RELEASE-MANIFEST
`embeddedProfileCatalogSha256`, resulting archive digest, and provenance identity
are expected to change. Repository-wide current paths are
now classified by the exactly-one approved matcher as `RETAIN_BY_RULE`, except
for the 13 exact reviewed paths with explicit dispositions. Unknown or future
paths fail closed; this classification neither approves deletion nor grants
permanent immutability, and does not claim `v1.1.2` complete.

## `1.1.3`: infrastructure determinism and isolation

`v1.1.3` owns four-seed infrastructure determinism/isolation, deletion of the
temporary seed pin, and an evidence-backed CI duplication assessment. It may
contain at most one separately approved, equivalence-proven mechanical setup
convergence. It must not reduce a gate, verifier, or trust boundary. The exact
residual [VERIFY-111-XUNIT-SEED-01](../governance/change-records/VERIFY-111-XUNIT-SEED-01.json)
is immutable historical evidence; its former `v1.1.2` allocation is superseded
to `v1.1.3` by the current owner decision.

## `1.1.4`: session diagnostics and Version-page review

`v1.1.4` owns privacy-filtered current-session diagnostics/history, a Catalog
releaseNotes version list, and a complete-page Settings Version proportion
audit. The version list remains sourced only from typed admitted Catalog
`releaseNotes`. The owner first approves a full-page reference at exact
viewports before implementation changes content width, column ratio, modal/page
scale, or minimum-size behavior; this audit is not permission for a redesign.

## `1.1.5`: CtrlRAM selector visual contract

`v1.1.5` owns only the selector visual corrections in the proposed,
pending-owner-approval
[`v1.1.5 CtrlRAM selector visual contract`](../ui/v1.1.5-ctrlram-selector-visual-contract.md):
increase only vertical information-row spacing at constrained viewports, give
Base firmware and every CtrlRAM group the same horizontal anchors, and replace
hover-dependent nested outlines with one stable section outline and one inset
input-card outline. A wrapped file/type title or filename must reserve its full
multi-line height and keep a clear gap before `Verified` or other badges and
before the following filename/status row.

## `1.1.6`: first-entry selection and CtrlRAM Replace diagnosis

`v1.1.6` owns the shared first-entry IC-selection reuse decision and a
controlled CtrlRAM Replace cold-versus-warm first-open diagnosis. It authorizes
neither implementation nor a performance claim until a measured target and
separate owner approval exist. The recorded boundary is the
[`post-v1.1.0 navigation and CtrlRAM first-open handoff`](../ui/post-v1.1.0-navigation-and-ctrlram-first-open-handoff.md).

## `1.1.7`: Memory Layout review

`v1.1.7` owns a Memory Layout review of the per-section presentation contract.
It must make each canonical section and its address-space/range boundaries
explicit rather than visually flattening independent sections. This is a typed
Application-to-Presentation projection review; it cannot move firmware facts,
range authority, or memory interpretation into UI code.

## `1.1.8`: AB selector and DP metadata layout

`v1.1.8` owns the [AB IC selector scope correction](../ui/v1.1.x-ab-ic-selector-scope-handoff.md)
and [AB DP metadata layout](../ui/v1.1.x-ab-dp-metadata-layout-handoff.md).

## `1.1.9`: Standard Merge verification feedback

`v1.1.9` owns [Standard Merge input-verification feedback](../ui/v1.1.x-standard-merge-input-verification-feedback-handoff.md)
only.

## `1.1.10`: Report History usability

`v1.1.10` owns [Report History usability](../ui/v1.1.x-report-history-usability-handoff.md)
only.

## `1.1.11`: Report Changes compare

`v1.1.11` owns [Report Changes compare](../ui/v1.1.x-report-changes-compare-handoff.md)
only.

## `1.1.12`: bundle primary-output rename

`v1.1.12` owns the [bundle primary-output rename correction](../ui/v1.1.x-bundle-primary-output-rename-handoff.md)
only.

## `1.1.13`: agent execution workflow and AI-skill pilot

`v1.1.13` owns the agent execution workflow/AI-skill audit and bounded pilot:
capability/difficulty/risk-based conductor and worker routing, inventory and
routing consistency, and forward-testing. Its retained discussion evidence is
the [`post-v1.1.0 Tool development process retrospective handoff`](../governance/post-v1.1.0-tool-development-process-retrospective-handoff.md).

## `1.1.14`: evidence-preserving semantic convergence and minimality

After `v1.1.2` physical document-cleanup planning, `v1.1.14` owns
evidence-preserving semantic architecture/version-rule convergence and a
dead-code/minimality review. It reconciles stale SPEC/release evidence and
current-versus-historical headings, and normalizes every active handoff's open
TODO, owner, blocker, and next-action summary. It preserves historical evidence
and must not add a parallel documentation framework.

## `1.2.0`: bounded Launcher hardening and development

Launcher work remains secondary to every `v1.1.x` UI and performance priority,
but `v1.2.0` begins real bounded Launcher hardening/development. It starts with
a comprehensive current defect, security, and evidence inventory, then
implements one owner-approved, reviewable remediation tranche. This is a
development allocation, not merely an extraction or architecture review. It
does not claim Catalog/Registry production activation or release readiness;
activation remains **NO-GO** until separate R3 security/evidence closure.
Existing package identity, installed bytes, managed-version, verification,
recovery, and rollback authority remain unchanged.

## `1.3.0`: Launcher and publication-system extraction review

After `v1.2.0` begins the first Launcher remediation tranche, `v1.3.0` is the
subsequent architecture/repository extraction review for the Launcher and
release/publication system. It is not the first Launcher delivery. The preferred
direction is an independently versioned
repository when the shared contracts, ownership, migration, security, and
release lifecycle can be separated without duplicating NVT FW Combiner's
managed-version semantics. Extraction is not pre-approved: an ADR must first
define the public contracts, migration/deletion milestones, repository trust
boundary, and rollback path. No extracted component may own firmware facts,
composition, profiles, Golden authority, or product-specific support policy.
It remains secondary to the `v1.1.x` plan and is not allocated to a current
`v1.1.x` release.

## Explicit owner-unallocated queue

The following work has no approved release version. Its former version labels
are provenance only, not current assignments. Nothing in this queue begins
until the owner separately allocates a bounded milestone and its gates.

1. Catalog/Registry production activation, remaining publisher
   trust/signing/security closure, delta-download minimization, and later
   Launcher/Installer residual refinements remain secondary and
   owner-unallocated after the first `v1.2.0` remediation tranche. Launcher
   activation is **NO-GO** until separately approved R3 security/evidence
   closure; no unallocated delivery item can redefine package identity,
   installed bytes, verification, recovery, or rollback authority.
2. Former planned `v1.1.0` product-expansion/analyzer bundle: complete General
   Merge and General Replace authoring; saved/customized-rule authoring,
   persistence, import, and validation through the existing typed operation
   model; new IC and firmware-evidence/capability intake under normal profile,
   Golden, and firmware-owner gates; NT51950 AB `1 IC`/`Cascade`, selector-free NT51951
   AB, Perfect-family, and `ldc-tp-only` evidence tracks; and repository-wide
   analyzer cleanup. The published `v1.1.0` made no ordinary DP Replace
   retirement-or-reopening decision, so that decision is also owner-unallocated.
   The analyzer baseline remains 169 deferred style diagnostics: `IDE0007` 142,
   `IDE0002` 10, `IDE0001` 7, `JSON002` 5, `IDE0008` 4, and `IDE0003` 1.
3. Former generic `v1.1.x` Golden-evidence item: supply independent expected
   output for the two retained input-only canonical cases and re-review the
   three fact-scoped aliases that depend on them. Until those outputs and
   firmware-owner evidence exist, all five entries remain repository-only and
   must not be packaged, called Golden, or used to promote runtime support. One
   topology, IC, workflow, or fact-scoped alias never certifies another beyond
   its owner-approved evidence scope.
4. IC family/rule authoring UI after the trusted-bundle and evidence models are
   implemented and reviewed.

## Update rule

Dated `0.9.x` roadmaps are historical evidence. New milestone ordering or
resequencing is recorded here, while implementation detail is changed only in
its canonical specification, ADR, contract, profile, evidence record, or issue.
