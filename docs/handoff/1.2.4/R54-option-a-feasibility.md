# R54 option A feasibility

Status: 2026-10-02; source commit `1f1be9bc7e0238a2cc7bc2e39f9026b051093855`; spike, not merged. The experiment ran
the same day (section "Experiment result" at the end). The firmware owner then chose option B (board decision
266): this document stays as the record of what option A needed and of the experiment; it is not the plan.
Everything from the "Recommendation" line to "Unconfirmed and limits" was written before the experiment and
before decision 266: its recommendation, the option A write set and its "not built, not run" statements are
superseded by the experiment result at the end and by that decision. Two test files it cites
(`TrustedProfileBundleCatalogFactoryTests.ReplaceLowering.cs`, `.RuntimeReferenceReplace.cs`) were renamed by
pull request #528.
Recommendation: **static inspection shows option A is feasible with bounded code/test changes; a build, freshly computed fingerprints and the Golden run must confirm it.** No bundle rewrite or survivor re-pinning is predicted, not yet proved by execution.
The retained declaration is admitted catalog content, not active route publication. Withdrawal alone does not prohibit direct compilation.
Scope: the shared NT51926 CtrlRAM admission/artifact boundary in plan section 6 question 1 and step 4, not all R54 removals.
Decision 229 reopens only General Merge; decision 230 retires General Replace including NT51926 compatibility (`docs/handoff/1.2.x.md:314`, `:325`).
Decision 230 explicitly includes the **profile** in retirement (`docs/handoff/1.2.x.md:333`). Option A keeps that profile's bytes in the shared bundle. This retention is a firmware-owner choice still requiring acceptance; it is not implied by decision 230. The data-only experiment below does not settle that choice or implement full retirement.
Source citations and removal positions below refer to the source commit, before the data-only spike.

## 1. What binds the eight routes

- Each family/profile/schema entry hashes **raw file bytes** with SHA-256, including whitespace/line endings (`src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleFileSnapshot.cs:146`).
- Bundle content hash uses `sha256-rfc8785-entry-array-v1`: sort by entryId, kind, path, schemaId, contentHash; project each object in contentHash/entryId/kind/path/schemaId order; compact UTF-8 JSON; SHA-256 (`src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleEntryArrayHasher.cs:21`, `:34`, `:45`).
- The bundle hashes the entire nine-entry array, including General Replace, not registrations or the trust-index file. Its content hash is `241d2059770c4a88c2ec20e582652832e3ea3d0e51a07711a6827707e53b8cfa` (`profiles/built-in/nt51926-ctrlram-replace-candidate/profile-bundle.json:6`, `:38`; `profiles/built-in/package-trust-index.json:187`).
- Five surviving profiles bind family raw SHA-256 `ee315aa3f713ea0a7ea748bae6f3c8ee9aa60cda7cdf38adeef05cf9943ced0f`; binding matches id, version and hash exactly (`profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-runtime-single.json:20`; `src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalogFactory.cs:279`).
- Route fingerprints include route axes, profile id/version, **bundle content hash**, allowed maps, compiler semantic id and sorted distinct semantic bindings. CtrlRAM adds processor, selector, postbuild-plan fingerprint, report-metadata identities/projections and optional memory context (`src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs:355`, `:389`, `:479`).
- Fingerprinting uses length-framed label/value fields, normalized sets, then SHA-256 of UTF-8 text (`src/NvtFwCombiner.Application/Capabilities/CapabilityDefinitionFingerprint.cs:64`, `:80`, `:112`). It does not hash policy/trust-index/Golden-root-manifest bytes.
- Each policy row and its three decisions pin the fingerprint; dynamic materialization compares computed identity with that pin (`docs/contracts/canonical-capability-policy-v1.json:563`; `src/NvtFwCombiner.Infrastructure/Capabilities/BuiltInCanonicalCapabilityPolicy.cs:139`; `src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalogSource.cs:127`).
- Eight Golden routeEvidence rows pin the same route/fingerprint pairs (`testdata/golden/canonical/manifest.json:332`, `:406`); the validator cross-checks policy evidence IDs, route IDs and fingerprints (`scripts/canonical_golden_validation.py:483`).
- **Plan statement confirmed, with an ordering qualification:** deleting the family map/region set changes the family hash; leaving its pins stale first fails admission. Properly propagating that change modifies five profiles and bundle identity. Deleting the General Replace bundle entry also changes bundle identity directly, hence all eight fingerprints after rehashing. The shared map/region locations are `profiles/built-in/nt51926-ctrlram-replace-candidate/families/nt51926-ctrlram-replace.json:523`, `:698`.

