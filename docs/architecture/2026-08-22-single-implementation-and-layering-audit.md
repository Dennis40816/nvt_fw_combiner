# Single-implementation and layering audit — 2026-08-22

Status: evidence snapshot and refactor input; not authority for firmware
semantics. Canonical ownership remains in `SPEC.md`, accepted ADRs, contracts,
profiles, and the two `0.10.x` maintainability working designs.

Current-head refresh: 2026-08-23 at
`c660e84e774e1b25c0ec0774f430e76c11232f47`. This refresh supersedes the
numeric inventory and verification counts below where explicitly stated; it
does not rewrite the original 2026-08-22 provenance.

## Scope and method

The audit covers every production project under `src/` at audited feature
commit `611eae91c707b2cca2abe85aa5f6764e9fd65e45`. It inspected project
references, production declarations,
callers, ports/adapters, compatibility/fallback terms, large partial modules,
canonical ownership documents, and architecture/behavior tests. The final
verifier is recorded after the working tree is frozen.

The 2026-08-23 refresh repeats the production declaration/caller, project
reference, partial aggregate, compatibility/fallback, canonical route-policy,
test-source, and verifier-evidence inventories at `c660e84e`. The static
low-reference scan is only a candidate finder: DI roots, XAML/reflection,
extension-method dispatch, package entry points, ports/adapters, and tests are
checked before any deletion disposition.

“One implementation” means one semantic owner. A port and its infrastructure
adapter, a compiler and its UI projection, or two closed protocol adapters are
not duplicates when they do not independently decide the same fact.

## Result

No confirmed duplicate firmware-semantic owner was found in the audited
capabilities. The repository already has strong executable convergence tests
for catalog, input inspection, execution, naming, memory layout, processor
plans, page isolation, persistence, and retired compatibility paths.

One concrete dependency defect, one owner-decision-only adapter candidate, and
four structural refactor candidates remain. They do not justify broad
refactoring before the current functional verification is finished. The
refresh still finds no `workaround`, `obsolete`, or `deprecated` marker in
production source; text occurrence is not used as proof that a module is live
or dead.

## Capability inventory

