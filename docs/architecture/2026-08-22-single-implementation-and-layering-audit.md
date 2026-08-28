# Single-implementation and layering audit — 2026-08-22

Status: evidence snapshot and refactor input; not authority for firmware
semantics. Canonical ownership remains in `SPEC.md`, accepted ADRs, contracts,
profiles, and the two `0.10.x` maintainability working designs.

Current-head refresh: 2026-08-23 at
`c660e84e774e1b25c0ec0774f430e76c11232f47`. This refresh supersedes the
numeric inventory and verification counts below where explicitly stated; it
does not rewrite the original 2026-08-22 provenance.

Adversarial architecture follow-up: 2026-08-23 at docs-only audit head
`7f853623fc839723bea28d74d8602585332ca563`. This follow-up records pre-existing
boundary and verification-strategy debt that the earlier scoped selector
reviews did not assess. It is refactor input, not evidence of a new firmware
byte regression.

Owner refactor-start authority: 2026-08-24 at
`ff5aa7a3`. The owner accepted the complete recorded functional-freeze gap
ledger as sufficient authority to begin A1-A11 in the dependency order below.
Unchecked Windows, release, firmware-owner, and R3 observations remain explicit
residual gates; this authorization does not relabel them as verified or permit
firmware-semantic changes. The post-refactor gate is a fresh whole-repository
architecture/duplication review followed by owner review of the managed-version
network release-folder contract.

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
ten structural refactor candidates remain. They do not justify broad
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

**Completed 2026-08-24 — A1 plus the project-graph portion of A9.** Bootstrap
owns construction of both version-management contexts; the main Application
and Infrastructure projects no longer carry their unused bounded-context
references. Every project whose source directly consumes a version Application
or Infrastructure namespace now declares that owning project directly. A
fresh-restore review caught that the initial exact-graph guard covered only the
three main composition projects and stale assets had masked missing direct
references in Presentation, Desktop, TestSupport and tests. The corrected
architecture guard derives every source consumer and fails if its direct
reference is absent; the canonical repository validator locks the same graph.
The red Presentation build reproduced 19 missing-type errors and the expanded
guard separately caught the version-Infrastructure test dependency before the
fix. Fresh restore and full Release build now succeed. Production C#/AXAML and
counted runtime size remain unchanged.

### A2 — Presentation orchestration depth (high)

The refreshed partial-module scan reports `MainWindowViewModel` 1,060 lines,
`WorkflowSessionPresentationViewModel` 1,687, `MergePresentationViewModel`
1,346, and `ReplacePresentationViewModel` 1,813. The shell target says
`MainWindowViewModel` should remain shallow. The current page-isolation tests
protect behavior, but the aggregate size raises change-coupling risk.

Refactor later by extracting focused navigation/activity/build-result
coordinators behind existing typed children. Do not create a generic workflow
god ViewModel and do not move session/readiness/firmware semantics into UI.

**Completed 2026-08-24.** Build-result and activity/history ownership were
confirmed already focused, so no duplicate coordinators were created. The one
remaining shell concern—navigation history, breadcrumb projection, pending
clear confirmation, and back/confirm/cancel commands—now belongs to the typed
`ShellNavigationViewModel`. Main retains page application, workflow callbacks,
settings, and blocking-surface composition. The modal remains deferred and all
approved XAML geometry is unchanged. Architecture passes 224/224 and UI Smoke
passes 673/673. `MainWindowViewModel` falls to 974 nonblank lines, while full
production descends to 792 files / 110,594 nonblank lines and counted runtime
remains 535 / 75,289.

### A3 — compiler and execution owner depth (high)

`V2CompositionPlanCompiler` totals 2,798 lines across nine partials and
`CompositionRunService` totals 2,337 across sixteen. Each is currently one
correct semantic owner, so replacing it with parallel services would make the
architecture worse. After golden and exact-write evidence is frozen, extract
pure internal phase objects while retaining one public compiler and one
execution port.

