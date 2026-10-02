# R54: Customized Replace retirement inventory and step plan

## 1. Status and purpose

Status: Owner-decided retirement plan; 2026-10-02; refreshed against branch `feature/1.2.4/r54-step-1`.
Version: 1.2.4, R54; local plan and step 1 tests on `feature/1.2.4/r54-step-1`.
The original matching-file inventory was captured at `d38f4e7c3`; dispositions now follow decisions 264, 266 and 269.
Step 1 has two tests-only pull requests: 1a (this branch) retargets General Replace shared tests and report fixtures;
1b rehosts the decision 269 `fw141-cascade` definition tests on registered profiles.
PR 1a has local filtered-test evidence; commander full-project verification is pending. PR 1b and steps 2-8 remain future work.

Retire Customized Replace (`General Replace`, `general-replace`) while preserving Customized Merge.
Decision 230 fixes retirement of active code, route, profile and saved-rule execution, including NT51926 compatibility.
Decision 229 reopens only Customized Merge. This document records the accepted implementation boundaries;
it changes no production behavior.

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
Decision 269 adds two files outside those alternatives: the unused CtrlRAM profile and its Bootstrap test file.
The Architecture test and shared test helpers already have disposition rows; do not count them again.
Generated files are excluded by normal `rg` ignore rules. Counts describe the expanded inventory, not deletion volume.

Each classified file appears in one disposition count. For mixed files, KEEP-SHARED preserves the file and its surviving
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
| Profiles: retired bundled candidate | 1 | DELETE | `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-general-replace-dp-single-candidate.json:3`: delete the complete 0.1.0 candidate declaration in step 4 under decision 266 (B). Keep readable historical syntax and refuse externally supplied General Replace compilation at the shared compiler boundary. |
| Profiles: unused CtrlRAM definition (decision 269 addition) | 1 | DELETE | `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-cascade.json:3`: delete this unregistered 0.7.0 declaration and its bundle entry in step 4 with the Customized Replace candidate. It has no exclusive family/map/region set; the registered runtime profiles retain both FW 1.4.1 maps. |
| Profiles: shared bundle, family and trust index | 3 | KEEP-SHARED | Rewrite these shared files under decisions 266 (B) and 269, retaining the four registered CtrlRAM profiles. Remove bundle entries at `profiles/built-in/nt51926-ctrlram-replace-candidate/profile-bundle.json:31` and `:38`; only Customized Replace removes the family region set at `profiles/built-in/nt51926-ctrlram-replace-candidate/families/nt51926-ctrlram-replace.json:523`, map at `:698` and trust-index registration at `profiles/built-in/package-trust-index.json:230`. Section 4 orders the combined hash closure and one re-registration of eight survivors. |
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
| Tests: Application.Tests, shared | 13 | KEEP-SHARED | `tests/NvtFwCombiner.Application.Tests/Authoring/GeneralSelectedFileSessionLifecycleTests.cs:435`; `tests/NvtFwCombiner.Application.Tests/Authoring/MergeAuthoringSessionIsolationTests.cs:175`. General Merge's session/initializer coverage at `tests/NvtFwCombiner.Application.Tests/Authoring/MergeAuthoringSessionIsolationTests.cs:194` remains. `tests/NvtFwCombiner.Application.Tests/MemoryLayout/MemoryLayoutProjectorTests.GeneralMerge.cs:12` now protects the live shared projector through synthetic General Merge logical output, including file and inline source attribution. |
| Tests: Application.Tests, historical projections | 1 | KEEP-REPLAY | `tests/NvtFwCombiner.Application.Tests/Composition/GeneralReplaceDiagnosticPreviewTests.cs:99`: retain historical diagnostic interpretation checks without invoking retired preparation. |
| Tests: Infrastructure.Tests, shared | 1 | KEEP-SHARED | `tests/NvtFwCombiner.Infrastructure.Tests/Bundles/ProfileBundlePackageTrustIndexLoaderTests.cs:74`: update the removed registration assertion; CtrlRAM registrations remain covered at `tests/NvtFwCombiner.Infrastructure.Tests/Bundles/ProfileBundlePackageTrustIndexLoaderTests.cs:95`. |
| Tests: Infrastructure.Tests, historical schemas | 2 | KEEP-REPLAY | `tests/NvtFwCombiner.Infrastructure.Tests/Bundles/ProfileBundleSchemaValidatorTests.V26.cs:8`; `tests/NvtFwCombiner.Infrastructure.Tests/Bundles/ProfileBundleSchemaValidatorTests.V29.cs:8`: schema readability is distinct from compiler permission. |
| Tests: Infrastructure.Tests, exclusive selector | 1 | DELETE | `tests/NvtFwCombiner.Infrastructure.Tests/Composition/CanonicalDynamicRouteInventoryNumberChoiceTests.cs:44`: tests only the General Replace projection that leaves. |
| Tests: ProfileContract.Tests, shared | 4 | KEEP-SHARED | `tests/NvtFwCombiner.ProfileContract.Tests/CompositionProfileV2HeaderTests.cs:18`; `tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogReplaceLoweringTests.ReplaceLowering.cs:69`. Retain parser and generic reference-clone cases; CtrlRAM compilation remains at `tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.RuntimeReferenceReplaceProcessor.cs:71`. |
| Tests: ProfileContract.Tests, old executable candidate | 1 | REFUSE | `tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogRuntimeReferenceReplaceTests.RuntimeReferenceReplace.cs:16`: valid General Replace syntax must now produce the typed retirement failure and no artifact. Move reusable range/clone assertions to CtrlRAM before replacing positive admission. |
| Tests: Bootstrap.Tests, shared | 24 | KEEP-SHARED | `tests/NvtFwCombiner.Bootstrap.Tests/GeneralWorkflowTestSupport.cs:1`; `tests/NvtFwCombiner.Bootstrap.Tests/Fixtures/canonical-route-axes-v2.txt:1`. General Merge CLI coverage remains at `tests/NvtFwCombiner.Bootstrap.Tests/GeneralMergeCliCommandTests.cs:1`. Migrate report helpers and route snapshots; do not simply delete shared assertions. |
| Tests: Bootstrap.Tests, unused-definition dependency (decision 269 addition) | 1 | KEEP-SHARED | `tests/NvtFwCombiner.Bootstrap.Tests/Nt51926CtrlRamReplaceCandidateProfileTests.cs:580` loads the unused profile through private helpers. Retarget eight behavioral tests to registered `nt51926-ctrlram-replace-fw141-runtime-cascade` in step 1; delete only the two obsolete compiled-candidate/routed equivalence tests in step 4. The registered-profile Golden test at `:404` remains. The test disposition table below accounts for each member and helper. |
| Tests: Bootstrap.Tests, retirement and NT51926 rules | 9 | REFUSE | `tests/NvtFwCombiner.Bootstrap.Tests/Nt51926GeneralReplaceCandidateProfileTests.cs:19`; `tests/NvtFwCombiner.Bootstrap.Tests/ReplaceCliCommandTests.General.cs:1`; `tests/NvtFwCombiner.Bootstrap.Tests/ReplaceCliCommandTests.General.SchemaMigration.cs:37`; `tests/NvtFwCombiner.Bootstrap.Tests/SavedRuleCliCommandTests.Mappings.cs:10`; `tests/NvtFwCombiner.Bootstrap.Tests/WorkbenchGeneralReplacePatchTests.cs:18`. Replace positive execution and intermediate generic failures with explicit retirement/no-side-effect cases, including Saved Rule v1 and v2. |
| Tests: Architecture.Tests | 20 | KEEP-SHARED | `tests/NvtFwCombiner.Architecture.Tests/ApplicationBoundaryTests.AcceptedExecutionConvergence.cs:33`; `tests/NvtFwCombiner.Architecture.Tests/PresentationBoundaryTests.ShellSurface.cs:70`. Reverse obsolete presence assertions and add the General retirement boundary; General Merge's focused owner remains protected at `tests/NvtFwCombiner.Architecture.Tests/ApplicationBoundaryTests.GeneralWorkflowConvergence.cs:38`. Decision 269 also retargets `BootstrapCliBoundaryTests.BootstrapStructure.cs:192` to registered `nt51926-ctrlram-replace-fw141-runtime-single` in step 1; preserve the shared supported-profile assertion at `:290`. |
| Tests: UiSmoke.Tests, shared | 25 | KEEP-SHARED | `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.GeneralAdmission.cs:14`; `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.FirmwareInspection.NavigationMatrix.cs:119`. General Merge cancellation/admission at `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.GeneralAdmission.cs:223` remains; move shared navigation, lifecycle and output coverage to survivors. |
| Tests: UiSmoke.Tests, reports | 4 | KEEP-REPLAY | `tests/NvtFwCombiner.UiSmoke.Tests/ReportJsonSamples.cs:50`; `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.ReportHexDiff.cs:17`; `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.ReportPerformance.cs:13`; `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.ReportPerformanceObservations.cs:14`. Keep old-report tests; step 1 now uses synthetic historical report objects/replay byte planes and embedded diagnostic JSON, preserving Raw/export and unknown fields without live Replace preparation. Commander execution is pending. |
| Tests: UiSmoke.Tests, mixed authoring and localization | 1 | KEEP-SHARED | `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.Localization.cs` owns the three whole-shell localization tests moved first in step 1. Assertions remain. Only Replace authoring tests leave; the live NT51926 test at `tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.GeneralReplace.cs:98` changes with route removal in step 4. |
| Tests: TestSupport | 1 | KEEP-SHARED | `tests/NvtFwCombiner.TestSupport/RuntimeReferenceReplaceTestDocuments.cs:69`: retain synthetic documents for rejection and shared lowering; CtrlRAM caller at `tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.RuntimeReferenceReplaceProcessor.cs:75`. |
| Tests: CatalogProbe | 2 | KEEP-SHARED | `tests/NvtFwCombiner.CatalogProbe/CompilationScenarios.cs:60`; `tests/NvtFwCombiner.CatalogProbe/CatalogEvidence.cs:195`: remove General registration enumeration and success expectations; General Merge scenario remains at `tests/NvtFwCombiner.CatalogProbe/CompilationScenarios.cs:54`. |
| Tests: Shared helpers | 1 | KEEP-SHARED | `tests/Shared/TrustedProfileBundleCatalogTestExtensions.cs:58`: retain the generic compile helper; CtrlRAM request caller at `tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.RuntimeReferenceReplaceProcessor.cs:84`. |
| Tests: GoldenRegression.Tests and ReadyProbe | 0 | KEEP-SHARED | No exact matches. This is not proof of unaffected execution or a Golden pass; applicable surviving Golden cases remain required. |
| SPEC and current handoff documents | 4 | KEEP-SHARED | `SPEC.md:249` still advertises executable General Replace and must change during implementation; shared General Merge authoring remains at `SPEC.md:584`. Preserve decisions at `docs/handoff/1.2.x.md:325`, allocation at `docs/handoff/1.1.14/1.2.x-allocation.md:203`, and the remaining-workflow inventory at `docs/handoff/1.1.14/1.2.x-inventory.md:535`. |
| SPEC and documents: dated residue assessment | 1 | KEEP-REPLAY | `docs/handoff/1.2.1/R12-02.md:190`: preserve its dated findings and use them as the removal checklist; R39's related historical boundary is at `docs/handoff/1.2.1/R39.md:39` (zero counted matches). |
| Test data: manifests | 1 | KEEP-SHARED | `testdata/golden/canonical/manifest.json:427` is contract-only, not a certified output case. Remove its active retired-route entry with policy/registration removal; General Merge evidence at `testdata/golden/canonical/manifest.json:420` and Standard Merge Golden evidence at `:434` remain. Do not alter surviving expected output hashes or bytes; decision 266 permits only the reviewed route-evidence re-pins described in section 4. |
| Test data: dated owner evidence request | 1 | KEEP-REPLAY | `testdata/golden/owner-handoff/0718-missing-owner-evidence/README_請先看.md:26` is a dated General Replace migration evidence request. Preserve it as history; `testdata/**` is R3 and this file is outside the implementation write set. |

