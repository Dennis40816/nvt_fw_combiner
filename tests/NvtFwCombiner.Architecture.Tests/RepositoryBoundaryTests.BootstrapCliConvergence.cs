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

        Assert.DoesNotContain(
            "public static WorkbenchOutputFileNameSuggestion CreateFlashCodeOutputFileName(",
            bootstrap,
            StringComparison.Ordinal);
        Assert.DoesNotContain("private static string? FindDpVersionToken(", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("private static string? FindTpVersionToken(", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("public static WorkbenchFirmwareInspection InspectFirmware(", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("public static WorkbenchDpVersionMetadata? TryReadDpVersionMetadata(", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("public static WorkbenchCmiDpCodeMetadata? TryReadCmiDpCodeMetadata(", bootstrap, StringComparison.Ordinal);
        Assert.DoesNotContain("public static WorkbenchFirmwareContextSuggestion? TryReadFirmwareContextSuggestion(", bootstrap, StringComparison.Ordinal);

        Assert.False(File.Exists(Path.Combine(
            Root.FullName,
            "src",
            "NvtFwCombiner.Bootstrap",
            "CliApplication.AbMerge.cs")));
        string cli = ReadText("src/NvtFwCombiner.Bootstrap/CliApplication.cs");
        Assert.DoesNotContain("RunAbMergeAsync(args[1..]", cli, StringComparison.Ordinal);
        Assert.Contains("AbMergeCliCommandHandler.RunAsync(", cli, StringComparison.Ordinal);

        string generalMerge = ReadText(
            "src/NvtFwCombiner.Bootstrap/MergeCliCommandHandler.cs");
        Assert.DoesNotContain("string command,", generalMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("unknown merge command", generalMerge, StringComparison.Ordinal);

        string standardMerge = ReadText(
            "src/NvtFwCombiner.Bootstrap/CliApplication.StandardMerge.cs");
        Assert.DoesNotContain("CompositionAddressSpaceIds.DpAbInput", standardMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionAddressSpaceIds.TpAInput", standardMerge, StringComparison.Ordinal);
        Assert.DoesNotContain("CompositionAddressSpaceIds.TpBInput", standardMerge, StringComparison.Ordinal);

        Assert.Contains(
            "private static async ValueTask<WorkbenchRunResult> RunStandardMergeCoreAsync(",
            ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.StandardMerge.cs"),
            StringComparison.Ordinal);
        Assert.Contains(
            "private static async ValueTask<WorkbenchRunResult> RunAbMergeCoreAsync(",
            ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.AbMerge.cs"),
            StringComparison.Ordinal);
        Assert.Contains(
            "private static async ValueTask<WorkbenchRunResult> RunReplaceCoreAsync(",
            ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.Replace.cs"),
            StringComparison.Ordinal);
        Assert.Contains(
            "private static async ValueTask<WorkbenchRunResult> RunCtrlRamReplaceWithProcessorCoreAsync(",
            ReadText("src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.Replace.CtrlRam.cs"),
            StringComparison.Ordinal);

        string generalReplaceExecution = ReadText(
            "src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.Replace.cs")
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains(
            "private static async ValueTask<WorkbenchRunResult>\n        RunGeneralReplaceWithInitialInspectionAsync(",
            generalReplaceExecution,
            StringComparison.Ordinal);
        string generalReplaceStrategy = ReadText(
            "src/NvtFwCombiner.Bootstrap/CompositionExecutionAdapter.Replace.General.cs");
        Assert.Contains("private static readonly GeneralReplaceRunActionStrategy PreviewGeneralReplaceStrategy", generalReplaceStrategy, StringComparison.Ordinal);
        Assert.Contains("private static readonly GeneralReplaceRunActionStrategy BuildGeneralReplaceStrategy", generalReplaceStrategy, StringComparison.Ordinal);
        Assert.Contains("private sealed record GeneralReplaceRunActionStrategy(", generalReplaceStrategy, StringComparison.Ordinal);
        Assert.Contains("private sealed record GeneralReplaceRunFailure(", generalReplaceStrategy, StringComparison.Ordinal);
        Assert.Contains("private sealed record GeneralReplacePostbuildUnavailable(", generalReplaceStrategy, StringComparison.Ordinal);
        Assert.Contains("private sealed record GeneralReplacePreparedRun(", generalReplaceStrategy, StringComparison.Ordinal);
        Assert.DoesNotContain("new AuthoringRevision(0)", bootstrap, StringComparison.Ordinal);
    }
}
