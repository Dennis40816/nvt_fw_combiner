namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>
    /// Prevents new production callers from expanding the four remaining compatibility authorities while
    /// evidence-gated migrations remove the listed callers one reviewed workflow at a time.
    /// </summary>
    [Fact]
    public void RemainingCompatibilityAuthoritiesHaveClosedProductionCallerSets()
    {
        AssertProductionCallers(
            "CompositionProfileCompiler",
            "src/NvtFwCombiner.Profiles/CompositionProfileCompiler",
            []);
        AssertProductionCallers(
            "BuiltInTpFlashMapCatalog",
            "src/NvtFwCombiner.Infrastructure/FlashMaps/BuiltInTpFlashMapCatalog",
            [
                "src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.cs",
                "src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.cs",
                "src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.Context.cs",
                "src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.FirmwareVersion.cs",
            ]);
        AssertNoProductionText("IcMetadataFacade");
        AssertNoProductionText("LegacyCombinerPostbuildCatalog");
        AssertProductionCallers(
            "BuiltInPostbuildProfileCatalog",
            "src/NvtFwCombiner.Infrastructure/ExternalTools/BuiltInPostbuildProfileCatalog",
            [
                "src/NvtFwCombiner.Infrastructure/Composition/BuiltInGeneralAuthoringPlanner.cs",
                "src/NvtFwCombiner.Infrastructure/Composition/CanonicalCapabilityDisclosureInventory.cs",
                "src/NvtFwCombiner.Infrastructure/Composition/CanonicalDynamicRouteInventory.cs",
                "src/NvtFwCombiner.Infrastructure/Composition/BuiltInCtrlRamAuthoringAdapter.Context.cs",
                "src/NvtFwCombiner.Infrastructure/Composition/BuiltInPostbuildProfileResolver.cs",
                "src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.Metadata.cs",
                "src/NvtFwCombiner.Infrastructure/Composition/BuiltInFirmwareInspection.cs",
            ]);
        AssertNoProductionText("CurrentSupportMatrixCatalog");
    }

    /// <summary>Prevents zero-caller Workbench planning and selector helpers from returning.</summary>
    [Fact]
    public void RetiredInfrastructurePlanningHelpersStayAbsent()
    {
        string registrationRegistry = ReadText(
            "src/NvtFwCombiner.Infrastructure/Composition/BuiltInV2RegistrationRegistry.cs");
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Infrastructure",
            "Composition",
            "BuiltInCtrlRamAuthoringAdapter.Planning.cs")));
        AssertNoProductionText("CreateCtrlRamReplaceBindings");
        AssertNoProductionText("CreateCtrlRamPlanningOperations");
        Assert.DoesNotContain("MatchesSelector", registrationRegistry, StringComparison.Ordinal);
        AssertNoProductionText("IsBuiltInV2StandardMergeMapCapacityPending");
        AssertNoProductionText("FormatStandardMergeSupportedDpLengths");
    }

    /// <summary>Prevents zero-caller compatibility and test shortcuts from returning around canonical owners.</summary>
    [Fact]
    public void RetiredZeroCallerCompatibilityHelpersStayAbsent()
    {
        string reportHexDiffRanges = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReportHexDiffRangeViewModels.cs");
        AssertNoProductionText("GetOrCreateOrNull");
        AssertNoProductionText("CliRunReportWriter");
        AssertNoProductionText("WriteReportFileIfRequestedAsync");
        AssertNoProductionText("SetStartupOptions");
        AssertNoProductionText("ResolveMergeOutputFileNameForSaveAsync");
        AssertNoProductionText("TryCreateAbAFlashCodeDeliveryPlanAsync");
        AssertNoProductionText("AbAFlashCodeDeliveryPrompt");
        Assert.DoesNotContain("FindContaining", reportHexDiffRanges, StringComparison.Ordinal);
        AssertNoProductionText("TryGetBuiltInV2StandardMergeContainerPolicy");
        AssertNoProductionText("TryGetDefaultProfile");
    }

    /// <summary>Prevents the unbound TP root/category projection from returning after report semantics moved to the header catalog.</summary>
    [Fact]
    public void RetiredTpBinaryRootProjectionStaysAbsent()
    {
        AssertNoProductionText("TpBinaryModelCatalog");
        AssertNoProductionText("TpBinaryModel");
        AssertNoProductionText("TpBinaryCategory");
        AssertNoProductionText("TpBinaryAddressAnchor");
    }

    /// <summary>Prevents the retired V1 profile compiler implementation from returning.</summary>
    [Fact]
    public void RetiredLegacyCompilerCapabilitiesStayAbsent()
    {
        Assert.Empty(Directory.EnumerateFiles(
            Path.Combine(Root.FullName, "src", "NvtFwCombiner.Profiles"),
            "CompositionProfileCompiler*.cs"));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "ProfileCompileResult.cs")));
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Profiles",
            "LegacyProfileValidationRequirements.cs")));
    }

    private static void AssertNoProductionText(string token)
    {
        Assert.DoesNotContain(
            Directory.EnumerateFiles(Path.Combine(Root.FullName, "src"), "*.cs", SearchOption.AllDirectories),
            path => File.ReadAllText(path).Contains(token, StringComparison.Ordinal));
    }

    private static void AssertProductionCallers(
        string authorityName,
        string definitionPathPrefix,
        IReadOnlyList<string> expectedCallers)
    {
        string sourceRoot = Path.Combine(Root.FullName, "src");
        string[] actualCallers =
        [
            .. Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
                .Where(path => File.ReadAllText(path).Contains(authorityName, StringComparison.Ordinal))
                .Select(path => Path.GetRelativePath(Root.FullName, path).Replace('\\', '/'))
                .Where(path => !path.StartsWith(definitionPathPrefix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Equal(expectedCallers.Order(StringComparer.Ordinal), actualCallers);
    }
}