## 2. Reader-by-reader acceptance

Acceptance statements in this table describe static inspection; fresh materializer, JSON and prebuilt execution remain commander gates.

| Reader | Option A disposition and evidence |
| --- | --- |
| Materializer registration validation | **Accepts the reduced data.** Validates registration shapes/uniqueness, not a bijection between registrations and profile entries. It neither requires every profile registered nor joins every registration to an existing profile. That resolution occurs downstream. Workflow whitelist applies only to registrations (`eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleTrustIndex.tasks:354`, `:388`). |
| Materializer bundle validation/copy | **Accepts retained content.** Iterates every manifest entry and verifies raw hashes and canonical array hash, without inspecting a profile's workflow (`eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleTrustIndex.tasks:476`, `:515`, `:545`). Copies the indexed bundle's files including the retained candidate (`eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleMaterializer.targets:15`, `:55`). |
| Trust-index runtime loader | **Accepts reduced registrations.** Schema validation and unique registration keys have no profile-count equality requirement (`src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundlePackageTrustIndex.cs:77`, `:148`). Removing General Replace from its normative workflow enum needs a contract update (`docs/contracts/profile-bundle-package-trust-index-v1.schema.json:63`). |
| JSON bundle admission | **Accepts unchanged entries.** Entry schemas remain the indexed v2.9/family schemas; do not narrow those historical schemas. Closed inventory is manifest-based, not registration-based (`src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleLoader.cs:133`; `src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundleInventoryVerifier.cs:23`). |
| Catalog projection/factory | **Retains and normalizes the unregistered profile; does not ignore or refuse it.** All projected profiles enter normalization, family/map validation and identity uniqueness checks. Factory receives no registration or active-workflow set and does not compile plans (`src/NvtFwCombiner.Infrastructure/Composition/TrustedProfileBundleCatalogProjection.cs:16`; `src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalogFactory.cs:56`, `:183`). An invalid retained declaration could fail the whole catalog; option A leaves this valid declaration unchanged. |
| Prebuilt generator/admission | **Accepts after rebuild.** Generator loads all entries and embeds the new index identity (`eng/prebuilt-profile-catalog/PrebuiltProfileCatalogGenerator.cs:32`, `:47`). Stale pack/index/assembly combinations fail build binding or prebuilt admission; runtime falls back to JSON (`src/NvtFwCombiner.Infrastructure/Bundles/AcceptedPrebuiltProfileCatalog.cs:62`; `src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2Bundle.cs:48`). No registration-completeness check. |
| Route inventory | **Disappears cleanly when registration and policy row both leave.** General Replace dictionary filters trust registrations; CtrlRAM filters only CtrlRAM registrations (`src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs:56`, `:86`; `src/NvtFwCombiner.Infrastructure/Composition/CtrlRamV2RouteRegistry.cs:59`). Publication enumerates policy rows, not all catalog profiles (`src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalogSource.cs:82`). A direct resolver call for the retired route throws “No General Replace definition matches” (`src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs:298`). |
| General planner and Saved Rule production readers | **Already refuse after registration withdrawal.** `CanPlanGeneralReplace` becomes false, admission returns null, and planning returns `ReplaceWorkflowNotSupported` (`src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.cs:24`, `:55`, `:141`). Saved Rule reference-slot lookup returns null and loading returns `ParentUnavailable` (`src/NvtFwCombiner.Infrastructure/Composition/BuiltInSavedRuleAuthoring.cs:27`, `:40`, `:63`); inspection finds zero exact parents and refuses (`src/NvtFwCombiner.Infrastructure/Composition/SavedRuleV2Inspector.cs:113`, `:122`). These read `GeneralReplaceByIc`; the proposed compiler refusal covers direct catalog compilation that bypasses registration, not a missing production-path refusal. |
| Preload | **Accepts unchanged bundle set.** Shared CtrlRAM bundle remains a layer entry; excluded-bundle checks concern General Merge only (`src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2BundlePreload.cs:37`, `:114`). |
| Active capability policy | **Accepts the reduced rows after SHA projection.** Four-workflow enforcement requires removing General Replace from `BuiltInCanonicalCapabilityPolicy.CreateRoute` and the policy schema enum (`src/NvtFwCombiner.Infrastructure/Capabilities/BuiltInCanonicalCapabilityPolicy.cs:12`, `:66`; `docs/contracts/canonical-capability-policy-v1.schema.json:89`). This vocabulary is admission, not a compiler switch. |
| Compiler | **Needs code change. No retirement refusal exists for General Replace today.** Catalog selection is profile id/version, then experience equality/map matching, without trust-index registration checks (`src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalog.Selection.cs:52`; `src/NvtFwCombiner.Profiles/V2/TrustedProfileBundleCatalog.Compilation.cs:370`). Otherwise valid General Replace can mint an artifact; only DP is refused at shared `Succeed` (`src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs:131`). Extend that owner with **one merged DP/General Replace condition and one issue construction**, preserving `profile.v2.plan.retired-experience` exactly once; do not reject normalization or change survivor lowering. R3 firmware change. |
| Compiler boundary pins and size policy | **Need synchronized pins with the eventual refusal.** `RemoveAdmittedCompilerIdentity` pins the DP refusal fragment verbatim (`tests/NvtFwCombiner.Architecture.Tests/CanonicalCatalogBoundaryTests.CanonicalProfileDefinition.cs:351`, `:354`); retirement coverage requires exactly one production occurrence of the error code (`tests/NvtFwCombiner.Architecture.Tests/RetirementBoundaryTests.DpReplaceRetirement.cs:32`). Update the fragment and coverage for the merged condition, retaining uniqueness. `V2CompositionPlanCompiler*.cs` contributes to the aggregate hotspot capped at 3,900 nonblank lines (`scripts/code_size_policy.py:42`, `:292`); the refusal extension must not add aggregate nonblank lines. |
| CatalogProbe and downstream host tests | **Retained content is actively read, not ignored.** The probe directly compiles `nt51926-general-replace-dp-single-candidate` and expects success at `0x3E000`, failure at `0x40000` (`tests/NvtFwCombiner.CatalogProbe/CompilationScenarios.cs:57`, `:60`). Its parent evidence enumerates `GeneralReplaceByIc` (`tests/NvtFwCombiner.CatalogProbe/CatalogEvidence.cs:195`). Withdrawal changes parents/publication; the later compiler refusal flips the valid scenario (`tests/NvtFwCombiner.Bootstrap.Tests/PrebuiltProfileCatalogEquivalenceTests.cs:29`, `:58`, `:171`). Bootstrap copies this child host (`tests/NvtFwCombiner.Bootstrap.Tests/NvtFwCombiner.Bootstrap.Tests.csproj:9`, `:40`); UiSmoke checks the shipped Desktop catalog probe (`tests/NvtFwCombiner.UiSmoke.Tests/ProfileCatalogProbeTests.cs:43`), rather than running `CompilationScenarios` itself. Both consumer gates belong in the eventual write/verification set. |
| Structure/bootstrap tests | **Need expectation changes, not a bundle rewrite.** Trust test pins 52 registrations and requires one General Replace registration (`tests/NvtFwCombiner.Architecture.Tests/PackageTrustBoundaryTests.PackageTrustIndex.cs:246`, `:264`). Bootstrap direct-file assertions still pass with retained bytes, but planner/live-workflow assertions must become retirement coverage (`tests/NvtFwCombiner.Architecture.Tests/BootstrapCliBoundaryTests.BootstrapStructure.cs:242`, `:419`, `:446`). |
| ProfileContract tests | **Factory contract is compatible; compiler positives need retargeting/refusal coverage.** Factory preserves all source hashes and rejects bad family/map bindings (`tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.cs:21`, `:88`, `:106`). DP already proves readable catalog declaration plus artifact refusal; General Replace currently has a positive lowering test (`tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.ReplaceLowering.cs:15`, `:273`; `tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.RuntimeReferenceReplace.cs:14`). |
| Other pinned tests | **Need bounded updates.** Trust loader repeats 52/General Replace expectations; policy tests pin 85 routes, 10 internal and 48 contract-only (`tests/NvtFwCombiner.Infrastructure.Tests/Bundles/ProfileBundlePackageTrustIndexLoaderTests.cs:69`; `tests/NvtFwCombiner.Infrastructure.Tests/Capabilities/BuiltInCanonicalCapabilityPolicyTests.cs:88`, `:113`, `:147`). Active frozen axes are **v2**, not DP's older v1 fixture (`tests/NvtFwCombiner.Bootstrap.Tests/HeadlessCanonicalConvergenceTests.cs:37`, `:68`; `tests/NvtFwCombiner.Bootstrap.Tests/Fixtures/canonical-route-axes-v2.txt:30`). |
| Golden manifest reader | **Accepts matched policy/evidence deletion by inspection.** Delete the contract-only General Replace row together with policy, retain all cases and expected data (`testdata/golden/canonical/manifest.json:427`; `scripts/canonical_golden_validation.py:483`). Full validation/execution remains unconfirmed. |
| Release pin projections | **Existing producer suffices; no producer code change.** It projects raw policy/index SHA into four existing targets, not firmware identities (`scripts/release_source_pins.py:11`, `:38`, `:77`). See section 3. |

