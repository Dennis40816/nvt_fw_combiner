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

The original allocation also included CI-flow optimization. The 2026-09-05
owner decision moves every unfinished `1.1.x` CI/release improvement to
`v1.1.3`, including these residuals; the preceding `v1.1.1` scope is historical
allocation, not a competing current work queue or a reopened release.

No product feature, firmware behavior, support promotion, selector/Settings
change, Memory Layout review, generic Golden-evidence closure, Installer or
Launcher refinement, or former product-expansion item belongs to `v1.1.1`.

## `1.1.2`: support-neutral repository and DPCMI convergence

`v1.1.2` is the approved support-neutral release-candidate scope for completed
whole-repository document convergence and boilerplate cleanup; bounded verifier
script sharding/deadline work without a skipped gate or second verifier; the AB
selector cold-start/Mode/IC-menu correction without support promotion; public
certified NT51929 input-only evidence without expected-output, Golden-parity,
or support claims; and Standard Merge DPCMI CMD1 Page 0 authority for the
NT51919/NT51929/NT51932 perfect family. Standard reads only
`[0x401A,0x401D)`; AB retains `[0x401A,0x401D)` and `[0x4401A,0x4401D)`;
General has no DPCMI reader. The intended naming correction is `D0200` to
`D2004` while NT51929 Golden output bytes and SHA remain unchanged.

The [`v1.1.2 repository document convergence handoff`](v1.1.2-repository-document-convergence-handoff.md)
and [manifest](v1.1.2-repository-document-convergence-manifest.md) preserve
D1-D7 and C1 final historical evidence, frozen identities, and the completed
boilerplate cleanup. They do not create an inventory, TODO, or archive
framework and do not authorize further cleanup. FWConfig and unrelated metadata
are non-goals. Dummy DP was deferred past `v1.1.2`; its current allocation is
`v1.1.4` below. Launcher
remains secondary; bounded development begins in `v1.2.0`, with activation
separately gated. This version cannot be called released until full verification,
protected CI, package and smoke checks, release approval, publication, and
fresh-download verification complete as mandatory no-waiver gates.

## `1.1.3`: consolidated CI and release optimization

Owner decision on 2026-09-05: complete all remaining CI/release optimizations
allocated anywhere in `1.1.x` in `v1.1.3`; move the UI work previously assigned
to `v1.1.3` to `v1.1.4`. The subsequent same-day owner resequencing below
consolidates all existing UI corrections in `v1.1.4` and allocates the remaining
backlog without pulling UI, AI-skill, firmware or Launcher work into `v1.1.3`.
Target roughly 10 minutes of complete CI plus release packaging;
this is a measurement target, not an achieved runtime or a relaxed deadline.
Every release still freshly executes every applicable certified Golden output
case. Only non-Golden source verification may reuse successful exact-source CI;
PR equal-tree evidence, hashes alone and an older green run are insufficient.

This is the single current allocation for the following work; linked contracts
remain the behavior owners, and completed historical evidence is not rewritten.

| Workstream | Completion criterion |
| --- | --- |
| Pipeline duplication and runtime | Measure PR, exact-source `main`, manual package-preview and release-candidate paths; remove duplicate orchestration/setup and reuse only admitted exact-source non-Golden evidence. |
| Isolation and determinism | Safely parallelize independent CI lanes, retain local serial fallback, and close the existing four-seed Infrastructure evidence gate before removing its temporary seed pin. |
| Mechanical preflight | Synchronize all registered source-derived projections before formal verification, without regenerating Golden expectations or approvals. |
| Governance and document-check burden | Resolve the reviewed ten-file AGENTS batch through bounded owner-authorized admission; retain useful safety/traceability guards and remove unnecessary prose-snapshot verification. Assess remaining mechanism costs with evidence. |
| Package and publication | Preserve approved package contents and runtime behavior, review build/setup duplication, complete version/release notes, and pass immutable publication plus public-download smoke. |
| CI/release document convergence | Reconcile active CI/release instructions, release-evidence entry points and these version assignments; this part moves from the former `1.1.14` documentation allocation, while remaining SPEC/dead-code work is now combined with agent workflow work in `1.1.6`. |
| Measured result | Report complete source-CI and release elapsed times, phase/queue/approval breakdowns and the measured historical release reduction below. |

