# R54: Customized Replace retirement inventory and step plan

## 1. Status and purpose

Status: Proposed retirement plan; inventory only; 2026-10-02; source HEAD `d38f4e7c3`.
Version: 1.2.4, R54; recorded through `feature/1.2.4/records-r54-r33-05-idle`.

Retire Customized Replace (`General Replace`, `general-replace`) while preserving Customized Merge.
Decision 230 fixes retirement of active code, route, profile and saved-rule execution, including NT51926 compatibility.
Decision 229 reopens only Customized Merge. This document proposes implementation boundaries; it changes no behavior.

Authority: `docs/handoff/1.2.x.md:314` and `docs/handoff/1.2.x.md:325`.
Allocation: `docs/handoff/1.1.14/1.2.x-allocation.md:203` also requires historical reports to remain readable.
No dedicated R54 row exists in the allocation inventory; its O05 cross-reference is at
`docs/handoff/1.1.14/1.2.x-inventory.md:535`.
The retirement residue input is `docs/handoff/1.2.1/R12-02.md:190`.
R39's hidden General Merge/Replace boundary is historical context: `docs/handoff/1.2.1/R39.md:39`.

No change is proposed to remaining workflows' bytes, ranges, CRC/header behavior, padding, ordering or output naming.

## 2. Table 1: Inventory by area

Counts are unique files containing the case-sensitive alternatives `GeneralReplace`, `general-replace`,
`General Replace` or `Customized Replace`, across the requested source, tests, profiles, contracts, SPEC and test data.
The four matching handoff documents named above are included; R39 has zero exact matches and is read as context.
Generated files are excluded by normal `rg` ignore rules. Counts describe this HEAD, not deletion volume.

Each matching file appears in one disposition count. For mixed files, KEEP-SHARED preserves the file and its surviving
members; Replace-only members still leave. KEEP-REPLAY preserves historical shapes, schemas or interpretation coverage,
not execution admission. REFUSE includes files whose old entry or positive tests must become retirement refusal.
DELETE applies only to exclusive files. Each row gives at most five representative evidence locations.

