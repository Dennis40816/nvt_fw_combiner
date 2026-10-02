# R33-05 candidates and split decisions for 1.2.11

## 1. Status and purpose

Status: P/A executed; C-F planned at `6f87c66fd` (2026-10-02); O25 decided (260); O24 open.
Measured 2026-10-02; no split or execution evidence produced.
Source commit: `d38f4e7c379bce1a908ad1b31d3b2356c58591d4` on `feature/1.2.11/r33-05-candidates`.
Purpose: record the next R33-05 batches under O25 and retain the O24 coverage choice.
T2a from PR #519 is present; decision 260 applies its mechanical-split procedure to the six plans below (P, A and C to F).
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
Decision 260 excludes Golden and firmware-evidence classes from the commander's scheduling authority; that
the five input readers belong to them is the commander's reading, not the decision's wording.
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
| L4 (merged as `05f1be1ed`; no longer held) | `feature/1.2.11/t2b-deserialize` (PR #522) | Table 2: `CanonicalCatalogBoundaryTests.CanonicalAdmission.cs`, `HostInfrastructureBoundaryTests.FirstInstallationProgress.cs`, `PresentationBoundaryTests.Localization.cs`, `RepositoryBoundaryTests.cs`; hold all four classes |
| L5 (merged as `10ac919c3`; no longer held) | `feature/1.2.11/r33-03-rename-batch-1` | One of thirteen renames belongs to Table 2: `LegacyCombinerPostbuildProcessorTestSupport.cs` becomes `LegacyCombinerPostbuildProcessorTests.TestSupport.cs`; hold `LegacyCombinerPostbuildProcessorTests`. The other twelve have no Table 1/2 overlap. |

The other L1 classes are below 1,500 lines here. Neither planned split intersects these locks.
L4 and L5 merged after this measurement: the "hold" in their rows and the L4/L5 marks in Table 2 describe the
state at measurement; their classes are free to schedule again, and the four Architecture
classes are one line shorter on the trunk than the figures in the tables (measure again before planning them).
The R54 retirement (`docs/handoff/1.2.4/R54-retirement-plan.md`) also changes
`TrustedProfileBundleCatalogFactoryTests.ReplaceLowering.cs` and `.RuntimeReferenceReplace.cs`; the split of
that class lands first, and R54 then edits the files under their new class names.

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

## 5. Six mechanical split plans

The pilot's "After the pilot" names ProfileContract and Application aggregates, but no particular class.
Decision 260 names two classes: `TrustedProfileBundleCatalogFactoryTests` (ProfileContract) first, then
`VersionManagementExperienceTests` (Application); each is the largest current aggregate in its project.
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

### Basis for Plans C-F

Plans C-F use trunk `6f87c66fd5051e428dcb712e8f732a4fd688f731`, measured on 2026-10-02 by section 2.
All 50 declaring files were enumerated by class identity, including the five differently named C partials.
The three projects have `IsTestProject=true`; their Compile links add no candidate declaration,
and no Compile Remove excludes one. Counts include helper-only files, comments and literals; each
Fact/Theory method counts once. All four totals still match Table 1; these are source counts, not discovery.

`git show --stat dcb66470e` is the first split's final cleanup, not its original move commit.
Its trunk support is one non-partial `TrustedProfileBundleCatalogFactoryTestSupport` file.
`git show --stat 8dfe624cc` records filename-preserving topic classes, one serial collection,
and `VersionManagementExperienceTestSupport` with helper-only partials. Both use `global using static`.
Follow those results: whole test files, unchanged bodies, one support owner and one serial collection per plan.

Source-file IDs below identify helper users and declaration sites. The declaration line in each file table
is the S4 audit location: every declaration has **no class-level attribute** (quoted attribute set: `none`).
There is no class-level Collection, Trait or Skip. Preserve method attributes verbatim.
Destination counts are conservative nonblank ceilings after the specified extraction/local helper placement,
allowing two new lines per test class for its summary/collection; they are not implementation measurements.
F's pinned remainder instead gets a five-nonblank-line membership partial. Remeasure at dispatch and head.

For each moved name, S3 hazard columns mean:
H1 = same-named member/type remaining in a split class, its enclosing type or its base-list type;
H2 = same-named top-level type anywhere in that test project, including linked Compile inputs;
H3 = overloads divided between support and a class file;
H4 = a moved helper's dependency on a private member left in a class file.
`N/N/N/N` records four negative checks for that name, after the specified dependency closure moves.
All destination test classes remain top-level sealed classes with no enclosing/base-list type.
Nested types move whole, including their instance members; those members are not independent extractions.

**S7 lock recheck, common to C-F.** Each command was
`git diff --name-only 6f87c66fd <branch>` against the local branch, without a merge-base shortcut.
Candidate matching included all declaring paths, not only filenames starting with the old class name.

| Local branch | Changed test paths | C/D/E/F declaring-path intersection |
| --- | ---: | --- |
| `feature/1.2.4/r54-step-1` | 34 | None |
| `feature/1.2.3/r14-04-capture-saved-report` | 12 | None |
| `feature/1.2.3/r14-02-cli-additions` | 13 | None |
| `feature/1.2.2/executor-contract` | 55 | None |
| `feature/1.2.3/test-selection-inputs` | 7 | None |

No C-F file intersects supplied L1-L3 paths; L4/L5 are recorded as merged. No class is locked at this base.
R54's proposed later edits are pins below, even though its current branch touches none of C-F.
Recheck all active locks before execution; a new intersection holds the entire aggregate under S7.

Reference search covered every old class name and all 50 filenames in testdata JSON, docs/contracts,
docs/handoff (including bugs), other docs, scripts and test sources. No candidate has a Golden evidenceRefs
pin, live test-source reader or `scripts/verify.py` partition/skip-list entry. Individual hits follow each plan.
ADR 0079:119-123 and `docs/handoff/1.1.13/ADR-DRAFT-test-architecture.md:114-118` are historical size rows,
not source pins. Table 1 in this document remains its dated inventory.

Canaries below are **candidates**, not executed evidence. Use one token change at a time in a scratch copy,
in a fresh process, with the named finite tests mapped through E1 to their destination classes.
They change no wait, retry, cancellation, gate or launch predicate. Record actual failures under new names,
restore the token, and retain the E1-E7 envelope below; no runtime or equivalence pass is claimed here.

### Plan C: FirmwareFamilyResolutionNormalizerTests

Base: **11 files / 3,496 nonblank lines / 76 methods**, in ProfileContract.Tests.
Files are under `tests/NvtFwCombiner.ProfileContract.Tests/`.
Golden/evidence check: inputs are constructed DTOs, JSON elements or synthetic byte arrays;
`family-evidence`, `capability-evidence` and provider hashes are synthetic strings, not firmware fixture reads.
The linked `FirmwareImageMapTestFactory` constructs facts in memory. No Golden/firmware-evidence reader found.

| ID | Source file | Lines | Methods | Declaration | Target |
| --- | --- | ---: | ---: | ---: | --- |
| C0 | `FirmwareFamilyResolutionNormalizerTests.cs` | 596 | 15 | 10 | remainder |
| C1 | `FirmwareFamilyResolutionNormalizerTests.DefinitionReferences.cs` | 158 | 2 | 7 | remainder |
| C2 | `FirmwareFamilyResolutionNormalizerTests.AbFormatPolicy.cs` | 405 | 11 | 7 | remainder |
| C3 | `FirmwareFamilyResolutionNormalizerTests.PrimaryDiscovery.cs` | 158 | 4 | 7 | remainder |
| C4 | `FirmwareFamilyResolutionNormalizerTests.TestSupport.cs` | 291 | 0 | 9 | remainder / S3 |
| C5 | `FirmwareFamilyResolutionNormalizerAliasTests.cs` | 510 | 11 | 7 | C-Alias |
| C6 | `FirmwareFamilyResolutionNormalizerAliasNegativeTests.cs` | 221 | 6 | 6 | C-Negative |
| C7 | `FirmwareFamilyResolutionNormalizerCapabilityAdmissionTests.cs` | 259 | 6 | 9 | C-Admission |
| C8 | `FirmwareFamilyResolutionNormalizerRelationshipTests.cs` | 428 | 10 | 7 | C-Relationship |
| C9 | `FirmwareFamilyFullImageMetadataTests.cs` | 159 | 6 | 8 | C-Metadata |
| C10 | `FirmwareFamilyResolutionNormalizerTests.TpFlashHeader.cs` | 311 | 5 | 7 | C-Header |

| Target | Class | Source lines / methods | Planned ceiling / methods |
| --- | --- | ---: | ---: |
| remainder | `FirmwareFamilyResolutionNormalizerTests` | 1,608 / 32 | 1,396 / 32 |
| C-Alias | `FirmwareFamilyResolutionNormalizerAliasTests` | 510 / 11 | 463 / 11 |
| C-Negative | `FirmwareFamilyResolutionNormalizerAliasNegativeTests` | 221 / 6 | 223 / 6 |
| C-Admission | `FirmwareFamilyResolutionNormalizerCapabilityAdmissionTests` | 259 / 6 | 261 / 6 |
| C-Relationship | `FirmwareFamilyResolutionNormalizerRelationshipTests` | 428 / 10 | 430 / 10 |
| C-Metadata | `FirmwareFamilyFullImageMetadataTests` | 159 / 6 | 161 / 6 |
| C-Header | `FirmwareFamilyResolutionNormalizerTpFlashHeaderTests` | 311 / 5 | 230 / 5 |

Keep C0-C9 filenames. Rename C10 to `FirmwareFamilyResolutionNormalizerTpFlashHeaderTests.cs`.
C4 retains the four remainder-only helpers below; extract shared support into
`FirmwareFamilyResolutionNormalizerTestSupport.cs`, one `internal static` class (ceiling 370 lines, zero tests).
The remainder's smaller headroom follows the pinned definition tests and their AB-policy users;
it finishes more than 100 lines below 1,500 without changing a test body.

**S3 support.** Import `NvtFwCombiner.ProfileContract.Tests.FirmwareFamilyResolutionNormalizerTestSupport`
with `global using static`. Rows list direct users by source file and factory dependency closures.
Only access modifiers change. C1's extracted resolver starts after all cited test lines.

| Moved name | Declaration | Users by file / closure | H1/H2/H3/H4 |
| --- | --- | --- | --- |
| `FamilyHash` | C0:12 | C0-C3, C5-C10; C4 helper | N/N/N/N |
| `Document` | C4:85 | C0-C2, C5-C10; C4 helper | N/N/N/N |
| `RegionSet` | C4:128 | C4 Document closure for all its users | N/N/N/N |
| `MetadataSet` | C4:161 | C4 Document closure for all its users | N/N/N/N |
| `Fields` | C4:193 | C4 MetadataSet closure for all its users | N/N/N/N |
| `Map` | C4:233 | C4 Document closure for all its users | N/N/N/N |
| `AbsoluteLocator` | C4:252 | C0, C4 Document closure | N/N/N/N |
| `Inputs` | C4:272 | C0, C7 | N/N/N/N |
| `Range` | C4:282 | C0, C3, C10; C4 helper closure | N/N/N/N |
| `AddressedRange` | C4:289 | C2, C3, C9; C4 helper closure | N/N/N/N |
| `Number` | C4:297 | C0, C2, C3, C5, C6, C9, C10; C4 closure | N/N/N/N |
| `PhysicalAliasDocument` | C5:498 | C5, C6 | N/N/N/N |
| `AliasApplicability` | C5:539 | C5, C6, C7 | N/N/N/N |
| `ExactDefinitionResolver` | C1:150 | C1, C2, C9 | N/N/N/N |
| `WithTpFlashHeader` | C10:246 | C9, C10 | N/N/N/N |
| `TpFlashHeaderPayload` | C10:272 | C9, C10 | N/N/N/N |

Existing global catalog support also imports `Inputs(long, string)`; this plan's `Inputs(byte[])` has
incompatible parameter types and no ambiguous call. No overload remains in a split class.
The named tuple element Document and DTO properties such as Fields are qualified data members,
not remaining members of a split class. Neither is a top-level project type.

| Local or rejected extraction | Declaration / users | Disposition |
| --- | --- | --- |
| `NormalizeSingleRegion`, `WithPredicate`, `MarkerLocator`, `Text` | C4:11,61,260,303 / C0 | Stay in C4 remainder |
| `ProviderFamilyId`, `ProviderFamilyVersion`, `ProviderFamilyHash` | C1:9-12 / C1, C2 | Stay; S6 cited-line pin |
| `AbPolicyDocument` | C2:325 / C2, C3 | H4: private provider constants stay; keep users in remainder |
| `AbPolicyDirectDocument` | C2:395 / C2 and its factory | Stay with remainder-only AB factory |
| `ReferencedDocument`, `PrimaryContextDocument` | C1:102, C3:82 / own files | Stay in remainder |
| `AssertSameApplicabilityShape` | C5:93 / C5 | Stay in C-Alias |
| `ResolveMap`, `CapabilityProfile`, `Admit`, `CapabilityAdmissionResult` | C7:216,223,263,275 / C7 | C-Admission |
| `CreateTwoMapSharedFactDocument`, `WithDuplicatedSharedRegion` | C8:355,422 / C8 | Stay in C-Relationship |
| `FullImageView` | C9:163 / C9 | Stay in C-Metadata |

**S4 collection.** Add `FirmwareFamilyResolutionNormalizerSerialGroup.cs` in this directory/namespace:
`[CollectionDefinition(nameof(FirmwareFamilyResolutionNormalizerSerialGroup))]` on its empty public sealed class.
Add `[Collection(nameof(FirmwareFamilyResolutionNormalizerSerialGroup))]` once per test class, on C0 and C5-C10.
No DisableParallelization argument or fixture is added. C1-C4 get no attributes or new summary lines.
The original attribute set is `none` at every declaration location in the file table.

**S6 / S7 references.** The common lock recheck has no intersection.

| Reference | Pin / handling |
| --- | --- |
| `docs/handoff/1.2.1/R10-01.md:70,214` | C1:18-90 citations; keep path, class and these lines |
| `docs/handoff/1.2.1/R13-01.md:355` | Historical class timing; no source pin |
| `docs/governance/change-records/AB-116-PRIMARY-DISCOVERY-09.json:16-17` | C2/C3 historical mutablePaths; retain |
| `docs/governance/change-records/CONFIG-116-FORMAT-06.json:17` | C2 historical mutablePaths; retain |
| `docs/governance/change-records/FULL-IMAGE-METADATA-CORE-110-01.json:20` | C9 historical mutablePaths; retain |

No other candidate reference was found in testdata JSON, docs/contracts, bugs, scripts or test sources,
beyond declarations and the common historical size rows. Keep C1's prefix through its cited tests unchanged;
extracting the resolver at :150 does not alter those lines. C2/C3 stay for H4, not a branch lock.

| S9 aspect | Before, from reading | Planned mechanical result / pending evidence |
| --- | --- | --- |
| Attributes / fixtures | No attributes, base, construction or fixture | Shared collection; no fixtures added |
| Static state | Constants, stateless factories; per-instance resolver | One support; provider constants stay |
| Indirect resources | DTO/JSON factories, synthetic arrays; no file/UI | Same inputs, resolver and factories |
| Background / cleanup | Synchronous; JsonDocuments disposed, elements cloned | Same; no task/process added |
| Process-wide state | No environment/culture/current-directory mutation | Same; invariant numeric formatting retained |
| Order / collection | Default class collection; no cross-test handoff found | One serial group; E7 checks order |

No constructor, Dispose, IAsyncLifetime, IClassFixture or ICollectionFixture exists on these test classes.

**E4 candidates.** Both are finite switch lookup failures before any task or IO:
C-canary-1: `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.Scalars.cs:54`.
C-canary-2: `src/NvtFwCombiner.Profiles/FirmwareFamilies/FirmwareFamilyResolutionNormalizer.Metadata.cs:294`.

| Production location at B | One-token mutation | Intended quick failures after class mapping |
| --- | --- | --- |
| C-canary-1 | `"system"` to `"system-canary"` | C5 valid aliases; C7 direct capability; C8 perfect-like |
| C-canary-2 | `"unsigned-integer"` to `"unsigned-integer-canary"` | C5 valid aliases; C9 round trip; C10 header |

### Plan D: ManagedDistributionLauncherRuntimeTests

Base: **6 files / 3,019 nonblank lines / 69 methods**, in Infrastructure.Tests.
Files are under `tests/NvtFwCombiner.Infrastructure.Tests/VersionManagement/`.
Golden/evidence check: package bytes are synthetic release payloads from CreatePackageForManagedSetup
(`FileSystemManagedVersionRepositoryWriteCustodyTests.cs:9` -> `FileSystemManagedVersionRepositoryTests.cs:449`).
Executable inputs are ReadyProbe, LauncherBootstrap and optional system where.exe, not firmware evidence.
No Golden fixture, firmware BIN or evidenceRefs reader found; marker/recovery evidence is installation state.

| ID | Source file | Lines | Methods | Declaration | Target |
| --- | --- | ---: | ---: | ---: | --- |
| D0 | `ManagedDistributionLauncherRuntimeTests.cs` | 590 | 19 | 7 | remainder |
| D1 | `ManagedDistributionLauncherRuntimeTests.Admission.cs` | 370 | 15 | 6 | D-Admission |
| D2 | `ManagedDistributionLauncherRuntimeTests.Custody.cs` | 651 | 15 | 9 | D-Custody |
| D3 | `ManagedDistributionLauncherRuntimeTests.Progress.cs` | 121 | 2 | 8 | D-Progress |
| D4 | `ManagedDistributionLauncherRuntimeTests.RealPackage.cs` | 856 | 18 | 8 | D-Package |
| D5 | `ManagedDistributionLauncherRuntimeTests.Support.cs` | 431 | 0 | 9 | S3 / local helpers |

| Target | Class | Source lines / methods | Planned ceiling / methods |
| --- | --- | ---: | ---: |
| remainder | `ManagedDistributionLauncherRuntimeTests` | 590 / 19 | 588 / 19 |
| D-Admission | `ManagedDistributionLauncherAdmissionTests` | 370 / 15 | 380 / 15 |
| D-Custody | `ManagedDistributionLauncherCustodyTests` | 651 / 15 | 660 / 15 |
| D-Progress | `ManagedDistributionLauncherProgressTests` | 121 / 2 | 123 / 2 |
| D-Package | `ManagedDistributionLauncherRealPackageTests` | 856 / 18 | 888 / 18 |

Keep D0. Rename D1-D4 to their target class names plus `.cs`.
Rename D5 to `ManagedDistributionLauncherRuntimeTestSupport.cs`, one `internal static` class
(ceiling 400 lines, zero tests), after placing its three single-class helpers as listed below.

**S3 support.** Import
`NvtFwCombiner.Infrastructure.Tests.VersionManagement.ManagedDistributionLauncherRuntimeTestSupport`
with `global using static`. Preserve both PayloadDescriptor overloads together.

| Moved name | Declaration | Users by file / closure | H1/H2/H3/H4 |
| --- | --- | --- | --- |
| `HashA` | D0:9 | D1, D5 factory closure used by D0-D4 | N/N/N/N |
| `HashB` | D0:11 | D5 candidate closure used by D0-D4 | N/N/N/N |
| `PayloadIdentity` | D5:31 | D0-D4, D5 local materialization factory | N/N/N/N |
| `PayloadDescriptor` (both overloads) | D5:66,71 | D0, D1; byte overload calls length overload | N/N/N/N |
| `Candidate` | D5:98 | D0, D1, D2, D4, D5 materialization factory | N/N/N/N |
| `RealPackageCandidate` | D5:136 | D2, D3, D4 | N/N/N/N |
| `Admission` | D5:167 | D0-D4, D5 materialization factory | N/N/N/N |
| `Hash` | D5:175 | D0, D1, D5 payload/descriptor closures | N/N/N/N |
| `TestPayloadCapture` | D5:180 | D0-D4, D5 materialization factory | N/N/N/N |
| `TrackingResource` | D5:242 | D0, D1, D5 stream closure | N/N/N/N |
| `TrackingResourceStream` | D5:286 | D5 TrackingResource.Open closure for D0/D1 | N/N/N/N |
| `MaterializingRepository` | D5:365 | D0, D1, D2, D4, D5 materialization factory | N/N/N/N |
| `TemporaryRoot` | D5:455 | D0-D4 | N/N/N/N |

No H1-H4 hazard found. The nested stream's Stream overrides and repository interface implementations move
with their types; no dependency is left behind. Instance state remains on individual doubles.
Single-class helpers stay private to that destination, placed verbatim without a test-body edit:

| Local helper | Current declaration | Users / destination placement |
| --- | --- | --- |
| `MaterializePromotedInstallationAsync`, `PayloadIdentityAsync` | D5:11,47 | D2; append to D-Custody file |
| `OpaquePayloadCapture` | D5:232 | D1; append to D-Admission file |
| `ReadPackageMembersAsync`, `ReadInstalledMembersAsync` | D2:672,690 | D4; append to D-Package file |
| `OpenRepositoryStagingHandle` | D4:753 | D4; stay there |
| `InlineProgress<T>` | D3:123 | D3; stay there |

The three D5 local helpers and two D2 hash readers also have H1/H2/H3/H4 = N/N/N/N after placement.
Their unqualified dependencies resolve to the shared support; none duplicates a destination member or type.

**S4 collection.** Add `ManagedDistributionLauncherRuntimeSerialGroup.cs` in this directory/namespace,
with `[CollectionDefinition(nameof(ManagedDistributionLauncherRuntimeSerialGroup))]` on a public sealed class.
The collection definition is empty and has no constructor, fixture or mutable state.
Each of D0-D4 gets `[Collection(nameof(ManagedDistributionLauncherRuntimeSerialGroup))]` once.
No fixture or DisableParallelization argument is added. Original attributes are `none` at D0-D5's listed lines.
Calling a static helper on FileSystemManagedVersionRepositoryTests does not inherit its
ReadyProbeProcessSerialGroup; retain the existing independent serialization boundary.

**S6 / S7 references.** All five branch intersections are empty. The only additional reference is
`docs/governance/change-records/VERIFY-111-CI-ASSEMBLY-EXECUTION-02.json:16`, a historical observation of
`NvtFwCombiner.Infrastructure.Tests.VersionManagement.ManagedDistributionLauncherRuntimeTests.`
`PromotedCustodyCloneStartsWhileRootReplacementBlocked`. It is not a live selector or source pin; retain
the old execution identity and map the new identity through E1. No testdata, contract, bug-record,
script selector, partition or test-source pin found. No D file must remain for S6.

| S9 aspect | Before, from reading | Planned mechanical result / pending evidence |
| --- | --- | --- |
| Attributes / fixtures | None; no class base, constructor or lifetime fixture | Shared collection; OS guards retained |
| Static / initialization | Hash constants; per-instance counters/callbacks | One constant owner; per-test doubles |
| Temp / filesystem | GUID roots; fixed children; read-only probe paths | Same allocation/cleanup; no fixed root |
| Indirect resources | Synthetic ZIPs, probes, handles and symbolic links | Same; no Golden/UI/firmware execution |
| Background / cleanup | Gate, child launch, scoped handles/budgets | Same; failure-path cleanup not proved |
| Process-wide state | TempPath/AppContext/SystemDirectory reads; no global writes | Same; retain serial execution |
| Order / collection | Default class collection; no cross-test handoff found | One serial group; E2/E7 check order |

No constructor, Dispose, IAsyncLifetime, IClassFixture or ICollectionFixture exists on the test classes.
D0:343 uses the fixed read-only blocked-probe observation; D0:365 releases its gate in finally.
D2:388 owns a clone-derived lease and child launch; :478-481 disposes lease/installation, with 5s/10s budgets.
D4 staging-holder tests have finite retry/cancellation scenarios and scoped native handles.
TemporaryRoot deletes recursively once; it adds no retry or background cleanup. The two missing-launcher
paths under Path.GetTempPath are read-only probes. No environment/current-directory/culture mutation found.

**E4 candidates.** Use finite D1 resource-admission tests; neither changes a read loop or native wait.
Both locations are in
`src/NvtFwCombiner.VersionManagement.Infrastructure/VersionManagement/ManagedDistributionLauncherRuntime.cs`.
Labels abbreviate ExactMaximumDescriptorIsAdmittedWithoutBootstrapContentRead,
CanonicalMaximumBootstrapIsAdmittedWithoutReadingContent, ResourceAccessFailuresRemainTypedUnavailable,
and EmbeddedPayloadSourceClassifiesBootstrapPresence, all in D1.

| Production location at B | One-token mutation | Intended quick failures after class mapping |
| --- | --- | --- |
| D-canary-1 (:27) | MaxDepth `12` to `1` | ExactMaximumDescriptor; CanonicalMaximumBootstrap; ResourceAccess |
| D-canary-2 (:245) | descriptor count `>` to `<` | CanonicalMaximumBootstrap; ResourceAccess; BootstrapPresence |

The 64 KiB-plus-one allocation/read bound stays intact for both candidates. Filter to these resource tests
in the scratch run, excluding custody child-process and blocked-observation tests.

### Plan E: ProfileBundleSchemaValidatorTests

Base: **21 files / 2,879 nonblank lines / 64 methods**, in Infrastructure.Tests.
Files are under `tests/NvtFwCombiner.Infrastructure.Tests/Bundles/`.
Golden/evidence check: reads are contract schemas and built-in profile/family JSON, not Golden/firmware.
Capture writes synthetic JSON into GUID TempWorkspace directories and retains immutable snapshots.
RepositoryPaths reads NFC_TEST_REPOSITORY_ROOT; no firmware evidenceRefs reader or BIN input found.

| ID | Source file | Lines | Methods | Declaration | Target |
| --- | --- | ---: | ---: | ---: | --- |
| E0 | `ProfileBundleSchemaValidatorTests.cs` | 648 | 16 | 12 | remainder |
| E1 | `ProfileBundleSchemaValidatorTests.SchemaReuse.cs` | 55 | 3 | 7 | E-Reuse |
| E2 | `ProfileBundleSchemaValidatorTests.ConcurrentReuse.cs` | 96 | 2 | 11 | E-Reuse |
| E3 | `ProfileBundleSchemaValidatorTests.ProfileShapeOwnership.cs` | 286 | 4 | 7 | E-Shape |
| E4 | `ProfileBundleSchemaValidatorTests.ProfileExperience.cs` | 45 | 2 | 7 | E-Shape |
| E5 | `ProfileBundleSchemaValidatorTests.FullImageMetadata.cs` | 101 | 2 | 6 | remainder |
| E6 | `ProfileBundleSchemaValidatorTests.V12BankInstances.cs` | 160 | 1 | 8 | E-Banks |
| E7 | `ProfileBundleSchemaValidatorTests.V23.cs` | 75 | 4 | 7 | E-Processors |
| E8 | `ProfileBundleSchemaValidatorTests.V24.cs` | 135 | 3 | 7 | E-Logical |
| E9 | `ProfileBundleSchemaValidatorTests.V25.cs` | 27 | 2 | 6 | E-Logical |
| E10 | `ProfileBundleSchemaValidatorTests.V26.cs` | 153 | 2 | 6 | remainder |
| E11 | `ProfileBundleSchemaValidatorTests.V27.cs` | 38 | 1 | 7 | E-Processors |
| E12 | `ProfileBundleSchemaValidatorTests.V28.cs` | 33 | 1 | 7 | E-Processors |
| E13 | `ProfileBundleSchemaValidatorTests.V29.cs` | 165 | 5 | 6 | remainder |
| E14 | `ProfileBundleSchemaValidatorTests.V210.cs` | 102 | 4 | 7 | E-Input |
| E15 | `ProfileBundleSchemaValidatorTests.V211.cs` | 260 | 3 | 8 | remainder |
| E16 | `ProfileBundleSchemaValidatorTests.V213.cs` | 108 | 3 | 7 | E-Input |
| E17 | `ProfileBundleSchemaValidatorTests.V214.cs` | 93 | 1 | 6 | E-Input |
| E18 | `ProfileBundleSchemaValidatorTests.V215.cs` | 127 | 1 | 7 | E-Output |
| E19 | `ProfileBundleSchemaValidatorTests.V216.cs` | 136 | 3 | 7 | E-Output |
| E20 | `ProfileBundleSchemaValidatorTests.V217.cs` | 36 | 1 | 7 | E-Output |

| Target | Class | Source lines / methods | Planned ceiling / methods |
| --- | --- | ---: | ---: |
| remainder | `ProfileBundleSchemaValidatorTests` | 1,327 / 28 | 1,198 / 28 |
| E-Reuse | `ProfileBundleSchemaReuseTests` | 151 / 5 | 153 / 5 |
| E-Shape | `ProfileBundleSchemaProfileShapeTests` | 331 / 6 | 333 / 6 |
| E-Banks | `ProfileBundleSchemaBankInstanceTests` | 160 / 1 | 162 / 1 |
| E-Processors | `ProfileBundleSchemaProcessorBindingTests` | 146 / 6 | 148 / 6 |
| E-Logical | `ProfileBundleSchemaLogicalOutputTests` | 162 / 5 | 164 / 5 |
| E-Input | `ProfileBundleSchemaInputAuthorityTests` | 303 / 8 | 305 / 8 |
| E-Output | `ProfileBundleSchemaOutputAuthorityTests` | 299 / 5 | 301 / 5 |

Keep E0/E5/E10/E13/E15 paths. All others become `<TargetClass>.<ExistingTopic>.cs`, preserving the source
suffix, including SchemaReuse, ConcurrentReuse, ProfileShapeOwnership, ProfileExperience and each V-number.
E6 becomes `ProfileBundleSchemaBankInstanceTests.V12BankInstances.cs`. This specifies every rename.

**S3 support.** Add `ProfileBundleSchemaValidatorTestSupport.cs`, one internal static class
(ceiling 150 lines, zero tests). Import
`NvtFwCombiner.Infrastructure.Tests.Bundles.ProfileBundleSchemaValidatorTestSupport` with `global using static`.

| Moved name | Declaration | Users by file / closure | H1/H2/H3/H4 |
| --- | --- | --- | --- |
| `SchemaId` | E0:14 | E0 and moved Entry/Schema closures | N/N/N/N |
| `Capture` | E0:462 | E0, E1, E6, E15 | N/N/N/N |
| `CaptureCompositionProfile` | E0:532 | E0, E3, E4, E7-E14, E16-E20 | N/N/N/N |
| `LegacyCombinerStage` | E0:574 | E0, E3, E7, E11, E12 | N/N/N/N |
| `Entry` | E0:599 | E0, E6, E15 and moved Capture closures | N/N/N/N |
| `Schema` | E0:609 | E0, E1 | N/N/N/N |
| `Hash` | E0:632 | moved Entry closure for capture users | N/N/N/N |

The two CaptureFirmwareFamily overloads at E0:494 and E15:235 are an H1/H3 hazard if only one is extracted.
**Keep both and all users E0/E5/E15 in the remainder**, so no divided overload set is imported.
E2's tuple field Schema is accessed through item.Schema/cases[index].Schema; it is not a member of
E-Reuse or an enclosing/base-list class and does not shadow an unqualified helper call.
No moved helper has a remaining private dependency; Entry's SchemaId/Hash closure moves together.

All other helpers/constants remain private to their one destination:
E0 FirmwareFamilyV11AliasJson;
E2 ConcurrentWorkers/ConcurrentRounds/ParseContractSchema/BuiltInDocuments/ValidAndInvalid;
E3 CrcWorkerStage; E4 ExperienceSchema; E5 FullImageMetadataSchema;
E6 RegionInstanceFamily/RegionSet/RegionInstance/CaptureFirmwareFamilyBankInstances;
E8 LogicalOutputProfile (also E9); E10 RuntimeReferenceReplaceProfile/InputSlot (also E13);
E13 RuntimeReferenceReplaceProcessorProfile; E14 DeclaredPrefixProfile;
E15 FindSemantics/LoadFirmwareFamilyWithTpHeader/LoadFirmwareFamilyWithRelationships/FirstMetadataStructure/
CaptureFirmwareFamilyV11TpHeader (E5 shares load/structure helpers inside the remainder);
E16 SourceViewCoverageProfile (also E17)/GetSourceViewCoverageLengthRule; E18 Token (also E19)/Source.
These cross-file users explain the feature groups; no helper needs a second support owner.

**S4 collection.** Add `ProfileBundleSchemaValidatorSerialGroup.cs` in this directory/namespace,
with `[CollectionDefinition(nameof(ProfileBundleSchemaValidatorSerialGroup))]` on an empty public sealed class.
Add `[Collection(nameof(ProfileBundleSchemaValidatorSerialGroup))]` once per destination, on E0, E1, E3,
E6, E7, E8, E14 and E18. No attribute or summary is inserted into pinned E10/E13/E15.
Original attributes are `none` on each E0-E20 at its listed declaration line.
No fixture or DisableParallelization argument is added.

**S6 / S7 references.** No supplied or five-branch lock intersects the class.

| Reference | Pin / handling |
| --- | --- |
| `docs/handoff/1.2.1/R10-01.md:212` | E15:20,86 citations; preserve file/class and these lines |
| `docs/handoff/1.2.4/R54-retirement-plan.md:58` | E10/E13:8 citations; preserve paths/classes/lines |
| `tests/scripts/test_verify_orchestration.py:6574,6625` | Synthetic old FQN for shape test; no reader/selector |
| `docs/handoff/1.1.12/WS-PERF.md:131` | Historical filename glob; not a live selector; retain |
| `docs/handoff/1.2.1/R13-01.md:344` | Historical class timing; retain |
| `docs/governance/change-records/DP-ENVELOPE-1110-C-01.json:47` | E19 historical mutablePaths; no synchronization needed |
| `docs/governance/change-records/DP-ENVELOPE-1110-FOUNDATION-01.json:42` | E19 historical mutablePaths; no synchronization needed |
| `docs/governance/change-records/FULL-IMAGE-METADATA-CORE-110-01.json:22` | E5 historical mutablePaths; no synchronization needed |
| `docs/governance/change-records/STARTUP-CATALOG-1112-01.json:23-24` | E1/E2 historical mutablePaths; no synchronization needed |

The synthetic FQN is `NvtFwCombiner.Infrastructure.Tests.Bundles.ProfileBundleSchemaValidatorTests.`
`ValidateEntriesRejectsMissingOrNullCompositionProfileShape`. The test constructs its own TRX text and
asserts that identity; it does not discover/require the real class. No update is needed.
No other testdata JSON, contract, bug-record, script/verify partition or test-source pin found.

| S9 aspect | Before, from reading | Planned mechanical result / pending evidence |
| --- | --- | --- |
| Attributes / fixtures | None; no class base, constructor or fixture | Shared collection; no fixture added |
| Static / initialization | Constants; production schema cache/lock/options/counters | Same process-wide owners |
| Files / temp | Contract/built-in JSON; GUID workspaces, scoped disposal | Same paths/snapshots and unique roots |
| Concurrency / cleanup | E2 finite Parallel.For, 8 workers/6 rounds, joined | Same concurrency; classes serial |
| Process-wide state | RepositoryPaths environment read; schema registry use | Same build lock/local registries |
| Order / collection | Default collection; explicit in-test reuse | One serial collection; E2/E7 check cache/order |

No constructor, Dispose, IAsyncLifetime, IClassFixture or ICollectionFixture exists on the test classes.
Production validated-schema state stays once per process. TempWorkspace adds GUIDs to its fixed prefixes,
with bounded Windows disposal retries; child filenames do not share a root. No global mutable test field,
environment/culture/current-directory write, child process or indirect UI dispatcher found.

**E4 candidates.** Use fresh-process finite schema tests, not timing/cache-count assertions.
Both locations are in `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleSchemaValidator.cs`.

| Production location at B | One-token mutation | Intended quick failures after class mapping |
| --- | --- | --- |
| E-canary-1 (:165) | delete `!` before IsInstanceValid | E-Reuse independent schemas; E-Shape; E-Logical |
| E-canary-2 (:118) | `"$schema"` to `"$schema-canary"` | E-Reuse; E-Processors V23; E-Logical V24/V25 |

The first reverses a synchronous verdict; the second fails a required-property check before schema build.
Neither changes a lock, iteration bound, registry or cleanup path.

### Plan F: CompiledCompositionTests

Base: **12 files / 2,618 nonblank lines / 50 methods**, in Domain.Tests.
Files are under `tests/NvtFwCombiner.Domain.Tests/Composition/`.
Golden/evidence check: maps, plans, byte assertions and provenance are synthetic, constructed in memory.
V2's Golden promotion blocker/evidence strings are test data; ScalarTransform's pinned fingerprint is an
inline synthetic-plan hash. Neither reads a Golden fixture or represents owner-certified firmware bytes.
FirmwareImageMapTestFactory and CompiledInputSlotTestFactory do no file/firmware IO.

| ID | Source file | Lines | Methods | Declaration | Target |
| --- | --- | ---: | ---: | ---: | --- |
| F0 | `CompiledCompositionTests.cs` | 552 | 9 | 6 | remainder |
| F1 | `CompiledCompositionTests.V2.cs` | 654 | 13 | 7 | F-Core |
| F2 | `CompiledCompositionTests.CapabilityFingerprint.cs` | 29 | 1 | 5 | F-Core |
| F3 | `CompiledCompositionTests.V2Promotion.cs` | 40 | 1 | 5 | F-Core |
| F4 | `CompiledCompositionTests.V2CanonicalReferences.cs` | 75 | 2 | 6 | F-Core |
| F5 | `CompiledCompositionTests.V2InputContract.cs` | 285 | 6 | 6 | F-Input |
| F6 | `CompiledCompositionTests.V2InputGeometry.cs` | 421 | 8 | 6 | F-Input |
| F7 | `CompiledCompositionTests.OutputNaming.cs` | 100 | 1 | 5 | F-Naming |
| F8 | `CompiledCompositionTests.OutputNaming.Validation.cs` | 139 | 2 | 5 | F-Naming |
| F9 | `CompiledCompositionTests.ScalarTransform.cs` | 150 | 3 | 5 | F-Transform |
| F10 | `CompiledCompositionTests.SourceEnvelope.cs` | 19 | 1 | 5 | F-Transform |
| F11 | `CompiledCompositionTests.FingerprintWireCompatibility.cs` | 154 | 3 | 9 | F-Wire |

| Target | Class | Source lines / methods | Planned ceiling / methods |
| --- | --- | ---: | ---: |
| remainder | `CompiledCompositionTests` | 552 / 9 | 536 / 9 |
| F-Core | `CompiledCompositionV2ProvenanceTests` | 798 / 17 | 657 / 17 |
| F-Input | `CompiledCompositionInputContractTests` | 706 / 14 | 708 / 14 |
| F-Naming | `CompiledCompositionOutputNamingTests` | 239 / 3 | 170 / 3 |
| F-Transform | `CompiledCompositionTransformTests` | 169 / 4 | 171 / 4 |
| F-Wire | `CompiledCompositionFingerprintWireCompatibilityTests` | 154 / 3 | 156 / 3 |

Keep F0. F1-F10 become `<TargetClass>.<ExistingTopic>.cs`, preserving the complete source suffix,
including OutputNaming.Validation. F11 becomes `CompiledCompositionFingerprintWireCompatibilityTests.cs`.

**S3 support.** Add `CompiledCompositionTestSupport.cs`, one internal static class
(ceiling 255 lines, zero tests). Import
`NvtFwCombiner.Domain.Tests.Composition.CompiledCompositionTestSupport` with `global using static`.

| Moved name | Declaration | Users by file / closure | H1/H2/H3/H4 |
| --- | --- | --- | --- |
| `CreateV2` | F1:468 | F0-F2, F4-F7, F9 | N/N/N/N |
| `CreateInputContract` | F1:553 | F1 CreateV2 closure for all its users | N/N/N/N |
| `CreateResolvedMap` | F1:591 | F0, F1, F4, F6, F9, F11; CreateV2 closure | N/N/N/N |
| `CreateExactMapInputContract` | F0:561 | F0, F9 | N/N/N/N |
| `NormalFlashCodeOutput` | F7:33 | F1, F7 | N/N/N/N |
| `TpFirmwareOutput` | F7:74 | F1, F7 | N/N/N/N |

No H1-H4 hazard found. CreateV2's private default-input/map dependencies move together.
F5's distinct InputContract name is not an overload of CreateInputContract; F6's callers remain with F5,
so Slot/InputContract need no extraction. Keep other helpers private in their one feature group:
F0 CreateMerge/CreateReplace/CreateMultiOutput/CreatePatchComposition/CreateProcessorComposition/CreateProtocolPlan;
F1 CreateRegionAccessContract (also F4)/CreateResolvedNestedMap;
F5 Slot/InputContract/DirectCapabilityAdmission/AliasedCapabilityAdmission/Binding;
F6 CreateTpComposition/CreateSourceViewComposition/CreateDeclaredPrefixComposition/CreateDpReplaceComposition/
CreateTpPlan/CreateTpRegionAccessContract; F8 CreateTypedNormalOutput/NormalTokenRequirements;
F9 CreateScalarTransformComposition/CreateExternalProcessorComposition;
F11 InvokeFingerprintWriter/ReadFingerprintField.

**S4 collection.** Add `CompiledCompositionSerialGroup.cs` in this directory/namespace with
`[CollectionDefinition(nameof(CompiledCompositionSerialGroup))]` on an empty public sealed class.
Add `[Collection(nameof(CompiledCompositionSerialGroup))]` once per new class, on F1/F5/F7/F9/F11.
For the pinned remainder add `CompiledCompositionTests.Collection.cs`: same namespace, the collection
attribute and an empty `public sealed partial class CompiledCompositionTests`. Insert no line into F0.
Original attributes are `none` at every F0-F11 declaration line. No fixture/DisableParallelization is added.

**S6 / S7 references.** No supplied or five-branch lock intersects the class.

| Reference | Pin / handling |
| --- | --- |
| `docs/handoff/1.2.4/R54-retirement-plan.md:54` | F0:61,398 citations; keep path/class and cited lines |
| `docs/handoff/1.2.1/R13-01.md:348,409` | Historical class timing/hotspot; retain |
| `docs/governance/change-records/DP-ENVELOPE-1110-FOUNDATION-01.json:39` | F10 historical mutablePaths; no synchronization needed |

F0 extraction starts at :561, after both citations; preserve its prefix and put collection membership
in the new partial. No testdata, contract, bug-record, script/verify partition or test-source pin found.
F11 reflects on production CompiledComposition, not the test class; its method-name strings stay unchanged.

| S9 aspect | Before, from reading | Planned mechanical result / pending evidence |
| --- | --- | --- |
| Attributes / fixtures | None; no class base, constructor or fixture | Shared group; remainder on new partial |
| Static / initialization | Stateless factories, no mutable field/initializer | One support; local topic factories |
| Indirect resources | In-memory facts/slot factories, production reflection | Same; no UI/file/Golden/process IO |
| Background / cleanup | Synchronous; no task/process/temp workspace | Same; no new cleanup scope |
| Process-wide state | Invariant wire formatting; no global mutation | Same |
| Order / collection | Default class collection; no cross-test handoff found | One serial group; E2/E7 check order |

No constructor, Dispose, IAsyncLifetime, IClassFixture or ICollectionFixture exists on the test classes.
No environment/culture/current-directory write or static initialization multiplication found.

**E4 candidates.** Both change a finite StringBuilder append in the existing production writer,
`src/NvtFwCombiner.Domain/Firmware/FirmwareFingerprintWriter.cs`.
The three F-Wire methods are InputPolicyFingerprintWireCodesRemainCompatible,
ValidationFingerprintWireCodesRemainCompatible and ContextAndRegionAccessFingerprintWireCodesRemainCompatible.

| Production location at B | One-token mutation | Intended quick failures after class mapping |
| --- | --- | --- |
| F-canary-1 (:172) | final Append argument `value` to `fieldName` | F-Core; F-Input; F-Naming; F-Wire |
| F-canary-2 (:173) | `'\n'` to `'\r'` | All three F-Wire methods |

Use the three F-Wire methods as the smallest fast filter for either candidate; the first also exercises
other mapped classes through compiler-evidence, capability-provenance and typed-naming fingerprint tests.
No loop bound, reflection target, validation recursion, filesystem or wait operation changes.

**Dispatch disposition.** All four plans are defined and unblocked at B; none is stopped for Golden/firmware
evidence or a current branch lock. Preserve the explicit pins and C's rejected extraction.
No open product/design question remains. Discovery, canary outcomes, timing, CI and independent review
remain execution evidence under the unchanged envelope below, not claims made by these plans.

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