`scripts/verify.py` remains the only repository verifier. Windows-only execution,
Golden whole-output comparisons, write ranges, package allowlists,
SBOM/provenance, stable required checks, branch protection and fresh-download
verification remain enforced. This consolidation does not pre-approve a cache,
alter packaging flags or activate a Launcher distribution system.

The first local batch fixes published-smoke skip propagation, binds release
admission to the actual source's latest complete successful CI workflow/attempt,
and reuses the existing verifier for fresh complete Bootstrap/GoldenRegression
projects. It also removes the repeated-timeout test's wall-clock dependency.
These are local changes under verification, not integrated or released results.
Packaging, independent public download/smoke, protected approval and immutable
publication boundaries remain. No firmware bytes, expected outputs or UI
behavior change in this CI batch.

The next owner-approved local efficiency batch isolates the existing three
repository-script shards on separate Windows CI runners. The unchanged required
`python-worker / verify` check rejects any non-success shard result before
running the worker-only lane. All local full-verifier lanes remain serial.
Release Golden omits only repeated ownership/format prechecks and unused
Coverlet collection; full solution restore/build, immutable output checks,
discovery/TRX reconciliation and all certified output cases remain. A proposed
two-project-only build was rejected: its dependency closure omits four production
outputs required by the existing shadow validation. No new scheduler, retry,
case filter, or shortened Golden comparison was introduced.