| Area | Files | Disposition | Representative items and boundary |
| --- | ---: | --- | --- |
| Domain | 1 | KEEP-REPLAY | `src/NvtFwCombiner.Domain/Composition/ExperienceIds.cs:22`: retain the stable token for old reports and explicit refusal; do not remove generic Replace operations. |
| Application: shared authoring, execution and catalog | 21 | KEEP-SHARED | `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.Contracts.cs:10`; `src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs:169`. General Merge calls preparation at `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:62` and consumes `AcceptedGeneralExecutionPlan` at `src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs:150`. Remove only Replace members; Table 2 protects the shared core. |
| Application: diagnostic report shape | 1 | KEEP-REPLAY | `src/NvtFwCombiner.Application/Composition/GeneralReplaceDiagnosticPreview.cs:27`: preserve the summary and coverage shape used by `CompositionRunReport.DiagnosticPreview` for historical diagnostic reports; remove only active projectors at `src/NvtFwCombiner.Application/Composition/GeneralReplaceDiagnosticPreview.cs:50` and `:127`. Table 2 explains the serialization boundary. |
| Infrastructure: exclusive Replace adapters | 5 | DELETE | `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralReplace.Mapping.cs:13`; `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralReplace.Readiness.cs:13`; `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralReplace.V2.cs:9`; `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2GeneralReplaceDraftLoader.cs:9`; `src/NvtFwCombiner.Infrastructure/Composition/SavedCompositionRuleV2Admission.GeneralReplace.cs:12`. Delete after refusal replaces admission. |
| Infrastructure: shared planner, bundle, registry, policy and projection | 7 | KEEP-SHARED | `src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs:56` loses Replace registration; General Merge remains at `src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs:46`. Preserve loader projection used by Merge at `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2GeneralMergeDraftLoader.cs:46`; preserve CtrlRAM compilation at `src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.V2.cs:110`. |
| Infrastructure: Saved Rule admission/inspection | 2 | REFUSE | `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2Inspector.cs:104`; `src/NvtFwCombiner.Infrastructure/Composition/BuiltInSavedRuleAuthoring.cs:34`: recognize retired declarations before missing Parent/route errors; NT51926 rules must receive retirement rather than migration advice. General Merge loading at `src/NvtFwCombiner.Infrastructure/Composition/BuiltInSavedRuleAuthoring.cs:6` survives. |
| Profiles code | 2 | KEEP-SHARED | `src/NvtFwCombiner.Profiles/V2/V2RuntimeReferenceReplaceCompileRequest.cs:105`; `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.cs:504`. The misleading General Replace wording does not make reference-clone lowering exclusive: CtrlRAM creates the request at `src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.V2.cs:218`. Add refusal at the existing compiler boundary. |
| Profiles: bundled candidate, pending owner choice | 1 | KEEP-SHARED | `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-general-replace-dp-single-candidate.json:3`: keep the internal 0.1.0 candidate under option A in section 6; refuse compilation and remove its registration. Option B would delete it and rewrite the shared bundle. Do not select B through cleanup. |
| Profiles: shared bundle, family and trust index | 3 | KEEP-SHARED | Keep bundle/family bytes under option A; remove registration at `profiles/built-in/package-trust-index.json:230`. Option B removes entry at `profiles/built-in/nt51926-ctrlram-replace-candidate/profile-bundle.json:38` and map/region set at `profiles/built-in/nt51926-ctrlram-replace-candidate/families/nt51926-ctrlram-replace.json:523` and `:698`, changing surviving CtrlRAM bindings. Section 4 traces the hash consequences; section 6 requires a firmware-owner choice. |
| Contracts and schemas: current shared contracts | 8 | KEEP-SHARED | `docs/contracts/composition-request-v1.md:24` also serves General Merge; `docs/contracts/composition-profile-v2.17.schema.json:362` can recognize retired syntax without granting artifacts. Remove the active policy row at `docs/contracts/canonical-capability-policy-v1.json:821`; shared reference-clone behavior remains at `docs/contracts/composition-profile-v2.md:158`. |
| Contracts and schemas: versioned interpretation and live dependencies | 14 | KEEP-REPLAY | Eleven profile schemas 2.6 through 2.16, plus the v1 profile prose, v1 Saved Rule schema and report contract. These schemas also serve live bundles: `profiles/built-in/package-trust-index.json:189` selects v2.9. Representatives: `docs/contracts/composition-profile-v2.6.schema.json:126`; `docs/contracts/saved-composition-rule-v1.schema.json:49`; `docs/contracts/composition-report-v1.md:104`. Schema acceptance grants no retired execution authority. |
| Contracts and schemas: Saved Rule v2 | 2 | REFUSE | `docs/contracts/saved-composition-rule-v2.schema.json:32`; `docs/contracts/saved-composition-rule-v2.md:126`. Retain enough syntax recognition to diagnose old Replace rules; replace the NT51926 execution promise at `docs/contracts/saved-composition-rule-v2.md:144` with retirement. General Merge initializer remains at `docs/contracts/saved-composition-rule-v2.md:71`. |
| CLI: exclusive command body | 1 | DELETE | `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.General.cs:10`: remove preparation, mappings/patches/fill execution and diagnostic Preview. |
| CLI: command and Saved Rule refusal | 2 | REFUSE | `src/NvtFwCombiner.Cli/CliApplication.cs:108`; `src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.cs:67`: preserve recognition, refuse execution and stop producing executable Replace mapping fragments. The shared Merge formatter at `src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.cs:65` remains. |
| CLI: shared dispatch/help | 3 | KEEP-SHARED | `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs:22`; `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Usage.cs:16`; `src/NvtFwCombiner.Cli/CliApplication.Usage.cs:23`. Remove Replace advertisements and dispatch arms; CtrlRAM dispatch remains at `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs:109`. |
| Presentation: exclusive mapping row | 1 | DELETE | `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/GeneralReplaceMappingViewModel.cs:5`: remove the Replace-only row after survivor coverage exists. |
| Presentation: shared publication in authoring partial | 1 | KEEP-SHARED | `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.General.cs:10` owns `WindowPublication`; CtrlRAM uses it at `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Authoring.cs:148` and `:170`; `src/NvtFwCombiner.Presentation.Avalonia/MainWindow.axaml.cs:96` sets it. Step 5 moves the property to a surviving partial before removing General Replace members. |
| Presentation: mixed views, state, resources and composition | 27 | KEEP-SHARED | Remove Home command at `src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowPageTemplates.axaml:44`, panel at `src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowWorkflowTemplates.axaml:82`, and IC disclosure at `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/WorkflowSessionPresentationViewModel.DeviceContext.cs:147`. General Merge uses the same row view at `src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowWorkflowTemplates.axaml:244`; CtrlRAM remains at `src/NvtFwCombiner.Presentation.Avalonia/PresentationCompositionServices.cs:44`. Preserve historical report labels. |
| Bootstrap/composition | 0 | KEEP-SHARED | No exact naming match; dependency tracing reaches `src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs:79`. General Merge still needs the General owner, planner and inspector through `src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs:132`. Do not delete this registration. |
| Tests: Domain.Tests | 1 | KEEP-SHARED | `tests/NvtFwCombiner.Domain.Tests/Composition/CompiledCompositionTests.cs:398`: Replace sample identity may change; General Merge fingerprint coverage remains at `tests/NvtFwCombiner.Domain.Tests/Composition/CompiledCompositionTests.cs:61`. |
| Tests: Application.Tests, shared | 13 | KEEP-SHARED | `tests/NvtFwCombiner.Application.Tests/Authoring/GeneralSelectedFileSessionLifecycleTests.cs:435`; `tests/NvtFwCombiner.Application.Tests/Authoring/MergeAuthoringSessionSetTests.cs:173`. General Merge's session/initializer coverage at `tests/NvtFwCombiner.Application.Tests/Authoring/MergeAuthoringSessionSetTests.cs:177` remains. `tests/NvtFwCombiner.Application.Tests/MemoryLayout/MemoryLayoutProjectorGeneralReplaceTests.cs:11` tests the live shared projector, not historical replay; rehost its generic projection assertions on survivors. |
| Tests: Application.Tests, historical projections | 1 | KEEP-REPLAY | `tests/NvtFwCombiner.Application.Tests/Composition/GeneralReplaceDiagnosticPreviewTests.cs:99`: retain historical diagnostic interpretation checks without invoking retired preparation. |
| Tests: Infrastructure.Tests, shared | 1 | KEEP-SHARED | `tests/NvtFwCombiner.Infrastructure.Tests/Bundles/ProfileBundlePackageTrustIndexLoaderTests.cs:74`: update the removed registration assertion; CtrlRAM registrations remain covered at `tests/NvtFwCombiner.Infrastructure.Tests/Bundles/ProfileBundlePackageTrustIndexLoaderTests.cs:95`. |
| Tests: Infrastructure.Tests, historical schemas | 2 | KEEP-REPLAY | `tests/NvtFwCombiner.Infrastructure.Tests/Bundles/ProfileBundleSchemaValidatorTests.V26.cs:8`; `tests/NvtFwCombiner.Infrastructure.Tests/Bundles/ProfileBundleSchemaValidatorTests.V29.cs:8`: schema readability is distinct from compiler permission. |
| Tests: Infrastructure.Tests, exclusive selector | 1 | DELETE | `tests/NvtFwCombiner.Infrastructure.Tests/Composition/CanonicalDynamicRouteInventoryNumberChoiceTests.cs:44`: tests only the General Replace projection that leaves. |
| Tests: ProfileContract.Tests, shared | 4 | KEEP-SHARED | `tests/NvtFwCombiner.ProfileContract.Tests/CompositionProfileV2HeaderTests.cs:18`; `tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.ReplaceLowering.cs:56`. Retain parser and generic reference-clone cases; CtrlRAM compilation remains at `tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.RuntimeReferenceReplaceProcessor.cs:71`. |
| Tests: ProfileContract.Tests, old executable candidate | 1 | REFUSE | `tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.RuntimeReferenceReplace.cs:12`: valid General Replace syntax must now produce the typed retirement failure and no artifact. Move reusable range/clone assertions to CtrlRAM before replacing positive admission. |
| Tests: Bootstrap.Tests, shared | 24 | KEEP-SHARED | `tests/NvtFwCombiner.Bootstrap.Tests/GeneralWorkflowTestSupport.cs:1`; `tests/NvtFwCombiner.Bootstrap.Tests/Fixtures/canonical-route-axes-v2.txt:1`. General Merge CLI coverage remains at `tests/NvtFwCombiner.Bootstrap.Tests/GeneralMergeCliCommandTests.cs:1`. Migrate report helpers and route snapshots; do not simply delete shared assertions. |
| Tests: Bootstrap.Tests, retirement and NT51926 rules | 9 | REFUSE | `tests/NvtFwCombiner.Bootstrap.Tests/Nt51926GeneralReplaceCandidateProfileTests.cs:19`; `tests/NvtFwCombiner.Bootstrap.Tests/ReplaceCliCommandTests.General.cs:1`; `tests/NvtFwCombiner.Bootstrap.Tests/ReplaceCliCommandTests.General.SchemaMigration.cs:37`; `tests/NvtFwCombiner.Bootstrap.Tests/SavedRuleCliCommandTests.Mappings.cs:10`; `tests/NvtFwCombiner.Bootstrap.Tests/WorkbenchGeneralReplacePatchTests.cs:18`. Replace positive execution and intermediate generic failures with explicit retirement/no-side-effect cases, including Saved Rule v1 and v2. |
| Tests: Architecture.Tests | 20 | KEEP-SHARED | `tests/NvtFwCombiner.Architecture.Tests/ApplicationBoundaryTests.AcceptedExecutionConvergence.cs:33`; `tests/NvtFwCombiner.Architecture.Tests/PresentationBoundaryTests.ShellSurface.cs:70`. Reverse obsolete presence assertions and add the General retirement boundary; General Merge's focused owner remains protected at `tests/NvtFwCombiner.Architecture.Tests/ApplicationBoundaryTests.GeneralWorkflowConvergence.cs:38`. |
| Tests: UiSmoke.Tests, shared | 25 | KEEP-SHARED | `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.GeneralAdmission.cs:14`; `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.FirmwareInspection.NavigationMatrix.cs:119`. General Merge cancellation/admission at `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.GeneralAdmission.cs:223` remains; move shared navigation, lifecycle and output coverage to survivors. |
| Tests: UiSmoke.Tests, reports | 4 | KEEP-REPLAY | `tests/NvtFwCombiner.UiSmoke.Tests/ReportJsonSamples.cs:283`; `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.ReportHexDiff.cs:17`; `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.ReportPerformance.cs:13`; `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.ReportPerformanceObservations.cs:14`. Keep old-report tests; replace live General Replace fixture generation with synthetic persisted reports or surviving execution. |
| Tests: UiSmoke.Tests, mixed authoring and localization | 1 | KEEP-SHARED | `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.GeneralReplace.cs:14`, `:22` and `:37` are whole-shell localization tests. Step 1 moves all three first; only Replace authoring tests leave. The live NT51926 test at `:138` changes with route removal in step 4. |
| Tests: TestSupport | 1 | KEEP-SHARED | `tests/NvtFwCombiner.TestSupport/RuntimeReferenceReplaceTestDocuments.cs:69`: retain synthetic documents for rejection and shared lowering; CtrlRAM caller at `tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.RuntimeReferenceReplaceProcessor.cs:75`. |
| Tests: CatalogProbe | 2 | KEEP-SHARED | `tests/NvtFwCombiner.CatalogProbe/CompilationScenarios.cs:60`; `tests/NvtFwCombiner.CatalogProbe/CatalogEvidence.cs:195`: remove General registration enumeration and success expectations; General Merge scenario remains at `tests/NvtFwCombiner.CatalogProbe/CompilationScenarios.cs:54`. |
| Tests: Shared helpers | 1 | KEEP-SHARED | `tests/Shared/TrustedProfileBundleCatalogTestExtensions.cs:58`: retain the generic compile helper; CtrlRAM request caller at `tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.RuntimeReferenceReplaceProcessor.cs:84`. |
| Tests: GoldenRegression.Tests and ReadyProbe | 0 | KEEP-SHARED | No exact matches. This is not proof of unaffected execution or a Golden pass; applicable surviving Golden cases remain required. |
| SPEC and current handoff documents | 4 | KEEP-SHARED | `SPEC.md:249` still advertises executable General Replace and must change during implementation; shared General Merge authoring remains at `SPEC.md:584`. Preserve decisions at `docs/handoff/1.2.x.md:325`, allocation at `docs/handoff/1.1.14/1.2.x-allocation.md:203`, and the remaining-workflow inventory at `docs/handoff/1.1.14/1.2.x-inventory.md:535`. |
| SPEC and documents: dated residue assessment | 1 | KEEP-REPLAY | `docs/handoff/1.2.1/R12-02.md:190`: preserve its dated findings and use them as the removal checklist; R39's related historical boundary is at `docs/handoff/1.2.1/R39.md:39` (zero counted matches). |
| Test data: manifests | 1 | KEEP-SHARED | `testdata/golden/canonical/manifest.json:427` is contract-only, not a certified output case. Remove its active retired-route entry with policy/registration removal; General Merge evidence at `testdata/golden/canonical/manifest.json:420` and Standard Merge Golden evidence at `:434` remain. Do not alter surviving expected output hashes or bytes; option B permits only the reviewed route-evidence re-pins described in section 4. |
| Test data: dated owner evidence request | 1 | KEEP-REPLAY | `testdata/golden/owner-handoff/0718-missing-owner-evidence/README_請先看.md:26` is a dated General Replace migration evidence request. Preserve it as history; `testdata/**` is R3 and this file is outside the implementation write set. |