Four-workflow enforcement also narrows the materializer whitelist and synchronizes its trust-schema SHA (and version guard if the approved schema version changes): `eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleTrustIndex.tasks:95`, `:220`, `:388`.
That build-tool change is R2; the affected trust/policy contracts, policy loader and compiler are R3. Test expectation updates are R1, with script-test paths R2.
None requires a second semantic path or excluding the retained profile from the catalog.

## 3. Hash changes and the file-only experiment

Executed `python -B -` with a standard-library inline script and three temporary JSON copies inside this worktree; removed all copies.
It removed exactly one registration, one policy route and the matching contract-only routeEvidence row. Source files were never modified.
All nine entry raw hashes and the bundle array hash matched; all eight checked-in bundle/family/profile JSON files remained byte-identical.
Registrations: 52 -> 51; policy routes: 85 -> 84; routeEvidence: 85 -> 84; bundle count stays 24.
Eight policy rows, their 24 decisions and eight routeEvidence rows remained identical; remaining evidence IDs still matched policy exactly.
All General Merge rows remained. This compared **existing pins after editing three inputs that are not inputs of the bundle content hash**. It establishes consistency of those data files, not acceptance by the code, freshly computed C# route fingerprints, or firmware output parity.
Removing only the bundle entry counterfactually yielded `f14f7a7f391e1c3c5cb017f99b69b3838c56f6547c103b85fa57b1db10f473ae`, confirming option B's hash consequence.