Original inventory: 220 unique matching files; decision 269 adds two files, for 222 unique classified files.
Combined dispositions: DELETE 10; KEEP-SHARED 171; KEEP-REPLAY 25; REFUSE 16.
The four requested manifests were searched; only the canonical manifest names General Replace.
Test data has two matching files including the dated request above. Disposition counts apply decisions 266 (B)
and 269 to the original inventory plus the two named additions; step 1 moves are not a new census.
Additional hash/pin/materializer dependencies below are outside this matching-file count.
These counts include tests and documents, and do not mean ten complete production components or 16 runtime entries.

### Decision 269: unused definition and test dispositions

Static evidence on `feature/1.2.4/r54-step-1` confirms the decision's premise. Paths in the first table are relative to
`profiles/built-in/nt51926-ctrlram-replace-candidate/` unless another repository-relative path is given.

| Fact | Evidence and consequence |
| --- | --- |
| Declaration and bundle membership | `profiles/nt51926-ctrlram-replace-fw141-cascade.json:3-4` declares profile 0.7.0; `profile-bundle.json:31-35` admits its entry. Membership is not runtime registration. Delete both in step 4. |
| Family/map binding is shared | The unused profile binds the family at `profiles/nt51926-ctrlram-replace-fw141-cascade.json:20-25`. Registered `profiles/nt51926-ctrlram-replace-fw141-runtime-single.json:18-23` and `profiles/nt51926-ctrlram-replace-fw141-runtime-cascade.json:18-23` bind the same family and two FW 1.4.1 maps. No family binding is exclusive to the unused definition. |
| No exclusive map or region set | `families/nt51926-ctrlram-replace.json:622-650` maps TP work and full Flash to shared `nt51926-ctrlram-fw141-tp-work` and `nt51926-full-flash-tail`. Preserve those maps/sets and their metadata; the unused definition adds no family deletion. Only Customized Replace's map/set leave. |
| No production route or registration | `profiles/built-in/package-trust-index.json:192-234` registers four runtime CtrlRAM profiles and the General Replace candidate, never the unused profile. `src/NvtFwCombiner.Infrastructure/Composition/CtrlRamV2RouteRegistry.cs:58-70` derives CtrlRAM routes only from those registrations. Exact-ID search of `src/` finds no reference to the unused profile. |
| No policy or Golden membership | `docs/contracts/canonical-capability-policy-v1.json:557-787` and `testdata/golden/canonical/manifest.json:332-410` name the eight surviving CtrlRAM routes; neither file names the unused profile. Exact-ID search across profiles, contracts and testdata finds no other route, policy, trust-index registration or Golden row for it. |