Totals: 220 unique matching files: DELETE 8; KEEP-SHARED 171; KEEP-REPLAY 25; REFUSE 16.
The four requested manifests were searched; only the canonical manifest names General Replace.
Test data has two matching files including the dated request above. Counts use conservative option A pending decision.
Additional hash/pin/materializer dependencies below are outside this matching-file count.
These counts include tests and documents, and do not mean eight complete production components or 16 runtime entries.

## 3. Table 2: Shared dependencies that must remain

Retain these types and listed members even when their file also contains General Replace.
Application owns admission, preparation and terminal execution; Infrastructure supplies planner/read adapters;
Presentation and CLI consume the same typed ports. No replacement executor or UI-owned firmware policy is needed.

| Shared type or members to retain | Definition / owner | General Merge caller or dependency |
| --- | --- | --- |
| `IGeneralAuthoring`: `GetMergeAdmission`, default length/fill, `PrepareMergeSessionAsync` | `src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs:170` | `src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs:132`; `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.General.cs:216` |
| `IGeneralAuthoringPlanner`: Merge admission, initializer and `PlanGeneralMerge`; Merge plan/result types | `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.Contracts.cs:10` | `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:97`; `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.cs:94` |
| `GeneralAuthoringExperience` and `GeneralAuthoringSessionPreparation` | `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:10`; `src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs:291` | `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.General.cs:215`; registration at `src/NvtFwCombiner.Bootstrap/CompositionHostServices.cs:79` |
| `CaptureSelectedFilesAsync`, `AcceptSelectedFiles`, `CreateExactCatalog`, `TryRetainGeneralMappingCompilation`, `FileRows`, failure/result helpers | `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:318`; `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.Results.cs:23` | Merge preparation calls them at `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:71`, `:76`, `:87`, `:107`, `:117`; these are shared method bodies, not Replace cleanup targets. |
| `AuthoringSessionState`: draft, activation, revision, selected-file adoption and immutable acceptance | `src/NvtFwCombiner.Application/Authoring/AuthoringSessionState.cs:310` | `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:115`; Merge mapping adoption at `src/NvtFwCombiner.Application/Authoring/AuthoringSessionState.SelectedFileInspection.cs:637` |
| `AuthoringMappingState.Create`; `CreateGeneralMergeAuthoringState`, `TryCreateGeneralMergeAuthoringDraft`, common draft completion | `src/NvtFwCombiner.Application/Authoring/AuthoringMappingState.cs:22`; `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringMappingUseCase.cs:11` | `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.General.cs:118` and `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/MergePresentationViewModel.General.cs:132` |
| `GeneralMappingDraftState`: `Rows`, Saved Rule resource policy, value/compilation equality and accepted-length handling | `src/NvtFwCombiner.Application/Authoring/GeneralMappingDraftState.cs:390` | `src/NvtFwCombiner.Application/Authoring/GeneralMergeDraftState.cs:106`; equality at `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:126`; Saved Rule projection at `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2GeneralMergeDraftLoader.cs:111` |
| `GeneralMappingDraftRow`; `GeneralMappingSource.File`; `GeneralMappingSourceKind.FileArtifact`; `GeneralMappingFileRangePreset.SourceSlice` | `src/NvtFwCombiner.Application/Authoring/GeneralMappingDraftState.cs:6`, `:22`, `:130`, `:170` | `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringMappingUseCase.cs:22`: creates Merge rows with the file source and source-slice preset. Keep source/target ranges, provenance, alignment and overlap members. |
| `GeneralMergeDraftState`, `GeneralMergeOutputInitializer`, `GeneralMergeAuthoringUseCase` | `src/NvtFwCombiner.Application/Authoring/GeneralMergeDraftState.cs:93`; `src/NvtFwCombiner.Application/Authoring/GeneralMergeAuthoringUseCase.cs:53` | `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2GeneralMergeDraftLoader.cs:60`; `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:52` |
| `GeneralAuthoringAdmissionUseCase`, admission evaluator/result and occupancy summaries | `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringAdmissionUseCase.cs:60`; `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringAdmissionEvaluator.cs:6` | `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.V2.cs:36`; shared evaluator invocation at `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringAdmissionUseCase.cs:168` |
| `GeneralInputResource`, resource limits and `GeneralSavedRuleResourcePolicy` | `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringResourceLimits.cs:67`; `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringResourceLimits.cs:260` | `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.V2.cs:33`; accepted input resources at `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:109` |
| `ISelectedFileContentInspector`, `SelectedFileContentInspection`, `FileContentSnapshotInspector` | `src/NvtFwCombiner.Application/Ports/ISelectedFileContentInspector.cs:9`; `src/NvtFwCombiner.Infrastructure/Files/FileContentSnapshotInspector.cs:12` | Merge captures files at `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:76`, through the injected port at `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:21`. |
| `GeneralSelectedFileInspection`, issue/result types and inspection service | `src/NvtFwCombiner.Application/Authoring/GeneralSelectedFileInspection.cs:10`; `src/NvtFwCombiner.Application/Authoring/GeneralSelectedFileInspection.cs:88` | `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:74`; capture calls `InspectAsync` at `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs:328`. |
| `AcceptedGeneralExecutionPlan`: admission, bindings, immutable artifacts and compilation identity | `src/NvtFwCombiner.Application/Capabilities/AcceptedGeneralExecutionPlan.cs:11` | `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.V2.cs:137`; execution consumes it at `src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs:150`. |
| `AcceptedCompositionExecutionRequest`, sole `ICompositionExecution`, `CreateGeneralBindings` and shared run/delivery methods | `src/NvtFwCombiner.Application/Composition/AcceptedCompositionExecutionRequest.cs:95`; `src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs:381` | `src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs:161`; shared bundle bindings at `src/NvtFwCombiner.Application/Composition/CompositionExecutionBundleDelivery.cs:231`. Remove only `CreateGeneralReplaceBindings` and the Replace dispatch/body. |
| `ExplicitMapping`, `ExplicitMappingOperationKind.CopyRange`, immutable input spaces/bindings, `CompiledComposition`, sole `CompositionEngine` | `src/NvtFwCombiner.Domain/Composition/ExplicitMapping.cs:4`; `src/NvtFwCombiner.Domain/Composition/CompositionEngine.cs:4` | Merge lowers spaces, bindings and mappings at `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralMerge.Mapping.cs:68`, `:69`, `:73`; shared execution at `src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs:165`. Generic `ReplaceRange`/reference cloning also remains for CtrlRAM. |
| General Merge registry, dynamic-route resolver and shared V2 bundle/catalog | `src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs:46`; `src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs:269` | `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.cs:34`; Merge Parent binding at `src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2Bundle.cs:202`. |
| `ISavedRuleAuthoring.LoadGeneralMergeSavedRule`, `SavedRuleV2GeneralMergeDraftLoader`, common `LoadFile`/`ProjectMappings` and Merge admission | `src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs:212`; `src/NvtFwCombiner.Infrastructure/Composition/SavedCompositionRuleV2Admission.cs:43` | `src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs:28`; Merge projection at `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2GeneralMergeDraftLoader.cs:46`. The Replace-named partial is removable; this shared class is not. |
| `GeneralMappingRowViewModel`, `GeneralMergeMappingViewModel`, `GeneralMappingRow` view and common range editor/inspection state | `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/GeneralMappingRowViewModel.cs:6`; `src/NvtFwCombiner.Presentation.Avalonia/Views/GeneralMappingRow.axaml:7` | `src/NvtFwCombiner.Presentation.Avalonia/ViewModels/GeneralMergeMappingViewModel.cs:4`; surviving Merge view at `src/NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowWorkflowTemplates.axaml:244`. |
| `CompositionRunReport.DiagnosticPreview`, constructor parameter and summary types | `src/NvtFwCombiner.Application/Composition/CompositionRunReport.cs:33`, `:134-135` | Keep the report contract and historical non-null diagnostic payload shape. Contrary to the review, the property has `WhenWritingNull`; `src/NvtFwCombiner.Application/Composition/CompositionRunReportJson.cs:75-90` honors that attribute. Surviving ordinary reports omit null diagnostics; deletion is not proven to change every survivor's JSON. Preserve current survivor serialization and historical diagnostics rather than removing these CLR shapes in R54. |
| `SavedRuleCliSupport` | `src/NvtFwCombiner.Cli/SavedRuleCliSupport.cs:6` | General Merge calls `TryCreateSlotBindings` at `src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs:19` and `PrintIssues` at `:34`. Steps 3 and 6 retain both. |
| `CompleteReplaceRunAsync` | `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs:32` | CtrlRAM calls it at `src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs:183`. Retain its shared output, report and delivery handling. |
| `AuthoringByteRangeCodec.TryParseStartAndLength` | `src/NvtFwCombiner.Application/Authoring/AuthoringByteRangeCodec.cs:32` | General Merge parses manual mappings at `src/NvtFwCombiner.Cli/MergeCliCommandHandler.ManualMappings.cs:118` and `:123`. Retain the shared range codec. |

