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
        string[] broadHostHandlers =
        [
            .. Directory.GetFiles(cliDirectory, "*.cs", SearchOption.TopDirectoryOnly)
                .Where(path => !StringComparer.Ordinal.Equals(
                    Path.GetFileName(path),
                    "CliApplication.cs"))
                .Where(path => File.ReadAllText(path).Contains(
                    "CompositionHostServices",
                    StringComparison.Ordinal))
                .Select(path => Path.GetFileName(path) ??
                    throw new InvalidOperationException("CLI source path has no file name."))
                .Order(StringComparer.Ordinal),
        ];

        Assert.Empty(Directory.GetFiles(
            bootstrapDirectory,
            "*Cli*.cs",
            SearchOption.TopDirectoryOnly));
        Assert.Empty(Directory.GetFiles(
            bootstrapDirectory,
            "*CommandHandler*.cs",
            SearchOption.TopDirectoryOnly));
        AssertContainsAll(cliSources, "namespace NvtFwCombiner.Cli;");
        AssertDoesNotContainAny(cliSources, "namespace NvtFwCombiner.Bootstrap;", "ReplaceRunAttempt");
        Assert.Empty(broadHostHandlers);
        AssertDoesNotContainAny(bootstrap, "WarmCanonicalCapabilities", "StartCanonicalCatalogLoad",
            "public static OutputFileNameSuggestion CreateFlashCodeOutputFileName(",
            "private static string? FindDpVersionToken(", "private static string? FindTpVersionToken(",
            "public static FirmwareInspectionSnapshot InspectFirmware(",
            "public static DpVersionMetadata? TryReadDpVersionMetadata(",
            "public static CmiDpCodeMetadata? TryReadCmiDpCodeMetadata(",
            "public static FirmwareContextSuggestion? TryReadFirmwareContextSuggestion(");

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CliApplication.AbMerge.cs")));
        string cli = ReadText("src/NvtFwCombiner.Cli/CliApplication.cs");
        AssertDoesNotContainAny(cli, "RunAbMergeAsync(args[1..]");
        AssertContainsAll(cli, "AbMergeCliCommandHandler.RunAsync(");

        string generalMerge = ReadText(
            "src/NvtFwCombiner.Cli/MergeCliCommandHandler.cs");
        AssertDoesNotContainAny(generalMerge, "string command,", "unknown merge command");

        string standardMerge = ReadText(
            "src/NvtFwCombiner.Cli/CliApplication.StandardMerge.cs");
        AssertDoesNotContainAny(standardMerge, "CompositionAddressSpaceIds.DpAbInput",
            "CompositionAddressSpaceIds.TpAInput", "CompositionAddressSpaceIds.TpBInput");

        string sharedExecution = ReadText(
            "src/NvtFwCombiner.Application/Composition/CompositionExecutionExperience.cs");
        string standardExecution = sharedExecution;
        string abExecution = sharedExecution;
        AssertContainsAll(sharedExecution,
            "private ValueTask<CompositionRunResult> ExecuteAcceptedCompositionAsync(",
            "internal async ValueTask<CompositionRunResult> ExecuteCtrlRamReplaceAsync(",
            "internal async ValueTask<CompositionRunResult> ExecuteGeneralReplaceAsync(",
            "AcceptedSessionExecutionInputs.CreateGeneralReplaceBindings(");
        Assert.Equal(2, CountOccurrences(sharedExecution, "AcceptedSessionExecutionInputs.CreateBindings("));
        AssertContainsAll(standardExecution, "ExecuteAcceptedCompositionAsync(");
        AssertContainsAll(abExecution, "ExecuteAcceptedCompositionAsync(");
        AssertDoesNotContainAny(bootstrap, "RunStandardMergeCoreAsync(", "RunAbMergeCoreAsync(",
            "RunReplaceCoreAsync(", "RunCtrlRamReplaceWithProcessorAsync(",
            "new AuthoringRevision(0)");
        string replaceExecution = sharedExecution;
        AssertContainsAll(replaceExecution, "ExecuteAcceptedCompositionAsync(");

        string generalReplaceExecution = sharedExecution;
        AssertDoesNotContainAny(generalReplaceExecution, "GeneralReplaceEphemeralDraft",
            "GeneralReplaceWithInitialInspection", "GeneralReplaceRunActionStrategy",
            "TryPlanGeneralReplacePostbuild", "TryCreateGeneralReplaceMappings");
    }
}
