namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies CLI and Workbench share the Application-owned single-run automatic Build gate.</summary>
    [Fact]
    public void BootstrapUsesOneAutomaticBuildExecutionGate()
    {
        string bootstrapSource = ReadBootstrapSources();
        string cli = ReadText("src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs");
        string abCliRouter = ReadText("src/NvtFwCombiner.Cli/CliApplication.cs");
        string abCliHandler = ReadText("src/NvtFwCombiner.Cli/AbMergeCliCommandHandler.cs");
        string abExecution = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string runner = abExecution;
        string applicationExecution = ReadText(
            "src/NvtFwCombiner.Application/Composition/AcceptedSessionCompositionExecution.cs");
        int cliCalls = CountOccurrences(cli, ".PreviewOrBuildAsync(");
        int runnerCalls = CountOccurrences(runner, ".PreviewOrBuildAsync(");

        Assert.DoesNotContain("CompositionRunExecutionSupport", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildWithInternalPreviewAsync", bootstrapSource, StringComparison.Ordinal);
        Assert.Equal(0, cliCalls);
        Assert.Equal(0, runnerCalls);
        Assert.Equal(1, CountOccurrences(applicationExecution, ".PreviewOrBuildAsync("));
        Assert.Contains("AbMergeCliCommandHandler.RunAsync", abCliRouter, StringComparison.Ordinal);
        Assert.DoesNotContain(".PreviewOrBuildAsync(", abCliRouter, StringComparison.Ordinal);
        Assert.Contains("services.AbMergeAuthoring.PrepareSession", abCliHandler, StringComparison.Ordinal);
        Assert.Contains(".ExecuteAsync(", abCliHandler, StringComparison.Ordinal);
        Assert.Contains("AcceptedCompositionExecutionRequest", abCliHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("RunAbMergeForCliAsync", abCliHandler, StringComparison.Ordinal);
        Assert.DoesNotContain("RunAbMergeAsync(", abExecution, StringComparison.Ordinal);
        Assert.DoesNotContain("RunAbMergeWithProgressAsync(", abExecution, StringComparison.Ordinal);
        Assert.Contains("ExecuteAcceptedCompositionAsync", abExecution, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(runner, "AcceptedSessionExecutionInputs.CreateBindings"));
        Assert.Contains("AcceptedSessionCompositionExecution.ExecuteAsync", runner, StringComparison.Ordinal);
        Assert.DoesNotContain(".PreviewOrBuildAsync(", abCliHandler, StringComparison.Ordinal);
        Assert.Contains("CompositionRunProgressFeed progress", applicationExecution, StringComparison.Ordinal);
        Assert.Equal(0, CountOccurrences(bootstrapSource, ".PreviewOrBuildAsync("));
        Assert.DoesNotContain("WithApprovedPreviewToken", bootstrapSource, StringComparison.Ordinal);
    }

    /// <summary>Verifies Replace CLI routes only through the registered Workbench/V2 paths.</summary>
    [Fact]
    public void BootstrapReplaceCliRetiresLegacyProfileExecutionPath()
    {
        string root = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.cs");

        Assert.Contains("internal static async Task<int> RunAsync", root, StringComparison.Ordinal);
        Assert.Contains("RunDpReplaceAsync", root, StringComparison.Ordinal);
        Assert.Contains("RunCtrlRamReplaceAsync", root, StringComparison.Ordinal);
        Assert.Contains("RunGeneralReplaceAsync", root, StringComparison.Ordinal);
        Assert.DoesNotContain("RunWorkbench", root, StringComparison.Ordinal);
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
        string ctrlRam = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs");
        string ctrlRamSlots = ReadText(
            "src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.Slots.cs");
        string support = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs");
        string report = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Report.cs");

        Assert.Contains("RunCtrlRamReplaceAsync", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryResolveIc", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static InputArtifactBinding[] CreateBindings", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task WriteWorkbenchReportFileIfRequestedAsync", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static async Task PrintCompositionRunResultAsync", ctrlRam, StringComparison.Ordinal);
        Assert.DoesNotContain("private static bool TryCreateCtrlRamSlotPaths", ctrlRam, StringComparison.Ordinal);
        Assert.Contains("private static bool TryCreateCtrlRamSlotPaths", ctrlRamSlots, StringComparison.Ordinal);
        Assert.Contains("private static Dictionary<string, ReplaceInputSlot> CreateCtrlRamSlotLookup", ctrlRamSlots, StringComparison.Ordinal);
        Assert.Contains("private static bool TryResolveIc", support, StringComparison.Ordinal);
        Assert.Contains("private static InputArtifactBinding[] CreateBindings", support, StringComparison.Ordinal);
        Assert.DoesNotContain("WriteWorkbenchReportFileIfRequestedAsync", report, StringComparison.Ordinal);
        Assert.Contains("CliCompositionRunSupport.WriteReportJsonAsync", support, StringComparison.Ordinal);
        Assert.Contains("private static async Task PrintCompositionRunResultAsync", report, StringComparison.Ordinal);
    }

    /// <summary>Verifies DP Replace IC facts come from the trusted V2 registrations instead of a legacy C# catalog.</summary>
    [Fact]
    public void BootstrapProjectsDpReplaceIcFactsFromV2Registrations()
    {
        string bootstrapSource = string.Concat(
            ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Dp.cs"),
            ReadText("src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs"));

        Assert.Contains("GetDpReplaceProfileSummaries()", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950/NT51951", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Nt51950", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51928", bootstrapSource, StringComparison.Ordinal);
    }

    /// <summary>Verifies supported DP Replace reaches the shared engine from the trusted V2 bundle, not legacy profiles.</summary>
    [Fact]
    public void BootstrapRoutesSupportedDpReplaceThroughTrustedV2Artifacts()
    {
        string replaceDp = ReadText("src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string sharedExecution = replaceDp;
        string capabilityResolution = ReadText(
            "src/NvtFwCombiner.Application/Capabilities/CanonicalCapabilityCompiler.DpReplace.cs");
        string v2Resolution = ReadText(
            "src/NvtFwCombiner.Application/Authoring/DpReplaceAuthoringExperience.cs");
        string memoryLayout = ReadText("src/NvtFwCombiner.Application/MemoryLayout/MemoryLayoutProjector.cs");
        string replaceCli = string.Concat(
            ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.Dp.cs"),
            ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.RunSupport.cs"));
        string bundle = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2Bundle.cs");
        string registrations = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs");
        string packageTrustIndex = ReadText("profiles/built-in/package-trust-index.json");

        Assert.Contains("TryCompileDpReplace", capabilityResolution, StringComparison.Ordinal);
        Assert.Contains("ExecuteAcceptedCompositionAsync", replaceDp, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(sharedExecution, "AcceptedSessionExecutionInputs.CreateBindings"));
        Assert.DoesNotContain("CompiledCompositionInputBindingFactory.Create", replaceDp, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInReplaceProfiles", replaceDp, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", replaceDp, StringComparison.Ordinal);
        Assert.Contains("services.DpReplaceAuthoring.PrepareSession", replaceCli, StringComparison.Ordinal);
        Assert.DoesNotContain("TryResolveBuiltInV2DpReplaceSelector", replaceCli, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInReplaceProfiles", replaceCli, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition", replaceCli, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.DpReplace", registrations, StringComparison.Ordinal);
        Assert.Contains(
            "nt51950-nt51951-standard-merge",
            packageTrustIndex,
            StringComparison.Ordinal);
        Assert.Contains("TryResolveDpReplaceContracts", v2Resolution, StringComparison.Ordinal);
        Assert.Contains("CompositionPlan plan = composition.Plan", memoryLayout, StringComparison.Ordinal);
        Assert.Contains("plan.OrderedOperations", memoryLayout, StringComparison.Ordinal);
        Assert.DoesNotContain("DpPerspectiveCatalog", memoryLayout, StringComparison.Ordinal);
        foreach (string retiredProjection in new[]
                 {
                     "CompositionMemoryProjection.Replace.Dp.cs",
                     "CompositionMemoryProjection.Replace.cs",
                     "CompositionMemoryProjection.Replace.Coverage.cs",
                 })
        {
            Assert.False(File.Exists(Path.Combine(
                Root.FullName,
                "src",
                "NvtFwCombiner.Bootstrap",
                retiredProjection)));
        }
        Assert.DoesNotContain("DpPerspectiveCatalog", string.Concat(
            replaceDp,
            v2Resolution,
            memoryLayout,
            replaceCli), StringComparison.Ordinal);
        Assert.Contains("ProfileBundleLoader.Load", bundle, StringComparison.Ordinal);
        Assert.Contains("Path.Combine(\"profiles\", \"built-in\", bundleDirectory)", bundle, StringComparison.Ordinal);
        Assert.DoesNotContain("profiles\\\\built-in\\\\", ReadBootstrapSources(), StringComparison.Ordinal);
    }

    /// <summary>Verifies CtrlRAM Replace V2 runtime routes remain limited to the reviewed request shapes.</summary>
    [Fact]
    public void CtrlRamReplaceV2RuntimeRoutesStayPreciselyScoped()
    {
        string ctrlRamRuntime = ReadText("src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string ctrlRamV2 = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.V2.cs");
        string ctrlRamRoutes = ReadText("src/NvtFwCombiner.Infrastructure/Composition/CtrlRamV2RouteRegistry.cs");
        string ctrlRamVersionAdapter = ReadText("src/NvtFwCombiner.Infrastructure/Composition/CtrlRamV2FirmwareVersionAdapter.cs");
        string generalRuntime = ReadText("src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs") +
            ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralReplace.Readiness.cs") +
            ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.cs") +
            ReadText("src/NvtFwCombiner.Application/Authoring/GeneralAuthoringExperience.cs");
        string generalV2 = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.GeneralReplace.V2.cs");
        string cli = ReadText("src/NvtFwCombiner.Cli/ReplaceCliCommandHandler.CtrlRam.cs");
        string registrations = ReadText("src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs");
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
            "plan.IcNumberSelection",
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

    /// <summary>Verifies Merge and Replace clients use canonical workflow identities.</summary>
    [Fact]
    public void WorkflowModesUseCanonicalExperienceIds()
    {
        string bootstrapSource = ReadBootstrapSources();
        string infrastructureComposition = ReadInfrastructureCompositionSources();

        Assert.Contains("ExperienceIds.DpReplace", infrastructureComposition, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.CtrlRamReplace", infrastructureComposition, StringComparison.Ordinal);
        Assert.Contains("ExperienceIds.GeneralReplace", infrastructureComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("ExperienceIds.", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchReplaceModes", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchMergeModes", bootstrapSource, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(Root.FullName, "src", "NvtFwCombiner.Bootstrap", "WorkbenchReplaceModes.cs")));
        Assert.False(File.Exists(Path.Combine(Root.FullName, "src", "NvtFwCombiner.Bootstrap", "WorkbenchMergeModes.cs")));
    }

    /// <summary>Verifies executable run id prefixes stay centralized and blocked actions never fabricate reports.</summary>
    [Fact]
    public void ApplicationOwnsAcceptedExecutionRunIdPrefixes()
    {
        string runner = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string bootstrapSource = ReadBootstrapSources();

        Assert.Contains("private const string StandardMergeRunIdPrefix = \"ui\";", runner, StringComparison.Ordinal);
        Assert.Contains("private const string GeneralMergeRunIdPrefix = \"ui-merge-general\";", runner, StringComparison.Ordinal);
        Assert.Contains("private const string DpReplaceRunIdPrefix = \"ui-replace-dp\";", runner, StringComparison.Ordinal);
        Assert.Contains("private const string CtrlRamReplaceRunIdPrefix = \"ui-replace-ctrlram\";", runner, StringComparison.Ordinal);
        Assert.Contains("private const string GeneralReplaceRunIdPrefix = \"ui-replace-general\";", runner, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string CreateWorkbenchReportRunId", runner, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(runner, "StandardMergeRunIdPrefix"));
        Assert.Equal(2, CountOccurrences(runner, "GeneralMergeRunIdPrefix"));
        Assert.Equal(2, CountOccurrences(runner, "DpReplaceRunIdPrefix"));
        Assert.Equal(2, CountOccurrences(runner, "CtrlRamReplaceRunIdPrefix"));
        Assert.Equal(2, CountOccurrences(runner, "GeneralReplaceRunIdPrefix"));
        Assert.DoesNotContain("CreateBlockedCompositionRunResult", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("CreateReplaceReadinessOnlyResult", bootstrapSource, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CompositionExecutionAdapter.Results.cs")));
        Assert.DoesNotContain("CompositionExecutionAdapter", bootstrapSource, StringComparison.Ordinal);
    }

    /// <summary>Verifies clients create IC-number selections through the canonical Application parser.</summary>
    [Fact]
    public void ApplicationOwnsIcNumberSelectionConstruction()
    {
        string bootstrapSource = ReadBootstrapSources();
        string infrastructureComposition = ReadInfrastructureCompositionSources();
        string selection = ReadText("src/NvtFwCombiner.Application/Composition/IcNumberSelection.cs");

        Assert.Contains("public static IcNumberSelection FromToken", selection, StringComparison.Ordinal);
        Assert.Contains("IcNumberSelection.FromToken", infrastructureComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("new IcNumberSelection", infrastructureComposition, StringComparison.Ordinal);
        Assert.DoesNotContain("IcNumberSelection", bootstrapSource, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "WorkbenchIcNumberSelections.cs")));
    }

    /// <summary>Verifies client slot aliases are Application-owned until compiler ids replace them.</summary>
    [Fact]
    public void ApplicationOwnsCompositionSlotIds()
    {
        string bootstrapSource = ReadBootstrapSources();
        string slotIds = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionSlotIds.cs");

        Assert.Contains("public const string MergeDp = \"merge-dp\"", slotIds, StringComparison.Ordinal);
        Assert.Contains("public const string MergeTp = \"merge-tp\"", slotIds, StringComparison.Ordinal);
        Assert.Contains("public const string MergeLdc = \"merge-ldc\"", slotIds, StringComparison.Ordinal);
        Assert.Contains("public const string ReplaceBase = \"replace-base\"", slotIds, StringComparison.Ordinal);
        Assert.Contains("public const string ReplaceDp = \"replace-dp\"", slotIds, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "WorkbenchSlotIds.cs")));
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
            Assert.DoesNotContain(slotLiteral, bootstrapSource, StringComparison.Ordinal);
        }
    }

    /// <summary>Verifies the retired Bootstrap workflow-token mirror cannot return.</summary>
    [Fact]
    public void BootstrapWorkflowIdMirrorIsRetired()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "WorkbenchWorkflowIds.cs")));
    }

    /// <summary>Verifies report output-difference classifications are projected from the report contract.</summary>
    [Fact]
    public void BootstrapOutputDifferenceClassificationMirrorIsRetired()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "WorkbenchOutputDifferenceClassifications.cs")));
    }

    /// <summary>Verifies the retired Bootstrap composition-issue mirror cannot return.</summary>
    [Fact]
    public void BootstrapCompositionIssueCodeMirrorIsRetired()
    {
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "WorkbenchCompositionIssueCodes.cs")));
    }

    /// <summary>Verifies planning/report issue codes stay centralized in Application for every typed client.</summary>
    [Fact]
    public void ApplicationOwnsCompositionPlanningIssueCodes()
    {
        string bootstrapSource = ReadBootstrapSources();
        string issueCodes = ReadText("src/NvtFwCombiner.Application/Composition/CompositionPlanningIssueCodes.cs");

        Assert.Contains("public const string GeneralMergeSourceOutOfBounds = \"ui.general-merge.source-out-of-bounds\"", issueCodes, StringComparison.Ordinal);
        Assert.Contains("public const string ReplaceCtrlRamPostbuildCategoryUnknown = \"replace.ctrlram.postbuild-category-unknown\"", issueCodes, StringComparison.Ordinal);
        Assert.Contains("public const string InputArtifactReadFailed = \"input.artifact.read-failed\"", issueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ui.general-merge.", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ui.general-replace.", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"ui.input.", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"replace.ctrlram.", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"replace.general.", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"replace.dp.", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"replace.mode.", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("\"input.artifact.", bootstrapSource, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src/NvtFwCombiner.Bootstrap/CompositionPlanningIssueCodes.cs")));
    }

    /// <summary>Verifies v2 saved-rule validation codes stay centralized without restoring v1 codes.</summary>
    [Fact]
    public void BootstrapOwnsSavedRuleIssueCodes()
    {
        string bootstrapSource = ReadBootstrapSources();
        string issueCodes = ReadText("src/NvtFwCombiner.Infrastructure/Composition/SavedRuleIssueCodes.cs");
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
        Assert.Contains(
            "public const string ParentUnavailable = \"saved-rule.parent.unavailable\"",
            issueCodes,
            StringComparison.Ordinal);
        Assert.DoesNotContain("PropertyUnknown", issueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("OperationFragmentProcessorDependencyUnsupported", issueCodes, StringComparison.Ordinal);
        Assert.DoesNotContain("\"saved-rule.", bootstrapWithoutIssueCodes, StringComparison.Ordinal);
    }

    /// <summary>Verifies General mapping text parsing uses the Application codec without an adapter mirror.</summary>
    [Fact]
    public void GeneralRangeTextUsesApplicationCodecDirectly()
    {
        string production = ReadProductionSources();
        string applicationCodec = ReadText(
            "src/NvtFwCombiner.Application/Authoring/AuthoringByteRangeCodec.cs");

        Assert.Contains("public static bool TryParseNonNegativeLong", applicationCodec, StringComparison.Ordinal);
        Assert.Contains("public static string FormatHex", applicationCodec, StringComparison.Ordinal);
        Assert.DoesNotContain("BootstrapRangeText", production, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Infrastructure",
            "Composition",
            "BootstrapRangeText.cs")));
    }

    /// <summary>Verifies trusted bundle catalog handoff remains one Infrastructure adapter.</summary>
    [Fact]
    public void InfrastructureTrustedBundleCatalogBridgeDoesNotOwnSemanticResolution()
    {
        string bridge = ReadText("src/NvtFwCombiner.Infrastructure/Composition/TrustedProfileBundleCatalogProjection.cs");
        string infrastructureProject = ReadText("src/NvtFwCombiner.Infrastructure/NvtFwCombiner.Infrastructure.csproj");

        Assert.Contains("TrustedProfileBundleCatalogFactory.Create", bridge, StringComparison.Ordinal);
        Assert.Contains("CopyIdentity", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("JsonSerializer", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("Normalizer", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("MapResolution", bridge, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", bridge, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(
            infrastructureProject,
            "NvtFwCombiner.Profiles\\NvtFwCombiner.Profiles.csproj"));
        Assert.DoesNotContain(
            "NvtFwCombiner.Profiles",
            ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj"),
            StringComparison.Ordinal);
    }

}
