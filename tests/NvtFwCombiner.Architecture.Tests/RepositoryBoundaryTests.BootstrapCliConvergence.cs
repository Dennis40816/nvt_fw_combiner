namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Bootstrap and CLI keep only production-owned inspection and dispatch entry points.</summary>
    [Fact]
    public void BootstrapCliConvergenceRemovesTestOnlyAndImpossibleSurfaces()
    {
        string bootstrapDirectory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap");
        string bootstrap = string.Join(
            Environment.NewLine,
            Directory.GetFiles(bootstrapDirectory, "*.cs", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));
        string cliDirectory = Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Cli");
        string cliSources = string.Join(
            Environment.NewLine,
            Directory.GetFiles(cliDirectory, "*.cs", SearchOption.TopDirectoryOnly)
                .Order(StringComparer.Ordinal)
                .Select(File.ReadAllText));

        Assert.Empty(Directory.GetFiles(
            bootstrapDirectory,
            "*Cli*.cs",
            SearchOption.TopDirectoryOnly));
        Assert.Empty(Directory.GetFiles(
            bootstrapDirectory,
            "*CommandHandler*.cs",
            SearchOption.TopDirectoryOnly));
        Assert.Contains("namespace NvtFwCombiner.Cli;", cliSources, StringComparison.Ordinal);
        Assert.DoesNotContain("namespace NvtFwCombiner.Bootstrap;", cliSources, StringComparison.Ordinal);

        Assert.DoesNotContain(
            "public static OutputFileNameSuggestion CreateFlashCodeOutputFileName(",
            bootstrap,
            StringComparison.Ordinal);
        Assert.DoesNotContain("private static string? FindDpVersionToken(", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string? FindTpVersionToken(", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("public static FirmwareInspectionSnapshot InspectFirmware(", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("public static DpVersionMetadata? TryReadDpVersionMetadata(", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("public static CmiDpCodeMetadata? TryReadCmiDpCodeMetadata(", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("public static FirmwareContextSuggestion? TryReadFirmwareContextSuggestion(", bootstrap, StringComparison.Ordinal);

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CliApplication.AbMerge.cs")));
        string cli = ReadText("src/NvtFwCombiner.Cli/CliApplication.cs");
        Assert.DoesNotContain("RunAbMergeAsync(args[1..]", cli, StringComparison.Ordinal);
        Assert.Contains("AbMergeCliCommandHandler.RunAsync(", cli, StringComparison.Ordinal);

        string generalMerge = ReadText(
            "src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs");
        Assert.DoesNotContain("string command,", generalMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("unknown merge command", generalMerge, StringComparison.Ordinal);

        string standardMerge = ReadText(
            "src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs");
        Assert.DoesNotContain("CompositionAddressSpaceIds.DpAbInput", standardMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionAddressSpaceIds.TpAInput", standardMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionAddressSpaceIds.TpBInput", standardMerge, StringComparison.Ordinal);

        string sharedExecution = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string standardExecution = sharedExecution;
        string abExecution = sharedExecution;
        Assert.Contains("private ValueTask<CompositionRunResult> ExecuteAcceptedCompositionAsync(", sharedExecution, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(sharedExecution, "AcceptedSessionExecutionInputs.CreateBindings("));
        Assert.Contains("ExecuteAcceptedCompositionAsync(", standardExecution, StringComparison.Ordinal);
        Assert.Contains("ExecuteAcceptedCompositionAsync(", abExecution, StringComparison.Ordinal);
        Assert.DoesNotContain("RunStandardMergeCoreAsync(", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("RunAbMergeCoreAsync(", bootstrap, StringComparison.Ordinal);
        string replaceExecution = sharedExecution;
        Assert.DoesNotContain("RunReplaceCoreAsync(", bootstrap, StringComparison.Ordinal);
        Assert.Contains("ExecuteAcceptedCompositionAsync(", replaceExecution, StringComparison.Ordinal);
        Assert.Contains(
            "private async ValueTask<CompositionRunResult> ExecuteCtrlRamReplaceAsync(",
            sharedExecution,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "RunCtrlRamReplaceWithProcessorAsync(",
            bootstrap,
            StringComparison.Ordinal);

        string generalReplaceExecution = sharedExecution;
        Assert.DoesNotContain("GeneralReplaceEphemeralDraft", generalReplaceExecution, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralReplaceWithInitialInspection", generalReplaceExecution, StringComparison.Ordinal);
        Assert.Contains("private async ValueTask<CompositionRunResult> ExecuteGeneralReplaceAsync(", generalReplaceExecution, StringComparison.Ordinal);
        Assert.Contains("AcceptedSessionExecutionInputs.CreateGeneralReplaceBindings(", generalReplaceExecution, StringComparison.Ordinal);
        Assert.DoesNotContain("GeneralReplaceRunActionStrategy", generalReplaceExecution, StringComparison.Ordinal);
        Assert.DoesNotContain("TryPlanGeneralReplacePostbuild", generalReplaceExecution, StringComparison.Ordinal);
        Assert.DoesNotContain("TryCreateGeneralReplaceMappings", generalReplaceExecution, StringComparison.Ordinal);
        Assert.DoesNotContain("new AuthoringRevision(0)", bootstrap, StringComparison.Ordinal);
    }
}