The owner also approved source-derived preflight automation. The common
[`sync_derived.py` tool](../../scripts/sync_derived.py) has four fixed providers:
live workflow-contract identity projections, the exact CI documentation mirror,
approved policy/trust-index/Golden-allowlist consumer pins, and the two numeric document-version
headers derived from authorized `VERSION`. Status prose, dates and historical
results are not version projections. Authorized local changes are
synchronized before formal tests; the existing structure lane checks all
providers first without writing. Golden expectations, historical evidence and
approvals are not regenerated. Existing artifact-stage generators remain their
sole owners. [Contributing](../../CONTRIBUTING.md#derived-file-preflight) owns
the commands; [ADR 0068](../adr/0068-derived-file-synchronization.md) owns the
single-writer boundary. The live workflow contract and CI mirror are synchronized;
current policy/index payloads and their trust pins are unchanged.
The owner renewed the unchanged 35-case/158-file Golden reference selection for
`v1.1.3` on 2026-09-05. Only its authorized version/date changed; the tool updated
the two existing package/smoke raw-SHA pins, then a second write and the
all-provider read-only check reported zero changes. Canonical payloads and
historical evidence remain unchanged; this does not replace final release gates.
The structure adapter also selects historical parity H2 from its existing
immutable final-review record, not the last edit to the mixed current/historical
plan. The original H1/H2 and exact H1-H4 package-source checks remain unchanged;
current workflow fingerprint synchronization does not renew old certification.

Latest local evidence on 2026-09-05:

| Check | Result |
| --- | --- |
| Combined local `--all` candidate | FAIL: the whole .NET lane reached its unchanged 900 s deadline. Structure (134.7 s), all three script shards (414/155/367 tests; five existing platform skips in the last shard) and the 30-test CRC worker passed. Seven .NET projects produced fresh passing TRX for all 4,494 tests; Infrastructure produced only a start log and no final TRX. No complete coverage/freshness or full-pass claim follows. |
| Infrastructure-only diagnostic after that failure | Existing canonical session/shadow/discovery/coverage collector passed 1,070 identities (1,068 passed, two existing platform skips) in 125.9 s with detailed VSTest diagnostics and a no-dump hang observer; cleanup completed. This isolated pass does not reproduce or close the full-lane stall. Preserve both runs and diagnose the ordering/concurrency/lifetime difference before another full gate. |
| Complete .NET diagnostic with native long-running messages | `--skip-structure --skip-python` passed in 592.5 s after the background-test fixture changes: all eight projects, 5,564 identities, 5,562 passed and two existing Unix-only skips. Line coverage was 89.46% (75,703/84,624), branch coverage 78.04% (24,798/31,774); freshness and exact-session cleanup passed. This run did not reproduce the earlier stall; it is neither a root-cause fix claim, final `--all`, nor GitHub CI/release timing. |
| Pinned .NET 10.0.301 `--release-golden` | Earlier method-level run: 88.9 s end to end. After per-case identity correction, the fresh complete run passed all 25 canonical case identities, 1,172 Bootstrap + 25 GoldenRegression tests, zero skips and cleanup. Standard cases are separate theory rows; direct versus TP-only/alias checks retain separate executions. Full build took 14.2 s; test lanes 54.4/9.5 s. These phase measurements are not a complete release stopwatch. |
| Per-case Golden gate and public source-CI projection | The old method-only gate failed the missing/shared-case regression; all 197 orchestration tests now pass with four existing platform skips. Fresh TRX exposed three direct/TP-or-alias methods needing distinct test names; their comparisons remain unchanged and the strict 25-case gate now passes. Canonical validation: 80 passed. Promotion policy: 71 passed, including sentinel exclusion from collected source-CI and candidate manifest; no raw actor/author/runner/step metadata is published. |
| Worker-only CI lane | 13.0 s; 30 tests, 100% line/branch coverage. |
| Promotion/package policy | Complete 148-test rerun passed in 359.7 s. |
| Repository scripts s-z after workflow-contract synchronization | Complete 341-test shard passed in 225.6 s, five expected platform skips; measured before the later generic-tool additions. |
| Generic automation, parity approval and coverage-CI contract | 58 tests passed in 115.5 s, zero skips; includes pre-write convergence, protected-authority rejection and fail-fast verifier ordering. |
| Real synchronization | All-provider check passes; repeated selected writes change zero files. The structure preflight took 0.8 s. |
| Golden redistribution renewal and historical binding | Canonical Golden validation: 80 tests passed; common sync: 23 passed; package policy: 78 passed in 319.4 s. The historical-selector regression first reproduced the planned-commit failure, then the complete 46-test parity-contract module passed in 79.1 s with original H1/H2 and H1-H4 rejection tests retained. Committed-candidate and final release gates remain required. |
| Roadmap-test simplification | Reproduced the stale-heading failure; replaced the first method's paragraph snapshots with canonical routing/version-structure checks. Full Architecture project passed 256/256 with zero skips before the final compact later-version presence guard. Actual Golden/package/firmware tests remain unchanged. |
| Infrastructure seed deletion gate | Seeds 1, 1738590270, 460925769 and 1950330798 passed in 107.9/108.3/106.5/109.6 s. Each reconciled 1,070 identities, 1,068 passed and the two existing approved Unix-only skips, with zero failures and normal freshness/coverage/cleanup. |
| Temporary seed removal | Existing command regression failed with the old pin, then passed after removing only the seed. Complete orchestration module: 195 tests in 15.5 s, four expected platform skips. Both serialization settings and all identity/coverage gates remain. |
| Ordinary-document workflow | Root/docs instructions, CONTRIBUTING, execution runbook and Polytail policy agree on the R0 diff/affected-link short path. Thirteen existing authority-boundary regressions passed in 12.0 s; affected links and scoped review passed. Governed documents and all release/Golden gates remain protected. |
| Version-header synchronization | All 21 common-tool tests passed, including no-other-bytes/idempotence and negative header/SDK checks. The actual write updated only the two numeric headers; subsequent all-provider check and selected write changed zero files. The historical tag index no longer needs a duplicate current-version entry. |
| Path-state Git transport | All 114 governance tests passed in 197.5 s, including frozen v1 digest equality, corruption/deletion/mode/duplicate-path handling and existing historical tamper gates. Only exact-read transport is batched; no digest or historical authority is changed. |

The 70 affected Application clock/registry tests also passed. Workflow validation
and Ruff passed. The earlier scoped review returned `PASS-WITH-HUMAN-GATE`.
Pre-freeze review then found the per-case identity and raw source-CI metadata
gaps recorded above; both are corrected locally and independent incremental
reviews returned `PASS-WITH-HUMAN-GATE` with no P0-P3 findings. The resulting
pre-freeze structure check passed in 144.7 s. Integration and release-owner gates
are not waived.
After the owner-authorized, personally reviewed ten-file AGENTS batch received
one aggregate [R2 admission](../governance/change-records/AGENTS-113-SCOPE-RECONCILIATION-01.json),
the latest `--structure-only` passed in 135.3 s after the Git-read and version-header
corrections, including a 0.7 s derived-file check. The preceding local structure
pass was 198.7 s: these single-machine samples show about 32% less structure-lane
time, not a measured full-CI or release improvement.
No classifier, historical record, Golden expectation or release permission was
relaxed. The previous missing-record blocker and four-seed deletion gate are
closed; frozen-head integration and release gates still remain.

Mechanism assessment: exact changed-source/review binding and tamper detection
remain useful for firmware/release authority (ADRs 0054/0059/0061). A filename-only
R2 classification cannot distinguish instruction wording from a real permission
change; this batch's ten-file barrier is evidence of that overbroad scope, not a
reason to disable all authority checks. The existing aggregate admission resolves
this occurrence without a new bypass mechanism. Paragraph snapshot assertions
have been reduced. Ordinary non-normative, non-classifier-governed prose now
uses diff/affected-link review, with extra checks for layout or parsed consumers;
it needs no issue, code-size census, full-suite run or separate handoff artifact.
This does not reclassify AGENTS/governance or normative release/permission rules.
A read-only caller profile measured 1,228 per-path Git processes taking 67.2 s
within the path-state digest. That transport now uses at most two Git processes
per record with unchanged v1 digest bytes; the full governance module passes.
No immutable-history check has been removed. This local optimization does not
yet establish the complete GitHub CI/release elapsed-time reduction.
The initial local Golden baseline recorded about 62 s of build-related commands
and 118.4/28.2 s test lanes, but no complete stopwatch; it cannot establish a
total percentage reduction. These local results are not a fresh GitHub
CI/package/publication measurement.

Read-only inspection of historical release run `33905351419` confirms the
319-second packaging step includes Desktop self-contained/ReadyToRun publish
(about 184 seconds from clean completion to app publish), managed launcher,
worker packaging (about 10 seconds), ZIP creation, and subsequent distribution
launcher packaging. No packaging flags or artifacts were changed in this batch;
disabling ReadyToRun would require a separately measured startup/package tradeoff.

Owner acceptance now explicitly requires the **complete release time reduction**,
not only the Golden lane. Historical run
[`33905351419`](https://github.com/Dennis40816/nvt_fw_combiner/actions/runs/33905351419)
took **31 min 15 s** from creation to completion: source verification 1,303 s,
packaging 319 s and promotion 65 s, with remaining setup/transfer/queue time.
Its `published-smoke` job was skipped, so it is not a complete smoke-equivalent
baseline. Measure the new admitted source from release dispatch through successful
public-download smoke, report phase durations and queue/approval waits separately,
and calculate `(1875 - newElapsedSeconds) / 1875 * 100`. Label this as comparison
with the historical observed workflow, explicitly noting the added smoke gate.
Do not substitute local Golden time for GitHub time or claim an achieved reduction
before that authorized run. The separate source-CI duration must also be reported
so moving work before release is not represented as deleting it.

`v1.1.3` completed the four-seed Infrastructure gate and removed only the
temporary seed pin. This sample is not a claim that all possible orderings are
deterministic; final verification without the pin remains required. Shared
restore/lock-file mutation must be isolated before parallelizing script tests
against .NET builds. The exact
residual [VERIFY-111-XUNIT-SEED-01](../governance/change-records/VERIFY-111-XUNIT-SEED-01.json)
is immutable historical evidence; its former `v1.1.2` allocation is superseded
to `v1.1.3` by the current owner decision.

## `1.1.4`: UI corrections and AB Dummy DP

Latest owner decision on 2026-09-05: consolidate all existing UI corrections
formerly split across `1.1.4`-`1.1.12`, plus the retained theme audit, into
`1.1.4`. This supersedes the earlier same-day decision to move only the
former `1.1.3` UI. The owner delegated the remaining task allocation below;
`1.1.3` CI/release work continues and receives none of these UI changes.
The later owner amendment adds the AB Dummy DP checkbox and its complete
firmware behavior to this same release; it is not a visual-only change.

Implement the following as bounded changes within one release, using the
existing controls and semantic owners. This allocation does not approve a
new visual reference, a state-lifetime decision, or a firmware/delivery
contract change. Close those existing gates before the affected implementation.

| Workstream | Retained scope and acceptance boundary |
| --- | --- |
| Session diagnostics and Version page | Privacy-filtered current-session diagnostics/history; version list from typed admitted Catalog `releaseNotes`; complete Settings Version-page proportion audit. Obtain owner approval of a full-page reference at exact viewports before changing width, column ratio, modal/page scale or minimum-size behavior. |
| Shared visual/theme coverage | [Issue #291](https://github.com/Dennis40816/nvt_fw_combiner/issues/291): reproduce the recorded dark-theme baseline and audit Merge, Replace, Settings, Message Center, Report, Inspector, dialogs, menus, tooltips, cards and overlays. Cover normal/hover/focus/selected/disabled/checking/verified/warning/error states, Light/Dark/high contrast, token ownership, contrast and keyboard visibility. Reuse this common regression matrix across the corrections; do not turn it into an unrelated redesign. |
| CtrlRAM selector | [Visual contract](../ui/v1.1.x-ctrlram-selector-visual-contract.md): common horizontal anchors, stable section/input outlines unaffected by hover, constrained-viewport vertical spacing and complete wrapped-title/filename height with badge gaps. The proposed reference remains pending approval; retain exact viewport, language, scale and accessibility evidence. |
| First-entry IC selection | [Navigation handoff section 1](../ui/post-v1.1.0-navigation-and-ctrlram-first-open-handoff.md#1-shared-first-entry-ic-selection): reuse Home/navigation/accepted-session admission when a workflow lacks compatible accepted IC context. Decide selection lifetime, invalidation, cross-workflow compatibility and Cancel/Back behavior first; no second catalog, remembered-selection owner or UI-only admission rule. |
| Memory Layout | Make each canonical section and its address-space/range boundaries explicit instead of flattening independent sections. Use typed Application-to-Presentation projections; no firmware facts, range authority or memory interpretation move into UI. |
| AB DP metadata | [Metadata handoff](../ui/v1.1.x-ab-dp-metadata-layout-handoff.md): DP1/DP2 Version and optional Jira Index layout, long/missing values, unknown-bank feedback and unused width. The AB IC selector/Mode correction already completed in `1.1.2` is not reopened. |
| Standard Merge feedback | [Verification-feedback handoff](../ui/v1.1.x-standard-merge-input-verification-feedback-handoff.md): show uniform-content warnings and useful localized range/finding/cause/next-step/Build-impact copy, without changing warning severity, `BlocksBuild`, validation ownership or firmware bytes. |
| Report History | [History handoff](../ui/v1.1.x-report-history-usability-handoff.md): single-click cards, entry-only accessible delete action, full Raw JSON copy and Report-owned Load report entry. Preserve persistence/schema/capacity and existing cancel/latest-request/bulk-clear behavior. |
| Report Changes | [Compare handoff](../ui/v1.1.x-report-changes-compare-handoff.md): scrollbar gutter, physical-section grouping, Light Original colors and localized address layout. Preserve raw runs/order/hash/Why/Result/replay and virtualization; no nested scroll owner or report-semantic change. |
| Bundle primary-output rename | [Rename handoff](../ui/v1.1.x-bundle-primary-output-rename-handoff.md): editable primary filename while bundle output is enabled, independent folder name and consistent accepted override across report/receipt/Explorer/recent output. Retain R2 host-delivery review, collision/cancellation tests and unchanged bytes/automatic naming/other outputs. |

Use this order inside the release: agree full-page/reference and interaction
decisions; correct shared theme/control foundations; implement the independent
selector/metadata/feedback/report changes; complete navigation and delivery
contract changes; run the combined UI regression and packaged observation.
Do not require a complete release cycle for each small UI change.

Measured startup/first-open performance is a separate `1.1.5` outcome, not
an omitted UI correction. New General/saved-rule/IC-rule authoring capabilities
below are product expansion, not permission to redesign existing screens in
`1.1.4`. Ordinary UI corrections do not change firmware semantics. The requested
Dummy DP implementation must pass its own firmware gates below.

### AB Dummy DP checkbox and firmware behavior

Implement the owner-requested [Dummy DP scope](../ui/v1.1.x-ab-dummy-dp-handoff.md):
the checkbox is unchecked by default; when on, every output section that does not belong
to TP is filled with `0xFF`, and DP input is not allowed. This is not merely
filling the section named DP, and it was not shipped in `1.1.2` or `1.1.3`.

Before implementation, approve the per-profile TP/non-TP ownership and complete
output/write-range contract, then obtain independent expected output and the
firmware-owner R3 evidence. Keep this feature's Golden gate with this version;
the separate `1.1.7` evidence backlog does not defer or waive it. No support
promotion or byte behavior is certified by scheduling alone.

## `1.1.5`: measured startup and first-open performance

| Target | Measurement and implementation gate |
| --- | --- |
| Packaged Home first window | Close the retained [700 ms residual](../references/v0.10.4-startup-700ms-evidence.md): exact package on the controlled owner Windows machine, one unscored warm-up and five scored launches, median nonzero main-window handle at or below 700 ms; record cold behavior separately. The stable predecessor did not reproduce the target. |
| CtrlRAM Replace cold first-open | [Navigation handoff section 2](../ui/post-v1.1.0-navigation-and-ctrlram-first-open-handoff.md#2-ctrlram-replace-cold-first-open-performance): establish cold/warm and per-stage baselines with fixed IC/topology/catalog/profile/window/theme/preload. Obtain a measured target and owner approval before optimization. The Home 700 ms threshold does not apply automatically. |

Reuse the existing preload, navigation, accepted-session and immutable
projection owners. Preserve visible loading, validation, slot/readiness
equivalence and bounded lifetime; no duplicate semantic path or unbounded
cache. Claims require comparable packaged measurements, not a stopwatch from a
different environment.

## `1.1.6`: agent workflows, documentation and minimality

The owner merged the previous `1.1.6` and `1.1.7` milestones on 2026-09-05.
This version combines the AI-skill/workflow audit and bounded pilot with
related documentation, semantic-consistency and minimality work.
Use capability, task difficulty, risk and coordination cost to select models
and reasoning effort from all available models; disclose actual known model
configuration, without permanent model-name roles. Audit skill inventory and
routing consistency and forward-test the resulting guidance.

The [Tool-development retrospective handoff](../governance/post-v1.1.0-tool-development-process-retrospective-handoff.md)
owns the evidence/confidence scorecard, separated bottleneck timeline,
lightweight versus R2/R3 workflow, reusable checklist/automation priorities,
lead-time/rework/flakiness/recovery measurements, reversible pilot and
conductor/architect playbook. The resulting workflow still requires separate
owner approval. Do not migrate every repository, create a new tool repository
or impose enterprise ceremony through this allocation.

### Documentation, semantic consistency and minimality

Retain the complete documentation scope: evidence-preserving semantic
architecture/version-rule convergence, stale SPEC evidence and
current-versus-historical headings, and each active handoff's open TODO,
owner, blocker and next action. CI/release instruction and evidence convergence
remains in `1.1.3`; completed `1.1.2` cleanup is history, not reopened work.
Preserve historical evidence and do not add a parallel documentation framework.

Combine the related dead-code/minimality review with the deferred analyzer
cleanup. Re-measure the recorded baseline before editing: 169 style diagnostics
(`IDE0007` 142, `IDE0002` 10, `IDE0001` 7, `JSON002` 5, `IDE0008` 4,
`IDE0003` 1). Remove only proven-unused code and make reviewable mechanical
changes with affected tests; no authority rewrite, suppression or evidence
deletion is implied.

Reconcile the historical tracker records below against actual implementations
and retained acceptance criteria. This is provenance/status reconciliation,
not permission to auto-close issues or reopen immutable final records.

## `1.1.7`: independent Golden evidence completion

Supply independent expected output for the two retained input-only canonical
cases, then re-review the three fact-scoped aliases that depend on them.
Until those outputs and firmware-owner evidence exist, all five entries remain
repository-only: no packaging, Golden claim or runtime-support promotion.
One topology, IC, workflow or fact-scoped alias never certifies another beyond
its approved evidence scope. Missing external evidence blocks this milestone;
it does not authorize generating expectations from the implementation.

## `1.1.8`: IC and firmware-evidence intake

Allocate the retained new-IC/firmware-evidence/capability intake here, including
NT51950 AB `1 IC`/`Cascade`, selector-free NT51951 AB, Perfect-family and
`ldc-tp-only` evidence tracks. Inventory each track's existing implementation,
actual evidence gap and profile-owned contract before choosing its bounded
change. These are evidence/intake tracks, not claims that existing functions
are absent or that support is newly certified.

Each affected route retains profile, independent Golden and firmware-owner
gates; use `1.1.7` evidence only within its independently admitted scope.
Earlier firmware-affecting milestones still require their own applicable
evidence and cannot wait until this version to execute mandatory Golden cases.

## `1.2.0`: bounded Launcher hardening and development

Launcher remains secondary to the `1.1.x` UI/performance priorities, but this
version begins real development: a comprehensive current defect, security and
evidence inventory, followed by one owner-approved, reviewable remediation
tranche. This is not merely an architecture or extraction review.

Inventory remaining Launcher/Installer refinements for `1.2.1`-`1.2.3`.
Preserve package identity, installed bytes, managed-version, verification,
recovery and rollback authority. Production activation remains NO-GO until the
separate security/evidence and activation gates below close.

## `1.2.1`: publisher trust, signing and security closure

Address the publisher trust, signing and security/evidence gaps identified by
the `1.2.0` inventory as bounded reviewed changes. Key custody, signing
identity, external service configuration and permissions require their actual
owner approvals; no credential access, production deployment or trust-policy
relaxation is implied. Produce the independent R3 evidence required before
Catalog/Registry activation.

## `1.2.2`: conditional Catalog and Registry activation

Make the production GO/NO-GO decision only after the independent R3 security
and evidence closure passes and the owner approves the actual activation.
The planned version is not itself a GO decision. Revalidate package identity,
verification, recovery and rollback readiness; do not activate with missing
evidence or replace protected publication with an agent-side path.

## `1.2.3`: download minimization and Installer refinements

Address delta-download minimization and the remaining bounded Launcher/
Installer refinements from the `1.2.0` inventory. Preserve exact installed
bytes, package identity, verification, recovery and rollback semantics.
Measure transfer savings and failure/recovery behavior before making an
improvement claim; later-discovered unrelated work needs a new allocation.

## `1.2.4`: General Merge authoring

Complete the retained General Merge authoring scope through the existing
typed mappings, profile compiler and shared planner/executor. Define the
bounded authoring gaps and acceptance from the current implementation before
coding; do not rebuild already-complete execution infrastructure.
Preserve profile-owned access, overlap, range, validation and integrity rules.
Saved/custom rule persistence is a separate `1.2.6` outcome.

## `1.2.5`: General Replace authoring and DP Replace decision

Complete retained General Replace authoring through the same typed operation
model, immutable required reference and canonical access/postbuild policies.
UI/CLI cannot bypass TP, range, integrity or processor authority.

Close the separately retained ordinary DP Replace retirement-or-reopening
decision in this version. Neither choice is preselected: retirement requires
its migration decision; reopening requires applicable capability and firmware
evidence. Do not infer either action from the published `1.1.0` or this schedule.

## `1.2.6`: saved and customized rule authoring

After the General authoring contracts are settled, complete saved/customized
rule authoring, persistence, import and validation through the existing typed
operation model. Preserve validation on load/import and the same execution
owner; arbitrary scripts and per-run executable paths remain forbidden.
Rule/schema/migration details require their existing contract review before
implementation.

## `1.3.0`: IC rule authoring and Launcher extraction review

### IC family / rule-authoring UI

Owner decision on 2026-09-05: defer the IC family/rule-authoring UI from its
tentative `1.1.9` slot to `1.3.0`. This schedules the maintenance feature, not
an already-approved screen specification. General Merge/Replace and saved/custom
user rules retain their separate `1.2.4`-`1.2.6` allocations.

The [SPEC](../../SPEC.md) describes a future maintainer-facing editor that
could create, validate and export untrusted IC-definition bundle candidates:
family membership, declared memory maps and reusable profile/rule definitions.
This is different from selecting an already supported IC or editing one
General Merge/Replace mapping and saving that user rule. It is not an
existing-screen correction omitted from `1.1.4`.

Define the bounded editing/export scope before implementation, and admit the
required trusted-bundle/evidence models and IC/profile/rule contracts.
Candidates remain untrusted until independent review, CI/evidence and
firmware-owner approval promote exact bytes through the existing trust index.
The UI cannot mutate the live catalog, grant support or approve its own rules.

### Launcher and publication-system extraction review

After `1.2.0` begins the first remediation tranche, review whether Launcher
and release/publication infrastructure can move to an independently versioned
repository. This is not the first Launcher delivery, and extraction is not
pre-approved.

An ADR must first define public contracts, migration/deletion milestones,
repository trust boundary and rollback. Do not duplicate NVT FW Combiner's
managed-version semantics or move firmware facts, composition, profiles,
Golden authority or product-specific support policy into the extracted owner.

## Explicit owner-unallocated queue

All items inventoried for the 2026-09-05 owner resequencing now have the
milestone allocations above. Execution prerequisites and external-evidence
blockers stay with those milestones; a scheduled item is not automatically
approved for implementation, support promotion, publication or activation.
New findings require explicit allocation rather than silently expanding a
release.

### Historical tracker reconciliation

| Retained issue | Current allocation and reconciliation boundary |
| --- | --- |
| [#380 preload evidence/release](https://github.com/Dennis40816/nvt_fw_combiner/issues/380) | Current CI/release residuals belong to `1.1.3`; the Home startup residual belongs to `1.1.5`. Reconcile historical completion/provenance in `1.1.6`; do not restore its old five-minute CI target or re-release `0.10.5`. |
| [#291 theme audit](https://github.com/Dennis40816/nvt_fw_combiner/issues/291) | Full existing-surface theme audit belongs to `1.1.4`, with its retained reproduction/accessibility evidence. |
| [#2 early UI planning](https://github.com/Dennis40816/nvt_fw_combiner/issues/2) | Reconcile the early umbrella in `1.1.6`; route real existing-screen corrections to `1.1.4` and new authoring work to its explicit milestones, without redoing completed demo/shell work. |
| [#1 early core implementation](https://github.com/Dennis40816/nvt_fw_combiner/issues/1) | Reconcile the early umbrella in `1.1.6`; an old open item is not evidence that the current compiler/planner/executor is missing. Retain any genuine unmet acceptance criteria. |

GitHub still owns live open/closed state. This table allocates work and does
not close, relabel or rewrite the issues.

## Update rule

Dated `0.9.x` roadmaps are historical evidence. New milestone ordering or
resequencing is recorded here, while implementation detail is changed only in
its canonical specification, ADR, contract, profile, evidence record, or issue.