Additional safeguards:

- KEEP-SHARED: inline-source codec and HexOverwrite/HexFill members in
  `src/NvtFwCombiner.Application/Authoring/GeneralMappingDraftState.cs:32`.
  Merge rows are file-only at line 229. After Replace-only production members leave, the shared evaluator at
  `src/NvtFwCombiner.Application/Authoring/GeneralAuthoringAdmissionEvaluator.cs:280-311` and the inline UI branch at
  `src/NvtFwCombiner.Presentation.Avalonia/Views/GeneralMappingRow.axaml:108` remain. Optional cleanup is outside R54.
- KEEP-SHARED: `VirtualArtifactLocator.IsVirtual` at
  `src/NvtFwCombiner.Application/Composition/VirtualArtifactLocator.cs:16` has three surviving production consumers:
  `src/NvtFwCombiner.Application/Composition/CompositionExecutionBundleDelivery.cs:284`,
  `src/NvtFwCombiner.Infrastructure/Files/ProtectedPathGuard.cs:49` and
  `src/NvtFwCombiner.Application/Composition/CompositionOutputConfirmationSummary.cs:62`.
  Its only production producer is in the DELETE file
  `src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralReplace.Mapping.cs:207`.
  No surviving production producer was found; remove `CreateGeneralReplacePatch` after rehosting its generic tests,
  and retain the shared classifier. This closes the producer uncertainty without expanding cleanup scope.