All direct test references are in two files. The Bootstrap file is newly counted; the Architecture file was already
KEEP-SHARED. In the following table, Bootstrap members are in
`tests/NvtFwCombiner.Bootstrap.Tests/Nt51926CtrlRamReplaceCandidateProfileTests.cs`; line numbers refer to that file.
Delivery: PR 1a carries General Replace retargeting and the scoped Architecture supported-profile read.
The Bootstrap rows below that carry "step 1" for decision 269 are delivered as a second step 1 pull request (1b);
a first attempt showed that five of the tests assert properties of the
fixed-slot definition itself (one processor operation, its fingerprints, `map-capacity-unavailable`, its staged
source), so they need the re-expression described in their rows, not a change of the profile name.
Every retargeted Bootstrap test uses registered `nt51926-ctrlram-replace-fw141-runtime-cascade` 0.4.0, with its
runtime-reference request and explicit topology. Changing the profile ID alone cannot retarget the old `Compile` call.
Use existing runtime compilation/route helpers; preserve the tested byte, range, normalization and refusal purposes.
Keep the old fixed-slot loader isolated for the two comparator tests until step 4; do not make those tests compare
one runtime compilation with itself during step 1. Their old loader leaves with them after survivor coverage exists.

| Project / test or helper | Purpose and disposition / step |
| --- | --- |
| Bootstrap.Tests: `CandidateProfileCompilesTheLegacyCascadeStagingAndWriteAuthority` (`:30`) | KEEP-SHARED, step 1: protect the registered cascade map/metadata, legacy command plan and exact read/write authority through runtime mappings/postbuild compilation. Fixed-slot staging assertions belong to the removed declaration; express survivor staging through the existing runtime request/adapter. |
| Bootstrap.Tests: `CandidateProcessorReceivesAndReturnsOnlyTheExactTpWorkImageAsync` (`:112`) | KEEP-SHARED, step 1: retain exact TP processor scope, reference clone and returned bytes with the registered cascade profile. |
| Bootstrap.Tests: `CandidateFullFlashStagesTpPrefixAndPreservesContainerTailAsync` (`:138`) | KEEP-SHARED, step 1: retain full-Flash clone, TP-only processor scope and unchanged container tail with the registered cascade profile. |
| Bootstrap.Tests: `CandidateFingerprintsAreExactAndRepeatable` (`:173`) | KEEP-SHARED, step 1: retain exact/repeatable resolution and compilation identity checks for the registered cascade profile, using its actual compiler outputs. Re-pin these identity vectors again after the combined step 4 closure; never retain the removed profile's constants. |
| Bootstrap.Tests: `CandidateRejectsEveryUndeclaredReferenceLength` (`:197`) | KEEP-SHARED, step 1: retain rejection of the four undeclared TP/full-Flash lengths with the registered cascade profile. |
| Bootstrap.Tests: `CandidatePlanTruncatesOnlyCtrlRamInputsBeforeHostStagingAsync` (`:210`) | KEEP-SHARED, step 1: retain CtrlRAM-only oversize truncation, issue and immutable input assertions through the registered cascade request/adapter and host staging. |
| Bootstrap.Tests: `RoutedV2MatchesCompiledCandidateForOwnerApprovedSelfReplacementAsync` (`:244`) | DELETE member, step 4: equality between the withdrawn fixed-slot definition and the registered route exists only for that definition. Before deletion, PR 1b rehosts its full-Flash Golden comparison, approved 16 CRC-byte bound and input/tail immutability assertions on the registered cascade route as `RegisteredCascadeFullFlashMatchesOwnerApprovedSelfReplacementGoldenAsync`. Step 4 repoints the Golden references described below before deleting this member. |
| Bootstrap.Tests: `RoutedTpBaseMatchesCompiledCandidateForOwnerApprovedSelfReplacementAsync` (`:367`) | DELETE member, step 4: the same two-definition equivalence for TP work becomes obsolete. Before deletion, step 1 retains the registered TP route's Build and immutable-input assertions; decision 266 still requires complete trunk/branch comparisons for this route. |
| Bootstrap.Tests: `CandidateMatchesArchivedTpBaseLegacyCombinerGoldenForSelectiveVnReplacementAsync` (`:374`) | KEEP-SHARED, step 1: rehost selective-VN complete archived-output and immutable-source coverage on the registered cascade profile; consolidate with the existing registered-profile test at `:404` without weakening expected bytes. |
| Bootstrap.Tests: `CandidateCompilationRejectsMissingOrAmbiguousNvtBackupMarker` (`:438`) | KEEP-SHARED, step 1: retain missing/ambiguous marker refusal before compilation through the registered cascade profile. |
| Bootstrap.Tests: `RuntimeReferenceCandidateMatchesArchivedTpBaseLegacyCombinerGoldenAsync` (`:404`) | KEEP-SHARED: already compiles the registered cascade profile at `:508-540`; preserve its archived Golden assertions and use it for the selective-VN consolidation. It does not depend on the removed definition. |
| Bootstrap.Tests: private helpers (`:249`, `:460`, `:547`, `:565`, `:574`, `:588`) | `CompileCandidateResult` names/loads the unused ID at `:580`; its compilation, execution and input helpers serve the tests above. Step 1 adapts the surviving request/input helpers. Delete the obsolete two-definition comparator and remaining fixed-slot-only helpers with their callers in step 4; keep the registered execution and Golden readers at `:469`, `:508`, `:616`, `:630`. |
| Architecture.Tests: `tests/NvtFwCombiner.Architecture.Tests/BootstrapCliBoundaryTests.BootstrapStructure.cs:177` | KEEP-SHARED, step 1: `CtrlRamReplaceV2RuntimeRoutesStayPreciselyScoped` reads the unused file at `:192` and includes it in supported-profile checks at `:245`, `:290`. Retarget that read/array entry to registered `nt51926-ctrlram-replace-fw141-runtime-single`; the shared runtime/support boundary survives. |
| Shared test support: `tests/Shared/TrustedProfileBundleCatalogTestExtensions.cs:26-44` | KEEP-SHARED: generic compile forwarding used by the Bootstrap helper; it names no unused ID. The retargeted tests use the existing runtime-reference API instead. Preserve forwarding for other profiles; no definition-only support file is deleted. |
| CatalogProbe: `tests/NvtFwCombiner.CatalogProbe/CatalogEvidence.cs:73-95`, `:166-183` | KEEP-SHARED: bulk evidence loading/enumeration includes this bundled declaration today, without a profile-specific assertion. It naturally captures the final four registered profiles after step 4; retain catalog/source equivalence coverage. `CompilationScenarios.cs:57-63` targets General Replace, not the unused CtrlRAM definition, and follows the existing General Replace disposition. |

Step 4 Golden-reference migration (plan only; PR 1a changes no testdata): PR 1b names the rehosted test
`tests/NvtFwCombiner.Bootstrap.Tests/Nt51926CtrlRamReplaceCandidateProfileTests.cs#RegisteredCascadeFullFlashMatchesOwnerApprovedSelfReplacementGoldenAsync`.
Before deleting `RoutedV2MatchesCompiledCandidateForOwnerApprovedSelfReplacementAsync`, step 4 repoints both
`testdata/golden/canonical/manifest.json`'s direct-golden row for
`route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw141-full-flash-256k` and
`testdata/golden/canonical/NT51926/ctrlram-replace/fw1.4.1/cascade-2/nt51926-fw141-cascade2-auto-prj-597-20260717/provenance/case.json`
(`testDisposition.evidenceRefs`) to that reference. `scripts/canonical_golden_validation.py` reads the referenced
test source; verify reference resolution and execute the rehosted test at step 4. No expected bytes, hashes, views
or approved bounds change. This reference-only testdata edit is R3 and requires the firmware owner's approval.
Under board decision 269, show the preserved comparison and any loss at that approval; the owner may revise the
unused-definition removal there.

