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
        Assert.Contains("public static class MemoryLayoutProjector", production, StringComparison.Ordinal);
        Assert.Contains("public interface ICanonicalSupportMatrixQuery", production, StringComparison.Ordinal);
    }

    /// <summary>Application memory contracts stay semantic; rendering remains Presentation-owned.</summary>
    [Fact]
    public void ApplicationMemoryContractsContainNoRenderingTokens()
    {
        string contracts = string.Join(
            Environment.NewLine,
            ReadText("src/NvtFwCombiner.Application/Composition/WorkbenchCompositionModels.cs"),
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
        string memory = SliceInterface(contracts, "ICompositionMemoryPresentation", "IFirmwareInspection");
        string naming = SliceInterface(contracts, "ICompositionOutputNaming", "IAbMergeDeliveryPlanning");
        string delivery = SliceInterface(contracts, "IAbMergeDeliveryPlanning", "ICompositionExecution");
        string execution = contracts[contracts.IndexOf(
            "public interface ICompositionExecution",
            StringComparison.Ordinal)..];

        Assert.Contains("GetDpReplaceReferenceCapacityLabel", memory, StringComparison.Ordinal);
        Assert.Contains("ResolveAutomaticOutputFileNameAsync", naming, StringComparison.Ordinal);
        Assert.Contains("TryCreateAFlashCodeDeliveryPlanAsync", delivery, StringComparison.Ordinal);
        Assert.DoesNotContain("GetDpReplaceReferenceCapacityLabel", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveAutomaticOutputFileNameAsync", execution, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCreateAFlashCodeDeliveryPlanAsync", execution, StringComparison.Ordinal);
        Assert.Contains("RunStandardMergeAcceptedSessionWithProgressAsync", execution, StringComparison.Ordinal);
        Assert.Contains("RunReplaceAcceptedSessionWithProgressAsync", execution, StringComparison.Ordinal);

        string namingImplementation = ReadText(
            "src/NvtFwCombiner.Bootstrap/CompositionExperienceAdapters.cs");
        namingImplementation = namingImplementation[namingImplementation.IndexOf(
            "internal sealed class CompositionOutputNamingAdapter",
            StringComparison.Ordinal)..];
        string deliveryImplementation = ReadText(
            "src/NvtFwCombiner.Bootstrap/AbMergeDeliveryPlanningPort.cs");
        Assert.DoesNotContain("CompositionExecutionAdapter", namingImplementation, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionExecutionAdapter", deliveryImplementation, StringComparison.Ordinal);

        string executionImplementation = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(Root.FullName, "src", "NvtFwCombiner.Bootstrap"),
                    "CompositionExecutionAdapter*.cs",
                    SearchOption.TopDirectoryOnly)
                .Select(File.ReadAllText));
        Assert.DoesNotContain("GetDpReplaceInputSlots", executionImplementation, StringComparison.Ordinal);
        Assert.DoesNotContain("ResolveAutomaticOutputFileNameAsync", executionImplementation, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCreateAFlashCodeDeliveryPlanAsync", executionImplementation, StringComparison.Ordinal);
    }

    /// <summary>DP Replace preserves compiler slot identity separately from its bound address space end to end.</summary>
    [Fact]
    public void DpReplaceSlotAndAddressSpaceIdentitiesStayExplicitEndToEnd()
    {
        string application = ReadText(
            "src/NvtFwCombiner.Application/Authoring/CompiledAuthoringWorkflow.cs");
        string acceptedBinding = ReadText(
            "src/NvtFwCombiner.Bootstrap/AcceptedAuthoringSessionBinding.cs");
        string ctrlRamBindings = ReadText(
            "src/NvtFwCombiner.Bootstrap/CompositionPlanningAdapter.Replace.CtrlRam.Context.cs");
        string slotViewModel = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/FirmwareSlotViewModel.cs");
        string replaceAuthoring = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Authoring.cs");
        string replaceExecution = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/ViewModels/ReplacePresentationViewModel.Execution.cs");
        string replaceProjection = ReadText(
            "src/NvtFwCombiner.Presentation.Avalonia/UiCompositionRunner.Replace.cs");

        Assert.Contains(
            "CompiledAuthoringInputBinding(string SlotId, string AddressSpaceId)",
            application,
            StringComparison.Ordinal);
        Assert.Contains("ProjectInputBindings(capability)", application, StringComparison.Ordinal);
        Assert.Contains("ResolveSlotDefinitionId(", acceptedBinding, StringComparison.Ordinal);
        Assert.Contains(
            "compiledComposition.V2Details.InputContract.SpaceBindings",
            acceptedBinding,
            StringComparison.Ordinal);
        Assert.Contains(
            "acceptedSession,\n                        sourceSpaceId)",
            ctrlRamBindings.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
        Assert.Contains("public string? CompiledSlotId { get; }", slotViewModel, StringComparison.Ordinal);
        Assert.Contains("ReplaceDefinitionId(candidate, dpProjection)", replaceAuthoring, StringComparison.Ordinal);
        Assert.Contains("SelectedReplaceMode == CtrlRamReplaceMode", replaceAuthoring, StringComparison.Ordinal);
        Assert.Contains("WorkbenchAddressSpaceIds.ReferenceBase", replaceAuthoring, StringComparison.Ordinal);
        Assert.Contains("binding.AddressSpaceId", replaceAuthoring, StringComparison.Ordinal);
        Assert.Contains("slot.CompiledSlotId", replaceAuthoring, StringComparison.Ordinal);
        Assert.Contains(
            "candidate.SlotId, slot.CompiledSlotId",
            replaceExecution,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "candidate.SlotId, slot.AddressSpaceId",
            replaceExecution,
            StringComparison.Ordinal);
        Assert.Contains("compiledSlotId: slot.CompiledSlotId", replaceProjection, StringComparison.Ordinal);
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
        Assert.Contains(
            "internal static partial class CompositionPlanningAdapter",
            production,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public static partial class CompositionPlanningAdapter",
            production,
            StringComparison.Ordinal);
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
            ["V2CompositionPlanCompiler"],
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
