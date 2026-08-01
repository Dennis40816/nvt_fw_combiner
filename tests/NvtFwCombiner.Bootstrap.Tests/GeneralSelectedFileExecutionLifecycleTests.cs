using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Files;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Verifies that desktop-style Preview and Build consume one explicitly
/// accepted General selected-file draft until Reload/Rebind replaces it.
/// </summary>
public sealed class GeneralSelectedFileExecutionLifecycleTests
{
    /// <summary>Strict desktop execution rejects an uninspected draft without touching its path.</summary>
    [Fact]
    public async Task AcceptedDraftRunnerRejectsUnboundSelectedFile()
    {
        var unboundDraft = new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "mapping-1",
                ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(@"C:\does-not-exist\source.bin"),
                new ByteRange(0, 1),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(0, 1),
                OverlapPolicy.Reject,
                alignment: 1,
                "Unbound content snapshot test."),
        ]);

        WorkbenchRunResult result =
            await WorkbenchCompositionService.RunGeneralMergeAcceptedDraftWithProgressAsync(
                "NT51950",
                ResolveGeneralMergeInitializer("0x10"),
                unboundDraft,
                build: false,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Null(result.AcceptedGeneralMappingDraft);
        AssertIssue(
            result,
            GeneralSelectedFileInspectionIssueCodes.SnapshotRequired);
    }

    /// <summary>
    /// General Merge rejects same-size mutation against its Preview snapshot,
    /// then succeeds after explicit Reload without changing the mapping range.
    /// </summary>
    [Fact]
    public async Task GeneralMergePreviewMutationBuildRequiresExplicitReload()
    {
        using var workspace = TempWorkspace.Create("nfc-general-merge-content-lifecycle");
        string sourcePath = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);
        WorkbenchGeneralMergeMappingInput[] inputs =
        [
            new("mapping-1", sourcePath, "0x0", "0x0", "0x4"),
        ];
        WorkbenchRunResult preview =
            await WorkbenchCompositionService.RunGeneralMergeWithProgressAsync(
                "NT51950",
                "0x10",
                inputs,
                build: false,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken);

        Assert.True(preview.Succeeded, preview.ReportJson);
        GeneralMappingDraftState acceptedDraft =
            Assert.IsType<GeneralMappingDraftState>(
                preview.AcceptedGeneralMappingDraft);
        GeneralMappingDraftRow acceptedRow = Assert.Single(acceptedDraft.Rows);
        FileStamp acceptedStamp = Assert.IsType<FileStamp>(
            acceptedRow.Source.AcceptedFileStamp);
        await File.WriteAllBytesAsync(
            sourcePath,
            [0x10, 0x11, 0x99, 0x13],
            TestContext.Current.CancellationToken);
        string staleOutputPath = workspace.PathFor("stale-output.bin");

        WorkbenchRunResult staleBuild =
            await WorkbenchCompositionService.RunGeneralMergeAcceptedDraftWithProgressAsync(
                "NT51950",
                ResolveGeneralMergeInitializer("0x10"),
                acceptedDraft,
                build: true,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken,
                staleOutputPath);

        Assert.False(staleBuild.Succeeded);
        Assert.False(File.Exists(staleOutputPath));
        AssertIssue(
            staleBuild,
            CompositionRunIssueCodes.InputArtifactContentSnapshotMismatch);
        Assert.Equal(
            acceptedStamp,
            Assert.Single(staleBuild.AcceptedGeneralMappingDraft!.Rows)
                .Source.AcceptedFileStamp);

        GeneralSelectedFileDraftInspectionResult reloaded =
            await ReloadAsync(
                workspace.Root,
                acceptedDraft,
                "mapping-1",
                new AuthoringRevision(1));
        Assert.True(reloaded.Succeeded);
        Assert.Equal(new AuthoringRevision(2), reloaded.AuthoringRevision);
        GeneralMappingDraftRow reloadedRow = Assert.Single(reloaded.Draft!.Rows);
        Assert.NotEqual(acceptedStamp, reloadedRow.Source.AcceptedFileStamp);
        Assert.Equal(acceptedRow.SourceRange, reloadedRow.SourceRange);
        Assert.Equal(acceptedRow.TargetRange, reloadedRow.TargetRange);

        string outputPath = workspace.PathFor("output.bin");
        WorkbenchRunResult build =
            await WorkbenchCompositionService.RunGeneralMergeAcceptedDraftWithProgressAsync(
                "NT51950",
                ResolveGeneralMergeInitializer("0x10"),
                reloaded.Draft,
                build: true,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken,
                outputPath);

        Assert.True(build.Succeeded, build.ReportJson);
        Assert.Equal(
            [0x10, 0x11, 0x99, 0x13, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            await File.ReadAllBytesAsync(
                outputPath,
                TestContext.Current.CancellationToken));
        AssertReportInputHash(
            build,
            Path.GetFileName(sourcePath),
            reloadedRow.Source.AcceptedFileStamp!.Value.Sha256);
    }

    /// <summary>
    /// General Replace uses the same accepted-draft lifecycle and never
    /// silently rebinds changed replacement bytes at Build.
    /// </summary>
    [Fact]
    public async Task GeneralReplacePreviewMutationBuildRequiresExplicitReload()
    {
        using var workspace = TempWorkspace.Create("nfc-general-replace-content-lifecycle");
        string basePath = workspace.Write("base.bin", new byte[0x40000]);
        string sourcePath = workspace.Write("source.bin", [0xA5, 0x5A]);
        IReadOnlyDictionary<string, string> slotPaths =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] = basePath,
            };
        WorkbenchGeneralReplaceMappingInput[] inputs =
        [
            new("mapping-1", sourcePath, "0x3E020", "0x3E021"),
        ];
        WorkbenchRunResult preview =
            await WorkbenchCompositionService.RunReplaceWithProgressAsync(
                "NT51926",
                "single",
                WorkbenchReplaceModes.General,
                slotPaths,
                inputs,
                [],
                build: false,
                new CompositionRunProgressFeed(),
                TestContext.Current.CancellationToken);

        Assert.True(preview.Succeeded, preview.ReportJson);
        GeneralMappingDraftState acceptedDraft =
            Assert.IsType<GeneralMappingDraftState>(
                preview.AcceptedGeneralMappingDraft);
        GeneralMappingDraftRow acceptedRow = Assert.Single(acceptedDraft.Rows);
        FileStamp acceptedStamp = Assert.IsType<FileStamp>(
            acceptedRow.Source.AcceptedFileStamp);
        await File.WriteAllBytesAsync(
            sourcePath,
            [0xA5, 0xC3],
            TestContext.Current.CancellationToken);
        string staleOutputPath = workspace.PathFor("stale-output.bin");

        WorkbenchRunResult staleBuild =
            await WorkbenchCompositionService
                .RunGeneralReplaceAcceptedDraftWithProgressAsync(
                    "NT51926",
                    "single",
                    slotPaths,
                    acceptedDraft,
                    build: true,
                    new CompositionRunProgressFeed(),
                    TestContext.Current.CancellationToken,
                    staleOutputPath);

        Assert.False(staleBuild.Succeeded);
        Assert.False(File.Exists(staleOutputPath));
        AssertIssue(
            staleBuild,
            CompositionRunIssueCodes.InputArtifactContentSnapshotMismatch);

        GeneralSelectedFileDraftInspectionResult reloaded =
            await ReloadAsync(
                workspace.Root,
                acceptedDraft,
                "mapping-1",
                new AuthoringRevision(1));
        Assert.True(reloaded.Succeeded);
        Assert.Equal(new AuthoringRevision(2), reloaded.AuthoringRevision);
        GeneralMappingDraftRow reloadedRow = Assert.Single(reloaded.Draft!.Rows);
        Assert.NotEqual(acceptedStamp, reloadedRow.Source.AcceptedFileStamp);
        Assert.Equal(acceptedRow.SourceRange, reloadedRow.SourceRange);
        Assert.Equal(acceptedRow.TargetRange, reloadedRow.TargetRange);

        string outputPath = workspace.PathFor("output.bin");
        WorkbenchRunResult build =
            await WorkbenchCompositionService
                .RunGeneralReplaceAcceptedDraftWithProgressAsync(
                    "NT51926",
                    "single",
                    slotPaths,
                    reloaded.Draft,
                    build: true,
                    new CompositionRunProgressFeed(),
                    TestContext.Current.CancellationToken,
                    outputPath);

        Assert.True(build.Succeeded, build.ReportJson);
        byte[] output = await File.ReadAllBytesAsync(
            outputPath,
            TestContext.Current.CancellationToken);
        Assert.Equal(0xA5, output[0x3E020]);
        Assert.Equal(0xC3, output[0x3E021]);
        AssertReportInputHash(
            build,
            Path.GetFileName(sourcePath),
            reloadedRow.Source.AcceptedFileStamp!.Value.Sha256);
    }

    private static ValueTask<GeneralSelectedFileDraftInspectionResult> ReloadAsync(
        string allowedRoot,
        GeneralMappingDraftState acceptedDraft,
        string definitionId,
        AuthoringRevision currentRevision)
    {
        var service = new GeneralSelectedFileInspectionService(
            new FileContentSnapshotInspector([allowedRoot]));
        return service.ReloadAsync(
            acceptedDraft,
            definitionId,
            currentRevision,
            TestContext.Current.CancellationToken);
    }

    private static void AssertIssue(WorkbenchRunResult result, string issueCode)
    {
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => StringComparer.Ordinal.Equals(
                issue.GetProperty("Code").GetString(),
                issueCode));
    }

    private static WorkbenchGeneralMergeInitializer ResolveGeneralMergeInitializer(
        string outputLength)
    {
        Assert.True(
            WorkbenchCompositionService.TryResolveGeneralMergeOutputInitializer(
                outputLength,
                outputFillByte: null,
                out WorkbenchGeneralMergeInitializer? initializer));
        return initializer;
    }

    private static void AssertReportInputHash(
        WorkbenchRunResult result,
        string originalFileName,
        string expectedSha256)
    {
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement input = Assert.Single(
            report.RootElement.GetProperty("Inputs").EnumerateArray(),
            input => StringComparer.Ordinal.Equals(
                input.GetProperty("OriginalFileName").GetString(),
                originalFileName));
        Assert.Equal(expectedSha256, input.GetProperty("Sha256").GetString());
    }
}
