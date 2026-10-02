# R33-05 candidates and split decisions for 1.2.11

## 1. Status and purpose

Status: O25 decided by the owner on 2026-10-02 (board decision 260); O24 open.
Measured 2026-10-02; no split or execution evidence produced.
Source commit: `d38f4e7c379bce1a908ad1b31d3b2356c58591d4` on `feature/1.2.11/r33-05-candidates`.
Purpose: record the next R33-05 batches under O25 and retain the O24 coverage choice.
T2a from PR #519 is present; decision 260 applies its mechanical-split procedure to the two plans below.
Authority: [ADR 0079, item 7](../../adr/0079-test-architecture.md#7-class-size-naming-and-mechanical-splits),
[pilot](../1.1.13/PLAN-test-pilot-split.md), [inventory](../1.1.14/1.2.x-inventory.md) (R33-05/O24/O25).
[ADR 0080, item 17](../../adr/0080-governance-reset.md#size-and-agent-instructions) remains the size authority:
2,000-line aggregates need owner-approved growth and leave the list below 1,500.

## 2. Repeatable measurement

| Step | Repeatable rule |
| --- | --- |
| Project inputs | Enumerate the eight `tests/*/*.csproj` with `IsTestProject=true`. Take their own `*.cs` excluding bin/obj, add explicit Compile Include links and honor Compile Remove. Probe/TestSupport projects are not test projects. |
| Class identity | Mask comments and string/character literals; group public top-level class declarations by project, namespace and name, irrespective of filename. Include helper-only partials; assign each declaring file to its first declared class under ADR 0079. Do not add inherited helper files or referenced assemblies. |
| Nonblank count | Apply `scripts/code_size_policy.py:100-103`: `sum(bool(line.strip()) for line in path.read_text(encoding="utf-8-sig").splitlines())` to the original whole declaring file, including comments/attributes/literals. Sum over its unique files and count those files. |
| Test methods | Count declared methods with Fact, Theory, AvaloniaFact or AvaloniaTheory, once per method, not once per data row. Sum all partials before screening; count each class once per project. These are not discovered case counts. |
| Shared file | ADR 0079 counts a file only toward its first class. ShellViewModelTestGroups.cs adds its 98 nonblank lines only to ReportProjectionConcurrencyTests. The same rule excludes CtrlRamBoundedInspection and CtrlRamFirmwareVersion files from CtrlRamWorkflowTests. |
| Collections / pins | Read attributes on the particular declaration, not adjacent classes. Search candidate filenames in testdata JSON and docs/contracts; inspect evidenceRefs, document references, script class/test-ID lists and test source-file reads. |
| O24 | Read ProjectReference targets in all eight test .csproj files; distinguish ReferenceOutputAssembly=false from assembly references and from Compile links. |

Tables shorten project names by omitting `NvtFwCombiner.`; all are under `tests/`.
"Default class collection" means the existing xUnit serial unit is that class; it does not prove parallel safety.
UiSmoke also uses the shared Avalonia headless application/dispatcher, even without a named collection.

## 3. Table 1: aggregates at least 2,000 nonblank lines

| Project | Class | Files | Nonblank lines | Test methods | Collection / serialization | Golden / firmware evidence | S7 locks / script or test pins |
| --- | --- | ---: | ---: | ---: | --- | --- | --- |
| Application.Tests | `VersionManagementExperienceTests` | 15 | 5,086 | 119 | `Default class collection` | No Golden project / no evidenceRefs pin found | None in supplied lock map |
| Application.Tests | `MemoryLayoutProjectorTests` | 11 | 2,500 | 48 | `Default class collection` | No Golden project / no evidenceRefs pin found | None in supplied lock map |
| Application.Tests | `ManagedFirstInstallationExperienceTests` | 3 | 2,444 | 57 | `Default class collection` | No Golden project / no evidenceRefs pin found | None in supplied lock map |
| Application.Tests | `CompositionRunServiceTests` | 10 | 2,314 | 46 | `Default class collection` | No Golden project / no evidenceRefs pin found | None in supplied lock map |
| Bootstrap.Tests | `FirmwareInspectionSnapshotTests` | 5 | 2,771 | 58 | `Default class collection` | Reads Golden fixtures as input (4 files); firmware owner approves splits | L1: `FirmwareInspectionSnapshotTests.FileIdentity.cs`; hold the whole aggregate |
| Domain.Tests | `CompiledCompositionTests` | 12 | 2,618 | 50 | `Default class collection` | No Golden project / no evidenceRefs pin found | None in supplied lock map |
| Infrastructure.Tests | `ManagedDistributionLauncherRuntimeTests` | 6 | 3,019 | 69 | `Default class collection` | No Golden project / no evidenceRefs pin found | None in supplied lock map |
| Infrastructure.Tests | `ProfileBundleSchemaValidatorTests` | 21 | 2,879 | 64 | `Default class collection` | No Golden project / no evidenceRefs pin found | None in supplied lock map |
| Infrastructure.Tests | `FileSystemManagedVersionRepositoryTests` | 9 | 2,412 | 58 | `ReadyProbeProcessSerialGroup` | No Golden project / no evidenceRefs pin found | None in supplied lock map |
| Infrastructure.Tests | `AnonymousPipeManagedApplicationProcessTests` | 6 | 2,359 | 59 | `ReadyProbeProcessSerialGroup` | No Golden project / no evidenceRefs pin found | None in supplied lock map |
| Infrastructure.Tests | `ManagedSetupRecoveryExecutionTests` | 5 | 2,050 | 39 | `Default class collection` | No Golden project / no evidenceRefs pin found | None in supplied lock map |
| ProfileContract.Tests | `TrustedProfileBundleCatalogFactoryTests` | 16 | 5,945 | 147 | `Default class collection` | No Golden project / no evidenceRefs pin found | None in supplied lock map |
| ProfileContract.Tests | `FirmwareFamilyResolutionNormalizerTests` | 11 | 3,496 | 76 | `Default class collection` | No Golden project / no evidenceRefs pin found | None in supplied lock map |
| UiSmoke.Tests | `XamlControlStyleContractTests` | 45 | 10,305 | 164 | `UiAvaloniaRuntimeCollection.Name` | Reads Golden fixtures as input (1 file); firmware owner approves splits | Test pin: TestParallelism.cs reads the root collection attribute; script part 3 complement |
| UiSmoke.Tests | `ShellNavigationSystemTests` | 21 | 6,654 | 153 | `UiAvaloniaRuntimeCollection.Name` | Reads Golden fixtures as input (3 files); firmware owner approves splits | Script partition: part 3 complement; defer to ADR UI work |
| UiSmoke.Tests | `FirmwareInspectionSlotTests` | 22 | 5,387 | 125 | `UiAvaloniaRuntimeCollection.Name` | Reads Golden fixtures as input (2 files); firmware owner approves splits | Script pin: part 2; defer to ADR UI work |
| UiSmoke.Tests | `CtrlRamWorkflowTests` | 8 | 2,000 | 42 | `Default class collection` | Reads Golden fixtures as input (6 files); firmware owner approves splits | Script partition: part 3 complement; defer to ADR UI work |
| UiSmoke.Tests | `MemoryCoveragePopupTests` | 10 | 2,586 | 55 | `Default class collection` | No Golden project / no evidenceRefs pin found | Script partition: part 3 complement; defer to ADR UI work |
| UiSmoke.Tests | `VersionManagementSettingsTests` | 4 | 2,295 | 54 | `UiAvaloniaRuntimeCollection.Name` | No Golden project / no evidenceRefs pin found | Script pin: part 2; defer to ADR UI work |

There are **19** aggregates. No GoldenRegression.Tests aggregate reaches either table's lower bound.
"No pin found" describes the searched references; firmware-behavior tests can still have no pin.
Golden or firmware-evidence moves require the firmware owner as approver and reference updates under S6.
Decision 260 excludes these classes from the commander's scheduling authority, including the five input readers.
UiSmoke pins in `scripts/verify.py:587-650` keep their class partitions; new types enter the last part.
The Xaml pin is in `RepositoryBoundaryTestSupport.TestParallelism.cs:80-93`; see the UI-path limit in section 5.

### Supplied open-branch locks

Local `git diff --name-only HEAD...<branch>` and the rename batch's `git show` confirm these locks, not all locks.
S7 excludes a whole class while any file is locked, including classes below these bands.

| Lock | Branch | Paths that must not move |
| --- | --- | --- |
| L1 | `feature/1.2.4/bounded-identity-inspection` | `tests/NvtFwCombiner.Application.Tests/Authoring/GeneralSelectedFileContentTests.cs`; `tests/NvtFwCombiner.Application.Tests/Authoring/GeneralSelectedFileSessionLifecycleTests.cs`; `tests/NvtFwCombiner.Infrastructure.Tests/Files/FileContentSnapshotInspectorTests.cs` and branch-added `FileContentSnapshotInspectorTests.BoundedIdentity.cs` (recheck all `FileContentSnapshotInspectorTests*.cs`); `tests/NvtFwCombiner.Bootstrap.Tests/FirmwareInspectionSnapshotTests.FileIdentity.cs` |
| L2 | `feature/1.2.4/general-preparation-freshness` | `tests/NvtFwCombiner.Application.Tests/Authoring/GeneralPreparationFreshnessTests.cs` (branch-added; absent from this measured checkout) |
| L3 | `feature/1.2.6/launcher-entry-typed-reason` | All three current `ManagedLauncherEntryCoordinatorTests` files (`.cs`, `.HealthDeadline.cs`, `.Support.cs`), plus branch-added `.ReviewDiagnostics.cs` and `.TypedDiagnostics.cs`; hold the whole class |
| L4 | `feature/1.2.11/t2b-deserialize` (PR #522) | Table 2: `CanonicalCatalogBoundaryTests.CanonicalAdmission.cs`, `HostInfrastructureBoundaryTests.FirstInstallationProgress.cs`, `PresentationBoundaryTests.Localization.cs`, `RepositoryBoundaryTests.cs`; hold all four classes |
| L5 | `feature/1.2.11/r33-03-rename-batch-1` | One of thirteen renames belongs to Table 2: `LegacyCombinerPostbuildProcessorTestSupport.cs` becomes `LegacyCombinerPostbuildProcessorTests.TestSupport.cs`; hold `LegacyCombinerPostbuildProcessorTests`. The other twelve have no Table 1/2 overlap. |

The other L1 classes are below 1,500 lines here. Neither planned split intersects these locks.

## 4. Table 2: watch list, 1,500 to 1,999 nonblank lines

| Class | Project | Nonblank lines | Lock / pin / pending change |
| --- | --- | ---: | --- |
| `CanonicalCapabilityCatalogTests` | Application.Tests | 1,973 | None in supplied lock map |
| `AuthoringInputSlotInspectionTests` | Application.Tests | 1,697 | None in supplied lock map |
| `CompositionRunRequestV2Tests` | Application.Tests | 1,629 | None in supplied lock map |
| `ManagedLauncherEntryCoordinatorTests` | Application.Tests | 1,527 | L3: five branch files total 2,236 lines; becomes a Table 1 entry on merge |
| `PresentationBoundaryTests` | Architecture.Tests | 1,671 | L4 |
| `CanonicalCatalogBoundaryTests` | Architecture.Tests | 1,571 | L4 |
| `HostInfrastructureBoundaryTests` | Architecture.Tests | 1,534 | L4 |
| `RepositoryBoundaryTests` | Architecture.Tests | 1,504 | L4 |
| `AbMergeGoldenRegressionTests` | Bootstrap.Tests | 1,695 | Golden evidenceRefs and script test-ID pins; firmware owner approves any split |
| `ReplaceCliCommandTests` | Bootstrap.Tests | 1,651 | None in supplied lock map |
| `CanonicalCapabilityCatalogMigrationTests` | Bootstrap.Tests | 1,594 | None in supplied lock map |
| `LegacyCombinerPostbuildProcessorTests` | Infrastructure.Tests | 1,595 | L5: support-file rename |
| `AnonymousPipeManagedLauncherProcessTests` | Infrastructure.Tests | 1,552 | None in supplied lock map |
| `RunAndHexEditorTests` | UiSmoke.Tests | 1,708 | Script pin: part 1; defer to ADR UI work |

There are **14** aggregates. `AbMergeGoldenRegressionTests` is firmware evidence and is not proposed for a split.
Eight `testdata/golden/canonical/**/provenance/case.json` entries pin its root/PublicHost files via `evidenceRefs`.
`scripts/verify.py:3207-3211` also pins its test IDs in the Windows platform-skip list.
Its future move needs the firmware owner as approver and updates to both evidenceRefs and script pins.

## 5. Two mechanical split plans

The pilot's "After the pilot" names ProfileContract and Application aggregates, but no particular class.
Decision 260 selects the largest current aggregate in each project, ProfileContract first, then Application.
Larger UiSmoke classes are not offered here because ADR 0079 assigned their mixed-test splits to U2a.
ADR item 17 later closed U2a (decision 114) after U0 and adopted U5; U2a is not a pending scheduling gate.
Preserve S1-S10: no test-body, method-name, assertion or input change; no removal of serialization.

### Plan P: TrustedProfileBundleCatalogFactoryTests

Base: **16 files / 5,945 nonblank lines / 147 test methods**, all in ProfileContract.Tests.
Files are under `tests/NvtFwCombiner.ProfileContract.Tests/`; rows cover whole files.
Move topics to `<NewClass>.<Topic>.cs`; residual paths stay. Support uses its own stem and helper topic.
Once merged, `scripts/split_equivalence.py` from `feature/1.2.11/split-equivalence-tool` produces E1 and E3.
The tool branch is not yet merged; the other evidence and independent review remain required.

| Source file | Target class | Current lines | Methods | Handling |
| --- | --- | ---: | ---: | --- |
| `TrustedProfileBundleCatalogFactoryTests.cs` | `TrustedProfileBundleCatalogFactoryTests` | 145 | 5 | Residual root |
| `TrustedProfileBundleCatalogFactoryTests.InputGeometry.cs` | `TrustedProfileBundleCatalogInputGeometryTests` | 440 | 13 | Move whole file |
| `TrustedProfileBundleCatalogFactoryTests.LogicalOutput.cs` | `TrustedProfileBundleCatalogLogicalOutputTests` | 28 | 1 | Move whole file |
| `TrustedProfileBundleCatalogFactoryTests.LogicalOutputV25.cs` | `TrustedProfileBundleCatalogFactoryTests` | 466 | 10 | Residual: S6 pin |
| `TrustedProfileBundleCatalogFactoryTests.Lowering.cs` | `TrustedProfileBundleCatalogLoweringTests` | 624 | 18 | Move whole file |
| `TrustedProfileBundleCatalogFactoryTests.OperationLowering.cs` | `TrustedProfileBundleCatalogOperationLoweringTests` | 657 | 18 | Move whole file |
| `TrustedProfileBundleCatalogFactoryTests.OutputNaming.cs` | `TrustedProfileBundleCatalogOutputNamingTests` | 277 | 4 | Move whole file |
| `TrustedProfileBundleCatalogFactoryTests.Preparation.cs` | `TrustedProfileBundleCatalogPreparationTests` | 549 | 15 | Move whole file |
| `TrustedProfileBundleCatalogFactoryTests.ProcessorLowering.cs` | `TrustedProfileBundleCatalogProcessorLoweringTests` | 146 | 4 | Move whole file |
| `TrustedProfileBundleCatalogFactoryTests.RegionInstanceDeltaLowering.cs` | `TrustedProfileBundleCatalogRegionInstanceDeltaLoweringTests` | 123 | 3 | Move whole file |
| `TrustedProfileBundleCatalogFactoryTests.ReplaceLowering.cs` | `TrustedProfileBundleCatalogReplaceLoweringTests` | 471 | 11 | Move whole file |
| `TrustedProfileBundleCatalogFactoryTests.RuntimeReferenceReplace.cs` | `TrustedProfileBundleCatalogRuntimeReferenceReplaceTests` | 427 | 12 | Move whole file |
| `TrustedProfileBundleCatalogFactoryTests.RuntimeReferenceReplaceProcessor.cs` | `TrustedProfileBundleCatalogFactoryTests` | 638 | 17 | Residual: S6 pin |
| `TrustedProfileBundleCatalogFactoryTests.RuntimeSemanticEquivalence.cs` | `TrustedProfileBundleCatalogFactoryTestSupport` | 144 | 0 | S3 helper-only file |
| `TrustedProfileBundleCatalogFactoryTests.SourceEnvelope.cs` | `TrustedProfileBundleCatalogSourceEnvelopeTests` | 450 | 10 | Move whole file |
| `TrustedProfileBundleCatalogFactoryTests.WorkBufferLowering.cs` | `TrustedProfileBundleCatalogWorkBufferLoweringTests` | 360 | 6 | Move whole file |

The three residual files total **1,249 lines / 32 methods** before S3 extraction.
Other test-bearing rows are at most **657 lines** before extraction; remeasure all destinations at implementation.

**S3 support.** Use one `internal static partial TrustedProfileBundleCatalogFactoryTestSupport`;
import it with `global using static`. Move these helpers verbatim; change accessibility only as needed.

| Source topic | Shared helpers / dependencies for support |
| --- | --- |
| Root | CreateCatalogFromSources, Family, Profile, Hash, ManifestHash, BundleHash; schema-ID dependencies |
| Preparation | CreateCatalog, Select, PrepareAdmitted, Compile, PreparedProfile, Inputs, Parse |
| Lowering | PrepareSupportedBlankCopy, SupportedProfileJson, RuntimeSupportedProfileJson, ProfileRequiringCapability, FamilyJsonWithRootWriteConstraint, ParseFamily, Capability; private closure including Applicability |
| InputGeometry / OperationLowering | ProfileWithTpMaximumInput, ProfileWithInactiveOptionalBranch, ProfileWithDeclaredPrefix / ProfileWithWorkBuffer |
| WorkBufferLowering | ProfileWithWorkBufferCopyFlow, ProfileWithTemplateRangeCopyFlow, FamilyWithTwoBankInstances |
| LogicalOutputV25 / RuntimeReferenceReplace | LogicalTestMemberId / RuntimeReferenceReplaceRequest, RuntimeReferenceReplaceMapping |
| RuntimeSemanticEquivalence | Whole helper-only file: AssertEquivalentRuntimeExecutionSemantics, AssertEquivalentOperation, RegionChain |

Keep single-topic helpers and source-envelope serializer options local. Recheck symbol bindings at branch point.

**S4 collection.** Every destination test class, including the residual, joins one new
`TrustedProfileBundleCatalogFactorySerialGroup` collection, with no `DisableParallelization` change.

**S6 / S7.** No evidenceRefs hit or supplied lock intersects this class; recheck catalog work at dispatch.

| Source reference | Treatment |
| --- | --- |
| `docs/architecture/v1.2.1-handoff.md:97`; `docs/handoff/1.2.1/R02-01.md:112` | LogicalOutputV25 remains residual (link and line citations) |
| `docs/ui/v1.1.10-delivery.md:1642` | RuntimeReferenceReplaceProcessor remains residual (link) |
| `docs/governance/change-records/DP-ENVELOPE-1110-B1-01.json:38`, `DP-ENVELOPE-1110-B2-01.json:91`, `DP-ENVELOPE-1110-FOUNDATION-01.json:44` | Historical mutablePaths name SourceEnvelope; no synchronization needed |
| `docs/governance/change-records/DP-REPLACE-RETIREMENT-RUNTIME-110-01.json:160` | Historical mutablePaths names ReplaceLowering; no synchronization needed |

A later move of either live-linked residual file requires updates under its reference owner's authority.
Scripts do not read the historical mutablePaths fields; the pilot treats live document links as S6 pins.

| S9 aspect | Before, from reading | Planned mechanical result / pending evidence |
| --- | --- | --- |
| Attributes, construction, fixtures | No class collection/trait/skip, base/interface, constructor, Dispose, IAsyncLifetime or fixture found | Only the new shared collection attribute; method attributes unchanged; no fixture multiplication |
| Static/shared state and initialization | Constants; one static source-envelope JsonSerializerOptions instance; resolver objects and JSON graphs made per call | Constants/helpers move once into support; options stay with SourceEnvelope, still once per process; no shared mutable fixture added |
| Indirect resources | Shared document factories generate in-memory JSON; catalog extension helpers compile plans; SourceEnvelope reads committed profile JSON through RepositoryPaths | Same reads and factories; no UI dispatcher or external processor execution found in these helper paths |
| Background work / cleanup | No spawned task, child process or temporary-file writer found | Same; dynamic confirmation still required |
| Process-wide state | RepositoryPaths reads the configured repository-root environment variable; no environment/culture/current-directory writes found | Same; no new global writes |
| Order / collection | One default class collection; no cross-test state handoff found | One named serial collection; E7 must confirm initialization and order at base/head |

### Plan A: VersionManagementExperienceTests

Base: **15 files / 5,086 nonblank lines / 119 methods**, in Application.Tests.
Files are under `tests/NvtFwCombiner.Application.Tests/VersionManagement/`; use filename-suggested classes.
Once merged, `scripts/split_equivalence.py` from `feature/1.2.11/split-equivalence-tool` produces E1 and E3.
The tool branch is not yet merged; the other evidence and independent review remain required.

| Source file | Target class | Current lines | Methods | Handling |
| --- | --- | ---: | ---: | --- |
| `FreshInstallationCandidateTests.cs` | `FreshInstallationCandidateTests` | 413 | 13 | Whole file; keep filename |
| `LauncherMutationFenceTests.cs` | `LauncherMutationFenceTests` | 126 | 3 | Whole file; keep filename |
| `LauncherRecoveryFenceTests.cs` | `VersionManagementExperienceTests` | 30 | 1 | Residual: S6 source citation |
| `VersionManagementExperienceConcurrencyTests.cs` | `VersionManagementExperienceConcurrencyTests` | 279 | 3 | Whole file; keep filename |
| `VersionManagementExperienceDeleteRecoveryTransactionTests.cs` | `VersionManagementExperienceTests` | 141 | 4 | Residual: S6 source citation |
| `VersionManagementExperienceInstallResultBoundaryTests.cs` | `VersionManagementExperienceInstallResultBoundaryTests` | 172 | 4 | Whole file; keep filename |
| `VersionManagementExperienceInventoryUnavailableTests.cs` | `VersionManagementExperienceInventoryUnavailableTests` | 338 | 9 | Whole file; keep filename |
| `VersionManagementExperienceRegistrySelfTestTests.cs` | `VersionManagementExperienceRegistrySelfTestTests` | 565 | 16 | Whole file; keep filename |
| `VersionManagementExperienceRegistryTestDoubles.cs` | `VersionManagementExperienceTestSupport` | 413 | 0 | S3 helper-only file |
| `VersionManagementExperienceRegistryTests.cs` | `VersionManagementExperienceRegistryTests` | 863 | 23 | Whole file; keep filename |
| `VersionManagementExperienceTests.cs` | `VersionManagementExperienceTests` | 685 | 16 | Residual root |
| `VersionManagementExperienceTransactionSafetyTests.cs` | `VersionManagementExperienceTransactionSafetyTests` | 277 | 7 | Whole file; keep filename |
| `VersionManagementExperienceTransactionTestDoubles.cs` | `VersionManagementExperienceTestSupport` | 157 | 0 | S3 helper-only file |
| `VersionManagementExperienceTransactionTests.cs` | `VersionManagementExperienceTests` | 546 | 17 | Residual: S6 source citation |
| `VersionManagementExperienceVerificationBoundaryTests.cs` | `VersionManagementExperienceVerificationBoundaryTests` | 81 | 3 | Whole file; keep filename |

The four residual files total **1,402 lines / 38 methods** before S3 extraction.
Other test-bearing rows are at most **863 lines** before extraction; remeasure all destinations at implementation.

**S3 support.** Use one `internal static partial VersionManagementExperienceTestSupport`;
import it with `global using static`. Move helpers and dependency closures verbatim.

| Source | Support treatment |
| --- | --- |
| Both TestDoubles files | Whole support partials; needed accessibility changes only; registry builders, catalog sources, counters, lease doubles, FailingStateStore and TransactionRepository retain nested instance state |
| Root | Hash, State, Admission, Catalog, CatalogV2, FixedCatalogSource, MutableCatalogSource, MemoryStateStore, HealthyRepository |
| RegistryTests / LauncherMutationFenceTests | FirstRegistryDigest, SecondRegistryDigest / PendingProtection, RecordingLauncherFence |

Keep topic-local helpers local. Reuse `VersionManagementExperienceTestFactory`, not another experience constructor.

**S4 collection.** Every destination test class, including the residual, joins one new
`VersionManagementExperienceSerialGroup` collection. Do not disable or remove serialization.
Helpers have no collection; nested test-double state remains per instance.

**S6 / S7.** No `evidenceRefs` pin or Markdown hyperlink to these source files was found.
`docs/handoff/1.2.1/R03-01.md:120` cites TransactionTests, DeleteRecoveryTransactionTests and
LauncherRecoveryFenceTests at :428, :9 and :9 in the dated 2026-09-29 evaluation; keep their paths and class.
Put the collection attribute on each class's root file only, as in the pilot; add no attribute or summary
to these three residual files, preserving the cited line numbers.
Any later move must preserve that reference under its document owner's authority.
Recheck VersionManagement work and all locks at dispatch; no supplied lock intersects this class.

| S9 aspect | Before, from reading | Planned mechanical result / pending evidence |
| --- | --- | --- |
| Attributes, construction, fixtures | No class collection/trait/skip, base/interface, constructor, Dispose, IAsyncLifetime or fixture; experiences use using/explicit disposal | Only the new shared collection attribute; method attributes unchanged; no fixture multiplication; experience disposal stays per test |
| Static/shared state and initialization | Constants on the test class; mutable counters, dictionaries, gates and journals on per-test doubles; existing factory has one ClearLauncherMutationFence.Instance | Helpers/types move to support; doubles remain per-test objects; factory singleton remains once per process |
| Background work / cleanup | Async checks/install/verification use TaskCompletionSource gates and cancellable infinite waits; successful paths release/cancel and await operations | Same gates and cancellation; failure-path cleanup has not been proven by this read, so no de-serialization proposed |
| Indirect resources | Factory creates the Application experience with injected sources/stores/repos; write-lease support returns a disposable in-memory handle | Same adapters; no UI dispatcher, file writer or child process found in the inspected test/helper paths |
| Process-wide state | SourcePath computes absolute paths; no environment/culture/current-directory writes found | Same; path strings do not establish filesystem writes |
| Order / collection | One default class collection; state shared by experiences inside a test, not a demonstrated cross-test handoff | One shared serial collection; E7 and runtime evidence must confirm no semantic difference |

### Evidence and execution envelope for each later batch

| Item | Required evidence for each batch |
| --- | --- |
| Sequence | Freeze/recheck B (sizes, helpers, all locks and references, current policy); locked classes wait. Capture baseline discovery/outcomes/timing; extract support, move whole files into the shared collection, then complete exact-head review and one split-only PR per aggregate. |
| E1 / E2 | Identical discovered method/argument multiset after class-name normalization, mapped to targets; identical outcomes, no new skip |
| E3 / E4 | Mechanical rename/helper/attribute diff only; scratch canaries fail intended moved assertions (P: catalog binding/lowering; A: registry/transaction/fence outcomes) |
| E5 / E6 | Three H timings against B on the same quiet machine; applicable CI with unchanged discovered case counts |
| E7 | Completed before/after S9 table at B and H, explaining every difference beyond class names |

Stop for a body edit, unmapped case, changed outcome, new pin/lock or unexplained state. No E1-E7 pass is claimed.
De-serialization needs its own plan, S9 audit and repeated runs; pilot five-run/MSBuild gates are pilot-specific.

## 6. O24: Desktop and Launcher direct-reference gaps

Eight test projects vs 15 product projects yield **three** direct assembly-reference gaps, including build-only refs.

| Product project | Direct-reference finding | Source path:line |
| --- | --- | --- |
| `NvtFwCombiner.Desktop` | Only a build reference, ReferenceOutputAssembly=false; UiSmoke copies the host for process tests | `tests/NvtFwCombiner.UiSmoke.Tests/NvtFwCombiner.UiSmoke.Tests.csproj:7` (false at :8; copy target at :21) |
| `NvtFwCombiner.LauncherBootstrap` | Only a build reference, ReferenceOutputAssembly=false; Infrastructure copies its executable | `tests/NvtFwCombiner.Infrastructure.Tests/NvtFwCombiner.Infrastructure.Tests.csproj:22` (false at :23; copy target at :49) |
| `NvtFwCombiner.Platform` | No direct ProjectReference from a test project; transitively reached through Infrastructure | `src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj:10`; `src/NvtFwCombiner.Platform/NvtFwCombiner.Platform.csproj:6` grants test visibility, not a reference |

Launcher itself has a direct assembly reference at Infrastructure.Tests.csproj:16;
DistributionLauncher has direct references at Bootstrap.Tests.csproj:11 and UiSmoke.Tests.csproj:10.
Infrastructure.Tests already uses Platform types in `VersionManagement/ProcessLaunchGateTests.cs`,
`ManagedProcessLifetimeLeaseTests.cs` and `AnonymousPipeManagedApplicationProcessTests.Support.cs`.
This transitive behavioral coverage supports deferring Platform's direct-reference gap.
Process tests may cover behavior despite a reference gap; full-suite fallback/selector completion does not fill it.

## 7. Decided O25 and remaining O24 question

**Decided (2026-10-02, board decision 260):** the commander schedules the splits without approval per split:
`TrustedProfileBundleCatalogFactoryTests` first, then `VersionManagementExperienceTests`, each when no other
branch edits its test files. Follow T2a: whole-file moves, the same E1-E7 equivalence evidence and independent review.
Classes holding Golden or firmware evidence are excluded; ask the firmware owner separately before any split.

| Question | Options and recommendation |
| --- | --- |
| **O24:** Add real start-up tests for the desktop app and the launcher now and postpone the Platform library, or list all three gaps and pick versions later? | A (recommended): add Desktop and LauncherBootstrap start-up behavior tests now; postpone Platform. These two hosts have only build references, while Infrastructure.Tests already exercises Platform types. B: list all three gaps and pick versions later. Adding references alone does not establish behavior coverage. |

## 8. Limits

Only this document is revised. No test/source move, test change, build, test, network operation or Git write.
The source-method counts are not runtime discovery; timings, outcomes and E1-E7 equivalence remain unconfirmed.
Static review covers the test declarations and relevant helper paths, not every transitive product implementation.
O25 scheduling is decided under decision 260; O24 coverage and any serialization change remain undecided.