| Capability | Canonical semantic implementation | Adapters/projections | Executable evidence | Disposition |
| --- | --- | --- | --- | --- |
| Catalog and route resolution | `CanonicalCapabilityCatalog` joins the Application snapshot; `TrustedProfileBundleCatalog` normalizes/compiles trusted profile definitions | Bootstrap constructs one source; UI/CLI query the published snapshot | `CanonicalCapabilityCatalogOwnsItsPublicationLifecycle`, `CanonicalCapabilityCatalogStaysApplicationOwnedAndReferenceOnly` | retain |
| Authoring sessions | One `AuthoringSessionState` per workflow mode owns revision, selected input state, inspection publication, and invalidation | CLI creates an ephemeral session; Presentation owns the fixed six UI instances | session behavior tests and `PageInspectionIsolation` architecture checks | retain; reduce shell coordination later |
| Selected-input inspection | `CompiledInputArtifactInspectionService` and the compiled metadata plan own canonical artifact observations; General selection adds only content binding | `BuiltInFirmwareInspection` supplies the file/environment adapter; slot cards only project | `HeadlessSlotHealthKeepsOneApplicationInspectionAuthority` | retain |
| Readiness and admission | Separate typed dimensions: input selection, active-session blocker, general mapping admission, and final `CapabilityActionReadinessResolver` ranking | Presentation localizes the typed blocker/next action | readiness behavior suites and ownership design § “Accepted readiness decomposition” | retain dimensions; block new parallel `IsReady` logic |
| Output naming | `CompiledOutputNameResolver` is the sole renderer dispatch; `AcceptedSessionOutputNameResolver` binds an accepted session; `AbCodeOutputNameResolver` is one closed renderer selected by the compiled rule | `CompositionOutputNamingExperience` is the Application port used by UI/CLI | `MemoryAndNamingHaveOneApplicationOwnedProjectionPath` plus naming parity tests | retain; helpers are not competing authorities |
| Memory layout | `MemoryLayoutProjector` derives one typed layout from the compiled composition | `UiCompositionRunner.Common` maps typed segments to localized view models; map interaction is Presentation-only | `MemoryAndNamingHaveOneApplicationOwnedProjectionPath`, memory-layout behavior/UI tests | retain |
| Profile compilation | `V2CompositionPlanCompiler` lowers normalized definitions into the one operation model | `TrustedProfileBundleCatalog` selects the exact definition; no UI compiler | canonical operation/profile/compiled-composition architecture tests | retain owner; split internal phases later only |
| Preview/Build execution | `CompositionRunService` owns byte execution, processor invocation, output validation, and report evidence behind `ICompositionExecution` | `CompositionExecutionExperience` accepts a session; UI and CLI call the same port | `AcceptedWorkflowsExposeOneExecutionOperation`, golden and run-service suites | retain owner; reduce module depth later |
| Reports and history | `CompositionRunReport` is immutable run evidence; report projectors translate it; `ReportHistoryFileStore` persists only bounded UI history | `ReportReviewTypedProjector` and report ViewModels do not recompute firmware facts | report behavior, history persistence, and startup structure tests | retain |
| Output delivery | Application creates the delivery proposal/source plan; `AtomicFileCompositionOutputWriter` and `AtomicBundleCompositionOutputWriter` implement distinct loose/bundle commit contracts | protected destination provider selects the declared delivery type | additional-delivery convergence and atomic writer suites | retain |
| Settings/preferences | `SettingsViewModel` owns modal presentation state; `ShellPreferenceFileStore` owns the bounded local preference document | `LatestSnapshotPersistenceCoordinator` serializes latest-snapshot writes | `SettingsModalDoesNotOwnWorkflowNavigationOrFirmwareState`, `LocalUiFileStoresShareOneBoundedPlatformAdapter` | retain |
| Managed versions | `VersionManagementExperience` owns the use case and `ManagedActivationCoordinator` owns launcher recovery; `ManagedPackageVerifier` owns package verification | file-system catalog/repository/state-store adapters implement ports | version-management Application/Infrastructure/launcher/UI suites | retain bounded context; fix project references |
| System Activity and diagnostics | `SystemInformationService` owns the sole bounded current-process activity/diagnostic list | Message Center localizes and filters; JSON exporter serializes the privacy-filtered contract | System Information, exporter, Message Center, and reference visual tests | retain |
| Hex editing | `RawBinaryEditorSession` owns bounded in-memory edits; `RawBinaryEditorFileSession` owns file I/O | Hex workspace/control owns viewport and commands | Hex editor behavior and Workbench structure tests | retain |
| External processors | `ExternalProcessorRouter` is the single port implementation entry; `LegacyCombinerPostbuildProcessor` handles compiled multi-command protocol plans and `ExternalCombinerProcessor` handles manifest invocation profiles | `SystemExternalProcessRunner` is the OS process adapter | `ExternalProcessorAdapterDoesNotReconstructCompiledPlans` and processor safety suites | retain both protocol adapters |
| Compatibility paths | Remaining legacy postbuild/profile resolution is closed to named callers and has no general fallback authority | route-scoped adapters only | `RemainingCompatibilityAuthoritiesHaveClosedProductionCallerSets`, retired-helper absence tests | retain until their declared deletion gates |

## Layering assessment

| Layer | Assessment |
| --- | --- |
| Contracts / Domain | Clean bottom layers. Domain has no project references; serialized-shape and firmware invariants remain separated. |
| Profiles | Correctly depends on Domain/Contracts and owns normalization plus compilation. No Presentation or Infrastructure dependency. |
| Application | Firmware use cases and ports are correctly located, but its project file has an unused reference to `NvtFwCombiner.VersionManagement.Application`. No Application source file uses that namespace. |
| Infrastructure | Correct adapter direction to Domain/Application/Profiles, but its project file has an unused reference to `NvtFwCombiner.VersionManagement.Infrastructure`. No main Infrastructure source file uses that namespace. |
| Bootstrap | Correct composition root in behavior, but currently obtains managed-version types through those two transitive references instead of declaring the bounded-context dependencies itself. |
| Presentation / CLI / Desktop | Consume Application/Bootstrap boundaries. UI contains large orchestration modules, but architecture tests prevent firmware facts from moving into them. |
| Version-management bounded context | Its Application/Infrastructure split is coherent and independently tested. The required cleanup is at the main application composition boundary, not inside version semantics. |