| Hash/pin | Effect and consumer |
| --- | --- |
| Family/profile/schema entry SHA; bundle content hash; bundle-manifest raw SHA | Unchanged under A. `CtrlRamV2PlanClosureProfileTests` bundle pin stays (`tests/NvtFwCombiner.Bootstrap.Tests/CtrlRamV2PlanClosureProfileTests.cs:15`). These are trusted firmware-definition identities. |
| Eight computed route fingerprints | Inputs are unchanged by inspection; existing pins unchanged in the experiment. Fresh runtime comparison is required. Registration-list/policy/root-manifest hashes are not inputs to the fingerprint formula. |
| Trust-index raw SHA | Changes. Loader computes it; build task emits it into assembly metadata; prebuilt header and admission bind it (`src/NvtFwCombiner.Infrastructure/Bundles/ProfileBundlePackageTrustIndex.cs:159`; `eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleTrustIndex.tasks:580`; `eng/prebuilt-profile-catalog/NvtFwCombiner.ProfileAdmissionIdentity.targets:9`; `src/NvtFwCombiner.Infrastructure/Bundles/AcceptedPrebuiltProfileCatalog.cs:62`). Build/package source identity, not route identity. |
| Manifest-set SHA | Unchanged because bundle manifest bytes and directory set stay fixed (`src/NvtFwCombiner.Infrastructure/Bundles/BuiltInProfileBuildAdmissionIdentity.cs:58`). Rebuilt pack/assembly/package hashes may change with new source identity; not firmware output identity. |
| Policy raw SHA | Changes: loader ExpectedSha256 and snapshot SourceSha256, package/smoke policy contract, script-test constant (`src/NvtFwCombiner.Infrastructure/Capabilities/BuiltInCanonicalCapabilityPolicy.cs:12`, `:59`; `scripts/release_source_pins.py:58`, `:68`). Script tests verify those pins (`tests/scripts/test_release_package_policy.py:1437`). |
| Golden root manifest raw SHA | Changes; no fixed whole-file SHA pin found in the requested release readers/producer. They parse it and separately pin **case manifests** via the release allowlist (`scripts/package.ps1:1243`, `:1319`; `scripts/release_source_pins.py:13`). Case hashes, expectedView/output hashes and release allowlist stay unchanged. |
| Live trust/policy schema SHA | Changes when workflow enums narrow; trust schema SHA is pinned by the materializer and checked by Architecture tests (`eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleTrustIndex.tasks:95`; `tests/NvtFwCombiner.Architecture.Tests/PackageTrustBoundaryTests.PackageTrustIndex.cs:320`). These schemas are not the retained bundle's v2.9/family entry schemas. |