**Completed 2026-08-24.** `V2CompositionPlanCompiler` remains the sole public
compiler and now delegates its admitted resolved-map lowering sequence to one
private `ResolvedMapCompilationPhase`. `CompositionRunService` remains the
sole run publisher; accepted General draft, resolved capability, and delivery
evidence enter before execution and every published evidence property is
get-only. No alternate compiler, executor, result DTO, or cache was added.
Architecture passes 225/225; Application 688/688, Profile Contract 387/387,
Golden 18/18, Bootstrap 1,018/1,018, and UI Smoke 673/673 pass. Shipped
production falls to 793 files / 110,559 nonblank lines and counted runtime to
536 / 75,272.

### A4 — readiness vocabulary density (medium)

Input selection, General admission, active-session blocking, and action ranking
are intentionally separate dimensions, not confirmed duplicates. Their similar
names make a new `IsReady` helper easy to add incorrectly. Future changes must
declare the readiness dimension and reuse the final typed blocker/ranking path.

**Completed 2026-08-24.** The re-audit retained the distinct input-selection,
General-admission, active-session blocking, and action-ranking dimensions.
Three result-level flags with no non-default production writer were removed
with their unreachable client branches. The live action-readiness presentation
path remains typed and unchanged; no generic `IsReady` alias was introduced.

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

**Completed 2026-08-24 as a conservative deletion audit.** Constructor,
assignment, production-caller, composition-root, XAML/reflection, package, and
focused-test scans confirmed no whole production module safe to delete without
an owner compatibility decision. Only the three zero-writer run-result
dimensions proven unreachable by compiled clients were deleted. The
`SavedRuleDocumentIdentityReader` adapter remains the one explicit owner
decision and is not misreported as dead code.

### A6 — workflow-specific execution dispatch (high)

`CompositionExecutionExperience` still switches on `session.WorkflowId` and
dispatches Standard Merge, AB Merge, General Merge, General Replace, DP
Replace, and CtrlRAM Replace through workflow-specific methods before reaching
the shared `CompositionRunService`. The byte engine remains single, so this is
not a second firmware executor, but it conflicts with `SPEC.md` and ADR 0003,
which require the executor not to branch on experience identifiers.

After canonical Golden and manual flow evidence are frozen, bind the remaining
workflow-specific preparation, readiness, delivery, and report facts into one
typed accepted execution envelope. Make `ICompositionExecution` consume that
envelope without an experience switch. Preserve the sole planner/engine,
operation order, ranges, processors, output bytes, names, and reports.

**Completed 2026-08-24.** `AcceptedCompositionExecutionRequest` now resolves
its accepted session identity once into a closed immutable execution delegate.
`CompositionExecutionExperience.ExecuteAsync` invokes that typed envelope and
contains no `WorkflowId`/experience switch; the six route methods retain the
same capability result kind, compiled inputs, readiness, delivery, processor,
run-id prefix, and shared `AcceptedSessionCompositionExecution` convergence.
The exact six-route mapping and absence of executor string dispatch are locked
by red-capable architecture guards. Architecture 222/222, Golden 18/18,
Bootstrap 1017/1017, Release solution build, structure validation, and scoped
Polytail pass. The first correct-but-verbose implementation was rejected by
the repository size gate; the accepted form leaves all production ratchets
exactly unchanged at 791 files / 110,595 nonblank lines and runtime 535 /
75,289, including Application 217 / 33,476.

### A7 — friend-assembly boundary permeability (high)

Application exposes internals to Infrastructure and Bootstrap; Domain and
Profiles also expose semantic internals to those upper layers. Infrastructure
currently constructs against concrete Application internals including
`CanonicalCapabilityCompilerAdapter`, `StandardMergeAuthoringExperience`,
`AbMergeAuthoringExperience`, `DpReplaceAuthoringExperience`, and
`CtrlRamAuthoringExperience`. Assembly reference direction is legal, but the
contract boundary is not narrow: internal implementation changes propagate
across layers.

Replace production friend access one bounded caller set at a time with the
existing focused ports or the smallest missing typed port. Do not make all
internals public and do not move firmware semantics into Infrastructure. Lock
the permitted friend list and compiled dependency graph with architecture
tests before removing each access.

**Slice 1 completed 2026-08-24.** Exact friend-list evidence proved that
Bootstrap directly consumes no Domain or Profiles internal symbol, so those two
unused friend entries were removed and locked by a red-capable exact-list test.
Application/Infrastructure/VersionManagement friend access that still has
compiled callers remains explicit for later bounded A7 slices; it was not
papered over by making internal semantic types public.