## Findings and refactor candidates

### A1 — direct version-management dependency ownership (high)

`NvtFwCombiner.Application.csproj` references
`NvtFwCombiner.VersionManagement.Application`, and
`NvtFwCombiner.Infrastructure.csproj` references
`NvtFwCombiner.VersionManagement.Infrastructure`, yet `rg` finds no source
usage in either project. `CompositionHostServices` in Bootstrap constructs the
version-management graph by relying on those transitive references.

After all current version tests are frozen, move both version-management
project references to `NvtFwCombiner.Bootstrap.csproj`, remove the unused lower
references, add an architecture test for the exact graph, then run the launcher,
version-management, Bootstrap, and full gates. This is a dependency-only
refactor and must not alter update, verification, activation, or rollback
behavior.

### A2 — Presentation orchestration depth (high)

The refreshed partial-module scan reports `MainWindowViewModel` 1,060 lines,
`WorkflowSessionPresentationViewModel` 1,687, `MergePresentationViewModel`
1,346, and `ReplacePresentationViewModel` 1,813. The shell target says
`MainWindowViewModel` should remain shallow. The current page-isolation tests
protect behavior, but the aggregate size raises change-coupling risk.

Refactor later by extracting focused navigation/activity/build-result
coordinators behind existing typed children. Do not create a generic workflow
god ViewModel and do not move session/readiness/firmware semantics into UI.

### A3 — compiler and execution owner depth (high)

`V2CompositionPlanCompiler` totals 2,798 lines across nine partials and
`CompositionRunService` totals 2,337 across sixteen. Each is currently one
correct semantic owner, so replacing it with parallel services would make the
architecture worse. After golden and exact-write evidence is frozen, extract
pure internal phase objects while retaining one public compiler and one
execution port.

### A4 — readiness vocabulary density (medium)

Input selection, General admission, active-session blocking, and action ranking
are intentionally separate dimensions, not confirmed duplicates. Their similar
names make a new `IsReady` helper easy to add incorrectly. Future changes must
declare the readiness dimension and reuse the final typed blocker/ranking path.

### A5 — unused-module deletion confidence (medium)

No zero-caller production module is confirmed safe to delete. The compiler,
targeted caller scans, and compatibility architecture tests found live or
closed callers for the inspected candidates. A later deletion pass must combine
static call paths, DI/composition roots, XAML/reflection roots, package entry
points, and focused characterization tests; a low text-reference count alone
is not deletion evidence.

The refreshed conservative low-reference declaration scan produced two
type-name candidates after production callers, composition roots, XAML roots,
and focused tests were checked. `SystemActivityText` is live through its
extension methods, so
the class name itself is not expected at call sites. The other,
`SavedRuleDocumentIdentityReader` (55 nonblank lines), still has no production
composition-root instance; it implements the live
`ISavedRuleDocumentIdentityReader` port and is covered by focused strict-JSON
tests. This remains an explicit owner decision from the `v0.10.4` audit, not an
automatic deletion: delete the concrete adapter only if the owner confirms
that external Saved Rule identity adapters are not a compatibility boundary.

## 2026-08-23 code-size refresh

All measurements use the physical nonblank rules in
`scripts/code_size_policy.py`. Tests and data are shown separately so neither
can be presented as application production size.