Calling `plan_reviewed_source_pins` on the temporary data produced changes in exactly:
`src/NvtFwCombiner.Infrastructure/Capabilities/BuiltInCanonicalCapabilityPolicy.cs`,
`scripts/package.ps1`, `scripts/smoke-release.ps1`, `tests/scripts/test_release_package_policy.py`.
The index SHA projection goes to smoke; packaging compares published index SHA to current source dynamically (`scripts/package.ps1:410`).
No projected pin was written. Candidate digests from reserialized scratch JSON are not approved pins.

## 4. Existing unregistered-profile and DP Replace precedents

The strongest current precedent is inside this exact shared bundle: `nt51926-ctrlram-replace-fw141-cascade@0.7.0` is a manifest profile entry (`profiles/built-in/nt51926-ctrlram-replace-candidate/profile-bundle.json:31`; `profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-cascade.json:3`). Searching all trust-index registrations finds **zero** exact `profileId` matches; only the four runtime CtrlRAM profiles and the General Replace candidate are registered for this bundle (`profiles/built-in/package-trust-index.json:193`). The materializer validates/copies all manifest entries independently of registration, and the factory normalizes all projected profiles (section 2). Existing `Nt51926CtrlRamReplaceCandidateProfileTests.CandidateProfileCompilesTheLegacyCascadeStagingAndWriteAuthority` directly compiles this unregistered profile through the selected built-in catalog (`tests/NvtFwCombiner.Bootstrap.Tests/Nt51926CtrlRamReplaceCandidateProfileTests.cs:30`, `:579`). Thus today's code and executable contract already permit retained unregistered content; no fresh run of that precedent was performed here. It does not authorize retaining a retired profile without the firmware owner's choice.