**Slice 2 completed 2026-08-24.** Application no longer friends its sibling
Infrastructure assembly. Existing platform implementations now cross the
assembly boundary through their focused Application-owned ports and immutable
adapter records. The only missing seams were one generic compiled-slot
inspection port and one Standard Merge compilation port; the concrete compiler
and four authoring experiences remain internal. Bootstrap remains the sole
composition root allowed to construct them. A compiled metadata guard proves
those implementations are not public, and an exact friend-list guard locks the
removed sibling access. Domain/Profiles-to-Infrastructure and vertical
Bootstrap friend access remain because compiler evidence found live callers;
they are not disguised as completed cleanup. This boundary costs 61 runtime
nonblank lines: Application +60 and Infrastructure +1, with no firmware,
inspection, output, report, or UI behavior change.

### A8 — hidden build-time trust validator (high)

`NvtFwCombiner.Bootstrap.csproj` contains a RoslynCodeTaskFactory implementation
of more than five hundred lines that hashes schemas and manually restates
trust-index, manifest, closed-field, identifier, SemVer, path, and hash rules.
Runtime Infrastructure separately loads and validates the same package family.
Independent build/runtime rejection is required, but two handwritten semantic
interpretations can drift, and the inline C# is omitted from ordinary
production C#/AXAML size reporting.

Retain two independent enforcement points while converging on one normative
schema/contract implementation. Move the build operation to a focused tested
build tool or shared contract validator rather than another runtime service;
Bootstrap must return to wiring/materialization only. Preserve schema hash
binding, closed package admission, and fail-closed behavior.

**Completed 2026-08-24.** The exact existing task and materialization target
were mechanically extracted to the named repository build tool
`eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleMaterializer.targets`.
The normalized task/target body is byte-for-text identical to the previously
embedded block, so the independent build/runtime rejection points and their
normative schema hashes, closed fields, path checks, entry hashes, copy
destinations, errors, and fail-closed behavior are unchanged. Bootstrap's
project is now 28 physical / 27 nonblank lines of properties, references,
content wiring, and one import; it contains no task factory or materialization
target implementation. The focused build tool is 632 physical / 616 nonblank
lines and is now explicitly covered by the repository large-file guard.
Architecture 223/223 (including all materialization mutation cases), Bootstrap
1017/1017, Release solution build, structure validation, and scoped Polytail
pass. Shipped production remains exactly 791 files / 110,595 nonblank lines;
runtime remains 535 / 75,289 and Bootstrap + CLI + Desktop remains 34 / 3,503.

### A9 — architecture-test strategy overfits source text (high)

The Architecture suite passes 219 tests while A1 remains present because
`ProjectDependencyTests` protects only the Domain project graph. Most boundary
tests read source text and assert token presence/absence. These sentinels are
useful for forbidden legacy symbols, but a matching string somewhere in a
concatenated source set does not prove the compiled reference graph, concrete
call path, dispatch behavior, or single semantic owner.

Keep source sentinels only where literal absence is the contract. Add exact
project-reference, assembly dependency, friend-access, public-contract, and
behavior/work-count tests for the high-value boundaries. Each new architecture
refactor must first add a red-capable guard for the defect it removes.

**Completed 2026-08-24.** The refactor now has exact project-reference and
source-consumer dependency guards, exact friend lists, compiled public/internal
visibility checks, single-evaluation work-count evidence, immutable-result
metadata checks, closed six-workflow dispatch checks, bounded CLI-host checks,
and build-tool mutation tests. Source sentinels remain only for literal
repository-shape or forbidden-symbol contracts. The fresh-restore A1 failure
proved the broader project-consumer guard can catch a real compiled-graph defect
that the earlier text-only checks and stale assets missed. Architecture passes
227/227 at the final reviewed head.

### A10 — broad Bootstrap host exposure (medium)

`CompositionHostServices` publicly exposes catalog, authoring, inspection,
naming, execution, file, diagnostics, and version-management capabilities.
Desktop narrows these into Presentation services, but CLI handlers receive the
broad Bootstrap host directly. This is typed, not a string service locator, but
it allows handlers to reach unrelated capabilities and conflicts with the
SPEC's wiring-only Bootstrap target.

Create the smallest CLI dependency record or focused handler parameters at the
entry-point composition boundary. Keep object construction in Bootstrap and
do not introduce a DI framework or a second application facade.