- Confirmed CtrlRAM safeguard: `V2RuntimeReferenceReplaceCompileRequest` is constructed at
  `src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.V2.cs:218`.
  Do not delete the compiler, reference-clone primitives or postbuild policies because their comments name Replace.

## 4. What "as for DP Replace" means in practice

| DP retirement observed at this HEAD | General Replace equivalent |
| --- | --- |
| CLI recognizes the old command before host creation, writes `error: cli.retired-experience: DP Replace is retired.`, and returns 64: `src/NvtFwCombiner.Cli/CliApplication.cs:55`; constant at `src/NvtFwCombiner.Cli/CliApplication.cs:12`. Help processing precedes this guard. | Add the same early guard for `general-replace`, with text `error: cli.retired-experience: Customized Replace is retired.` and exit 64. Use the user-visible name consistently. Remove advertised syntax and handler dispatch; do not resolve host state, firmware paths, tools or delivery for ordinary retired invocations. Preserve the global help convention. |
| Refusal preserves existing BIN/report and creates no bundle, even with invalid input paths: `tests/NvtFwCombiner.Bootstrap.Tests/ReplaceCliCommandTests.Dp.cs:11`; hidden-help check at `tests/NvtFwCombiner.Bootstrap.Tests/ReplaceCliCommandTests.Validation.cs:9`. | Duplicate the no-side-effect boundary for preview/build, mapping/patch/fill, `--rule`, malformed firmware paths, existing output/report and bundle destinations. |
| Profiles rejects otherwise valid DP declarations at the common `Succeed` boundary with `profile.v2.plan.retired-experience` and no artifact: `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs:131`; contract at `docs/contracts/composition-profile-v2.md:151`. Earlier validation failures remain earlier failures. | Extend that existing boundary to General Replace. No `V2PlanCompiled`, `V2RuntimeExecutable` or usable plan may escape, including externally supplied historical profile declarations. Keep parsing/versioned schemas and shared clone lowering. Do not duplicate compiler retirement policy in the Domain engine. |
| No product DP capability or dynamic route remains; surviving routes are asserted: `tests/NvtFwCombiner.Bootstrap.Tests/DpRuntimeRetirementTests.cs:18`. No DP registry/runtime symbol is allowed: `tests/NvtFwCombiner.Architecture.Tests/RetirementBoundaryTests.DpReplaceRetirement.cs:24`. The DATA terminalContract deleted independent DP bundles and preserved all surviving bytes and 79 fingerprints. | Remove the registration, dynamic route, policy row and active evidence membership together. Physical candidate/map removal needs the firmware-owner choice in section 6 because this bundle/family also serves CtrlRAM. Option A keeps their bytes; option B changes eight survivor fingerprints and needs explicit evidence. Change the presence assertion at `tests/NvtFwCombiner.Bootstrap.Tests/DpRuntimeRetirementTests.cs:27` to absence; retain Merge/CtrlRAM controls. |
| Saved Rule v1 is already refused with `SchemaVersionUnsupported`: `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleSchemaVersionGate.cs:21`. V2 inspection admits only General Merge/Replace pairs: `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2Inspector.cs:30`. No DP-specific Saved Rule retirement diagnostic was found in the inspected path. | Add a narrow retired-experience classification for recognizable General Replace Saved Rule v1/v2 documents. It precedes missing NT51926 Parent/route errors on Replace and inspection paths and offers no runnable migration. Proposed typed issue: `saved-rule.retired-experience`; render `Customized Replace is retired` and return 64 to match DP command retirement. Failed checks currently return 1 at `src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.V2.cs:18`; the retirement-specific 64 is new compatibility behavior, not existing DP Saved Rule behavior. The Merge `--rule` case remains question 3. |
| Architecture checks forbid the old adapter, port, authoring owner, accepted plan, compiler/runner and session symbols: `tests/NvtFwCombiner.Architecture.Tests/RetirementBoundaryTests.DpReplaceRetirement.cs:9` and `:20`. The check currently expects exactly one compiler retirement diagnostic occurrence at line 33. | Add a General Replace retirement boundary with an explicit allowlist for historical identifiers, refusal and report shapes. Forbid registration, authoring methods, runtime dispatch, session, Home command and exclusive row/panel. Update the DP count assertion to protect one shared refusal site covering both experiences, rather than permitting duplicated paths. |