| Scope | Files | Nonblank lines | Interpretation |
| --- | ---: | ---: | --- |
| Full production C#/AXAML | 787 | 109,900 | Complete shipped source metric |
| Counted non-UI runtime | 535 | 75,253 | Four ADR 0021 runtime slices, including CRC worker |
| Domain + Profiles | 156 | 20,632 | Canonical firmware model, validation, normalization, compiler |
| Application, including Version Management Application | 217 | 33,445 | Authoring, inspection, readiness, execution, reports, version use cases |
| Bootstrap + CLI + Desktop + Launcher | 34 | 3,503 | Composition root and process entry points |
| Infrastructure + Contracts + Version Management Infrastructure + CRC worker | 128 | 17,673 | Filesystem/process/trust adapters, wire DTOs, worker |
| Presentation C#/AXAML project | 257 | 34,887 | ViewModels, Views, resources, styles and shell; CRC worker explains the 240-line difference from `full - runtime` |
| .NET test C#/AXAML | 596 | 123,611 | Evidence source, excluded from production ratchets |
| Repository Python verifier tests | 25 | 10,550 | Verifier/release/coverage orchestration evidence |
| CRC worker tests | 4 | 268 | Protocol and CRC vectors |
| Repository Python scripts | 21 | 11,545 | Tooling, not application production |
| Profile JSON | 110 | 30,688 | Reviewed data authority, not C#/AXAML source |
| Contract JSON | 37 | 35,421 | Schemas/policies, not C#/AXAML source |

There are zero exact duplicate JSON groups under the measured profile/contract
roots. The full-production increase above the historical 96,044 review floor
is not explained by a discovered duplicate firmware engine. Its largest
current concentrations are:

| Area | Nonblank lines | Assessment |
| --- | ---: | --- |
| Presentation ViewModels | 20,300 | Largest UI concentration; orchestration depth and localization projection, not firmware authority |
| Application Authoring | 8,448 | Six isolated sessions plus General/selection lifecycle; keep one shared typed lifecycle owner |
| Application Composition | 8,225 | One Preview/Build/report/delivery path; large but convergence-protected |
| Domain Composition | 7,600 | Immutable execution vocabulary and invariants |
| Profiles V2 | 5,286 | One canonical normalization/compiler path |
| Infrastructure Composition | 6,630 | Catalog/profile/inspection adapters; semantic decisions must remain upstream |
| Version Management Application + Infrastructure | 4,663 | Separately bounded update/install/activation context |
| Presentation Views/Resources/Styles | 9,538 | Current bilingual, Light/Dark, responsive and interaction surface |

The largest correct semantic owners remain
`V2CompositionPlanCompiler` (2,798), `CompositionRunService` (2,337),
`FirmwareFamilyResolutionNormalizer` (1,807), and
`AuthoringSessionState` (1,283). Their size justifies internal phase extraction
after functional freeze, not parallel public services. The verifier itself is
also a tooling hotspot (`scripts/verify.py` 3,031 physical lines; its
orchestration test is 3,448 nonblank / 3,770 physical lines), but it is outside
application production and remains
the sole canonical verifier; any later split must preserve one entry point and
the exact lane/coverage inventory.

The deletion/consolidation forecast is intentionally bounded by confirmed
callers rather than a target percentage:

| Order after freeze | Candidate | Maximum evidenced production reduction | Gate / expected outcome |
| ---: | --- | ---: | --- |
| 1 | Move Version Management references to Bootstrap and remove the unused lower references | 0 measured C#/AXAML lines; two project-reference declarations | Correct dependency ownership and lock the exact graph; no behavior change |
| 2 | Decide `SavedRuleDocumentIdentityReader` concrete adapter retention | Up to 55 nonblank lines if deletion is approved | Prove no external/production adapter requirement; retain the live port/use case unless separately authorized |
| 3 | Extract shallow Presentation coordinators from the existing partial owners | No safe saving forecast | Reduce change coupling; require each slice to be net-negative if it deletes a displaced coordinator |
| 4 | Split internal phases inside the one compiler and run-service owners | No safe saving forecast | Preserve one public owner and golden/operation identity; structural clarity, not line-count theater |
| 5 | Converge readiness vocabulary at the existing typed ranker | No safe saving forecast | Delete only a proven re-derivation; do not collapse distinct readiness dimensions |

Therefore the currently evidenced unconditional removable production total is
**zero lines**. The only quantified conditional deletion is 55 lines, and the
only unconditional correction is the project-reference graph. Claiming a
larger saving would require inventing callers or treating required firmware,
trust, UI, or evidence code as duplication.

## 2026-08-23 functional-verification matrix