`docs/governance/change-records/DP-REPLACE-RETIREMENT-DATA-110-01.json:52` specifies deletion of five independent bundles/ten profiles, ten registrations and fourteen routeEvidence/axis rows.
It preserves 24 surviving bundle/family/profile byte sets, 79 fingerprints/decisions and all 40 Golden cases, byte/hash contracts, difference bounds, alias facts and release allowlist.
It narrows trust/schema/materializer/policy admission and uses the existing source-pin producer; readable retired profiles still cannot compile artifacts (`tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.ReplaceLowering.cs:15`).
General Replace differs because its entry and map live inside the surviving CtrlRAM bundle/family. Deleting the entire bundle is impossible; removing just its content still changes shared identity.
Option A follows DP's **unchanged survivor definitions** property, while retaining one unregistered declaration. Current readers permit that separation.

## 5. Recommendation, planned files and owner evidence

Static inspection supports A for step 4, conditional on a build, freshly computed fingerprints and the Golden run. Whether B is needed remains a firmware-owner decision informed by that evidence.
Decision 230 retires the profile; retaining its bytes under A requires the firmware owner's explicit choice. In a deliverable, compiler refusal and withdrawal must land together; this non-mergeable spike intentionally tests withdrawal alone.
Minimal A-specific production/data write set (proposed only; broader CLI/UI/Saved Rule removal remains in the plan):

| Exact files | Change |
| --- | --- |
| `profiles/built-in/package-trust-index.json` | Remove only General Replace registration at line 230; preserve bundle descriptor and CtrlRAM registrations. |
| `docs/contracts/canonical-capability-policy-v1.json` | Remove only General Replace route and its three decisions at line 819. |
| `testdata/golden/canonical/manifest.json` | Remove only General Replace routeEvidence at line 427; no case/byte/hash edits. |
| `docs/contracts/profile-bundle-package-trust-index-v1.schema.json`; `docs/contracts/canonical-capability-policy-v1.schema.json` | Remove General Replace from active workflow enums; coordinate schema-version guards if versions change. |
| `docs/contracts/profile-bundle-package-trust-index-v1.md`; `docs/contracts/canonical-capability-policy-v1.md` | Synchronize current admission vocabulary (`:200`, `:9` respectively). |
| `eng/profile-bundle-materializer/NvtFwCombiner.ProfileBundleTrustIndex.tasks` | Narrow registration whitelist; recompute actual trust-schema SHA; align version guard. |
| `src/NvtFwCombiner.Infrastructure/Capabilities/BuiltInCanonicalCapabilityPolicy.cs` | Narrow CreateRoute workflow admission; project policy SHA. |
| `src/NvtFwCombiner.Profiles/V2/V2CompositionPlanCompiler.ContractLowering.cs` | Extend existing artifact retirement refusal with one merged condition and one error-code occurrence; preserve normalization/shared lowering and add no compiler aggregate nonblank lines. |
| `scripts/package.ps1`; `scripts/smoke-release.ps1`; `tests/scripts/test_release_package_policy.py` | Existing reviewed-source-pins projections; no hand-guessed SHA values. |