**Completed 2026-08-24.** `CliApplication.RunAsync` is now the only CLI source
that sees `CompositionHostServices`. It projects the existing public ports into
one CLI-only `CliCompositionServices` record; every command handler consumes
that bounded projection and cannot reach file, Hex, diagnostics, version,
startup, or external-environment host capabilities. The guard failed first on
seven broad-host handler files. Architecture passed 222/222, Bootstrap passed
1,017/1,017, Golden passed 18/18, the Release solution build succeeded with
zero warnings/errors, and structure/scoped Polytail passed. The production and
runtime line counts remain exactly unchanged.

### A11 — single-evaluation and immutable-evidence enforcement (medium)

Infrastructure has multiple compilation/evaluation sites for route inventory,
dynamic routes, capability disclosure, and readiness. Determinism tests are
green, but no work-count contract proves one parse/inspect/resolve/compile per
accepted revision. `CompositionRunResult` also exposes internal setters that
are populated after construction, so immutable run evidence is a convention
rather than a type-enforced boundary.

Add invocation-count and identity-sharing tests before consolidating repeated
evaluation. Then construct the complete run result once, or use one private
builder that returns an immutable result. Do not add a process-global cache or
change report/output identity.

**Completed 2026-08-24.** A red-capable work-count test proved the catalog
source classified every route twice. It now classifies each exact route once,
resolves it once, and creates disclosure once per policy revision while
preserving the existing static-before-dynamic progress/failure order. Existing
run tests continue to prove one successful read/hash for a shared immutable
artifact. `CompositionRunResult` now publishes complete defensive-copied,
get-only evidence in one construction; post-return evidence mutation is
impossible. No process-global cache or report/output identity change was made.

## 2026-08-24 post-refactor three-round review

Reviewed range `ff5aa7a3..6c147fb8`; all fixed-diff rounds passed with no
finding: (1) firmware/behavior parity retained operation ordering, six-workflow
bindings and single-run convergence, with Golden 18/18; (2) layering retained
approved XAML geometry, one bounded CLI host entry, focused public ports and
internal semantic implementations; (3) regression/dead-code/size/release
review found no new skip, fallback, duplicate owner, or safe whole-module
deletion, and retained the schema-bound relocatable update-source contract.
The AB IC regression covers consent, topology rebuild, compatible-input
retention and re-inspection. Exact size was 793 production files / 110,620
nonblank and 536 / 75,333 runtime.

The final full gate passed Structure, Python 413 with four declared platform
skips, CRC 30/30 at 100% line/branch, Release build with zero warnings/errors,
and 3,983 .NET tests with two Unix-only skips: Bootstrap 1,018; UI 673; Domain
411; Application 689; Infrastructure 560; Profile 387; Golden 18; Architecture
227. .NET coverage was 89.61% line and 79.11% branch.

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

## 2026-08-25 FORMAL-SUPPORT-01 superseding checkpoint

The 2026-08-25 owner decision supersedes the publication counts in the dated
2026-08-23 matrix below. Catalog `1.10.0` is generated from the independently
frozen 89-axis route denominator and current compiled/dynamic route inventory;
no route, support state, or evidence type is inferred from a filename, PID,
version string, or whole-file Golden hash.

| Policy axis | Current exact count | Decision |
| --- | ---: | --- |
| Standard Merge | 14 | All `Available` + `Supported`; evidence 7 Direct Golden / 2 Approved Alias / 4 Synthetic Oracle / 1 Contract Only |
| AB Merge | 6 | All `Available` + `Supported`; evidence 2 / 2 / 1 / 1 |
| CtrlRAM Replace | 44 | All `Available` + `Supported`; evidence 19 / 5 / 0 / 20; includes 11 new exact TP-prefix base routes |
| DP Replace | 14 | All `Unavailable` + `Internal`; evidence is honestly Contract Only until the `1.1.0` decision |
| General Merge / Replace | 11 | Existing 10 Internal + 1 Test Only publication retained; all Contract Only |
| Total | 89 | 64 Supported / 24 Internal / 1 Test Only; 75 Available / 14 Unavailable; evidence 28 Direct / 9 Alias / 5 Synthetic / 47 Contract |