The exact route denominator is the 78-row
`canonical-capability-policy-v1.json`, not a hand-maintained test list. All 78
routes are authoring-available, but availability is not certification: 76 are
`ContractOnly` and two are `DirectGolden`; publication is 72 `Internal`, three
`Candidate`, two `Supported`, and one `TestOnly`.

| Workflow | Exact routes / ICs | DirectGolden | ContractOnly | Executable evidence | Remaining gate |
| --- | ---: | ---: | ---: | --- | --- |
| Standard Merge | 14 / 10 | 1 | 13 | Profile Contract, `BuiltInV2StandardMergeRoutingTests`, executable canonical/950/951 fixture-oracle tests, CLI and selector lifecycle smoke; these tests do not rewrite policy `EvidenceStatus` | Firmware-owner/direct evidence for certification; owner-visible full-flow check |
| AB Merge | 6 / 5 | 0 | 6 | AB runtime admission, topology, executable fixture regression, CLI, six-order readiness and immutable-session tests; these tests do not rewrite policy `EvidenceStatus` | Same-TP authoring contract; direct evidence and firmware-owner certification |
| DP Replace | 14 / 10 | 1 | 13 | V2 routing, changed-input Golden Regression, 950/951 synthetic oracle, readiness smoke | Owner decision to retain/gate/retire; non-Standard naming gaps; remaining certification evidence |
| CtrlRAM Replace | 33 / 10 | 0 | 33 | V2 registry/plan closure, processor and report metadata, per-family evidence, NT51950 Normal-CtrlRAM non-termination, immutable-session tests | Typed Base discovery; trusted IC provenance; direct firmware/output evidence and R3 sign-off |
| General Merge | 10 / 10 | 0 | 10 | Candidate-profile, CLI, Saved Rule, initializer/mapping/engine tests | R3 owner/evidence decision for stale promotion-blocker metadata; owner-visible mapping/Build check |
| General Replace | 1 / 1 | 0 | 1 | Candidate profile, postbuild readiness, patch, ADR 0044 plan-only Diagnostic Preview, memory projection and UI tests | Owner-visible POSTBUILD/tool-unavailable and output-delivery check; ADR 0044 grants no fixed-workflow Diagnostic Preview authority |

The route-level matrix is supplemented by the following cross-cutting surface
matrix. `Automated green` means the frozen verifier protects the declared
behavior; it never promotes a route or replaces a visual/OS/firmware-owner
gate.

| Surface | Current automated evidence | Current disposition / open gap |
| --- | --- | --- |
| Canonical catalog and Support Matrix | Policy/profile materialization, fingerprint, Support Matrix Application/Bootstrap/UI/architecture tests | Automated green for 78-row identity and separation; 76 rows remain ContractOnly |
| Selector lifecycle and immutable input identity | Standard/AB/CtrlRAM ordering, Checking/Error/recovery, cancel no-op, 100 MB intake, same-path binding, post-Verified mutation | Automated green for approved slice; exact-one-file drop, same-TP authoring, per-slot Clear and CtrlRAM Base discovery remain open |
| Preview/Build and firmware execution | Shared engine, accepted session, operation/trace/report, golden and processor write-range suites | Automated green; ADR 0044 authorizes only General Replace plan-only Diagnostic Preview, not fixed-workflow execution/re-labelling of invalid selected bytes; no firmware byte/range/name/CRC change is authorized by this audit |
| Output naming and delivery | Naming parity, bundle proposal/admission, atomic loose/bundle writers, AB additional delivery, UI/CLI/report parity | Automated green for declared rules; DP/CtrlRAM non-Standard naming requires its firmware-owner/typed-artifact contract decision; native Save As/bundle owner check remains manual |
| Memory Layout | Application projector, Bootstrap convergence, Merge/Replace/UI template and interaction smoke | Automated green for semantic projection and approved Option B; final owner visual check remains manual across theme/language/compact widths |
| Reports and history | Typed report, JSON compatibility, Hex Diff/replay, bounded history persistence and report UI tests | Automated green; OS file reveal/import-export owner check remains manual |
| System Activity / diagnostics | Application bounded/privacy-filtered activity, exporter, Message Center and reference-template tests | Startup-duration event and narrow-window responsiveness remain open |
| Settings/preferences | Modal isolation, bounded preference storage and UI tests | Automated behavior green; remaining Settings positioning/scroll/typography work is visual and requires reference |
| Version management / launcher | Catalog validation, package/install integrity, state store, concurrency, activation rollback, local lab and Version UI tests | Automated local boundary green; UNC/network repository, real side-by-side launcher/restart and update prompt require owner-visible Windows checks |
| Localization/theme/accessibility | Resource/architecture, Light/Dark, focus/keyboard/reduced-motion and UI-smoke contracts | Automated contracts green; Traditional Chinese/English narrow-window and high-contrast visual review remains manual |
| Hex Editor / read-only viewport | Application editor/search, UI edit/cancel/style and report viewport tests | Automated green; large-file interaction and native Save As remain owner-visible OS checks |
| Packaging/release | Structure, package/release policy and managed package security tests | Clean Windows package, provenance/signing, release-owner approval, and the exact `1.0.0` IC/workflow support-subset firmware-owner sign-off remain outside this audit |