Associated expectation changes identified: `tests/NvtFwCombiner.Architecture.Tests/PackageTrustBoundaryTests.PackageTrustIndex.cs`;
`tests/NvtFwCombiner.Architecture.Tests/BootstrapCliBoundaryTests.BootstrapStructure.cs`;
`tests/NvtFwCombiner.Infrastructure.Tests/Bundles/ProfileBundlePackageTrustIndexLoaderTests.cs`;
`tests/NvtFwCombiner.Infrastructure.Tests/Capabilities/BuiltInCanonicalCapabilityPolicyTests.cs`;
`tests/NvtFwCombiner.Bootstrap.Tests/HeadlessCanonicalConvergenceTests.cs`;
`tests/NvtFwCombiner.Bootstrap.Tests/Fixtures/canonical-route-axes-v2.txt`;
`tests/NvtFwCombiner.CatalogProbe/CompilationScenarios.cs` (retarget success/refusal expectations);
`tests/NvtFwCombiner.CatalogProbe/CatalogEvidence.cs` (retired-parent evidence);
`tests/NvtFwCombiner.Bootstrap.Tests/PrebuiltProfileCatalogEquivalenceTests.cs` (parent count, publication pins and scenario coverage);
`tests/NvtFwCombiner.Architecture.Tests/CanonicalCatalogBoundaryTests.CanonicalProfileDefinition.cs` (verbatim merged-refusal pin);
`tests/NvtFwCombiner.Architecture.Tests/RetirementBoundaryTests.DpReplaceRetirement.cs` (merged retirement coverage, preserving one issue-code occurrence);
`tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.ReplaceLowering.cs` and
`tests/NvtFwCombiner.ProfileContract.Tests/TrustedProfileBundleCatalogFactoryTests.RuntimeReferenceReplace.cs`.
`tests/NvtFwCombiner.UiSmoke.Tests/ProfileCatalogProbeTests.cs` remains the shipped-host acceptance gate; change its expectations only if the approved retirement changes its protocol. Saved Rule/planner positives must become retirement coverage, notably `SavedRuleCliCommandTests.Mappings.cs`, `ReplaceCliCommandTests.General.cs`, `Nt51926GeneralReplaceCandidateProfileTests.cs`, `GeneralSelectedFileExecutionLifecycleTests.cs` and `GeneralOutputConfirmationTests.cs` under `tests/NvtFwCombiner.Bootstrap.Tests/`.
Related shared compiler test helpers may need survivor retargeting; this is not a complete R54 test deletion inventory.
No change to any file under `profiles/built-in/nt51926-ctrlram-replace-candidate/`, the plan-closure bundle pin, case manifests or expected Golden files.
No factory, catalog admission, generator, fingerprint algorithm or release-source-pin producer code change is needed.

Owner evidence: fresh JSON and prebuilt catalog acceptance with the unregistered declaration retained; no General Replace route/registration/artifact; four-workflow admission rejects consistent retired/unknown rows.
Compare all eight **computed** fingerprints before/after against the existing pins, and complete Golden outputs/exact write authority under each existing contract, preserving approved CRC differences and TP views.
Retain General Merge controls. Integration remains R3 with firmware-owner and release-owner evidence/approval; this spike supplies neither.

## 6. Data-only commander spike (never merge)

The working tree now removes exactly the registration, policy route with its three decisions, and contract-only routeEvidence row listed above. The data edits are deletion-only: 6, 29 and 7 lines respectively (42 total), with original UTF-8/LF, order and indentation preserved. For the final registration, deleting the old separator and the next object's fields reuses the existing final closing brace; no survivor property changes.
No bundle file, schema, compiler, materializer whitelist, case manifest, test expectation or SHA pin is edited. The commander alone runs `python scripts/sync_derived.py --write --only reviewed-source-pins` and reviews its generated changes before building. The spike is evidence, not a deliverable, and will never be merged.

`D:\NvtFwCombiner-TestArea\temp\r54-spike\EXPECTED.md` contains the ordered commands, expected stale-inventory failures, proof methods/comparison locations, all eight route pins and Golden case mappings, and refutation criteria. The key fingerprint gate is `CatalogLoadScopedCtrlRamTests.EveryLoadExpandsOnceAndPreservesAllPolicyFingerprints` (`tests/NvtFwCombiner.Bootstrap.Tests/CatalogLoadScopedCtrlRamTests.cs:34`), which compares freshly materialized identities to the unchanged policy pins, including all eight NT51926 CtrlRAM routes. Materialization itself rejects mismatches (`src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCatalogSource.cs:127`).
The data-only probe's direct General Replace compilation scenario should still pass: no compiler refusal was added. Parent-count/publication pins should fail. UiSmoke's shipped catalog probe should continue accepting the reduced publication. These are distinct assertions.

