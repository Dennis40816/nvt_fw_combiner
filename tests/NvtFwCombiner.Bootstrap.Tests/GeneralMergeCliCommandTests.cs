using System.Text.Json;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.WorkbenchIssueCodes;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI and workbench facade tests for General Merge command groups.</summary>
public sealed class GeneralMergeCliCommandTests
{
    /// <summary>Verifies General Merge build copies explicit source ranges over a blank output image.</summary>
    [Fact]
    public async Task GeneralMergeBuildWritesExplicitMappingsOverBlankOutput()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13, 0x14]);
        string output = workspace.PathFor("out.bin");
        string report = workspace.PathFor("report.json");

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "build",
            "--profile",
            "51950",
            "--size",
            "0x10",
            "--mapping",
            $"0x1+0x4+0x3={source}",
            "--output",
            output,
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Status: Succeeded", result.Output, StringComparison.Ordinal);
        Assert.Contains("Experience: general-merge", result.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("Issues:", result.Error, StringComparison.Ordinal);
        Assert.Equal(
            [0, 0, 0, 0, 0x11, 0x12, 0x13, 0, 0, 0, 0, 0, 0, 0, 0, 0],
            await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement root = document.RootElement;
        Assert.Equal("nt51950-general-merge-workbench", root.GetProperty("ProfileId").GetString());
        Assert.Equal("general-merge", root.GetProperty("ExperienceId").GetString());
        Assert.Equal("Merge", root.GetProperty("CompositionKind").GetString());
        JsonElement operation = Assert.Single(root.GetProperty("Operations").EnumerateArray());
        Assert.Equal("general-merge-map-1", operation.GetProperty("OperationId").GetString());
        Assert.Equal("CopyRange", operation.GetProperty("Kind").GetString());
        Assert.Equal("Succeeded", operation.GetProperty("Status").GetString());
        Assert.Equal(1, operation.GetProperty("SourceRange").GetProperty("Start").GetInt64());
        Assert.Equal(3, operation.GetProperty("SourceRange").GetProperty("Length").GetInt64());
        Assert.Equal(4, operation.GetProperty("TargetRange").GetProperty("Start").GetInt64());
        Assert.Equal(3, operation.GetProperty("TargetRange").GetProperty("Length").GetInt64());
        JsonElement provenance = operation.GetProperty("Provenance");
        Assert.Equal("runtime-general-mapping", provenance.GetProperty("Kind").GetString());
        Assert.Equal("general-merge-map-1", provenance.GetProperty("SourceId").GetString());
    }

    /// <summary>Rejects General Merge requests without explicit mappings.</summary>
    [Fact]
    public async Task GeneralMergePreviewRejectsMissingMapping()
    {
        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
            "--size",
            "0x10",
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("at least one --mapping", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects output paths on preview because preview must not commit firmware bytes.</summary>
    [Fact]
    public async Task GeneralMergePreviewRejectsOutputOption()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10]);

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
            "--size",
            "0x10",
            "--mapping",
            $"0x0+0x0+0x1={source}",
            "--output",
            workspace.PathFor("out.bin"),
        ]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("unknown option '--output'", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Reports source bounds violations before running the composition executor.</summary>
    [Fact]
    public async Task GeneralMergePreviewRejectsSourceRangePastInputLength()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10, 0x11]);

        WorkbenchRunResult result = await WorkbenchCompositionService.RunGeneralMergeAsync(
            "NT51950",
            "0x10",
            [new WorkbenchGeneralMergeMappingInput("general-merge-map-1", source, "0x1", "0x4", "0x3")],
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(GeneralMergeSourceOutOfBounds, issue.GetProperty("Code").GetString());
    }

    /// <summary>Prints General Merge issues directly when no report path is requested.</summary>
    [Fact]
    public async Task GeneralMergePreviewPrintsIssuesWhenReportIsNotRequested()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10, 0x11]);

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "NT51950",
            "--size",
            "0x10",
            "--mapping",
            $"0x1+0x4+0x3={source}",
        ]);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("General Merge failed; no JSON report was written. Issues:", result.Error, StringComparison.Ordinal);
        Assert.Contains(GeneralMergeSourceOutOfBounds, result.Error, StringComparison.Ordinal);
        Assert.Contains("source range exceeds the selected input file length", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Turns a blank output length into a report issue instead of throwing.</summary>
    [Fact]
    public async Task GeneralMergeBlankOutputLengthProducesReportIssue()
    {
        WorkbenchRunResult result = await WorkbenchCompositionService.RunGeneralMergeAsync(
            "NT51950",
            "",
            [],
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(GeneralMergeCapacityInvalid, issue.GetProperty("Code").GetString());
    }

    /// <summary>Preserves no-overwrite behavior at the final commit boundary.</summary>
    [Fact]
    public async Task GeneralMergeBuildDoesNotOverwriteExistingOutputWhenOverwriteIsFalse()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10, 0x11, 0x12]);
        string output = workspace.Write("out.bin", [0xFE, 0xED]);

        _ = await Assert.ThrowsAsync<IOException>(() =>
            WorkbenchCompositionService.RunGeneralMergeAsync(
                    "NT51950",
                    "0x4",
                    [new WorkbenchGeneralMergeMappingInput("general-merge-map-1", source, "0x0", "0x0", "0x2")],
                    build: true,
                    TestContext.Current.CancellationToken,
                    output,
                    overwrite: false)
                .AsTask());

        Assert.Equal([0xFE, 0xED], await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));
    }

    /// <summary>Keeps adjacent General Merge mappings visually distinct even when they use the same color.</summary>
    [Fact]
    public void GeneralMergeCoverageKeepsAdjacentMappingRowsDistinct()
    {
        IReadOnlyList<WorkbenchMemoryCoverageSegment> segments = WorkbenchCompositionService.GetGeneralMergeCoverageSegments(
            "0x10",
            [
                new WorkbenchGeneralMergeMappingInput("general-merge-map-1", "first.bin", "0x0", "0x0", "0x4"),
                new WorkbenchGeneralMergeMappingInput("general-merge-map-2", "second.bin", "0x0", "0x4", "0x4"),
            ]);

        WorkbenchMemoryCoverageSegment[] changed = [.. segments.Where(segment => segment.IsChanged)];
        Assert.Equal(2, changed.Length);
        Assert.Contains("general-merge-map-1", changed[0].Detail, StringComparison.Ordinal);
        Assert.Contains("general-merge-map-2", changed[1].Detail, StringComparison.Ordinal);
    }

    /// <summary>Rejects overlapping General Merge target mappings through the shared profile compiler.</summary>
    [Fact]
    public async Task GeneralMergePreviewRejectsOverlappingTargetMappings()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);

        WorkbenchRunResult result = await WorkbenchCompositionService.RunGeneralMergeAsync(
            "NT51950",
            "0x10",
            [
                new WorkbenchGeneralMergeMappingInput("general-merge-map-1", source, "0x0", "0x4", "0x3"),
                new WorkbenchGeneralMergeMappingInput("general-merge-map-2", source, "0x1", "0x5", "0x2"),
            ],
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        using var document = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(document.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal("profile.plan.invalid", issue.GetProperty("Code").GetString());
        Assert.Contains("overlaps earlier operation", issue.GetProperty("Message").GetString(), StringComparison.Ordinal);
    }

    /// <summary>Verifies each explicitly admitted V2 candidate preserves legacy bytes without changing the default General Merge route.</summary>
    [Theory]
    [InlineData("NT51920", "nt51920-general-merge-logical-candidate")]
    [InlineData("NT51917", "nt51917-general-merge-logical-candidate")]
    [InlineData("NT51919", "nt51919-general-merge-logical-candidate")]
    [InlineData("NT51929", "nt51929-general-merge-logical-candidate")]
    [InlineData("NT51932", "nt51932-general-merge-logical-candidate")]
    [InlineData("NT51923", "nt51923-general-merge-logical-candidate")]
    [InlineData("NT51926", "nt51926-general-merge-logical-candidate")]
    [InlineData("NT51927", "nt51927-general-merge-logical-candidate")]
    public async Task GeneralMergeV2CandidateMatchesLegacyBytesWithoutChangingDefaultRoute(
        string icId,
        string candidateProfileId)
    {
        ArgumentNullException.ThrowIfNull(icId);
        ArgumentNullException.ThrowIfNull(candidateProfileId);

        using var workspace = TempWorkspace.Create();
        string firstSource = workspace.Write("first.bin", [0x10, 0x11, 0x12, 0x13]);
        string secondSource = workspace.Write("second.bin", [0x20, 0x21, 0x22]);
        WorkbenchGeneralMergeMappingInput[] mappings =
        [
            new("general-merge-map-1", firstSource, "0x1", "0x0", "0x3"),
            new("general-merge-map-2", secondSource, "0x0", "0x6", "0x2"),
            new("general-merge-map-3", firstSource, "0x0", "0x8", "0x1"),
        ];
        string legacyOutput = workspace.PathFor("legacy.bin");
        string candidateOutput = workspace.PathFor("candidate.bin");

        WorkbenchRunResult legacy = await WorkbenchCompositionService.RunGeneralMergeAsync(
            icId,
            "0x10",
            mappings,
            build: true,
            TestContext.Current.CancellationToken,
            legacyOutput);
        WorkbenchRunResult candidate = await WorkbenchCompositionService.RunGeneralMergeV2CandidateAsync(
            icId,
            "0x10",
            mappings,
            build: true,
            TestContext.Current.CancellationToken,
            candidateOutput);

        Assert.True(legacy.Succeeded);
        Assert.True(candidate.Succeeded);
        Assert.Equal($"{icId.ToLowerInvariant()}-general-merge-workbench", legacy.ProfileId);
        Assert.Equal(candidateProfileId, candidate.ProfileId);
        Assert.Equal(
            await File.ReadAllBytesAsync(legacyOutput, TestContext.Current.CancellationToken),
            await File.ReadAllBytesAsync(candidateOutput, TestContext.Current.CancellationToken));

        using var candidateReport = JsonDocument.Parse(candidate.ReportJson);
        JsonElement root = candidateReport.RootElement;
        Assert.Equal(candidateProfileId, root.GetProperty("ProfileId").GetString());
        Assert.Equal("general-merge", root.GetProperty("ExperienceId").GetString());
        Assert.Equal("Merge", root.GetProperty("CompositionKind").GetString());
        Assert.Equal(3, root.GetProperty("Operations").GetArrayLength());
    }

    /// <summary>Verifies an admitted candidate reports its own profile identity before plan compilation begins.</summary>
    [Fact]
    public async Task GeneralMergeV2CandidateReportsRegisteredProfileForCapacityValidationFailure()
    {
        WorkbenchRunResult result = await WorkbenchCompositionService.RunGeneralMergeV2CandidateAsync(
            "NT51926",
            "",
            [],
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("nt51926-general-merge-logical-candidate", result.ProfileId);
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(report.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(GeneralMergeCapacityInvalid, issue.GetProperty("Code").GetString());
    }

    /// <summary>Verifies the V2 candidate rejects an unadmitted member rather than falling back to legacy General Merge.</summary>
    [Fact]
    public async Task GeneralMergeV2CandidateRejectsUnadmittedMemberWithoutLegacyFallback()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10]);

        WorkbenchRunResult result = await WorkbenchCompositionService.RunGeneralMergeV2CandidateAsync(
            "NT51950",
            "0x10",
            [new WorkbenchGeneralMergeMappingInput("general-merge-map-1", source, "0x0", "0x0", "0x1")],
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("general-merge-logical-output-candidate", result.ProfileId);
        using var report = JsonDocument.Parse(result.ReportJson);
        JsonElement issue = Assert.Single(report.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal("general-merge.v2-candidate.member-not-admitted", issue.GetProperty("Code").GetString());

        WorkbenchRunResult defaultResult = await WorkbenchCompositionService.RunGeneralMergeAsync(
            "NT51950",
            "0x10",
            [new WorkbenchGeneralMergeMappingInput("general-merge-map-1", source, "0x0", "0x0", "0x1")],
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(defaultResult.Succeeded);
        Assert.Equal("nt51950-general-merge-workbench", defaultResult.ProfileId);
    }

    private static Task<CliRunResult> RunCliAsync(string[] args)
    {
        return CliTestHarness.RunAsync(args, TestContext.Current.CancellationToken);
    }
}
