namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies CLI and Workbench share the Application-owned single-run automatic Build gate.</summary>
    [Fact]
    public void BootstrapUsesOneAutomaticBuildExecutionGate()
    {
        string bootstrapSource = ReadBootstrapSources();
        string cli = ReadText("src/NvtFwCombiner.Bootstrap/CliApplication.StandardMerge.cs");
        string abCliRouter = ReadText("src/NvtFwCombiner.Bootstrap/CliApplication.AbMerge.cs");
        string abCliHandler = ReadText("src/NvtFwCombiner.Bootstrap/AbMergeCliCommandHandler.cs");
        string runner = ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.cs");
        int cliCalls = CountOccurrences(cli, ".PreviewOrBuildAsync(");
        int runnerCalls = CountOccurrences(runner, ".PreviewOrBuildAsync(");

        Assert.DoesNotContain("CompositionRunExecutionSupport", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildWithInternalPreviewAsync", bootstrapSource, StringComparison.Ordinal);
        Assert.Equal(1, cliCalls);
        Assert.Equal(2, runnerCalls);
        Assert.Contains("AbMergeCliCommandHandler.RunAsync", abCliRouter, StringComparison.Ordinal);
        Assert.DoesNotContain(".PreviewOrBuildAsync(", abCliRouter, StringComparison.Ordinal);
        Assert.Contains("CompositionExecutionAdapter.RunAbMergeForCliAsync", abCliHandler, StringComparison.Ordinal);
        Assert.DoesNotContain(".PreviewOrBuildAsync(", abCliHandler, StringComparison.Ordinal);
        Assert.Contains("progress is null", runner, StringComparison.Ordinal);
        Assert.Equal(cliCalls + runnerCalls, CountOccurrences(bootstrapSource, ".PreviewOrBuildAsync("));
        Assert.DoesNotContain("WithApprovedPreviewToken", bootstrapSource, StringComparison.Ordinal);
    }

    /// <summary>Verifies Replace CLI routes only through the registered Workbench/V2 paths.</summary>
    [Fact]
    public void BootstrapReplaceCliRetiresLegacyProfileExecutionPath()
    {
        string root = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.cs");

        Assert.Contains("internal static async Task<int> RunAsync", root, StringComparison.Ordinal);
        Assert.Contains("RunWorkbenchDpReplaceAsync", root, StringComparison.Ordinal);
        Assert.Contains("RunWorkbenchCtrlRamReplaceAsync", root, StringComparison.Ordinal);
        Assert.Contains("RunWorkbenchGeneralReplaceAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("FixedInputOptionsByAddressSpace", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryCreateBindings", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryCreateIcNumberSelection", root, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryFindReplaceProfile", root, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition", root, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", root, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInReplaceProfiles", root, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralReplaceOperationId", root, StringComparison.Ordinal);
        foreach (string retiredFile in new[]
                 {
                     "ReplaceCliCommandHandler.Bindings.cs",
                     "ReplaceCliCommandHandler.IcNumbers.cs",
                     "ReplaceCliCommandHandler.ProfileCompile.cs",
                     "ReplaceCliCommandHandler.ProfileResolution.cs",
                 })
        {
            Assert.False(File.Exists(Path.Combine(
                Root.FullName,
                "src",
                "NvtFwCombiner.Bootstrap",
                retiredFile)));
        }
    }

    /// <summary>Verifies shared Replace workbench CLI helpers stay out of the CtrlRAM workflow file.</summary>
    [Fact]
    public void BootstrapReplaceWorkbenchCliHelpersStaySplit()
    {
        string ctrlRam = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.CtrlRamWorkbench.cs");
        string ctrlRamSlots = ReadText(
            "src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.CtrlRamWorkbench.Slots.cs");
        string support = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.WorkbenchSupport.cs");
        string report = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.WorkbenchReport.cs");

        Assert.Contains("RunWorkbenchCtrlRamReplaceAsync", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryResolveWorkbenchIc", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static InputArtifactBinding[] CreateWorkbenchBindings", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task WriteWorkbenchReportFileIfRequestedAsync", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task PrintWorkbenchRunResultAsync", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryCreateWorkbenchCtrlRamSlotPaths", ctrlRam, StringComparison.Ordinal);
        Assert.Contains("private static bool TryCreateWorkbenchCtrlRamSlotPaths", ctrlRamSlots, StringComparison.Ordinal);
        Assert.Contains("private static Dictionary<string, WorkbenchReplaceInputSlot> CreateCtrlRamSlotLookup", ctrlRamSlots, StringComparison.Ordinal);
        Assert.Contains("private static bool TryResolveWorkbenchIc", support, StringComparison.Ordinal);
        Assert.Contains("private static InputArtifactBinding[] CreateWorkbenchBindings", support, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteWorkbenchReportFileIfRequestedAsync", report, StringComparison.Ordinal);
        Assert.Contains("CliCompositionRunSupport.WriteReportJsonAsync", support, StringComparison.Ordinal);
        Assert.Contains("private static async Task PrintWorkbenchRunResultAsync", report, StringComparison.Ordinal);
    }

    /// <summary>Verifies DP Replace IC facts come from the trusted V2 registrations instead of a legacy C# catalog.</summary>
    [Fact]
    public void BootstrapProjectsDpReplaceIcFactsFromV2Registrations()
    {
        string bootstrapSource = string.Concat(
            ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.DpWorkbench.cs"),
            ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.Replace.Dp.cs"));

        Assert.Contains("CanonicalCapabilityProjection.FormatBuiltInV2DpReplaceIcIds()", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950/NT51951", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Nt51950", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51928", bootstrapSource, StringComparison.Ordinal);
    }

    /// <summary>Verifies supported DP Replace reaches the shared engine from the trusted V2 bundle, not legacy profiles.</summary>
    [Fact]
    public void BootstrapRoutesSupportedDpReplaceThroughTrustedV2Artifacts()
    {
        string replaceDp = ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.Replace.Dp.cs");
        string capabilityResolution = ReadText(
            "src/NvtFwCombiner.Bootstrap/CanonicalCapabilityResolution.DpReplace.cs");
        string v2Resolution = ReadText("src/NvtFwCombiner.Bootstrap/CanonicalCapabilityProjection.DpReplace.cs");
        string v2Display = ReadText("src/NvtFwCombiner.Bootstrap/CompositionMemoryProjection.Replace.Dp.cs");
        string replaceDisplay = ReadText("src/NvtFwCombiner.Bootstrap/CompositionMemoryProjection.Replace.cs");
        string replaceCoverage = ReadText("src/NvtFwCombiner.Bootstrap/CompositionMemoryProjection.Replace.Coverage.cs");
        string replaceCli = string.Concat(
            ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.DpWorkbench.cs"),
            ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.WorkbenchSupport.cs"));
        string bundle = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2Bundle.cs");
        string registrations = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");
        string packageTrustIndex = ReadText("profiles/built-in/package-trust-index.json");

        Assert.Contains("TryCompileDpReplace", capabilityResolution, StringComparison.Ordinal);
        Assert.Contains("CompiledCompositionInputBindingFactory.Create", replaceDp, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInReplaceProfiles", replaceDp, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", replaceDp, StringComparison.Ordinal);
        Assert.Contains("TryResolveBuiltInV2DpReplaceSelector", replaceCli, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInReplaceProfiles", replaceCli, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition", replaceCli, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.DpReplace", registrations, StringComparison.Ordinal);
        Assert.Contains(
            "nt51950-nt51951-standard-merge",
            packageTrustIndex,
            StringComparison.Ordinal);
        Assert.Contains("TryResolveBuiltInV2DpReplaceDisplay", v2Resolution, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", v2Display, StringComparison.Ordinal);
        Assert.Contains("CreateV2DpReplaceMemoryDisplay", v2Display, StringComparison.Ordinal);
        Assert.Contains("CreateV2DpReplaceMemoryDisplay", replaceDisplay, StringComparison.Ordinal);
        Assert.Contains("\"No DP Replace profile\"", replaceDisplay, StringComparison.Ordinal);
        Assert.Contains("\"No V2 profile\"", replaceDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateDpReplaceRows", replaceDisplay, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDpReplaceRegions", string.Concat(replaceDp, replaceDisplay, replaceCoverage), StringComparison.Ordinal);
        Assert.Contains("CreateReplaceCoverageSegments", replaceCoverage, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", string.Concat(
            replaceDp,
            v2Resolution,
            v2Display,
            replaceDisplay,
            replaceCoverage,
            replaceCli), StringComparison.Ordinal);
        Assert.Contains("ProfileBundleLoader.Load", bundle, StringComparison.Ordinal);
        Assert.Contains("Path.Combine(\"profiles\", \"built-in\", bundleDirectory)", bundle, StringComparison.Ordinal);
        Assert.DoesNotContain("profiles\\\\built-in\\\\", ReadBootstrapSources(), StringComparison.Ordinal);
    }

    /// <summary>Verifies CtrlRAM Replace V2 runtime routes remain limited to the reviewed request shapes.</summary>
    [Fact]
    public void CtrlRamReplaceV2RuntimeRoutesStayPreciselyScoped()
    {
        string ctrlRamRuntime = ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.Replace.CtrlRam.cs");
        string ctrlRamV2 = ReadText("src/NvtFwCombiner.Bootstrap/CompositionPlanningAdapter.Replace.CtrlRam.V2.cs");
        string ctrlRamRoutes = ReadText("src/NvtFwCombiner.Bootstrap/CtrlRamV2RouteRegistry.cs");
        string ctrlRamVersionAdapter = ReadText("src/NvtFwCombiner.Bootstrap/CtrlRamV2FirmwareVersionAdapter.cs");
        string generalRuntime = ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.Replace.General.cs") +
            ReadText("src/NvtFwCombiner.Bootstrap/CompositionAuthoringSessionAdapter.Replace.General.Readiness.cs");
        string generalV2 = ReadText("src/NvtFwCombiner.Bootstrap/CompositionPlanningAdapter.Replace.General.V2.cs");
        string cli = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.CtrlRamWorkbench.cs");
        string registrations = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");
        string packageTrustIndex = ReadText("profiles/built-in/package-trust-index.json");
        string diagnosticProfile = ReadText(
            "profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-cascade.json");
        string runtimeProfile = ReadText(
            "profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-runtime-cascade.json");
        string fw200SingleProfile = ReadText(
            "profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw200-runtime-single.json");
        string fw200CascadeProfile = ReadText(
            "profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw200-runtime-cascade.json");
        string nt51923SingleProfile = ReadText(
            "profiles/built-in/nt51923-ctrlram-replace-candidate/profiles/nt51923-ctrlram-replace-fw141-single.json");
        string nt51923CascadeProfile = ReadText(
            "profiles/built-in/nt51923-ctrlram-replace-candidate/profiles/nt51923-ctrlram-replace-fw141-cascade3.json");
        string nt51923Family = ReadText(
            "profiles/built-in/nt51923-ctrlram-replace-candidate/families/nt51923-ctrlram-replace.json");
        string nt51927SingleProfile = ReadText(
            "profiles/built-in/nt51927-ctrlram-replace-candidate/profiles/nt51927-ctrlram-replace-fw141-single.json");
        string nt51927TwoChipProfile = ReadText(
            "profiles/built-in/nt51927-ctrlram-replace-candidate/profiles/nt51927-ctrlram-replace-fw132-twochip.json");
        string nt51927ThreeChipProfile = ReadText(
            "profiles/built-in/nt51927-ctrlram-replace-candidate/profiles/nt51927-ctrlram-replace-fw140-threechip.json");
        string nt51927Family = ReadText(
            "profiles/built-in/nt51927-ctrlram-replace-candidate/families/nt51927-ctrlram-replace.json");
        string nt51917SingleProfile = ReadText(
            "profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profiles/nt51917-ctrlram-replace-fw141-single.json");
        string nt51917TwoChipProfile = ReadText(
            "profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profiles/nt51917-ctrlram-replace-fw132-twochip.json");
        string nt51917ThreeChipProfile = ReadText(
            "profiles/built-in/nt51917-ctrlram-replace-alias-candidate/profiles/nt51917-ctrlram-replace-fw140-threechip.json");
        string nt51929Profile = ReadText(
            "profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51929-ctrlram-replace-fw200-single.json");
        string nt51919Profile = ReadText(
            "profiles/built-in/nt51929-ctrlram-replace-candidate/profiles/nt51919-ctrlram-replace-fw200-single.json");
        string nt51929Family = ReadText(
            "profiles/built-in/nt51929-ctrlram-replace-candidate/families/nt51929-ctrlram-replace.json");
        string nt51932Profile = ReadText(
            "profiles/built-in/nt51932-ctrlram-replace-candidate/profiles/nt51932-ctrlram-replace-fw200-cascade.json");
        string nt51932Family = ReadText(
            "profiles/built-in/nt51932-ctrlram-replace-candidate/families/nt51932-ctrlram-replace.json");
        string nt51950Profile = ReadText(
            "profiles/built-in/nt51950-ctrlram-replace-candidate/profiles/nt51950-ctrlram-replace-fw200-single.json");
        string nt51950CascadeProfile = ReadText(
            "profiles/built-in/nt51950-ctrlram-replace-candidate/profiles/nt51950-ctrlram-replace-fw1x-cascade.json");
        string nt51950Family = ReadText(
            "profiles/built-in/nt51950-ctrlram-replace-candidate/families/nt51950-ctrlram-replace.json");
        string nt51951Profile = ReadText(
            "profiles/built-in/nt51951-ctrlram-replace-candidate/profiles/nt51951-ctrlram-replace-fw200-single.json");
        string nt51951CascadeProfile = ReadText(
            "profiles/built-in/nt51951-ctrlram-replace-candidate/profiles/nt51951-ctrlram-replace-fw1x-cascade.json");
        string nt51951Family = ReadText(
            "profiles/built-in/nt51951-ctrlram-replace-candidate/families/nt51951-ctrlram-replace.json");
        string generalProfile = ReadText(
            "profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-general-replace-dp-single-candidate.json");

        Assert.DoesNotContain("nt51923-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51923-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51926-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51926-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51927-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51927-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51917-ctrlram-replace-alias-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51917-ctrlram-replace-alias-candidate", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51929-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51929-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51932-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51932-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51950-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51950-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51951-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51951-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.Contains("nt51923-ctrlram-replace-candidate", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-replace-candidate", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51927-ctrlram-replace-candidate", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51917-ctrlram-replace-alias-candidate", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51929-ctrlram-replace-candidate", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51932-ctrlram-replace-candidate", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51950-ctrlram-replace-candidate", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51951-ctrlram-replace-candidate", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"runtime-route\"", diagnosticProfile, StringComparison.Ordinal);
        Assert.Contains(
            "CtrlRamV2FirmwareVersionAdapter.Create(context.FirmwareVersionWritePlan)",
            ctrlRamV2,
            StringComparison.Ordinal);
        Assert.Contains("V2RuntimeReferenceReplaceFirmwareVersionEdit", ctrlRamVersionAdapter, StringComparison.Ordinal);
        Assert.Contains(
            "new V2RuntimeReferenceReplaceCompileRequest(",
            ctrlRamV2,
            StringComparison.Ordinal);
        Assert.Contains("postbuildPolicy", ctrlRamV2, StringComparison.Ordinal);
        Assert.DoesNotContain("PatchScalar", ctrlRamV2, StringComparison.Ordinal);
        Assert.Contains("CtrlRamV2RouteRegistry.TryResolve", ctrlRamV2, StringComparison.Ordinal);
        Assert.Contains("context.CommandPlan.Selector.Token", ctrlRamV2, StringComparison.Ordinal);
        Assert.Contains("context.CommandPlan.TopologyCount", ctrlRamV2, StringComparison.Ordinal);
        Assert.DoesNotContain("context.CommandPlan.Selector.ResolveTopologyCount", ctrlRamV2, StringComparison.Ordinal);
        Assert.Contains(
            "context.Selection",
            ctrlRamRuntime,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "ToIcNumberSelection(context.CommandPlan.Selector.Token)",
            ctrlRamRuntime,
            StringComparison.Ordinal);
        Assert.DoesNotContain("CommonFwVersion", ctrlRamRoutes, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectId", ctrlRamRoutes, StringComparison.Ordinal);
        Assert.DoesNotContain("ChipNumber", ctrlRamRoutes, StringComparison.Ordinal);
        Assert.Contains("new CtrlRamV2RouteKey(profile.IcId, profile.ProcessorId, branch)", ctrlRamRoutes, StringComparison.Ordinal);
        Assert.DoesNotContain("requiredBaseSha256", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("referencePayload.Sha256", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("CompileCtrlRamV2", ctrlRamV2, StringComparison.Ordinal);
        Assert.Contains("BuiltInV2BundleRegistry.All[route.BundleId]", ctrlRamV2, StringComparison.Ordinal);
        Assert.Contains("nt51917-ctrlram-replace-alias-candidate", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51929-ctrlram-replace-candidate", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-replace-fw141-runtime-cascade", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-replace-fw200-runtime-single", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-replace-fw200-runtime-cascade", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51950-ctrlram-replace-fw1x-cascade", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("nt51951-ctrlram-replace-fw1x-cascade", packageTrustIndex, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", runtimeProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("\"blockerId\": \"direct-golden-evidence\"", runtimeProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("\"blockerId\": \"runtime-route\"", runtimeProfile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"firmware-owner-review\"", runtimeProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"cascade-selector\"", runtimeProfile, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-fw141-tp-work-240k", runtimeProfile, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-fw141-full-flash-256k", runtimeProfile, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", fw200SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"release-support-review\"", fw200SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", fw200SingleProfile, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-fw200-tp-work-240k", fw200SingleProfile, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-fw200-full-flash-256k", fw200SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", fw200CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"release-support-review\"", fw200CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"cascade-selector\"", fw200CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-fw200-tp-work-240k", fw200CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-fw200-full-flash-256k", fw200CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51923SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", nt51923SingleProfile, StringComparison.Ordinal);
        Assert.Contains("nt51923-ctrlram-fw141-single-full-flash", nt51923SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51923CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"cascade-selector\"", nt51923CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("nt51923-ctrlram-fw141-cascade3-full-flash", nt51923CascadeProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("metadataPredicates", nt51923Family, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51927SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-no-promotion\"", nt51927SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", nt51927SingleProfile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51927.ctrlram-postbuild-v1", nt51927SingleProfile, StringComparison.Ordinal);
        Assert.Contains("nt51927-ctrlram-fw141-single-full-flash", nt51927SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51927TwoChipProfile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-expected-derived-route\"", nt51927TwoChipProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"numeric-selector\"", nt51927TwoChipProfile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51927.ctrlram-postbuild-v1", nt51927TwoChipProfile, StringComparison.Ordinal);
        Assert.Contains("nt51927-ctrlram-fw132-twochip-full-flash", nt51927TwoChipProfile, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51927ThreeChipProfile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-expected-derived-route\"", nt51927ThreeChipProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"numeric-selector\"", nt51927ThreeChipProfile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51927.ctrlram-postbuild-v1", nt51927ThreeChipProfile, StringComparison.Ordinal);
        Assert.Contains("nt51927-ctrlram-fw140-threechip-full-flash", nt51927ThreeChipProfile, StringComparison.Ordinal);
        Assert.Contains("\"topologyRequirement\": { \"kind\": \"exact-count\", \"chipCount\": 2 }", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("\"topologyRequirement\": { \"kind\": \"exact-count\", \"chipCount\": 3 }", nt51927Family, StringComparison.Ordinal);
        Assert.DoesNotContain("metadataPredicates", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("\"memberId\": \"NT51917\"", nt51927Family, StringComparison.Ordinal);
        Assert.Equal(3, CountOccurrences(nt51927Family, "\"memberIds\": [ \"NT51917\", \"NT51927\" ]"));
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51917SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", nt51917SingleProfile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51917.ctrlram-postbuild-v1", nt51917SingleProfile, StringComparison.Ordinal);
        Assert.Contains("nt51927-ctrlram-fw141-single-full-flash", nt51917SingleProfile, StringComparison.Ordinal);
        Assert.Contains("ctrlram-replace-perfect-family-51917-to-51927", nt51917SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"numeric-selector\"", nt51917TwoChipProfile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51917.ctrlram-postbuild-v1", nt51917TwoChipProfile, StringComparison.Ordinal);
        Assert.Contains("nt51927-ctrlram-fw132-twochip-full-flash", nt51917TwoChipProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"numeric-selector\"", nt51917ThreeChipProfile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51917.ctrlram-postbuild-v1", nt51917ThreeChipProfile, StringComparison.Ordinal);
        Assert.Contains("nt51927-ctrlram-fw140-threechip-full-flash", nt51917ThreeChipProfile, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51929Profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-no-promotion\"", nt51929Profile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", nt51929Profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51929.ctrlram-postbuild-v1", nt51929Profile, StringComparison.Ordinal);
        Assert.Contains("nt51929-ctrlram-fw200-single-full-flash", nt51929Profile, StringComparison.Ordinal);
        Assert.DoesNotContain("metadataPredicates", nt51929Family, StringComparison.Ordinal);
        Assert.Contains("\"regionId\": \"fw-config-source\"", nt51929Family, StringComparison.Ordinal);
        Assert.Contains("\"regionId\": \"fw-config-backup\"", nt51929Family, StringComparison.Ordinal);
        Assert.Contains("\"memberId\": \"NT51919\"", nt51929Family, StringComparison.Ordinal);
        Assert.Contains("\"memberIds\": [ \"NT51919\", \"NT51929\" ]", nt51929Family, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51919Profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-no-promotion\"", nt51919Profile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", nt51919Profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51919.ctrlram-postbuild-v1", nt51919Profile, StringComparison.Ordinal);
        Assert.Contains("nt51929-ctrlram-fw200-single-full-flash", nt51919Profile, StringComparison.Ordinal);
        Assert.Contains("ctrlram-replace-perfect-family-51919-to-51929", nt51919Profile, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51932Profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-no-promotion\"", nt51932Profile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"cascade-selector\"", nt51932Profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51932.ctrlram-postbuild-v1", nt51932Profile, StringComparison.Ordinal);
        Assert.Contains("nt51932-ctrlram-fw200-cascade-full-flash", nt51932Profile, StringComparison.Ordinal);
        Assert.DoesNotContain("metadataPredicates", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51950Profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-no-promotion\"", nt51950Profile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", nt51950Profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51950.ctrlram-postbuild-v1", nt51950Profile, StringComparison.Ordinal);
        Assert.Contains("nt51950-ctrlram-fw200-single-full-flash", nt51950Profile, StringComparison.Ordinal);
        Assert.Contains("\"diff-ctrlram\"", nt51950CascadeProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("metadataPredicates", nt51950Family, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51951Profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-no-promotion\"", nt51951Profile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", nt51951Profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51951.ctrlram-postbuild-v1", nt51951Profile, StringComparison.Ordinal);
        Assert.Contains("nt51951-ctrlram-fw200-single-full-flash", nt51951Profile, StringComparison.Ordinal);
        Assert.Contains("\"diff-ctrlram\"", nt51951CascadeProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("metadataPredicates", nt51951Family, StringComparison.Ordinal);

        Assert.DoesNotContain("nt51926-general-replace-dp-single-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51926-general-replace-dp-single-candidate", cli, StringComparison.Ordinal);
        Assert.Contains("IsGeneralReplaceDpV2Route", generalRuntime, StringComparison.Ordinal);
        Assert.Contains(
            "BuiltInV2RegistrationRegistry.GeneralReplaceByIc",
            generalRuntime,
            StringComparison.Ordinal);
        Assert.DoesNotContain("NT51926", generalV2, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "nt51926-general-replace-dp-single-candidate",
            generalV2,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "nt51926-ctrlram-replace-candidate",
            generalV2,
            StringComparison.Ordinal);
        Assert.Contains("selection.Mode != IcNumberInputMode.SingleSelector", generalV2, StringComparison.Ordinal);
        Assert.Contains(
            "row.Source.Kind != GeneralMappingSourceKind.FileArtifact",
            generalV2,
            StringComparison.Ordinal);
        Assert.Contains("region.Range.Contains(mapping.TargetRange)", generalV2, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", generalProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("\"blockerId\": \"full-route-parity\"", generalProfile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"tp-postbuild-deferred\"", generalProfile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"firmware-owner-review\"", generalProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", generalProfile, StringComparison.Ordinal);
        Assert.Contains("nt51926-general-replace-full-flash-256k", generalProfile, StringComparison.Ordinal);
        Assert.DoesNotContain("legacy-combiner-v1", generalProfile, StringComparison.Ordinal);
    }

    /// <summary>Verifies workbench Replace mode ids stay centralized for UI and CLI adapters.</summary>
    [Fact]
    public void BootstrapOwnsWorkbenchReplaceModeIds()
    {
        string bootstrapSource = ReadBootstrapSources();
        string replaceModes = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchReplaceModes.cs");
        string mergeModes = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchMergeModes.cs");
        string bootstrapWithoutReplaceModes = bootstrapSource
            .Replace(replaceModes, string.Empty, StringComparison.Ordinal)
            .Replace(mergeModes, string.Empty, StringComparison.Ordinal);

        Assert.Contains("public const string Dp = \"DP\"", replaceModes, StringComparison.Ordinal);
        Assert.Contains("public const string CtrlRam = \"CtrlRAM\"", replaceModes, StringComparison.Ordinal);
        Assert.Contains("public const string General = \"General\"", replaceModes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"DP\"", bootstrapWithoutReplaceModes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"CtrlRAM\"", bootstrapWithoutReplaceModes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"General\"", bootstrapWithoutReplaceModes, StringComparison.Ordinal);
    }

    /// <summary>Verifies workbench Merge mode ids stay centralized for UI adapters.</summary>
    [Fact]
    public void BootstrapOwnsWorkbenchMergeModeIds()
    {
        string bootstrapSource = ReadBootstrapSources();
        string mergeModes = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchMergeModes.cs");
        string replaceModes = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchReplaceModes.cs");
        string bootstrapWithoutModeCatalogs = bootstrapSource
            .Replace(mergeModes, string.Empty, StringComparison.Ordinal)
            .Replace(replaceModes, string.Empty, StringComparison.Ordinal);

        Assert.Contains("public const string Standard = \"Normal\"", mergeModes, StringComparison.Ordinal);
        Assert.Contains("public const string AbCode = \"AB Code\"", mergeModes, StringComparison.Ordinal);
        Assert.Contains("public const string General = \"General\"", mergeModes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Normal\"", bootstrapWithoutModeCatalogs, StringComparison.Ordinal);
        Assert.DoesNotContain("\"AB Code\"", bootstrapWithoutModeCatalogs, StringComparison.Ordinal);
    }

    /// <summary>Verifies workbench report/run id prefixes stay centralized by workflow mode.</summary>
    [Fact]
    public void BootstrapOwnsWorkbenchRunIdPrefixes()
    {
        string runner = ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.cs");
        string standardMerge = ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.StandardMerge.cs");
        string generalMerge = ReadText("src/NvtFwCombiner.Bootstrap/CompositionMemoryProjection.GeneralMerge.cs");
        string generalMergeV2 = ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.GeneralMerge.V2.cs");
        string replaceDp = ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.Replace.Dp.cs");
        string replaceCtrlRam = ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.Replace.CtrlRam.cs");
        string replaceGeneral = ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.Replace.General.cs");
        string replaceReport = ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.Results.cs");

        Assert.Contains("internal const string StandardMergeRunIdPrefix = \"ui\";", runner, StringComparison.Ordinal);
        Assert.Contains("internal const string GeneralMergeRunIdPrefix = \"ui-merge-general\";", runner, StringComparison.Ordinal);
        Assert.Contains("internal const string DpReplaceRunIdPrefix = \"ui-replace-dp\";", runner, StringComparison.Ordinal);
        Assert.Contains("internal const string CtrlRamReplaceRunIdPrefix = \"ui-replace-ctrlram\";", runner, StringComparison.Ordinal);
        Assert.Contains("internal const string GeneralReplaceRunIdPrefix = \"ui-replace-general\";", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string CreateWorkbenchReportRunId", runner, StringComparison.Ordinal);
        Assert.Contains("private static string GetReplaceRunIdPrefix", runner, StringComparison.Ordinal);
        Assert.Contains("StandardMergeRunIdPrefix", standardMerge, StringComparison.Ordinal);
        Assert.Contains("GeneralMergeRunIdPrefix", generalMergeV2, StringComparison.Ordinal);
        Assert.Contains("CreateBlockedReportRunResult", generalMergeV2, StringComparison.Ordinal);
        Assert.Contains("DpReplaceRunIdPrefix", replaceDp, StringComparison.Ordinal);
        Assert.Contains("CtrlRamReplaceRunIdPrefix", replaceCtrlRam, StringComparison.Ordinal);
        Assert.Contains("GeneralReplaceRunIdPrefix", replaceGeneral, StringComparison.Ordinal);
        Assert.Contains("GetReplaceRunIdPrefix(replaceMode)", replaceReport, StringComparison.Ordinal);
        Assert.Contains(
            "$\"{runIdPrefix}-{runAction}-{FormatWorkbenchRunTimestamp(timestamp)}\"",
            replaceReport,
            StringComparison.Ordinal);
        foreach (string source in new[]
        {
            standardMerge,
            generalMerge,
            generalMergeV2,
            replaceDp,
            replaceCtrlRam,
            replaceGeneral,
            replaceReport,
        })
        {
            Assert.DoesNotContain("\"ui-merge-general\"", source, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ui-replace\"", source, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ui-replace-dp\"", source, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ui-replace-ctrlram\"", source, StringComparison.Ordinal);
            Assert.DoesNotContain("\"ui-replace-general\"", source, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies CLI and Workbench create IC-number selections through one Bootstrap helper.</summary>
    [Fact]
    public void BootstrapOwnsIcNumberSelectionConstruction()
    {
        string bootstrapSource = ReadBootstrapSources();
        string helper = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchIcNumberSelections.cs");
        string bootstrapWithoutHelper = bootstrapSource.Replace(helper, string.Empty, StringComparison.Ordinal);

        Assert.Contains("new IcNumberSelection", helper, StringComparison.Ordinal);
        Assert.Contains("WorkbenchIcNumberSelections.FromNumberToken", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("new IcNumberSelection", bootstrapWithoutHelper, StringComparison.Ordinal);
    }

    /// <summary>Verifies workbench slot ids stay centralized for CLI, UI, and report adapters.</summary>
    [Fact]
    public void BootstrapOwnsWorkbenchSlotIds()
    {
        string bootstrapSource = ReadBootstrapSources();
        string slotIds = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchSlotIds.cs");
        string bootstrapWithoutSlotIds = bootstrapSource.Replace(slotIds, string.Empty, StringComparison.Ordinal);

        Assert.Contains("public const string MergeDp = \"merge-dp\"", slotIds, StringComparison.Ordinal);
        Assert.Contains("public const string MergeTp = \"merge-tp\"", slotIds, StringComparison.Ordinal);
        Assert.Contains("public const string MergeLdc = \"merge-ldc\"", slotIds, StringComparison.Ordinal);
        Assert.Contains("public const string ReplaceBase = \"replace-base\"", slotIds, StringComparison.Ordinal);
        Assert.Contains("public const string ReplaceDp = \"replace-dp\"", slotIds, StringComparison.Ordinal);
        Assert.Contains(
            "public const string ReplaceCtrlRamPrefix = CompositionAddressSpaceIds.DynamicCtrlRamReplacementPrefix;",
            slotIds,
            StringComparison.Ordinal);
        foreach (string slotLiteral in new[]
        {
            "\"merge-dp\"",
            "\"merge-tp\"",
            "\"merge-ldc\"",
            "\"replace-base\"",
            "\"replace-dp\"",
            "\"replace-ctrlram-",
        })
        {
            Assert.DoesNotContain(slotLiteral, bootstrapWithoutSlotIds, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies UI-facing workflow ids are projected from the profile catalog without repeating literals.</summary>
    [Fact]
    public void BootstrapProjectsWorkflowIdsForUiAdapters()
    {
        string workflowIds = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchWorkflowIds.cs");

        Assert.Contains("public const string StandardMerge = ExperienceIds.StandardMerge;", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string GeneralMerge = ExperienceIds.GeneralMerge;", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string DpReplace = ExperienceIds.DpReplace;", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string CtrlRamReplace = ExperienceIds.CtrlRamReplace;", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string GeneralReplace = ExperienceIds.GeneralReplace;", workflowIds, StringComparison.Ordinal);
        Assert.DoesNotContain("\"standard-merge\"", workflowIds, StringComparison.Ordinal);
        Assert.DoesNotContain("\"dp-replace\"", workflowIds, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ctrlram-replace\"", workflowIds, StringComparison.Ordinal);
        Assert.DoesNotContain("\"general-merge\"", workflowIds, StringComparison.Ordinal);
        Assert.DoesNotContain("\"general-replace\"", workflowIds, StringComparison.Ordinal);
    }

    /// <summary>Verifies report output-difference classifications are projected from the report contract.</summary>
    [Fact]
    public void BootstrapProjectsOutputDifferenceClassifications()
    {
        string classifications = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchOutputDifferenceClassifications.cs");

        Assert.Contains("OutputDifferenceClassifications.DeclaredReplacement", classifications, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceClassifications.PostbuildCrcHeader", classifications, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceClassifications.PreservedReference", classifications, StringComparison.Ordinal);
        Assert.Contains("OutputDifferenceClassifications.Unexpected", classifications, StringComparison.Ordinal);
        Assert.DoesNotContain("\"DeclaredReplacement\"", classifications, StringComparison.Ordinal);
        Assert.DoesNotContain("\"PostbuildCrcHeader\"", classifications, StringComparison.Ordinal);
        Assert.DoesNotContain("\"PreservedReference\"", classifications, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Unexpected\"", classifications, StringComparison.Ordinal);
    }

    /// <summary>Verifies UI-facing composition issue codes are projected from the Domain contract.</summary>
    [Fact]
    public void BootstrapProjectsCompositionIssueCodes()
    {
        string bootstrapSource = ReadBootstrapSources();
        string issueCodes = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionIssueCodes.cs");

        Assert.Contains("CompositionIssueCodes.InputAddressSpaceLengthMismatch", bootstrapSource, StringComparison.Ordinal);
        Assert.Contains("CompositionIssueCodes.InputAddressSpaceTruncated", issueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"input.address-space.length-mismatch\"", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"input.address-space.truncated\"", bootstrapSource, StringComparison.Ordinal);
    }

    /// <summary>Verifies workbench planning/report issue codes stay centralized for UI and CLI adapters.</summary>
    [Fact]
    public void BootstrapOwnsWorkbenchIssueCodes()
    {
        string bootstrapSource = ReadBootstrapSources();
        string issueCodes = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchIssueCodes.cs");
        string bootstrapWithoutIssueCodes = bootstrapSource.Replace(issueCodes, string.Empty, StringComparison.Ordinal);

        Assert.Contains("public const string GeneralMergeSourceOutOfBounds = \"ui.general-merge.source-out-of-bounds\"", issueCodes, StringComparison.Ordinal);
        Assert.Contains("public const string ReplaceCtrlRamPostbuildCategoryUnknown = \"replace.ctrlram.postbuild-category-unknown\"", issueCodes, StringComparison.Ordinal);
        Assert.Contains("public const string InputArtifactReadFailed = \"input.artifact.read-failed\"", issueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ui.general-merge.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ui.general-replace.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ui.input.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"replace.ctrlram.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"replace.general.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"replace.dp.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"replace.mode.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"input.artifact.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
    }

    /// <summary>Verifies v2 saved-rule validation codes stay centralized without restoring v1 codes.</summary>
    [Fact]
    public void BootstrapOwnsSavedRuleIssueCodes()
    {
        string bootstrapSource = ReadBootstrapSources();
        string issueCodes = ReadText("src/NvtFwCombiner.Bootstrap/SavedRuleIssueCodes.cs");
        string bootstrapWithoutIssueCodes = bootstrapSource.Replace(issueCodes, string.Empty, StringComparison.Ordinal);

        Assert.Contains(
            "public const string SchemaVersionUnsupported =",
            issueCodes,
            StringComparison.Ordinal);
        Assert.Contains(
            "public const string V2ContractInvalid = \"saved-rule.v2.contract-invalid\"",
            issueCodes,
            StringComparison.Ordinal);
        Assert.Contains(
            "public const string ProcessorDependencyUnsupported =",
            issueCodes,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PropertyUnknown", issueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationFragmentProcessorDependencyUnsupported", issueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"saved-rule.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
    }

    /// <summary>Verifies General mapping text parsing is owned by one Bootstrap helper.</summary>
    [Fact]
    public void BootstrapRangeTextOwnsGeneralMappingParsing()
    {
        string bootstrapSource = ReadBootstrapSources();
        string rangeText = ReadText("src/NvtFwCombiner.Bootstrap/BootstrapRangeText.cs");

        Assert.Contains("internal static bool TryParseNonNegativeLong", rangeText, StringComparison.Ordinal);
        Assert.Contains("internal static string FormatHex", rangeText, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(bootstrapSource, "internal static bool TryParseNonNegativeLong"));
        Assert.Equal(1, CountOccurrences(bootstrapSource, "internal static string FormatHex"));
        Assert.DoesNotContain("private static bool TryParseNonNegativeLong", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string FormatHex(", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CliCompositionRunSupport.TryParseNonNegativeLong", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CliCompositionRunSupport.FormatHex", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FormatWorkbenchHex", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("TryParseCliNonNegativeLong", bootstrapSource, StringComparison.Ordinal);
    }

    /// <summary>Verifies trusted bundle catalog handoff remains a structural Bootstrap bridge.</summary>
    [Fact]
    public void BootstrapTrustedBundleCatalogBridgeDoesNotOwnSemanticResolution()
    {
        string bridge = ReadText("src/NvtFwCombiner.Bootstrap/TrustedProfileBundleCatalogProjection.cs");
        string infrastructureProject = ReadText("src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj");

        Assert.Contains("TrustedProfileBundleCatalogFactory.Create", bridge, StringComparison.Ordinal);
        Assert.Contains("CopyIdentity", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("Normalizer", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("MapResolution", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Profiles", infrastructureProject, StringComparison.Ordinal);
    }

}
