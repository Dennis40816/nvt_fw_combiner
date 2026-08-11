namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    private const int MaximumFocusedBootstrapPartialTypeLines = 3_000;

    /// <summary>The migration-only Workbench facade and its parallel support catalog cannot return.</summary>
    [Fact]
    public void WorkbenchFacadeAndParallelSupportCatalogAreRetired()
    {
        string bootstrapDirectory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap");
        string applicationSupportDirectory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Application",
            "Support");
        string infrastructureSupportDirectory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Infrastructure",
            "Support");

        Assert.Empty(Directory.GetFiles(
            bootstrapDirectory,
            "WorkbenchCompositionService*.cs",
            SearchOption.TopDirectoryOnly));
        Assert.False(File.Exists(Path.Combine(
            bootstrapDirectory,
            "AbMergeWorkbenchCompositionService.cs")));
        Assert.Empty(Directory.GetFiles(
            bootstrapDirectory,
            "*Facade*.cs",
            SearchOption.TopDirectoryOnly));
        Assert.Empty(Directory.GetFiles(
            Path.Combine(Root.FullName, "src"),
            "*AuthoringCatalog*.cs",
            SearchOption.AllDirectories));
        Assert.False(Directory.Exists(applicationSupportDirectory));
        Assert.False(Directory.Exists(infrastructureSupportDirectory));

        string production = ReadProductionSources();
        Assert.DoesNotContain("workbench", production, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("WorkbenchCompositionService", production, StringComparison.Ordinal);
        Assert.DoesNotContain("AbMergeWorkbenchCompositionService", production, StringComparison.Ordinal);
        Assert.DoesNotContain("NvtFwCombiner.Application.Support", production, StringComparison.Ordinal);
        Assert.DoesNotContain("SupportRouteIdentity", production, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchWorkflowReadiness", production, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchWorkflowEvidenceStatus", production, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchIcFamilySummary", production, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkbenchIcFamilyRelationship", production, StringComparison.Ordinal);

        Assert.Contains("public sealed partial class AuthoringSessionState", production, StringComparison.Ordinal);
        Assert.Contains("public sealed class CanonicalCapabilityCatalog", production, StringComparison.Ordinal);
        Assert.Contains("public sealed record CapabilityWorkflowReadiness", production, StringComparison.Ordinal);
        Assert.Contains("public sealed record CapabilityFamilySummary", production, StringComparison.Ordinal);
        Assert.Contains("public sealed partial class CompositionRunService", production, StringComparison.Ordinal);
        Assert.Contains(
            "public static partial class MemoryLayoutProjector",
            production,
            StringComparison.Ordinal);
        Assert.Contains("public interface ICanonicalSupportMatrixQuery", production, StringComparison.Ordinal);
    }

    /// <summary>Application memory contracts stay semantic; rendering remains Presentation-owned.</summary>
    [Fact]
    public void ApplicationMemoryContractsContainNoRenderingTokens()
    {
        string contracts = string.Join(
            Environment.NewLine,
            ReadText("src/NvtFwCombiner.Application/Composition/CompositionClientModels.cs"),
            ReadText("src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs"));

        Assert.DoesNotContain("BarWidth", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("CoverageFill", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("300d", contracts, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"#[0-9A-Fa-f]{6}(?:[0-9A-Fa-f]{2})?\b", contracts);
    }

    /// <summary>Execution exposes runs only; memory, naming, and delivery planning stay focused.</summary>
    [Fact]
    public void ApplicationCompositionPortsKeepExecutionFocused()
    {
        string contracts = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExperiencePorts.cs");
        string naming = SliceInterface(contracts, "ICompositionOutputNaming", "ICompositionExecution");
        string execution = contracts[contracts.IndexOf(
            "public interface ICompositionExecution",
            StringComparison.Ordinal)..];

        Assert.DoesNotContain("ICompositionMemoryPresentation", contracts, StringComparison.Ordinal);
        Assert.Contains("PrepareAutomaticOutputAsync", naming, StringComparison.Ordinal);
        Assert.Contains("CompositionOutputPreparation", naming, StringComparison.Ordinal);
        Assert.DoesNotContain("ProjectMemoryLayout", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareAutomaticOutputAsync", execution, StringComparison.Ordinal);
        Assert.Contains("ExecuteAsync", execution, StringComparison.Ordinal);
        Assert.Contains("AcceptedCompositionExecutionRequest", execution, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(execution, "ValueTask<CompositionRunResult>"));

        string namingImplementation = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionOutputNamingExperience.cs");
        Assert.DoesNotContain("CompositionExecutionAdapter", namingImplementation, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "AbMergeDeliveryPlanningPort.cs")));

        string executionImplementation = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(Root.FullName, "src", "NvtFwCombiner.Bootstrap"),
                    "CompositionExecutionAdapter*.cs",
                    SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));
        Assert.DoesNotContain("GetDpReplaceInputSlots", executionImplementation, StringComparison.Ordinal);
        Assert.DoesNotContain("PrepareAutomaticOutputAsync", executionImplementation, StringComparison.Ordinal);
    }

    /// <summary>Prevents the retired facade from returning under a new broad partial-type name.</summary>
    [Fact]
    public void FocusedBootstrapAdaptersCannotRegrowAReplacementGodFacade()
    {
        string bootstrapDirectory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap");
        string[] files = Directory.GetFiles(
            bootstrapDirectory,
            "*.cs",
            SearchOption.TopDirectoryOnly);
        Dictionary<string, List<string>> partialTypeFiles = new(StringComparer.Ordinal);
        foreach (string path in files)
        {
            foreach (System.Text.RegularExpressions.Match match in
                     BootstrapPartialTypeDeclarationRegex().Matches(File.ReadAllText(path)))
            {
                string name = match.Groups["name"].Value;
                if (!partialTypeFiles.TryGetValue(name, out List<string>? typeFiles))
                {
                    typeFiles = [];
                    partialTypeFiles.Add(name, typeFiles);
                }

                typeFiles.Add(path);
            }
        }

        foreach (KeyValuePair<string, List<string>> partialType in partialTypeFiles)
        {
            int nonblankLineCount = partialType.Value
                .Distinct(StringComparer.Ordinal)
                .Sum(path => File.ReadLines(path).Count(line => !string.IsNullOrWhiteSpace(line)));
            Assert.True(
                nonblankLineCount <= MaximumFocusedBootstrapPartialTypeLines,
                $"Bootstrap partial type {partialType.Key} has {nonblankLineCount} nonblank lines; " +
                $"the focused-owner ceiling is {MaximumFocusedBootstrapPartialTypeLines}.");
        }

        string production = ReadProductionSources();
        Assert.DoesNotContain("CompositionPlanningAdapter", production, StringComparison.Ordinal);
        Assert.DoesNotContain("WorkflowExecutionService", production, StringComparison.Ordinal);
    }

    /// <summary>Locks compiler and capability-fact ownership to the reviewed canonical owners.</summary>
    [Fact]
    public void RetirementDoesNotCreateParallelCompilerOrCapabilityFactOwners()
    {
        string production = ReadProductionSources();
        string[] compilerTypes =
        [
            .. CompilerTypeDeclarationRegex().Matches(production)
                .Select(match => match.Groups["name"].Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        Assert.Equal(
            ["LegacyCombinerPostbuildPlanCompiler", "V2CompositionPlanCompiler"],
            compilerTypes);

        Assert.Empty(WorkflowSpecificExecutionTypeRegex().Matches(production));

        string[] capabilityFactOwners =
        [
            .. CapabilityFactOwnerDeclarationRegex().Matches(production)
                .Select(match => match.Groups["name"].Value)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        Assert.Equal(
            ["BuiltInCanonicalCapabilityPolicy", "CanonicalCapabilityCatalog"],
            capabilityFactOwners);
    }

    [System.Text.RegularExpressions.GeneratedRegex(
        @"(?m)^\s*(?:public|internal)\s+(?:static\s+)?(?:sealed\s+)?partial\s+class\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex BootstrapPartialTypeDeclarationRegex();

    [System.Text.RegularExpressions.GeneratedRegex(
        @"\b(?:class|record|interface)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*Compiler)\b",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex CompilerTypeDeclarationRegex();

    [System.Text.RegularExpressions.GeneratedRegex(
        @"\b(?:class|record|interface)\s+(?<name>(?:StandardMerge|AbMerge|DpReplace|CtrlRamReplace|GeneralMerge|GeneralReplace)[A-Za-z0-9_]*(?:ExecutionService|Compiler))\b",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex WorkflowSpecificExecutionTypeRegex();

    [System.Text.RegularExpressions.GeneratedRegex(
        @"\b(?:class|record|interface)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*(?:Capability|Support)[A-Za-z0-9_]*(?:Catalog|Policy|Matrix|Registry))\b",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant)]
    private static partial System.Text.RegularExpressions.Regex CapabilityFactOwnerDeclarationRegex();

    private static string SliceInterface(string source, string startName, string endName)
    {
        int start = source.IndexOf($"public interface {startName}", StringComparison.Ordinal);
        int end = source.IndexOf($"public interface {endName}", start, StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, $"Could not slice {startName} before {endName}.");
        return source[start..end];
    }
}