Saved files are never automatically rewritten, deleted or converted to General Merge. Historical report reading remains
required by allocation and the existing report contract, not an execution exception.

Shared-definition consequences (step 4, firmware-owner decision before implementation):

- The DP precedent is `docs/governance/change-records/DP-REPLACE-RETIREMENT-DATA-110-01.json`, `terminalContract`.
  It deleted five independent bundles, preserving all 24 surviving bundles and all 79 route fingerprints.
  Decision 230 says "as for DP Replace"; preserving survivor definitions is therefore the starting point to examine first.
- Removing the General Replace region set at
  `profiles/built-in/nt51926-ctrlram-replace-candidate/families/nt51926-ctrlram-replace.json:523-546`
  and map at `:698-715` changes the family hash carried by five surviving CtrlRAM profiles, for example
  `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-runtime-single.json:20`.
- Removing the bundle entry at `profiles/built-in/nt51926-ctrlram-replace-candidate/profile-bundle.json:38`
  changes the bundle content hash pinned by `profiles/built-in/package-trust-index.json:187`.
  `src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs:389` binds that hash.
  All eight supported NT51926 CtrlRAM route fingerprints would change: 24 decision pins across eight policy rows
  starting at `docs/contracts/canonical-capability-policy-v1.json:563`, plus eight route-evidence pins at
  `testdata/golden/canonical/manifest.json:332-410`, require re-pinning under option B.
  `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:15` also pins the bundle hash;
  it is a dependency outside the original 220 matching files and must join step 4 if B is selected.
- Option A removes only the General Replace trust-index registration, policy row and manifest route-evidence row
  from active data; runtime registration/route removal and compiler refusal still apply. Bundle/family/profile bytes
  stay. Whether materializer and catalog accept the unregistered profile inside that bundle is unconfirmed,
  to be proven before choosing A. Option B rewrites definitions and re-pins eight routes, a deviation from DP;
  firmware-owner evidence must compare complete before/after outputs and exact write ranges for all eight routes.
  Neither option authorizes changes to survivor firmware bytes, ranges, CRC/header behavior, padding or naming.

Step 4 also shrinks active workflow sets from five to four, retaining `general-merge`, as DP retirement did:
`src/NvtFwCombiner.Infrastructure/Capabilities/BuiltInCanonicalCapabilityPolicy.cs:66-67`,
`docs/contracts/profile-bundle-package-trust-index-v1.schema.json:67` and the materializer whitelist at
`eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleTrustIndex.tasks:388`.
Synchronize that task's schema SHA at `:95` and schemaVersion guard at `:220` with the reviewed schema contract.
`scripts/release_source_pins.py:11-24` owns derived policy/trust-index SHA pins in `scripts/package.ps1`,
`scripts/smoke-release.ps1` and `tests/scripts/test_release_package_policy.py`; their step 4 changes need
release-owner R3 review for packaging and release smoke, alongside the firmware/contract roles.

## 5. Proposed steps

Eight changes, in order. Each includes the smallest related test updates so no intermediate change relies on dead tests.
"Before" means coverage must exist before removing the behavior; "after" means required acceptance for that change.
These are future gates, not tests run for this inventory.
Keep compiler artifact refusal and route removal in step 4 together: rejecting a still-registered profile could
break catalog loading before publication is reconciled. Earlier steps refuse external commands/rules first.

Path floors come from `docs/governance/authority-policy.json:14-40` and
`docs/governance/authority-policy.json:42-48`. `profiles/**`, `docs/contracts/**` and the Application/Infrastructure
`Composition/**` paths are R3. Profiles code, Capabilities, testdata and the Replace Execution partial are also R3.
R3 requires independent review/evidence and owner approval of the last push naming `firmware-owner`;
contracts also require `release-owner`; packaging/smoke scripts take that role under policy `:97-117`.
No approval, commit or integration is requested by this document.