### Owner-visible functional-freeze checklist

This dated checklist is the manual half of the matrix. It remains unchecked
until the owner performs or explicitly accepts each observation on the exact
candidate build; headless evidence cannot auto-complete it.

Before checking any item, record `candidate build / commit`, `executor`,
`execution date`, `result`, and an evidence reference (screenshot, report,
hash, or signed review note) next to that item. Blank metadata is a failing
gate, not an implicit pass.

- [ ] Standard Merge: representative ordinary, TP-first and NT51950 DP-first
  selection through native Browse/drop, Preview, Build, output name and report.
- [ ] AB Merge: every exposed topology, two independent TP cards, same/different
  TP intent, additional A delivery, output name and report. Same-TP authoring
  stays blocked until its separate approved implementation lands.
- [ ] DP Replace: representative retained route, exact-capacity failure,
  Preview/Build and output name, without treating AB FlashCode as Normal DP
  authority. Final owner keep/gate/retire decision remains separate.
- [ ] CtrlRAM Replace: representative family/topology including NT51950 Normal
  CtrlRAM, Base discovery, Warning/Error, firmware-version Preserve/Edit,
  POSTBUILD tool readiness, Build and report. Base discovery remains blocked
  on its typed non-terminal-state implementation.
- [ ] General Merge and General Replace: mapping edits, overlap/protected-range
  failures, Saved Rule, General Replace plan-only Diagnostic Preview, tool-
  unavailable state, Build and output delivery.
- [ ] Selector/native OS interaction: cancel exact no-op, replacement while
  Checking, invalid extension recovery, multi-file drop rejection after the
  exact-one contract lands, and no cross-page/session leakage.
- [ ] Memory Layout: approved Option B hover-only map, card overflow, correlated
  row state and no white edge in Light/Dark, English/Traditional Chinese and
  compact widths.
- [ ] Output/report OS integration: editable locked-name flow, loose versus
  bundle folder, collision suffix, native Save As, report import/export and
  Explorer reveal.
- [ ] Settings, System Activity and accessibility: workflow inputs survive the
  modal; narrow-window scrolling does not overlap; startup duration appears
  after implementation; keyboard/focus/reduced-motion/high-contrast behavior
  is readable in both languages/themes.
- [ ] Version management: moved local source, offline installed switching,
  damaged-version delete, retention prompt, real UNC/network source, update
  consent, side-by-side launcher ready handshake and rollback/restart.
- [ ] Hex Editor: representative large-file edit/search/undo/redo/cancel and
  native Save As; read-only report/BIN viewports retain bounded interaction.
- [ ] Candidate package: clean Windows smoke, package allowlist/integrity,
  provenance/SBOM/signing as applicable, and exact `1.0.0` support-subset plus
  firmware/release-owner approval.

The full functional-verification TODO therefore remains open. The automated
baseline is coherent, but accepting the freeze still requires the named manual
Windows/visual checks and the firmware/evidence decisions above. Refactoring
may start only after the owner accepts that gap ledger; a green test count alone
is insufficient.

## Refactor order after functional freeze