PR 1a shared-test dispositions retained across later steps:

| Named test / data | Disposition / step |
| --- | --- |
| `AcceptedExecutionAdmissionTests.InvalidRequestStaysAnInvariantFailureWhenTheCatalogIsAlsoStale` and `BuildOutcomeTests.ExecutionInvariantFailureStillOpensFailureReport` (`ShellViewModelTests.ExecutionAdmission.cs`) | KEEP-SHARED, PR 1a: now use processor-backed CtrlRAM with missing exact typed readiness. Preserve invariant priority over stale catalog, zero destination preparation, and UI failure-report behavior after retirement. |
| `GeneralOutputConfirmationTests.GeneralReplaceConfirmationRetainsReferenceAndRejectsInlinePatch` | Retain its three live General Replace cases in PR 1a. Step 7 closes this named residue: retain accepted reference identity on surviving CtrlRAM confirmation coverage and replace the unsupported General Replace inline-patch admission case with explicit retirement refusal/no accepted session; remove the positive retired cases and Replace-only helper branch. Preserve `AcceptedGeneralFilesAppearInConfirmation` and its General Merge identity checks. When step 6 removes preparation, replace the retired cases in that same change so tests compile; step 7 verifies the final disposition. |
| `CliReportReceiptTests.BuildCommands` `general-replace` row, `CreateArguments` branch and `ReportNamingAutomaticCommittedOutputDoesNotOverwriteIt("general-replace", "nt51926-general-replace.bin")` | Retain live coverage in PR 1a; remove these rows/branch in step 2 with command retirement and its refusal/no-side-effect tests. All surviving receipt/report-alias invariants remain. |

No other test or test-support file names the unused ID or directly selects that profile. Shared bulk catalog loaders
remain shared consumers of the bundle; their purpose survives its final registered profiles. TestSupport has no
profile-specific loader for this definition. Retargeting belongs in step 1 so survivor tests keep passing before step 4;
step 7 confirms the completed boundary, rather than postponing these prerequisites.

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
| No product DP capability or dynamic route remains; surviving routes are asserted: `tests/NvtFwCombiner.Bootstrap.Tests/DpRuntimeRetirementTests.cs:18`. No DP registry/runtime symbol is allowed: `tests/NvtFwCombiner.Architecture.Tests/RetirementBoundaryTests.DpReplaceRetirement.cs:24`. The DATA terminalContract deleted independent DP bundles and preserved all surviving bytes and 79 fingerprints. | Remove the registration, dynamic route, policy row and active evidence membership together. Decision 266 (B) removes the candidate, bundle entry, family map and region set while decision 269 also removes the unused CtrlRAM definition. The four registered CtrlRAM profiles remain. Eight survivor fingerprints change and require the evidence package below. Change the presence assertion at `tests/NvtFwCombiner.Bootstrap.Tests/DpRuntimeRetirementTests.cs:27` to absence; retain Merge/CtrlRAM controls. |
| Saved Rule v1 is already refused with `SchemaVersionUnsupported`: `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleSchemaVersionGate.cs:21`. V2 inspection admits only General Merge/Replace pairs: `src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2Inspector.cs:30`. No DP-specific Saved Rule retirement diagnostic was found in the inspected path. | Add a narrow retired-experience classification for recognizable General Replace Saved Rule v1/v2 documents. It precedes missing NT51926 Parent/route errors on Replace and inspection paths and offers no runnable migration. Proposed typed issue: `saved-rule.retired-experience`; render `Customized Replace is retired` and return 64 to match DP command retirement. Failed checks currently return 1 at `src/NvtFwCombiner.Cli/SavedRuleCliCommandHandler.V2.cs:18`; the retirement-specific 64 is new compatibility behavior, not existing DP Saved Rule behavior. Decision 264 requires the same retirement message and exit 64 for Merge `--rule`. |
| Architecture checks forbid the old adapter, port, authoring owner, accepted plan, compiler/runner and session symbols: `tests/NvtFwCombiner.Architecture.Tests/RetirementBoundaryTests.DpReplaceRetirement.cs:9` and `:20`. The check currently expects exactly one compiler retirement diagnostic occurrence at line 32. | Add a General Replace retirement boundary with an explicit allowlist for historical identifiers, refusal and report shapes. Forbid registration, authoring methods, runtime dispatch, session, Home command and exclusive row/panel. Update the DP count assertion to protect one shared refusal site covering both experiences, rather than permitting duplicated paths. |

Saved files are never automatically rewritten, deleted or converted to General Merge. Historical report reading remains
required by allocation and the existing report contract, not an execution exception.

Shared-definition consequences (step 4, decision 266: option B; decision 269: combined removal):

The DP terminalContract deleted independent bundles and preserved survivor definitions. Decision 266 deliberately
changes that precedent for this shared NT51926 bundle. Decision 269 also removes the unused CtrlRAM definition
in this same step. Remove exactly these definitions:

| File under `profiles/built-in/nt51926-ctrlram-replace-candidate/` | Removal |
| --- | --- |
| `profiles/nt51926-general-replace-dp-single-candidate.json` | Delete the entire profile declaration `nt51926-general-replace-dp-single-candidate`. |
| `profile-bundle.json:38` | Delete entry `nt51926-general-replace-dp-single-candidate-profile`, including its path/schema/hash fields. |
| `profiles/nt51926-ctrlram-replace-fw141-cascade.json:3` | Delete the entire unused 0.7.0 profile declaration `nt51926-ctrlram-replace-fw141-cascade` (decision 269). |
| `profile-bundle.json:31` | Delete entry `nt51926-ctrlram-replace-fw141-cascade-profile`, including its path/schema/hash fields. Its shared maps, region sets and family remain. |
| `families/nt51926-ctrlram-replace.json:523-546` | Delete region set `nt51926-general-replace-full-flash-tail`, including `general-replace-full-flash-unmapped-gap` and `general-replace-full-flash-dp-code`. |
| `families/nt51926-ctrlram-replace.json:698-715` | Delete map `nt51926-general-replace-full-flash-256k`, including its region-set references. |

Also remove Customized Replace's trust-index registration, runtime registration, policy route with its three decisions,
and canonical manifest route-evidence row. The unused CtrlRAM definition has none of these registrations/rows to remove.
Preserve all surviving maps, region sets, profiles, route axes and support/evidence facts. Four surviving profiles bind
the changed family; do not remove the shared bundle/family or copy them into a new bundle. Re-register the eight routes
once from the final bundle after both removals; the route identity algorithm does not change.
Compared with decision 266 alone, the family cleanup is the same; one additional profile entry leaves and only four
profile family-hash bindings are rewritten. Schemas, surviving maps/regions, route IDs, 24 decision values and eight
Golden case/evidence memberships stay unchanged; only their reviewed identity pins change.

Close identities in the following dependency order. "No writer" means no checked-in command automatically updates
that reviewed source field; do not treat a validator as a re-pin tool or add a second identity algorithm.