| Step / single reviewable change | Write set and risk | Tests required before -> after | Must not touch |
| --- | --- | --- | --- |
| 1. Protect surviving behavior and detach historical report fixtures from live Replace execution | `tests/NvtFwCombiner.Application.Tests/`, `tests/NvtFwCombiner.Bootstrap.Tests/`, `tests/NvtFwCombiner.UiSmoke.Tests/`, `tests/NvtFwCombiner.TestSupport/`; R1. Use embedded synthetic JSON/test objects, not new testdata files. Wait for bounded-identity integration before rewriting `GeneralSelectedFileSessionLifecycleTests.cs`. | First move the three localization tests at `ShellViewModelTests.GeneralReplace.cs:14`, `:22`, `:37` to a surviving partial. Before: General Merge CLI/session/admission and CtrlRAM clone/delivery tests; report HexDiff/performance baseline. After: generic assertions run through survivors; old diagnostic reports, unknown fields and Raw/export work without live Replace preparation. Remove the live-helper assertion at `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.ReportHexDiff.cs:17`; preserve shared live memory projection coverage. | Production, private BINs, expected Golden bytes/hashes, parser acceptance and report wire meaning. |
| 2. Establish the command retirement refusal | `src/NvtFwCombiner.Cli/`, related `tests/NvtFwCombiner.Bootstrap.Tests/` and `tests/NvtFwCombiner.Architecture.Tests/`; R1 for these paths. | Before: DP early-refusal/no-side-effect tests and surviving CLI controls. After: `general-replace` returns retirement/64 with no input/tool/output activity; retired syntax is absent from help; DP remains retired; CtrlRAM and General Merge CLI behavior remains. Compiler refusal is coupled to route removal in step 4. | Domain engine semantics, shared reference cloning, processor ranges, valid surviving schemas and bytes. |
| 3. Make saved NT51926 and other General Replace rules refuse explicitly | `src/NvtFwCombiner.Infrastructure/Composition/`, `src/NvtFwCombiner.Cli/`, `docs/contracts/saved-composition-rule-v2*`, related Bootstrap/ProfileContract tests; R3 (Composition and contracts; firmware-owner + release-owner). | Before: synthetic v1/v2 NT51926 documents, missing Parent, malformed JSON, General Merge saved-rule admission. After: recognizable retired rules report retirement for `saved-rule validate`, `saved-rule mappings` and Replace `--rule`, produce no runnable fragments or output, remain unchanged on disk; unrelated invalid documents keep existing errors; Merge rules still load. Apply owner question 2 to the old mapping listing and question 3 to Merge with an old Replace rule. | Automatic rule conversion, trust widening, Parent hashes, slot/range policy, new rule-authoring UI and Table 2 CLI helpers/range codec. |
| 4. Refuse profile artifacts and remove active route/profile publication atomically | `profiles/built-in/`, `src/NvtFwCombiner.Profiles/V2/`, Infrastructure `Composition/` and `Capabilities/`, Application `Capabilities/`, `docs/contracts/canonical-capability-policy-v1*`, `docs/contracts/profile-bundle-package-trust-index-v1*`, `testdata/golden/canonical/`; add `scripts/package.ps1`, `scripts/smoke-release.ps1`, `tests/scripts/test_release_package_policy.py` for derived SHA pins, `eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleTrustIndex.tasks` for schema SHA/version/workflow guards, and `BuiltInCanonicalCapabilityPolicy.cs` for the active workflow set and policy SHA. Include Architecture.Tests, UiSmoke.Tests, Infrastructure.Tests, ProfileContract.Tests, Bootstrap.Tests and catalog probes; include `CtrlRamV2PlanClosureProfileTests.cs` if B changes its pin. R3 (firmware-owner + release-owner; packaging/smoke require release-owner). | Before: owner question 1 resolved, A materializer/catalog acceptance proven if chosen; exact route inventory, trust/hash closure and synthetic valid General profile compilation with survivor controls. After: compiler refuses General Replace artifacts; no active route/registration/selectable mode; active workflow sets shrink from five to four. A preserves all bundle/family/profile bytes and survivor fingerprints; B closes changed hashes and re-pins eight routes, 24 decisions, eight manifest pins and the plan-closure test with full before/after output and write-range evidence. Rewrite Architecture direct-file/registration assertions and the live NT51926 UiSmoke test in this step. Recheck General Merge and affected CtrlRAM routes. | Whole CtrlRAM bundle/family deletion, survivor firmware bytes/ranges/CRC/header/padding/naming, support/evidence promotion, expected Golden output bytes/hashes and dated evidence requests. |
| 5. Remove Presentation entry, state, panel and visible residue | `src/NvtFwCombiner.Presentation.Avalonia/Resources/`, `Views/`, `ViewModels/`, converters and related UiSmoke/Architecture tests; R3 because `ReplacePresentationViewModel.Execution.cs` is an explicitly classified path (firmware-owner). | First move `WindowPublication` from `ReplacePresentationViewModel.General.cs` to a surviving partial before removing General Replace members. Before: R12 residue checklist and survivor navigation/inspection/cancellation tests. After: no General Replace Home command, hidden panel, mapping row, session or selectable mode; NT51926 IC Details/support matrix no longer advertise it. CtrlRAM publication/selection and General Merge row/inspection remain; historical report labels still display. | General Merge reopen policy, shared row/range editor, CtrlRAM version authoring, the three moved whole-shell localization tests and unrelated UI findings. |
| 6. Delete exclusive runtime/admission members and reconcile shared owners | `src/NvtFwCombiner.Application/Authoring/`, `Composition/`, `Capabilities/`, `src/NvtFwCombiner.Infrastructure/Composition/`, `src/NvtFwCombiner.Cli/`, minimal Bootstrap wiring and related tests; R3 (Composition/Capabilities; firmware-owner). | Before: both named branch changes below integrated/reconciled, step 1 survivor coverage and explicit retirement gates. After: no General Replace preparation/planning/execution/bindings/projector or registration; Table 2 callers compile and retain behaviors; Merge freshness, cancellation, identity, source slices, admission, Preview/Build and loose/bundle delivery pass. Preserve report shapes and shared inline/virtual consumers. | `CompositionRunReport.DiagnosticPreview` and its parameter/summary, survivor report serialization, `SavedRuleCliSupport`, `CompleteReplaceRunAsync`, `AuthoringByteRangeCodec.TryParseStartAndLength`, branch fixes, inspection ports, shared `General*` members, optional inline-codec cleanup or a second executor/admission path. |
| 7. Pin the completed retirement boundary and close test-project residue | `tests/NvtFwCombiner.Architecture.Tests/`, Application/Infrastructure/ProfileContract/Bootstrap/UiSmoke tests, CatalogProbe/TestSupport/shared helpers; R1 for these paths, scoped architecture review retained. | Before: steps 2-6 behavioral gates. After: retirement architecture checks prohibit active symbols/routes/UI while allowing historical tokens; no positive retired-run fixture generators remain; old reports still read; shared coverage is accounted for per project. At the frozen integration boundary, run the canonical verifier and every applicable owner-certified surviving Golden output case against that source. | Production, gate weakening, replacing expected bytes to fit code, release publication and unrelated test cleanup. |
| 8. Synchronize current specification and contract wording | `SPEC.md`, affected `docs/contracts/`, existing current planning/entry documents under `docs/`; R3 for contracts (firmware-owner + release-owner); SPEC/current architecture documents R2; handoff prose R0. | Before: completed retirement and preserved report/rule compatibility evidence. After: contract/schema/consumer checks agree on retired execution, rule refusal and readable history; links resolve; normative references no longer promise General Replace authoring. Keep `general-replace` in SPEC's retirement explanation: `scripts/validate_repository.py:1338` requires that token. Reuse exact-source evidence where applicable; rerun affected checks for changed parsed inputs. | Dated decisions/assessments, General Merge 1.2.4 reopen or later authoring allocation, unrelated ADR/governance policy and release notes/publication. |

Step 4 test dependencies: `tests/NvtFwCombiner.Architecture.Tests/BootstrapCliBoundaryTests.BootstrapStructure.cs:242`
reads the candidate directly and `:425-434` asserts its live profile/planner;
`tests/NvtFwCombiner.Architecture.Tests/PackageTrustBoundaryTests.PackageTrustIndex.cs:269`, `:316`, `:345`
read Replace sources and assert registration/runtime boundaries;
`tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.GeneralReplace.cs:138`
executes the NT51926 route. Rewrite these with step 4 rather than waiting for steps 5-7.
The predecessor comparator is unaffected: `scripts/predecessor_validation.py:55`, `:376` admits only available
authoring with supported/candidate publication, General Replace is internal, and the ledger has no such route or
fingerprint pin (`docs/contracts/predecessor-comparison-v1.md:62`).