1. Fix A1 and lock the exact project-reference graph.
2. Extract the shallow shell slices in A2 without changing the six session
   identities or page-isolation behavior.
3. Decompose internal compiler/run phases in A3 one golden-protected vertical
   slice at a time.
4. Re-audit readiness/naming/persistence after each slice; delete only a
   confirmed obsolete owner, never add a second path for migration convenience.

Every refactor slice must first pass the repository capability-reuse gate and
record `reuse`, `extend-owner`, `delete-then-replace`, `reject-duplicate`, or an
owner-approved migration seam with executable deletion criteria.

## Verification and residual gates

`python scripts/verify.py --all` passed on the frozen candidate on 2026-08-22:
structure, Python, and .NET lanes all passed. The .NET lane passed Domain 411,
Profile Contract 387, Architecture 217, Application 678, Golden 17,
Infrastructure 550 with two Unix-only skips, Bootstrap 974, and UI 623 tests.
The scripts suite passed 391 tests with four skips; the CRC worker passed 30
tests with 100% line and branch coverage.

The exact refreshed head `c660e84e` then passed
`python scripts/verify.py --all` on 2026-08-23: Python scripts 412 with four
platform skips; CRC worker 30/30 at 100% line/branch; and .NET 3,935 total,
3,933 passed, two platform skips. The project inventory was Bootstrap 1,015,
UI 639, Domain 411, Application 687, Infrastructure 560 total, Profile
Contract 387, Golden 17, and Architecture 219. The unchanged coverage policy
passed at 61,393/68,581 lines (89.52%) and 19,211/24,331 branches (78.96%).

Independent architecture, scoped Polytail, and route/evidence reviews found no
P0-P2. All three verdicts are `PASS-WITH-HUMAN-GATE`: the document is a
reliable readiness snapshot, but the unchecked manual/Windows/visual/R3 ledger
is still blocking authority. The final docs-only refresh also passes
`python scripts/verify.py --structure-only`.

The refreshed non-blocking code-size warnings remain explicit refactor inputs:
109,900 full production nonblank lines exceed the review threshold while
satisfying the exact ADR 0021 ratchet; `MainWindowViewModel` remains 1,060
lines against its 985-line partial-aggregate review threshold; and
`ShellTextResources` is 2,503 lines against the 2,500 default review threshold.
Findings A1–A5 and the Saved Rule adapter owner decision remain open for
post-functional-freeze work. The audit itself does not approve firmware-
semantic, version-activation, persistence, or UI behavior changes.

## Source anchors

- Catalog owner: `src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalog.cs:120`.
- Session owner: `src/NvtFwCombiner.Application/Authoring/AuthoringSessionState.cs:11`.
- Readiness ranker: `src/NvtFwCombiner.Application/Capabilities/CapabilityActionReadiness.cs:287`.
- Naming renderer dispatch: `src/NvtFwCombiner.Application/Composition/CompiledOutputNameResolver.cs:9`.
- Memory projector: `src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutProjector.cs:13`.
- Compiler owner: `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.cs:60`.
- Execution owner: `src/NvtFwCombiner.Application/Composition/CompositionRunService.cs:7`.
- System Activity owner: `src/NvtFwCombiner.Application/Diagnostics/SystemInformationService.cs:27`.
- Processor router: `src/NvtFwCombiner.Infrastructure/ExternalTools/ExternalProcessorRouter.cs:8`.
- Unused lower version references: `src/NvtFwCombiner.Application/NvtFwCombiner.Application.csproj:5`
  and `src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj:10`.
- Actual version composition root: `src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs:176`.
- Representative executable convergence gates:
  `tests/NvtFwCombiner.Architecture.Tests/RepositoryBoundaryTests.CanonicalCapabilityCatalog.cs:7`,
  `RepositoryBoundaryTests.InputInspectionStructure.cs:7`,
  `RepositoryBoundaryTests.MemoryNamingConvergence.cs:7`,
  `RepositoryBoundaryTests.SingleExecutionPort.cs:7`,
  `RepositoryBoundaryTests.ProcessorPlanConvergence.cs:7`, and
  `RepositoryBoundaryTests.LegacyRetirementStructure.cs:10`.