| Order / value | Existing producer and destination | Command or remaining manual work |
| --- | --- | --- |
| 1. Family raw SHA-256 | `ProfileBundleFileSnapshot` hashes raw bytes; `ProfileBundleEntryArrayHasher` consumes the resulting entry identities. | `Get-FileHash -Algorithm SHA256 <family-file>` computes the digest; normalize it to lowercase. No source-field writer: update this family's bundle entry and `mapBinding.familyContentHash` in the four surviving profiles. |
| 2. Surviving profile and schema entry hashes | Raw-file SHA-256, checked by `ProfileBundleFileSnapshot` and the materializer. Four surviving profile files change after order 1; schemas remain unchanged unless separately reviewed. | `Get-FileHash -Algorithm SHA256 <entry-file>` for each final entry. No source-field writer: update the four profile entries in `profile-bundle.json`; verify all seven retained entries (two schemas, one family, four profiles). |
| 3. Bundle content hash | `ProfileBundleEntryArrayHasher.CalculateContentHash` owns `sha256-rfc8785-entry-array-v1`; the materializer/package validators independently check the same sorted entry-array projection. | No checked-in rehash/write CLI was found. Remove both profile entries (source lines 31/38); use this existing calculation with the final seven entries, not a newly invented hasher; write its result into `profile-bundle.json.contentHash` and the NT51926 trust-index entry. |
| 4. Trust index and build identity | Remove only the Customized Replace registration from `profiles/built-in/package-trust-index.json`; the unused CtrlRAM profile has none. Carry order 3's combined bundle hash. `LoadProfileBundleTrustIndex` computes index raw SHA and manifest-set SHA; `ComputeBuiltInProfileAdmissionIdentity` emits Infrastructure assembly metadata. | No registration/index source writer. After source closure, the normal build executes `eng/prebuilt-profile-catalog/NvtFwCombiner.ProfileAdmissionIdentity.targets`. The changed manifest also changes manifest-set SHA. |
| 5. Prebuilt pack | `GeneratePrebuiltProfileCatalog` invokes `PrebuiltProfileCatalogGenerator.Generate`, which admits the final index/bundles and writes the pack bound to their build identity. | `dotnet build src/NvtFwCombiner.Desktop/NvtFwCombiner.Desktop.csproj -c Release` builds Bootstrap, which imports `NvtFwCombiner.PrebuiltProfileCatalog.targets`, and executes the existing materializer and generator targets. The generator's CLI is `<materialized-bundle-root> <trust-index> <output.pack>`; consume its exact built assembly, not a stale pack. |
| 6. Eight computed route fingerprints | `CanonicalDynamicRouteInventory.CreateResolver` / `ResolveCtrlRam` calls `CapabilityDefinitionFingerprint.Compute`; bundle hash and report-metadata semantic bindings participate. | No checked-in re-pin/export command for unpinned identities. Capture the existing resolver's eight outputs in an evidence driver before policy admission; do not duplicate its algorithm or read old policy pins as new values. |
| 7. Eight policy rows / 24 decisions | `docs/contracts/canonical-capability-policy-v1.json`: each route row and its authoring/publication/evidence decisions carry the computed fingerprint. | No writer. Re-pin only those eight rows and 24 decisions once for both removals, retaining route/decision IDs, values and evidence references. `CatalogLoadScopedCtrlRamTests.EveryLoadExpandsOnceAndPreservesAllPolicyFingerprints` verifies fresh materialization against them. |
| 8. Eight Golden route-evidence rows | `testdata/golden/canonical/manifest.json:332-410`; `scripts/canonical_golden_validation.py` verifies route/fingerprint/evidence agreement. | No writer. Carry order 6's combined-removal values into the same eight rows. The only case-manifest exception is step 4's test-reference migration in section 2: repoint the full-Flash row's `testReference` and its `case.json` `testDisposition.evidenceRefs` to PR 1b's rehosted test. Do not change other case-manifest fields, committed inputs, expected output bytes/hashes, views or approved difference bounds. |
| 9. Plan-closure pin | `tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:15`, `Nt51926BundleHash`. | No writer. Carry order 3's hash; this dependency is mandatory, outside the original matching-file count. Also update the four NT51926 TP-route pins in `CtrlRamDirectTpGoldenExecutionTests` and the resolution/compilation fingerprint vectors in `Nt51926CtrlRamReplaceCandidateProfileTests.CandidateFingerprintsAreExactAndRepeatable`, using existing resolution/compiler outputs. These are identity pins, not Golden output hashes; no automatic writer exists. |
| 10. Reviewed source pins | `scripts/sync_derived.py` provider `reviewed-source-pins` calls `scripts/release_source_pins.py:38`, `plan_reviewed_source_pins`. It hashes final policy/index/release-allowlist raw bytes and projects them into four existing targets. | `python scripts/sync_derived.py --write --only reviewed-source-pins`, then `python scripts/sync_derived.py --only reviewed-source-pins`. Outputs: `BuiltInCanonicalCapabilityPolicy.cs`, `scripts/package.ps1`, `scripts/smoke-release.ps1`, `tests/scripts/test_release_package_policy.py`. The unchanged release allowlist stays unchanged. |

The four profiles are `nt51926-ctrlram-replace-fw141-runtime-single.json`,
`nt51926-ctrlram-replace-fw141-runtime-cascade.json`,
`nt51926-ctrlram-replace-fw200-runtime-single.json` and `nt51926-ctrlram-replace-fw200-runtime-cascade.json`.
All are under that bundle's `profiles/` directory; their family binding is `mapBinding.familyContentHash`.
Raw hashes include whitespace and line endings: finalize each source before computing dependent identities.
The final policy SHA must be projected before building policy-consuming hosts. Order 5's first build can generate
bundle/index admission and the pack; after orders 6-10, rebuild the final host and pack for all acceptance and
evidence runs.
`sync_derived.py` has no bundle/family/route/policy-decision/Golden-evidence/plan-closure writer provider.
The materializer's schema SHA/version/workflow guards below also have no `reviewed-source-pins` producer;
review and update their named bindings explicitly before the first build.

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
Steps 1-3 stay in order. Step 4 atomically removes definitions/publication and extends compiler refusal, then closes
hashes/pins for both removals and produces firmware-owner evidence before integration; steps 5-8 retain their order.
Deletion alone removes built-in lookup, not the compiler's authority to accept externally supplied profile content.
The common `Succeed` boundary in `V2CompositionPlanCompiler.ContractLowering.cs:131` currently refuses only
DP Replace. Extend this existing Profiles owner to General Replace: otherwise a valid externally admitted bundle
can still produce a General Replace artifact. Earlier validation failures remain earlier failures; recognizable valid
retired declarations reach the typed `profile.v2.plan.retired-experience` refusal with no artifact.
Do not add a Domain refusal or delete shared runtime reference-clone lowering used by CtrlRAM.
Keep refusal and built-in withdrawal together so no intermediate catalog loads a rejected active registration.

Path floors come from `docs/governance/authority-policy.json:14-40` and
`docs/governance/authority-policy.json:42-48`. `profiles/**`, `docs/contracts/**` and the Application/Infrastructure
`Composition/**` paths are R3. Profiles code, Capabilities, testdata and the Replace Execution partial are also R3.
R3 requires independent review/evidence and owner approval of the last push naming `firmware-owner`;
contracts also require `release-owner`; packaging/smoke scripts take that role under policy `:97-117`.
No approval, commit or integration is requested by this document.