The 11 TP-prefix routes are separate exact route identities from their
full-flash counterparts. They authorize only the profile-declared effective TP
work-image range. A full-output prefix is Direct Golden evidence for a TP route
only when original-TP execution has exact or approved allowed-difference parity
with that view. NT51950 single and NT51951 single/two-IC TP execution succeeds,
but the outputs differ from their owner full-Flash prefixes because those views
contain DP-origin bytes; these three routes are therefore Contract Only pending
independent TP-only expected outputs. All route ids and capability fingerprints
remain unchanged. Multi-IC input-only cases are `ContractOnly`, not aliases; an
`ApprovedAlias` is used only when its exact source route resolves to Direct
Golden evidence.

The checked-in policy LF-byte SHA-256 is
`bf818a4c9aa4d539882e4bc4a0a662ef70ece67a44e78ae83356430365828f50`.
The current 26-bundle trust index is version `0.10.6.2` with SHA-256
`e365b73e53aff65faa107347400aac82546a3dc700160914b1412f6858fe276d`.
Runtime, packager, smoke, and release-policy tests pin these identities.

Focused local evidence for commit `ed834f9c` passed policy loader 20/20,
canonical host/route convergence 7/7, Support Matrix projection 8/8,
package-trust architecture 17/17, and release-package policy 30/30. The three
previously blocked CtrlRAM ProductServices cases also passed 3/3 after the
policy reconciliation. These are focused checks, not a full frozen-tree
verification.

Follow-up commit `851ffad7` closes the repository cross-link: manifest schema
`1.1` contains 89 strict `routeEvidence` rows, with zero missing, extra, kind,
route-id, or fingerprint mismatches against policy. The later TP execution
reconciliation preserves that join and corrects the totals to 28/9/5/47.

This checkpoint still does **not** claim release readiness. The full verifier,
independent R3/firmware review, package/clean-machine smoke, signing,
provenance, protected CI, and release-owner approval remain mandatory.

## 2026-08-23 functional-verification matrix

The detailed route matrix and owner-visible functional-freeze checklist are
maintained in [the dedicated verification matrix](2026-08-23-functional-verification-matrix.md).
That document remains part of this audit and does not reduce any release or
human-review gate.

## Refactor order after functional freeze

1. Close the canonical Golden manifest-to-runner coverage gate and accept the
   owner-visible functional-freeze ledger; do not begin structural changes
   before this prerequisite.
2. Fix A1 and add the exact project-reference graph portion of A9.
3. Reduce A7 friend access and A10 broad host exposure one compiled caller set
   at a time, with a narrow port and exact dependency tests for each slice.
4. Replace A6 workflow dispatch with one fully bound accepted execution
   envelope. Protect every workflow with direct Golden, synthetic oracle, and
   manual-flow evidence appropriate to its declared evidence class.
5. Extract A8's build validator into one focused, independently tested
   schema-bound tool while preserving separate build/runtime enforcement.
6. Extract the shallow shell slices in A2 without changing the six session
   identities or page-isolation behavior.
7. Decompose internal compiler/run phases in A3 and enforce A11 single
   evaluation/immutable result construction one golden-protected vertical slice
   at a time.
8. Re-audit A4 readiness vocabulary and A5 deletion candidates after each
   slice; delete only a confirmed obsolete owner and never add a second path for
   migration convenience.

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

The earlier independent architecture, scoped Polytail, and route/evidence
reviews found no P0-P2 in their selector/functional-verification scope. Their
verdicts remain `PASS-WITH-HUMAN-GATE` for those diffs. The later adversarial
whole-architecture review adds A6-A11 as pre-existing refactor blockers; it
does not claim a new byte regression or invalidate the scoped test passes. The
unchecked manual/Windows/visual/R3 ledger still blocks functional-freeze
authority. The final docs-only refresh also passes
`python scripts/verify.py --structure-only`.

The refreshed non-blocking code-size warnings remain explicit refactor inputs:
109,900 full production nonblank lines exceed the review threshold while
satisfying the exact ADR 0021 ratchet; `MainWindowViewModel` remains 1,060
lines against its 985-line partial-aggregate review threshold; and
`ShellTextResources` is 2,503 lines against the 2,500 default review threshold.
Findings A1–A11 and the Saved Rule adapter owner decision remain open for
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
