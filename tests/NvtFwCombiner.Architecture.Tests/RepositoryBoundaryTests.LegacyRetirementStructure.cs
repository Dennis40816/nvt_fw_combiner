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
            [
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.General.cs",
            ]);
        AssertProductionCallers(
            "BuiltInTpFlashMapCatalog",
            "src/NvtFwCombiner.Infrastructure/FlashMaps/BuiltInTpFlashMapCatalog",
            [
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.CtrlRamDisplay.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Coverage.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.Context.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.Planning.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Display.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.General.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.General.Profile.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.cs",
            ]);
        AssertProductionCallers(
            "IcMetadataFacade",
            "src/NvtFwCombiner.Bootstrap/IcMetadataFacade",
            [
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Catalog.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.FirmwareMetadata.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.Context.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.CtrlRam.Planning.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.General.Context.cs",
                "src/NvtFwCombiner.Bootstrap/WorkbenchCompositionService.Replace.Postbuild.cs",
            ]);
        AssertNoProductionText("LegacyCombinerPostbuildCatalog");
        AssertProductionCallers(
            "BuiltInPostbuildProfileCatalog",
            "src/NvtFwCombiner.Infrastructure/ExternalTools/BuiltInPostbuildProfileCatalog",
            [
                "src/NvtFwCombiner.Bootstrap/ExternalProcessorFactory.cs",
                "src/NvtFwCombiner.Bootstrap/IcMetadataFacade.cs",
            ]);
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