| Step / single reviewable change | Write set and risk | Tests required before -> after | Must not touch |
| --- | --- | --- | --- |
| 1. Protect surviving behavior and detach historical report fixtures from live Replace execution | `tests/NvtFwCombiner.Application.Tests/`, `tests/NvtFwCombiner.Bootstrap.Tests/`, `tests/NvtFwCombiner.UiSmoke.Tests/`, `tests/NvtFwCombiner.TestSupport/`, plus the scoped Architecture test above; R1. Use embedded synthetic JSON/test objects, not new testdata files. Wait for bounded-identity integration before rewriting `GeneralSelectedFileSessionLifecycleTests.cs`. | Delivery is split: PR 1a (this branch) covers General Replace shared tests/report fixtures and the scoped Architecture read; PR 1b covers the eight `fw141-cascade` behavioral tests and rehosted comparator checks. First move `LocalizedShellBundlesAreCached`, `HexEditorLabelsAreLocalized` and `LocalizedShellBundlesPopulateEveryString` from `ShellViewModelTests.GeneralReplace.cs` to `ShellViewModelTests.Localization.cs` (locally done; execution pending). Before: General Merge CLI/session/admission and CtrlRAM clone/delivery tests; report HexDiff/performance baseline. After: retarget the eight Bootstrap behavioral tests/helpers and the Architecture supported-profile read listed under decision 269 before step 4; rehost the two obsolete comparator tests' surviving Golden/immutability checks first. Generic assertions run through survivors; old diagnostic reports, unknown fields and Raw/export work without live Replace preparation. Remove `ReportInspectionFixtureUsesProductionGeneralReplace` from `ShellViewModelTests.ReportHexDiff.cs` (locally done); preserve shared live memory projection coverage. | Production, private BINs, expected Golden bytes/hashes, parser acceptance and report wire meaning. |
| 2. Establish the command retirement refusal | `src/NvtFwCombiner.Cli/`, related `tests/NvtFwCombiner.Bootstrap.Tests/` and `tests/NvtFwCombiner.Architecture.Tests/`; R1 for these paths. | Before: DP early-refusal/no-side-effect tests and surviving CLI controls. After: `general-replace` returns retirement/64 with no input/tool/output activity; retired syntax is absent from help; DP remains retired; CtrlRAM and General Merge CLI behavior remains. Remove the live `general-replace` data rows, argument branch and automatic-name row from `CliReportReceiptTests` here, replacing their command-admission coverage with retirement/no-side-effect checks; retain all survivor receipt checks. Compiler refusal is coupled to route removal in step 4. | Domain engine semantics, shared reference cloning, processor ranges, valid surviving schemas and bytes. |
| 3. Make saved NT51926 and other General Replace rules refuse explicitly | `src/NvtFwCombiner.Infrastructure/Composition/`, `src/NvtFwCombiner.Cli/`, `docs/contracts/saved-composition-rule-v2*`, related Bootstrap/ProfileContract tests; R3 (Composition and contracts; firmware-owner + release-owner). | Before: synthetic v1/v2 NT51926 documents, missing Parent, malformed JSON, General Merge saved-rule admission. After: recognizable retired rules report retirement for `saved-rule validate`, `saved-rule mappings` and Replace `--rule`, produce no runnable fragments or output, remain unchanged on disk; unrelated invalid documents keep existing errors; Merge rules still load. Apply decision 264: identify safely available rule metadata without listing ranges; Merge with an old Replace rule uses the same retirement message and exit 64. | Automatic rule conversion, trust widening, Parent hashes, slot/range policy, new rule-authoring UI and Table 2 CLI helpers/range codec. |
| 4. Refuse profile artifacts and remove active route/profile publication atomically | `profiles/built-in/`, `src/NvtFwCombiner.Profiles/V2/`, Infrastructure `Composition/` and `Capabilities/`, Application `Capabilities/`, `docs/contracts/canonical-capability-policy-v1*`, `docs/contracts/profile-bundle-package-trust-index-v1*`, `testdata/golden/canonical/`; add `scripts/package.ps1`, `scripts/smoke-release.ps1`, `tests/scripts/test_release_package_policy.py` for derived SHA pins, `eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleTrustIndex.tasks` for schema SHA/version/workflow guards, and `BuiltInCanonicalCapabilityPolicy.cs` for the active workflow set and policy SHA. Include Architecture.Tests, UiSmoke.Tests, Infrastructure.Tests, ProfileContract.Tests, Bootstrap.Tests and catalog probes; include `CtrlRamV2PlanClosureProfileTests.cs` and the four NT51926 TP-route fingerprint pins in `CtrlRamDirectTpGoldenExecutionTests.cs` and candidate resolution/compilation pins unconditionally. R3 (firmware-owner + release-owner; packaging/smoke require release-owner). | Before: decisions 266 (B) and 269, step 1 retargeting complete, exact eight-route inventory, trunk Build outputs/reports before either removal from committed Golden inputs, trust/hash closure and synthetic valid external General profile compilation with survivor controls. After: compiler refuses General Replace artifacts; no active route/registration/selectable mode; active workflow sets shrink from five to four. B deletes the Customized Replace declaration/bundle entry/family map/region set; decision 269 deletes the unused CtrlRAM declaration/bundle entry and its two definition-only comparator tests/helpers after repointing the full-Flash Golden references to PR 1b's `RegisteredCascadeFullFlashMatchesOwnerApprovedSelfReplacementGoldenAsync` (section 2). The reference-only testdata change is R3 and requires firmware-owner approval; decision 269 permits the owner to revise the removal at that approval. Close section 4 identities for both removals and re-register eight routes once: 24 decisions, eight manifest rows, plan-closure and route-test pins, then reviewed source pins. Produce the firmware-owner evidence package below; any output or write-range difference stops this step. Rewrite Architecture direct-file/registration assertions and the live NT51926 UiSmoke test in this step. Recheck General Merge and affected CtrlRAM routes. | Whole CtrlRAM bundle/family deletion, survivor firmware bytes/ranges/CRC/header/padding/naming, support/evidence promotion, expected Golden output bytes/hashes and dated evidence requests. |
| 5. Remove Presentation entry, state, panel and visible residue | `src/NvtFwCombiner.Presentation.Avalonia/Resources/`, `Views/`, `ViewModels/`, converters and related UiSmoke/Architecture tests; R3 because `ReplacePresentationViewModel.Execution.cs` is an explicitly classified path (firmware-owner). | First move `WindowPublication` from `ReplacePresentationViewModel.General.cs` to a surviving partial before removing General Replace members. Before: R12 residue checklist and survivor navigation/inspection/cancellation tests. After: no General Replace Home command, hidden panel, mapping row, session or selectable mode; NT51926 IC Details/support matrix no longer advertise it. CtrlRAM publication/selection and General Merge row/inspection remain; historical report labels still display. | General Merge reopen policy, shared row/range editor, CtrlRAM version authoring, the three moved whole-shell localization tests and unrelated UI findings. |
| 6. Delete exclusive runtime/admission members and reconcile shared owners | `src/NvtFwCombiner.Application/Authoring/`, `Composition/`, `Capabilities/`, `src/NvtFwCombiner.Infrastructure/Composition/`, `src/NvtFwCombiner.Cli/`, minimal Bootstrap wiring and related tests; R3 (Composition/Capabilities; firmware-owner). | Before: both named branch changes below integrated/reconciled, step 1 survivor coverage and explicit retirement gates. After: no General Replace preparation/planning/execution/bindings/projector or registration; Table 2 callers compile and retain behaviors; Merge freshness, cancellation, identity, source slices, admission, Preview/Build and loose/bundle delivery pass. Preserve report shapes and shared inline/virtual consumers. | `CompositionRunReport.DiagnosticPreview` and its parameter/summary, survivor report serialization, `SavedRuleCliSupport`, `CompleteReplaceRunAsync`, `AuthoringByteRangeCodec.TryParseStartAndLength`, branch fixes, inspection ports, shared `General*` members, optional inline-codec cleanup or a second executor/admission path. |
| 7. Pin the completed retirement boundary and close test-project residue | `tests/NvtFwCombiner.Architecture.Tests/`, Application/Infrastructure/ProfileContract/Bootstrap/UiSmoke tests, CatalogProbe/TestSupport/shared helpers; R1 for these paths, scoped architecture review retained. | Before: steps 2-6 behavioral gates. After: confirm decision 269's step 1 retargeting and step 4 definition-only test deletion are complete; retirement architecture checks prohibit active symbols/routes/UI while allowing historical tokens; no positive retired-run fixture generators remain; old reports still read; shared coverage is accounted for per project. Apply the named `GeneralOutputConfirmationTests` disposition in section 2. At the frozen integration boundary, run the canonical verifier and every applicable owner-certified surviving Golden output case against that source. | Production, gate weakening, replacing expected bytes to fit code, release publication and unrelated test cleanup. |
| 8. Synchronize current specification and contract wording | `SPEC.md`, affected `docs/contracts/`, existing current planning/entry documents under `docs/`; R3 for contracts (firmware-owner + release-owner); SPEC/current architecture documents R2; handoff prose R0. | Before: completed retirement and preserved report/rule compatibility evidence. After: contract/schema/consumer checks agree on retired execution, rule refusal and readable history; links resolve; normative references no longer promise General Replace authoring. Keep `general-replace` in SPEC's retirement explanation: `scripts/validate_repository.py:1338` requires that token. Reuse exact-source evidence where applicable; rerun affected checks for changed parsed inputs. | Dated decisions/assessments, General Merge 1.2.4 reopen or later authoring allocation, unrelated ADR/governance policy and release notes/publication. |