Branch interaction:

- `feature/1.2.4/bounded-identity-inspection` changes `ISelectedFileContentInspector.cs`,
  `FileContentSnapshotInspector.cs`, `GeneralSelectedFileInspection.cs` and
  `tests/NvtFwCombiner.Application.Tests/Authoring/GeneralSelectedFileSessionLifecycleTests.cs`.
  They stay KEEP-SHARED. Step 1's rewrite of that test file waits for branch integration; other preparation can proceed.
  Reconcile the final inspection API before step 6. The local branch diff confirms the lifecycle test also changes.
  Preserve bounded identity capture and its tests; do not restore the whole-file API observed at this HEAD.
- `feature/1.2.4/general-preparation-freshness` changes `AuthoringSessionState*.cs` and
  `GeneralAuthoringExperience.cs`. It owns the Merge freshness correction required before reopening.
  Step 6 must follow its integration or an explicitly coordinated reconciliation of the shared methods.
  Remove only Replace branches from the resulting source; preserve preparation leases/revision checks and adoption.
- Other branch file sets are owner-supplied context; only the bounded-identity lifecycle test diff was checked here.
  After integration refresh path/line evidence and counts; use one writer per shared file. No merge is authorized here.

Completion means retirement refusal, no active route/profile/UI/runtime execution, readable history and survivor gates.
Release readiness still requires independent review, R3 role approval and candidate Golden execution.

## 6. Decisions for the owner

Retirement, NT51926 execution refusal and readable historical reports are settled (decisions 229 and 230).
Old rule files stay unchanged on disk (decision 264).
Use "Customized Replace is retired" consistently because Customized Replace is the name users see.
Proposed retirement exit code is 64, matching DP's retired command; today's failed rule check returns 1.
This is a new retirement-specific result; unrelated invalid rules retain their existing errors and exit codes.
Questions 2 and 3 are answered by decision 264: option A in both (say that Customized Replace is retired and
identify the rule, without listing its ranges; the same message with 64 for a Customized Merge command).
Question 1 remains; this inventory chooses neither firmware option.

**1. Firmware-owner: keep shared bundle/family/profile bytes, or rewrite and re-pin eight surviving CtrlRAM routes?**

- **A — Keep bytes (conditional, not yet recommended):** keep bundle/family/profile files, including the retired declaration;
  remove only its trust-index registration, policy row and manifest row from active data. Remove runtime publication
  and refuse compiler artifacts. Surviving hashes/fingerprints stay unchanged; profile files become KEEP-SHARED.
  Materializer/catalog acceptance of the unregistered profile is **unconfirmed, to be proven before choosing** A.
- **B — Rewrite and re-pin:** remove the declaration, bundle entry, map and region set; update five surviving profile
  family bindings, bundle/index hashes, eight route fingerprints, 24 policy decisions, eight Golden route-evidence
  pins and the plan-closure test pin. Require firmware-owner complete before/after outputs and write-range comparisons.
  This deviates from DP's unchanged survivor definitions; expected firmware bytes/hashes remain unchanged.

Decision 230's "as for DP Replace" and the DP terminalContract support A if its acceptance is proven.
Failure to prove A does not select B automatically; the firmware owner must decide before step 4.
The proof of A is a separate feasibility check, started on 2026-10-02 and not finished at this record.
The split of `TrustedProfileBundleCatalogFactoryTests` (`docs/handoff/1.2.11/R33-05-candidates.md`) lands before
this retirement: the test files Table 1 lists under that class then carry their new class names.

**2. What should the command line show when it checks an old rule file?** Answered: A (decision 264).

The entries are `saved-rule validate|mappings` (`src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.cs:27`)
and a composition command's `--rule`; Presentation loads no Saved Rule file.

- **A — Refusal with identification (recommended):** show "Customized Replace is retired" and identify the rule
  when safely available. Do not list its source and destination ranges or offer commands to run them.
- **B — Refusal plus today's listing:** for `saved-rule mappings`, retain today's listing of the old source and
  destination ranges, marked unavailable for running, without the runnable `CLI mapping fragments` section.

Both choices leave the file unchanged and refuse execution. A follows the minimal DP refusal boundary;
B adds bounded read-only inspection in step 3, without a new Presentation viewer or rule conversion.

**3. Should `general-merge --rule <old Replace rule>` say retired, or keep today's parent mismatch?**
Answered: A (decision 264).

`src/NvtFwCombiner.Cli/MergeCliCommandHandler.SavedRules.cs:28` calls Merge admission; exact-parent mismatch is
reported at `src/NvtFwCombiner.Infrastructure/Composition/SavedCompositionRuleV2Admission.cs:70-75` for valid v2 rules.
- **A — Say it is retired (recommended):** a rule file that is recognizably an old Customized Replace rule gets
  the same message, "Customized Replace is retired", and exit code 64, as in question 2.
- **B — Keep today's message:** the command keeps reporting that the rule does not belong to Customized Merge,
  with exit code 1, and does not mention the retirement.

Neither choice retires General Merge or converts the old rule into a Merge rule.

## 7. Limits

- Static inspection of `d38f4e7c3`; no build, test, verifier, UI launch, network, commit, push or branch integration.
- Only this document is written. No private paths, firmware payloads, credentials or local saved rules were read.
- The inventory is bounded to the requested paths and their direct dependencies, not every historical mention in docs.
  No output parity or coverage percentage is claimed. Contract-only evidence is not an executed Golden comparison.
- Unconfirmed: materializer/catalog acceptance of an unregistered retired profile retained inside the shared bundle.
  Prove that before choosing option A; inline-source callers, virtual-locator consumers and report null behavior
  are resolved by the inspected source in Table 2 and its safeguards.
- The existing SPEC and positive tests still describe General Replace execution. That is expected retirement work,
  not proof R54 is already implemented. Earlier generic fail-closed cases are not the requested retirement refusal.
- The proposed typed Saved Rule retirement issue and its CLI exit 64 are new compatibility behavior requiring tests;
  malformed/unrecognizable documents continue through existing errors, without guessing workflow from a filename.
- R54 weight is still unassigned in allocation. This plan gives bounded changes, not a staffing or duration estimate.
