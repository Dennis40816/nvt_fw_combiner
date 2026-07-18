namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Verifies CLI and Workbench share the Application-owned single-run automatic Build gate.</summary>
    [Fact]
    public void BootstrapUsesOneAutomaticBuildExecutionGate()
    {
        string bootstrapSource = ReadBootstrapSources();

        Assert.DoesNotContain("CompositionRunExecutionSupport", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BuildWithInternalPreviewAsync", bootstrapSource, StringComparison.Ordinal);
        Assert.Equal(
            2,
            CountOccurrences(bootstrapSource, ".PreviewOrBuildAsync("));
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
            ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.cs"));

        Assert.Contains("WorkbenchCompositionService.FormatBuiltInV2DpReplaceIcIds()", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950/NT51951", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51950", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Nt51950", bootstrapSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NT51928", bootstrapSource, StringComparison.Ordinal);
    }

    /// <summary>Verifies supported DP Replace reaches the shared engine from the trusted V2 bundle, not legacy profiles.</summary>
    [Fact]
    public void BootstrapRoutesSupportedDpReplaceThroughTrustedV2Artifacts()
    {
        string replaceDp = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.cs");
        string v2Resolution = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.BuiltInV2.cs");
        string v2Display = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.V2Display.cs");
        string replaceDisplay = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Display.cs");
        string replaceCoverage = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Coverage.cs");
        string replaceCli = string.Concat(
            ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.DpWorkbench.cs"),
            ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.WorkbenchSupport.cs"));
        string bundle = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2Bundle.cs");
        string registrations = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");

        Assert.Contains("TryCompileBuiltInV2DpReplace", replaceDp, StringComparison.Ordinal);
        Assert.Contains("CompiledCompositionInputBindingFactory.Create", replaceDp, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInReplaceProfiles", replaceDp, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileCompiler", replaceDp, StringComparison.Ordinal);
        Assert.Contains("TryResolveBuiltInV2DpReplaceSelector", replaceCli, StringComparison.Ordinal);
        Assert.DoesNotContain("BuiltInReplaceProfiles", replaceCli, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionProfileDefinition", replaceCli, StringComparison.Ordinal);
        Assert.Contains("IcWorkflowIds.DpReplace", registrations, StringComparison.Ordinal);
        Assert.Contains(
            "BuiltInV2BundleRegistry.All[\"nt51950-nt51951-standard-merge\"]",
            registrations,
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
        string ctrlRamRuntime = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.cs");
        string ctrlRamV2 = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.V2.cs");
        string generalRuntime = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.General.cs");
        string generalV2 = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.General.V2.cs");
        string cli = ReadText("src/NvtFwCombiner.Bootstrap/ReplaceCliCommandHandler.CtrlRamWorkbench.cs");
        string registrations = ReadText("src/NvtFwCombiner.Bootstrap/BuiltInV2RegistrationRegistry.cs");
        string project = ReadText("src/NvtFwCombiner.Bootstrap/NvtFwCombiner.Bootstrap.csproj");
        string diagnosticProfile = ReadText(
            "profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-cascade.json");
        string runtimeProfile = ReadText(
            "profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw141-runtime-cascade.json");
        string fw200SingleProfile = ReadText(
            "profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw200-runtime-single.json");
        string fw200CascadeProfile = ReadText(
            "profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-ctrlram-replace-fw200-runtime-cascade.json");
        string nt51930Profile = ReadText(
            "profiles/built-in/nt51930-ctrlram-replace-candidate/profiles/nt51930-ctrlram-replace-fw130-cascade3.json");
        string nt51920SingleProfile = ReadText(
            "profiles/built-in/nt51920-ctrlram-replace-candidate/profiles/nt51920-ctrlram-replace-fw120-single.json");
        string nt51920CascadeProfile = ReadText(
            "profiles/built-in/nt51920-ctrlram-replace-candidate/profiles/nt51920-ctrlram-replace-fw120-cascade2.json");
        string nt51920Family = ReadText(
            "profiles/built-in/nt51920-ctrlram-replace-candidate/families/nt51920-ctrlram-replace.json");
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
        string nt51930Family = ReadText(
            "profiles/built-in/nt51930-ctrlram-replace-candidate/families/nt51930-ctrlram-replace.json");
        string nt51931Profile = ReadText(
            "profiles/built-in/nt51931-ctrlram-replace-candidate/profiles/nt51931-ctrlram-replace-fw130-cascade6.json");
        string nt51931Family = ReadText(
            "profiles/built-in/nt51931-ctrlram-replace-candidate/families/nt51931-ctrlram-replace.json");
        string nt51932Profile = ReadText(
            "profiles/built-in/nt51932-ctrlram-replace-candidate/profiles/nt51932-ctrlram-replace-fw200-cascade3.json");
        string nt51932Family = ReadText(
            "profiles/built-in/nt51932-ctrlram-replace-candidate/families/nt51932-ctrlram-replace.json");
        string nt51950Profile = ReadText(
            "profiles/built-in/nt51950-ctrlram-replace-candidate/profiles/nt51950-ctrlram-replace-fw200-single.json");
        string nt51950Family = ReadText(
            "profiles/built-in/nt51950-ctrlram-replace-candidate/families/nt51950-ctrlram-replace.json");
        string nt51951Profile = ReadText(
            "profiles/built-in/nt51951-ctrlram-replace-candidate/profiles/nt51951-ctrlram-replace-fw200-single.json");
        string nt51951Family = ReadText(
            "profiles/built-in/nt51951-ctrlram-replace-candidate/families/nt51951-ctrlram-replace.json");
        string generalProfile = ReadText(
            "profiles/built-in/nt51926-ctrlram-replace-candidate/profiles/nt51926-general-replace-dp-single-candidate.json");

        Assert.DoesNotContain("nt51920-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51920-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
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
        Assert.DoesNotContain("nt51930-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51930-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51931-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51931-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51932-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51932-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51950-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51950-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51951-ctrlram-replace-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51951-ctrlram-replace-candidate", cli, StringComparison.Ordinal);
        Assert.Contains("nt51920-ctrlram-replace-candidate", project, StringComparison.Ordinal);
        Assert.Contains("nt51923-ctrlram-replace-candidate", project, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-replace-candidate", project, StringComparison.Ordinal);
        Assert.Contains("nt51927-ctrlram-replace-candidate", project, StringComparison.Ordinal);
        Assert.Contains("nt51917-ctrlram-replace-alias-candidate", project, StringComparison.Ordinal);
        Assert.Contains("nt51929-ctrlram-replace-candidate", project, StringComparison.Ordinal);
        Assert.Contains("nt51930-ctrlram-replace-candidate", project, StringComparison.Ordinal);
        Assert.Contains("nt51931-ctrlram-replace-candidate", project, StringComparison.Ordinal);
        Assert.Contains("nt51932-ctrlram-replace-candidate", project, StringComparison.Ordinal);
        Assert.Contains("nt51950-ctrlram-replace-candidate", project, StringComparison.Ordinal);
        Assert.Contains("nt51951-ctrlram-replace-candidate", project, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"runtime-route\"", diagnosticProfile, StringComparison.Ordinal);
        Assert.Contains("firmwareVersionEdit is null", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("TryReadFirmwareContextSuggestion", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("context.PostbuildProfile!.IcId", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.DoesNotContain("string? v2ProfileId = (\n            icId,", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51920\", \"nfc.nt51920.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, \"1.2.0\", 1, 0xF401)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51920\", \"nfc.nt51920.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, \"1.2.0\", 2, 0x1403)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("b9965def2946fd6e28165af5929ede885e1d0e3c0ab29266a737ac458225920d", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("681f904ecdf5785ca26f94eabb8191ddaa8976e0e6f750145475568c6cde4d43", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51923\", \"nfc.nt51923.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, \"1.4.1\", 1, 0x6005)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51923\", \"nfc.nt51923.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, \"1.4.1\", 3, 0x4C03)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("a65ae33c9c11091f69d8935422ffc57db32262eb922590364d4bdd9c3af9916f", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("06dda13a592c151a767d47fff60da993f33d7bda37666794dd9ea5cf92094d18", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51927\", \"nfc.nt51927.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, \"1.4.1\", 1, 0x5709)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51927\", \"nfc.nt51927.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.TwoChip, IcNumberInputMode.NumericSelector, \"1.3.2\", 2, 0x1615)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51927\", \"nfc.nt51927.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.ThreeChip, IcNumberInputMode.NumericSelector, \"1.4.0\", 3, 0x570A)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51917\", \"nfc.nt51917.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, \"1.4.1\", 1, 0x5709)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51917\", \"nfc.nt51917.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.TwoChip, IcNumberInputMode.NumericSelector, \"1.3.2\", 2, 0x1615)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51917\", \"nfc.nt51917.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.ThreeChip, IcNumberInputMode.NumericSelector, \"1.4.0\", 3, 0x570A)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("fc4d2f9701c626b1c7cddd2b448970611d332295c64f86415af2855f1569c55a", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("11700ec5580f2e07195c7aec3788f929609eef5355d773287d3f88aa1f984dae", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("bc44561cc1cb338b9a49bbe701e5d7cbfe78ea40deda0926197fb22002b3061c", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51929\", \"nfc.nt51929.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, \"2.0.0\", 1, 0x4703)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51919\", \"nfc.nt51919.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, \"2.0.0\", 1, 0x4703)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("d3c958d2aac1e29bd1f88b8ac62dc74c36810ab11e707770199d4b34f5ce3910", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51926\", \"nfc.nt51926.ctrlram-postbuild-fw1.4.1\", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, _, > 1, _)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51926\", Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, \"2.0.0\", 1, 0x1309)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51926\", Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, \"2.0.0\", 3, 0x1309)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51930\", \"nfc.nt51930.ctrlram-postbuild-fw1.x\", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, \"1.3.0\", 3, 0x110D)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51931\", \"nfc.nt51931.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, \"1.3.0\", 6, 0x131B)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("2268ac5b49df546a03e177b97858805f0f83fa58b3e55a3b1590899ce9fd07c3", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51932\", \"nfc.nt51932.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, \"2.0.0\", 3, 0x5601)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("Sha256File(context.BasePath!)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("3eb556e0a9323dd4fbe4c703be1eb33679df2b1ba839e79ddd7bbffa235008fd", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51950\", \"nfc.nt51950.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, \"2.0.0\", 1, 0x4A06)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("ccda75d0aa08540e293f9ab4a8058c43c4e39d2dd0238238848a2f13df68e38e", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("(\"NT51951\", \"nfc.nt51951.ctrlram-postbuild-v1\", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, \"2.0.0\", 1, 0x5901)", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("c1cd54d93af431727220adc37fec2488765909dc09cb917d1ff69f6087bb6b69", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("CompileCtrlRamV2", ctrlRamV2, StringComparison.Ordinal);
        Assert.Contains("nt51917-ctrlram-replace-alias-candidate", ctrlRamV2, StringComparison.Ordinal);
        Assert.Contains("\"NT51919\" => \"nt51929-ctrlram-replace-candidate\"", ctrlRamV2, StringComparison.Ordinal);
        Assert.Contains("Nt51926Fw200ProcessorId", ctrlRamV2, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-replace-fw141-runtime-cascade", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-replace-fw200-runtime-single", ctrlRamRuntime, StringComparison.Ordinal);
        Assert.Contains("nt51926-ctrlram-replace-fw200-runtime-cascade", ctrlRamRuntime, StringComparison.Ordinal);
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
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51920SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", nt51920SingleProfile, StringComparison.Ordinal);
        Assert.Contains("nt51920-ctrlram-fw120-single-full-flash", nt51920SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51920CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"cascade-selector\"", nt51920CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("nt51920-ctrlram-fw120-cascade2-full-flash", nt51920CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [ 62465 ]", nt51920Family, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [ 5123 ]", nt51920Family, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51923SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", nt51923SingleProfile, StringComparison.Ordinal);
        Assert.Contains("nt51923-ctrlram-fw141-single-full-flash", nt51923SingleProfile, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51923CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"cascade-selector\"", nt51923CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("nt51923-ctrlram-fw141-cascade3-full-flash", nt51923CascadeProfile, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [\n              24581", nt51923Family, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [\n              19459", nt51923Family, StringComparison.Ordinal);
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
        Assert.Contains("\"expectedValues\": [ 22281 ]", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("\"topologyRequirement\": { \"kind\": \"exact-count\", \"chipCount\": 2 }", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [ 5653 ]", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("\"topologyRequirement\": { \"kind\": \"exact-count\", \"chipCount\": 3 }", nt51927Family, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [ 22282 ]", nt51927Family, StringComparison.Ordinal);
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
        Assert.Contains("\"chipCount\": 1", nt51929Family, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [ 18179 ]", nt51929Family, StringComparison.Ordinal);
        Assert.Contains("\"memberId\": \"NT51919\"", nt51929Family, StringComparison.Ordinal);
        Assert.Contains("\"memberIds\": [ \"NT51919\", \"NT51929\" ]", nt51929Family, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51919Profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-no-promotion\"", nt51919Profile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", nt51919Profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51919.ctrlram-postbuild-v1", nt51919Profile, StringComparison.Ordinal);
        Assert.Contains("nt51929-ctrlram-fw200-single-full-flash", nt51919Profile, StringComparison.Ordinal);
        Assert.Contains("ctrlram-replace-perfect-family-51919-to-51929", nt51919Profile, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51930Profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-no-promotion\"", nt51930Profile, StringComparison.Ordinal);
        Assert.Contains("\"kind\": \"release\"", nt51930Profile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"cascade-selector\"", nt51930Profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51930.ctrlram-postbuild-fw1.x", nt51930Profile, StringComparison.Ordinal);
        Assert.Contains("nt51930-ctrlram-fw130-cascade3-full-flash", nt51930Profile, StringComparison.Ordinal);
        Assert.Contains("\"chipCount\": 3", nt51930Family, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [ 4365 ]", nt51930Family, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51931Profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-no-promotion\"", nt51931Profile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"cascade-selector\"", nt51931Profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51931.ctrlram-postbuild-v1", nt51931Profile, StringComparison.Ordinal);
        Assert.Contains("nt51931-ctrlram-fw130-cascade6-full-flash", nt51931Profile, StringComparison.Ordinal);
        Assert.Contains("\"chipCount\": 6", nt51931Family, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [ 4891 ]", nt51931Family, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51932Profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-no-promotion\"", nt51932Profile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"cascade-selector\"", nt51932Profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51932.ctrlram-postbuild-v1", nt51932Profile, StringComparison.Ordinal);
        Assert.Contains("nt51932-ctrlram-fw200-cascade3-full-flash", nt51932Profile, StringComparison.Ordinal);
        Assert.Contains("\"chipCount\": 3", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [ 22017 ]", nt51932Family, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51950Profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-no-promotion\"", nt51950Profile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", nt51950Profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51950.ctrlram-postbuild-v1", nt51950Profile, StringComparison.Ordinal);
        Assert.Contains("nt51950-ctrlram-fw200-single-full-flash", nt51950Profile, StringComparison.Ordinal);
        Assert.Contains("\"chipCount\": 1", nt51950Family, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [ 18950 ]", nt51950Family, StringComparison.Ordinal);
        Assert.Contains("\"stage\": \"executable-candidate\"", nt51951Profile, StringComparison.Ordinal);
        Assert.Contains("\"blockerId\": \"support-neutral-no-promotion\"", nt51951Profile, StringComparison.Ordinal);
        Assert.Contains("\"icNumberInputMode\": \"single-selector\"", nt51951Profile, StringComparison.Ordinal);
        Assert.Contains("nfc.nt51951.ctrlram-postbuild-v1", nt51951Profile, StringComparison.Ordinal);
        Assert.Contains("nt51951-ctrlram-fw200-single-full-flash", nt51951Profile, StringComparison.Ordinal);
        Assert.Contains("\"chipCount\": 1", nt51951Family, StringComparison.Ordinal);
        Assert.Contains("\"expectedValues\": [ 22785 ]", nt51951Family, StringComparison.Ordinal);

        Assert.DoesNotContain("nt51926-general-replace-dp-single-candidate", registrations, StringComparison.Ordinal);
        Assert.DoesNotContain("nt51926-general-replace-dp-single-candidate", cli, StringComparison.Ordinal);
        Assert.Contains("IsNt51926GeneralReplaceDpV2Route", generalRuntime, StringComparison.Ordinal);
        Assert.Contains("StringComparer.Ordinal.Equals(icId, \"NT51926\")", generalV2, StringComparison.Ordinal);
        Assert.Contains("context.Selection.Mode != IcNumberInputMode.SingleSelector", generalV2, StringComparison.Ordinal);
        Assert.Contains("context.SelectedPatches.Length != 0", generalV2, StringComparison.Ordinal);
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
        string runner = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Runner.cs");
        string standardMerge = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.StandardMerge.Run.cs");
        string generalMerge = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.cs");
        string generalMergeV2 = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.GeneralMerge.V2.cs");
        string replaceDp = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Dp.cs");
        string replaceCtrlRam = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.cs");
        string replaceGeneral = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.General.cs");
        string replaceReport = ReadText("src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Report.cs");

        Assert.Contains("private const string StandardMergeRunIdPrefix = \"ui\";", runner, StringComparison.Ordinal);
        Assert.Contains("private const string GeneralMergeRunIdPrefix = \"ui-merge-general\";", runner, StringComparison.Ordinal);
        Assert.Contains("private const string DpReplaceRunIdPrefix = \"ui-replace-dp\";", runner, StringComparison.Ordinal);
        Assert.Contains("private const string CtrlRamReplaceRunIdPrefix = \"ui-replace-ctrlram\";", runner, StringComparison.Ordinal);
        Assert.Contains("private const string GeneralReplaceRunIdPrefix = \"ui-replace-general\";", runner, StringComparison.Ordinal);
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
            "$\"{runIdPrefix}-{FormatWorkbenchRunAction(build)}-{FormatWorkbenchRunTimestamp(timestamp)}\"",
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
        Assert.Contains("public const string MergeLd = \"merge-ld\"", slotIds, StringComparison.Ordinal);
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
            "\"merge-ld\"",
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

        Assert.Contains("public const string StandardMerge = IcWorkflowIds.StandardMerge;", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string GeneralMerge = IcWorkflowIds.GeneralMerge;", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string DpReplace = IcWorkflowIds.DpReplace;", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string CtrlRamReplace = IcWorkflowIds.CtrlRamReplace;", workflowIds, StringComparison.Ordinal);
        Assert.Contains("public const string GeneralReplace = IcWorkflowIds.GeneralReplace;", workflowIds, StringComparison.Ordinal);
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

    /// <summary>Verifies saved-rule validation codes stay centralized as a Bootstrap CLI contract.</summary>
    [Fact]
    public void BootstrapOwnsSavedRuleIssueCodes()
    {
        string bootstrapSource = ReadBootstrapSources();
        string issueCodes = ReadText("src/NvtFwCombiner.Bootstrap/SavedRuleIssueCodes.cs");
        string bootstrapWithoutIssueCodes = bootstrapSource.Replace(issueCodes, string.Empty, StringComparison.Ordinal);

        Assert.Contains("public const string PropertyUnknown = \"saved-rule.property.unknown\"", issueCodes, StringComparison.Ordinal);
        Assert.Contains(
            "public const string ProcessorDependencyUnsupported = \"saved-rule.processor-dependency.unsupported\"",
            issueCodes,
            StringComparison.Ordinal);
        Assert.Contains(
            "public const string OperationFragmentProcessorDependencyUnsupported = \"saved-rule.operation-fragment.processor-dependency.unsupported\"",
            issueCodes,
            StringComparison.Ordinal);
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