Decision 269's Bootstrap test prerequisites are in step 1 PR 1b; its Architecture read is in PR 1a as listed above.
The two obsolete Bootstrap comparator tests and
fixed-slot-only helper residue leave with the unused definition in step 4. They cannot be deferred to step 7.
Step 4 test dependencies: `tests/NvtFwCombiner.Architecture.Tests/BootstrapCliBoundaryTests.BootstrapStructure.cs:242`
reads the candidate directly and `:425-434` asserts its live profile/planner;
`tests/NvtFwCombiner.Architecture.Tests/PackageTrustBoundaryTests.PackageTrustIndex.cs:269`, `:316`, `:345`
read Replace sources and assert registration/runtime boundaries;
`tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.GeneralReplace.cs:98`
executes the NT51926 route. Rewrite these with step 4 rather than waiting for steps 5-7.
Risk under B: stale raw/family/bundle/index/assembly/pack/policy pins fail admission before execution; copying old
route pins is not recomputation. Keep the evidence driver independent of policy acceptance until new resolver values
are captured. Unexpected survivor output or write authority blocks step 4 and must return to the firmware owner.
No input/output/CRC expectation may be weakened to accommodate a changed identity.

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

## Firmware-owner evidence for the re-pin

Decision 266 requires this package before the step 4 merge, at the exact reviewed branch source. Capture trunk
`1.2.x` before either removal (record its exact source SHA when capturing) and the final branch after both removals, using the same committed Golden inputs,
input hashes, processor configuration and environment. Decision 269 combines withdrawal of Customized Replace and
`nt51926-ctrlram-replace-fw141-cascade` at step 4; one registration of the eight routes covers both removals.
The route table and capture format stay unchanged. New values come from the final combined bundle through the existing
identity computation. For each of the eight NT51926 CtrlRAM routes:

- Retain both complete Build output files, their sizes/SHA-256, reports and an actual byte-for-byte comparison.
  Expected: the trunk and branch complete outputs are identical; equal hashes alone are not the comparison.
- Extract every run's write ranges from its report, retaining output address space, operation/stage/processor,
  sequence and half-open `[start, endExclusive)` range. Compare the complete ordered write authority and actual
  mutation ranges before/after. Expected: identical ranges and ordering; an empty trace is not evidence.
- Execute the case against its approved Golden contract on both sources. Retain executed case IDs, input and
  expected hashes, compared complete output or declared complete owner view, every observed difference and its
  approved bound. Golden-vs-output may retain the approved 16 CRC bytes; trunk-vs-branch permits zero differences.
- Supply the old/new identity table below, filling the new columns from the existing resolver and final bundle,
  not by copying a policy pin. Route axes/IDs remain unchanged; the fingerprint and bundle content hash change.

| Route ID (same before/after) | Old capability fingerprint | New capability fingerprint | Old bundle content hash | New bundle content hash |
| --- | --- | --- | --- | --- |
| `route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw141-full-flash-256k` | `ef39f5028f35d54f3fac6c352f761b33f462e468f52c171a668c0ca7da72037f` | Capture at step 4 | `241d2059770c4a88c2ec20e582652832e3ea3d0e51a07711a6827707e53b8cfa` | Capture at step 4 |
| `route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw141-tp-work-240k` | `9779b16acaf35f97803dabb15ea9453532972a03261b4aad64eebbf2f9c3abf7` | Capture at step 4 | `241d2059770c4a88c2ec20e582652832e3ea3d0e51a07711a6827707e53b8cfa` | Capture at step 4 |
| `route-7-nt51926-15-ctrlram-replace-4-1-ic-37-nt51926-ctrlram-fw200-full-flash-256k` | `a40614e3c02dbfc1d2b0e1a70cad141d3d7f939993802103532dcbfcc2e209fe` | Capture at step 4 | `241d2059770c4a88c2ec20e582652832e3ea3d0e51a07711a6827707e53b8cfa` | Capture at step 4 |
| `route-7-nt51926-15-ctrlram-replace-4-1-ic-34-nt51926-ctrlram-fw200-tp-work-240k` | `8130d6f57768093f35002cceed11fe70b98051bcf57d36a078b77714111c4dce` | Capture at step 4 | `241d2059770c4a88c2ec20e582652832e3ea3d0e51a07711a6827707e53b8cfa` | Capture at step 4 |
| `route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw141-full-flash-256k` | `42012edc1b7dfef590b1fbe6a96b7f5dfaa5c5a671382e16b8d466a82fc493e9` | Capture at step 4 | `241d2059770c4a88c2ec20e582652832e3ea3d0e51a07711a6827707e53b8cfa` | Capture at step 4 |
| `route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw141-tp-work-240k` | `153a1cde0f00d1acfb225c1340223e310f3f76f6e4b4d013cd9a2c4f4a39b331` | Capture at step 4 | `241d2059770c4a88c2ec20e582652832e3ea3d0e51a07711a6827707e53b8cfa` | Capture at step 4 |
| `route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-37-nt51926-ctrlram-fw200-full-flash-256k` | `51450d385ab7aff2c1573932a90cf77468cf84480805ce17ff903d171add1e23` | Capture at step 4 | `241d2059770c4a88c2ec20e582652832e3ea3d0e51a07711a6827707e53b8cfa` | Capture at step 4 |
| `route-7-nt51926-15-ctrlram-replace-9-2-plus-ic-34-nt51926-ctrlram-fw200-tp-work-240k` | `5e47f59eee9fb01e30576ae6995d2d99065a1634353de1a1b8ccb67ab3ff5afa` | Capture at step 4 | `241d2059770c4a88c2ec20e582652832e3ea3d0e51a07711a6827707e53b8cfa` | Capture at step 4 |