`python scripts/verify.py --release-golden` runs both complete Bootstrap and GoldenRegression projects (`scripts/verify.py:4694`, `:4784`); unchanged General Replace tests can fail that command before its final executed-case summary. The targeted eight-route output tests must separately pass with fresh per-case execution evidence. An expected stale-inventory failure is neither a fresh Golden pass nor a refutation of retained bundle admission.
Frozen deliverable integration, outside this spike, additionally requires `python scripts/verify.py --all`; release Golden runs must prove every applicable certified output case actually executed, not merely fixture hashes.

Preparation checks on the actual spike: a standard-library comparison against `git show HEAD:<path>` confirmed exactly the three designated semantic deletions and unchanged survivor rows; all nine entry raw SHA values and the canonical bundle content hash matched. `scripts.canonical_golden_validation.validate_canonical_golden` passed for the edited inventory and unchanged fixture contracts, without executing firmware outputs. A read-only `plan_reviewed_source_pins` call predicted changes in exactly the existing four producer outputs; no pin was written. `git diff --check` passed. These are data checks, not C# acceptance or a Golden output run.

## Unconfirmed and limits

- No .NET build, materializer/catalog execution, fresh dynamic fingerprint calculation, complete Golden output comparison, repository verifier command or release smoke was run; these remain commander gates.
- The original scratch experiment used standard-library parsing/hash comparisons, not schema execution or C# acceptance. Optional schema execution for the actual spike was also unavailable: `jsonschema` could not be imported with the supplied Python dependency path. The successful Python checks above are data evidence only.
- Commander must load the user-level test-root declaration and set TEMP/TMP/TMPDIR before tests; the supplied Python dependency and temp paths are recorded in `EXPECTED.md`.
- Schema/catalog version assignments and the remaining positive-test helper retargeting belong to implementation; no guessed versions/hashes were written.
- Actual retained worktree changes: this untracked feasibility document plus the three tracked data deletions; `EXPECTED.md` is outside the worktree at the owner-specified test-area path. No complete retirement implementation or SHA synchronization was performed.
- No network, credentials, firmware payloads, commit, push, git configuration change, other-worktree write or branch integration. No claim of release readiness.
- Scoped review: PASS-WITH-HUMAN-GATE for this feasibility report; runtime/Golden evidence and the firmware-owner option choice remain open.

## Experiment result (the commander, 2026-10-02, outside the sandbox)

The data-only spike (one registration, one policy route with its decisions and one route-evidence row removed; the
bundle, family and profile files unchanged; pins synchronized by the existing producer) was rebuilt in full and run
with the commands of the spike's instruction file. Evidence: the test area, `evidence/1.2.4/r54-option-a-spike` and
`temp/r54-spike/run-20261002-165807-630`.

- Passed, as predicted: the catalog contracts (196), the prebuilt acceptance (53), the surviving contracts (112),
  the probe contracts (2), the Golden host's prebuilt admission (1), the shipped Desktop probe (8) and the eight
  NT51926 CtrlRAM route outputs (11 cases, all eight routes executed, differences inside the approved 16 CRC bytes).
- The comparison of freshly computed route identity values with the unchanged pins passed for all 84 remaining
  routes (`CatalogLoadScopedCtrlRamTests.EveryLoadExpandsOnceAndPreservesAllPolicyFingerprints`).
- Failed: 97 distinct tests. An independent classification (Claude Opus 5.5) put 46 in "expects the old counts or a
  snapshot that includes the removed route" and 51 in "exercises Customized Replace itself"; none showed a surviving
  workflow, the shared bundle's admission, the prebuilt catalog, a surviving route or a Golden output changed. 42 of
  the 97 were not predicted by the instruction file; all fall in the same two classes.
- `python scripts/verify.py --release-golden` stopped at the Bootstrap lane because of those failures, so no formal
  executed-case summary exists: this is not a Golden pass of record.
- Not covered: the compiler refusal, the narrowed schema enums, the materializer's workflow list, the release
  package and smoke scripts, a complete `verify.py --all`, other platforms.

Conclusion: within that scope option A is accepted by the code. It was not chosen (decision 266).
