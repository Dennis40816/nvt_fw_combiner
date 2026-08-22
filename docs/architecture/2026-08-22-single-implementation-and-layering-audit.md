# Single-implementation and layering audit — 2026-08-22

Status: evidence snapshot and refactor input; not authority for firmware
semantics. Canonical ownership remains in `SPEC.md`, accepted ADRs, contracts,
profiles, and the two `0.10.x` maintainability working designs.

## Scope and method

The audit covers every production project under `src/` at audited feature
commit `611eae91c707b2cca2abe85aa5f6764e9fd65e45`. It inspected project
references, production declarations,
callers, ports/adapters, compatibility/fallback terms, large partial modules,
canonical ownership documents, and architecture/behavior tests. The final
verifier is recorded after the working tree is frozen.

“One implementation” means one semantic owner. A port and its infrastructure
adapter, a compiler and its UI projection, or two closed protocol adapters are
not duplicates when they do not independently decide the same fact.

## Result

No confirmed duplicate firmware-semantic owner was found in the audited
capabilities. The repository already has strong executable convergence tests
for catalog, input inspection, execution, naming, memory layout, processor
plans, page isolation, persistence, and retired compatibility paths.

One concrete dependency defect and four refactor candidates remain. They do
not justify broad refactoring before the current functional verification is
finished.

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

The partial-module scan reports `MainWindowViewModel` 1,060 lines,
`WorkflowSessionPresentationViewModel` 1,681, `MergePresentationViewModel`
1,346, and `ReplacePresentationViewModel` 1,902. The shell target says
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

The non-blocking code-size warnings remain explicit refactor inputs: 109,849
full production nonblank lines exceed the review threshold while satisfying
the exact ADR 0021 ratchet, and `MainWindowViewModel` remains 1,060 lines
against its 985-line partial-aggregate review threshold. Findings A1–A5 remain
open for post-functional-freeze refactoring; the audit itself does not approve
firmware-semantic, version-activation, or persistence behavior changes.

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