Existing Bootstrap execution coverage spans all eight routes across these four classes:
`Nt51926CtrlRamFw141SingleRouteTests`, `Nt51926CtrlRamFw200GoldenTests`,
`Nt51926CtrlRamReplaceCandidateProfileTests` and `CtrlRamDirectTpGoldenExecutionTests`.
The Common FW 1.4.1/2.0.0 single/cascade cases are respectively
`nt51926-fw141-single-auto-prj-747-20260717`, `nt51926-fw141-cascade2-auto-prj-597-20260717`,
`nt51926-fw200-single-auto-prj-597-20260718` and `nt51926-fw200-cascade3-auto-prj-597-20260718`.
The full-Flash runs build their reference through Standard Merge from the same committed case inputs;
the TP-work runs use the committed immutable TP input and the declared owner view. Keep both map extents.
Those tests already apply approved difference ranges (16 CRC bytes); they do not retain a trunk/branch pair or
compare those two complete outputs. Their successful assertions alone do not satisfy decision 266.

The commander must script an additional evidence driver around the existing Build helpers and report serializer:
run each exact route on both sources, preserve output/report pairs by route ID and source SHA, compare all bytes,
extract/compare the reported write authority and mutation ranges, and emit the old/new identity table and case
execution summary with approved bounds. Reuse `CtrlRamReplaceTestSupport`, `StandardMergeTestSupport` and
`CanonicalGoldenTestData`; do not implement a second planner, executor, CRC worker or fingerprint algorithm.
The driver is an evidence capture/comparison, not a producer of new expected Golden outputs or an automatic pin writer.
Run the four classes and the fresh fingerprint check after the final rebuild; execute all applicable certified cases
at the frozen boundary with `python scripts/verify.py --release-golden`, retaining the actual executed-case summary.
Before each build/test/verifier, load user-level `NFC_TEST_AREA_ROOT` and set `TEMP`, `TMP`, `TMPDIR` to its existing
`temp` child. Keep private outputs in that approved test area; do not add firmware BINs to Git.

**Stop condition:** any trunk/branch output-byte or write-range difference stops step 4 and goes to the firmware owner.
Missing/skipped route or required Golden evidence also blocks merge. Do not change inputs, expected bytes/hashes,
approved bounds or support/evidence status to make the comparison pass. Independent review and the owner's last-push
approval as `firmware-owner` (plus all other affected roles) remain required; decision 266 is not advance merge
approval.

## 6. Decisions for the owner

Retirement, NT51926 execution refusal and readable historical reports are settled (decisions 229 and 230).
Old rule files stay unchanged on disk (decision 264).
Use "Customized Replace is retired" consistently because Customized Replace is the name users see.
Decision 264 fixes exit 64 for a Customized Merge command given an old Replace rule.
Exit 64 for `saved-rule validate|mappings` and the `general-replace` command remains this plan's proposal,
matching DP's retired command; today's failed rule check returns 1. These proposed retirement-specific results
need implementation tests; unrelated invalid rules retain their existing errors and exit codes.
Question 1 is answered B by decision 266. Questions 2 and 3 are answered A by decision 264:
say Customized Replace is retired and identify the rule without listing ranges; use the same message and 64
for a Customized Merge command.
Decision 269 additionally settles bundle membership: one bundle holds the definitions of one feature, and only
in-use definitions. Remove the unused CtrlRAM definition with Customized Replace in step 4; re-register the eight
surviving routes once for both removals under decision 266's complete-output/write-range/old-new identity evidence.
How route identities are computed does not change. R62, after R54 step 4, will add automatic rejection of an
unregistered profile or a map unused by registered profiles; that check is not part of R54.
No owner question is reopened by this follow-up. The proposed CLI exit codes above and decision 269's removal remain subject to their stated implementation/approval gates.

**1. Firmware-owner: keep shared bundle/family/profile bytes, or rewrite and re-pin eight surviving CtrlRAM routes?**
Answered: B (decision 266): the definition leaves the shared bundle in this retirement and the eight routes are
registered again once, with complete before and after outputs and write ranges for the owner. The text below is
kept as the record of what was asked. Table 1, sections 4 and 7 and step 4 of section 5 are re-derived for B in
this revision. Decision 269 (owner, 2026-10-02) extends that accepted removal to the unused
`nt51926-ctrlram-replace-fw141-cascade` definition and its bundle entry. No additional map/region set is exclusive
to it. This is one final bundle/hash closure and one registration of the same eight routes, with decision 266's
unchanged evidence form and route identity computation; decisions 264 and 266 remain as recorded.

- **A — Keep bytes (conditional, not yet recommended):** keep bundle/family/profile files, including the retired
  declaration;
  remove only its trust-index registration, policy row and manifest row from active data. Remove runtime publication
  and refuse compiler artifacts. Surviving hashes/fingerprints stay unchanged; profile files become KEEP-SHARED.
  Materializer/catalog acceptance of the unregistered profile is **unconfirmed, to be proven before choosing** A.
- **B — Rewrite and re-pin:** remove the declaration, bundle entry, map and region set; update four surviving profile
  family bindings after the combined decision 269 removal, bundle/index hashes, eight route fingerprints,
  24 policy decisions, eight Golden route-evidence pins and the plan-closure test pin. Require firmware-owner complete
  before/after outputs and write-range comparisons.
  This deviates from DP's unchanged survivor definitions; expected firmware bytes/hashes remain unchanged.

The DP terminalContract was considered when A was asked; it is not the working assumption after decision 266.
The feasibility experiment for A ran on 2026-10-02 and showed A accepted by the code within its scope
(`R54-option-a-feasibility.md`, "Experiment result"); the owner chose B all the same.
The split of `TrustedProfileBundleCatalogFactoryTests` is merged at this source. Table 1 uses
`TrustedProfileBundleCatalogReplaceLoweringTests` and `TrustedProfileBundleCatalogRuntimeReferenceReplaceTests`;
`TrustedProfileBundleCatalogFactoryTests.RuntimeReferenceReplaceProcessor.cs` retains its existing class name.

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
       the same message, "Customized Replace is retired", and exit code 64, as fixed by decision 264.
- **B — Keep today's message:** the command keeps reporting that the rule does not belong to Customized Merge,
  with exit code 1, and does not mention the retirement.

Neither choice retires General Merge or converts the old rule into a Merge rule.

## 7. Limits

- Original inventory at `d38f4e7c3`; this follow-up inspects `feature/1.2.4/r54-step-1` and verifies its changed/restored tests locally by class filter.
  No full-project pass, Golden parity, UI launch, network, commit, push or branch integration is claimed.
- This review follow-up changes only PR 1a tests and this plan; it changes no production, profile, contract or testdata.
  Tests consume existing owner-approved Golden fixtures; no credentials or local saved rules were read.
- The inventory is bounded to the requested paths and their direct dependencies, not every historical mention in docs.
  No output parity or coverage percentage is claimed. Contract-only evidence is not an executed Golden comparison.
- Option A acceptance is recorded in `R54-option-a-feasibility.md`; A was not chosen. The B hash closure,
  rebuilt admission and before/after evidence remain step 4 gates; no new firmware parity is claimed here.
- The existing SPEC and positive tests still describe General Replace execution. That is expected retirement work,
  not proof R54 is already implemented. Earlier generic fail-closed cases are not the requested retirement refusal.
- The proposed typed Saved Rule retirement issue and its CLI exit 64 are new compatibility behavior requiring tests;
  malformed/unrecognizable documents continue through existing errors, without guessing workflow from a filename.
- R54 weight is still unassigned in allocation. This plan gives bounded changes, not a staffing or duration estimate.
