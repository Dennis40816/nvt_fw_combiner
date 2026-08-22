using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Application.Composition.CompositionPlanningIssueCodes;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>CLI and workbench facade tests for General Merge command groups.</summary>
public sealed partial class GeneralMergeCliCommandTests
{
    /// <summary>Verifies an explicit arbitrary blank byte initializes every unmapped output byte.</summary>
    [Theory]
    [InlineData("0x00", 0x00)]
    [InlineData("0x5A", 0x5A)]
    [InlineData("0xFF", 0xFF)]
    public async Task GeneralMergeBuildUsesCanonicalFillByte(
        string fillText,
        int expectedFill)
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10, 0x11]);
        string output = workspace.PathFor("out.bin");
        string report = workspace.PathFor("report.json");

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "build",
            "--profile",
            "51950",
            "--size",
            "0x4",
            "--fill",
            fillText,
            "--mapping",
            $"0x0+0x1+0x2={source}",
            "--output",
            output,
            "--report",
            report,
        ]);

        Assert.Equal(0, result.ExitCode);
        Assert.Equal(
            [checked((byte)expectedFill), 0x10, 0x11, checked((byte)expectedFill)],
            await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(
            report,
            TestContext.Current.CancellationToken));
        JsonElement initialization = document.RootElement.GetProperty("ImageInitialization");
        Assert.Equal("Blank", initialization.GetProperty("Kind").GetString());
        Assert.Equal(4, initialization.GetProperty("Capacity").GetInt64());
        Assert.Equal(expectedFill, initialization.GetProperty("FillByte").GetInt32());
    }

    /// <summary>Verifies the shared initializer validation rejects fill values outside one byte.</summary>
    [Theory]
    [InlineData("-1")]
    [InlineData("0x100")]
    [InlineData("not-a-byte")]
    public async Task GeneralMergeCliRejectsInvalidFillThroughCanonicalValidation(string fillText)
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10]);

        CliRunResult result = await RunCliAsync([
            "general-merge",
            "preview",
            "--profile",
            "51950",
            "--size",
            "0x4",
            "--fill",
            fillText,
            "--mapping",
            $"0x0+0x0+0x1={source}",
        ]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ui.general-merge.fill-byte-invalid", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Capacity and fill both bind Preview and report identity.</summary>
    [Fact]
    public async Task GeneralMergePreviewIdentityIncludesCompleteInitializer()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10]);
        AuthoringMappingState[] mappings =
        [
            Mapping("general-merge-map-1", source, "0x0", "0x0", "0x1"),
        ];

        CompositionRunResult zero = await GeneralWorkflowTestSupport.RunGeneralMergeAsync(BootstrapTestHost.Canonical,
            "NT51950",
            GeneralTestDraftFactory.CreateMergeDraft("0x4", mappings, "0x00"),
            build: false,
            TestContext.Current.CancellationToken);
        CompositionRunResult ff = await GeneralWorkflowTestSupport.RunGeneralMergeAsync(BootstrapTestHost.Canonical,
            "NT51950",
            GeneralTestDraftFactory.CreateMergeDraft("0x4", mappings, "0xFF"),
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(zero.Succeeded);
        Assert.True(ff.Succeeded);
        Assert.NotNull(zero.PreviewToken);
        Assert.NotNull(ff.PreviewToken);
        Assert.NotEqual(zero.PreviewToken, ff.PreviewToken);
        using var zeroReport = JsonDocument.Parse(CompositionRunReportJson.Serialize(zero));
        using var ffReport = JsonDocument.Parse(CompositionRunReportJson.Serialize(ff));
        Assert.NotEqual(
            zeroReport.RootElement.GetProperty("CompilationFingerprint").GetString(),
            ffReport.RootElement.GetProperty("CompilationFingerprint").GetString());
        Assert.Equal(
            0xFF,
            ffReport.RootElement.GetProperty("ImageInitialization")
                .GetProperty("FillByte").GetInt32());
    }

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
        Assert.Equal("nt51950-general-merge-logical-candidate", root.GetProperty("ProfileId").GetString());
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

        GeneralAuthoringSessionPreparation prepared =
            await GeneralWorkflowTestSupport.PrepareGeneralMergeAsync(BootstrapTestHost.Canonical,
            "NT51950",
            GeneralTestDraftFactory.CreateMergeDraft(
                "0x10",
                [Mapping("general-merge-map-1", source, "0x1", "0x4", "0x3")]),
            TestContext.Current.CancellationToken);

        Assert.False(prepared.Succeeded);
        Assert.Null(prepared.AcceptedSession);
        CompositionIssue issue = Assert.Single(prepared.Issues);
        Assert.Equal(
            "general.admission.source-out-of-bounds",
            issue.Code);
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
        Assert.Contains(
            "general.admission.source-out-of-bounds",
            result.Error,
            StringComparison.Ordinal);
        Assert.Contains("source [0x1, 0x4) exceeds", result.Error, StringComparison.Ordinal);
    }

    /// <summary>Rejects a blank output length at the canonical Application authoring boundary.</summary>
    [Fact]
    public void GeneralMergeBlankOutputLengthProducesAuthoringIssue()
    {
        bool resolved = new GeneralMergeInitializerInput("").TryResolve(
            out GeneralMergeOutputInitializer? initializer,
            out CompositionIssue? issue);

        Assert.False(resolved);
        Assert.Null(initializer);
        Assert.Equal(GeneralMergeCapacityInvalid, issue?.Code);
    }

    /// <summary>Atomically replaces an unrelated existing composition output.</summary>
    [Fact]
    public async Task GeneralMergeBuildReplacesExistingOutputWithoutOverwriteFlag()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10, 0x11, 0x12]);
        string output = workspace.Write("out.bin", [0xFE, 0xED]);

        CompositionRunResult result = await GeneralWorkflowTestSupport.RunGeneralMergeAsync(BootstrapTestHost.Canonical,
            "NT51950",
            GeneralTestDraftFactory.CreateMergeDraft(
                "0x4",
                [Mapping("general-merge-map-1", source, "0x0", "0x0", "0x2")]),
            build: true,
            TestContext.Current.CancellationToken,
            output);

        Assert.True(result.Succeeded);
        Assert.Equal([0x10, 0x11, 0x00, 0x00], await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));
    }

    /// <summary>Rejects overlapping General Merge targets through Application admission.</summary>
    [Fact]
    public async Task GeneralMergePreviewRejectsOverlappingTargetMappings()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);

        GeneralAuthoringSessionPreparation prepared =
            await GeneralWorkflowTestSupport.PrepareGeneralMergeAsync(BootstrapTestHost.Canonical,
            "NT51950",
            GeneralTestDraftFactory.CreateMergeDraft("0x10", [
                Mapping("general-merge-map-1", source, "0x0", "0x4", "0x3"),
                Mapping("general-merge-map-2", source, "0x1", "0x5", "0x2"),
            ]),
            TestContext.Current.CancellationToken);

        Assert.False(prepared.Succeeded);
        Assert.Null(prepared.AcceptedSession);
        CompositionIssue issue = Assert.Single(prepared.Issues);
        Assert.Equal(
            "general.admission.target-intersection",
            issue.Code);
        Assert.Contains(
            "[0x5, 0x7)",
            issue.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "general-merge-map-1",
            issue.Message,
            StringComparison.Ordinal);
        Assert.Contains(
            "general-merge-map-2",
            issue.Message,
            StringComparison.Ordinal);
    }

    /// <summary>Reordering stable General mapping ids preserves the same overlap blocker.</summary>
    [Fact]
    public async Task GeneralMergeOverlapReportIsIndependentOfRowOrder()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);
        AuthoringMappingState first = Mapping("stable-a", source, "0x0", "0x4", "0x3");
        AuthoringMappingState second = Mapping("stable-b", source, "0x1", "0x5", "0x2");

        GeneralAuthoringSessionPreparation forward =
            await GeneralWorkflowTestSupport.PrepareGeneralMergeAsync(BootstrapTestHost.Canonical,
            "NT51950",
            GeneralTestDraftFactory.CreateMergeDraft("0x10", [first, second]),
            TestContext.Current.CancellationToken);
        GeneralAuthoringSessionPreparation reverse =
            await GeneralWorkflowTestSupport.PrepareGeneralMergeAsync(BootstrapTestHost.Canonical,
            "NT51950",
            GeneralTestDraftFactory.CreateMergeDraft("0x10", [second, first]),
            TestContext.Current.CancellationToken);

        CompositionIssue forwardIssue = Assert.Single(forward.Issues);
        CompositionIssue reverseIssue = Assert.Single(reverse.Issues);
        Assert.Equal(
            (forwardIssue.Code, forwardIssue.Message, forwardIssue.OperationId, forwardIssue.Severity),
            (reverseIssue.Code, reverseIssue.Message, reverseIssue.OperationId, reverseIssue.Severity));
    }

    /// <summary>Verifies a rejected V2 General Merge request does not disable unrelated Standard Merge or Replace workflows.</summary>
    [Fact]
    public async Task GeneralMergeV2RejectionDoesNotDisableUnrelatedWorkflows()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10, 0x11, 0x12, 0x13]);

        GeneralAuthoringSessionPreparation rejected =
            await GeneralWorkflowTestSupport.PrepareGeneralMergeAsync(BootstrapTestHost.Canonical,
            "NT51950",
            GeneralTestDraftFactory.CreateMergeDraft("0x10", [
                Mapping("general-merge-map-1", source, "0x0", "0x4", "0x3"),
                Mapping("general-merge-map-2", source, "0x1", "0x5", "0x2"),
            ]),
            TestContext.Current.CancellationToken);

        Assert.False(rejected.Succeeded);

        string dp = workspace.Write("standard-dp.bin", new byte[0x40000]);
        string tp = workspace.Write("standard-tp.bin", new byte[0x3C000]);
        CompositionRunResult standardMerge = await StandardMergeTestSupport.RunAsync(BootstrapTestHost.Services,
            "NT51923",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = dp,
                [CompositionAddressSpaceIds.TpInput] = tp,
            },
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(standardMerge.Succeeded);

        string baseImage = workspace.Write("replace-base.bin", new byte[0x40000]);
        string replacementDp = workspace.Write("replace-dp.bin", new byte[0x40000]);
        CliRunResult replace = await RunCliAsync([
            "dp-replace",
            "preview",
            "--profile",
            "NT51950",
            "--ic-num",
            "single",
            "--base",
            baseImage,
            "--dp",
            replacementDp,
        ]);

        Assert.Equal(0, replace.ExitCode);
        Assert.Contains("Status: Succeeded", replace.Output, StringComparison.Ordinal);
    }

    /// <summary>
    /// Uses synthetic sources and a hand-authored full-output mapping oracle for every currently
    /// admitted built-in IC; this is not direct firmware Golden evidence.
    /// </summary>
    [Theory]
    [InlineData("NT51917", "nt51917-general-merge-logical-candidate")]
    [InlineData("NT51919", "nt51919-general-merge-logical-candidate")]
    [InlineData("NT51929", "nt51929-general-merge-logical-candidate")]
    [InlineData("NT51932", "nt51932-general-merge-logical-candidate")]
    [InlineData("NT51923", "nt51923-general-merge-logical-candidate")]
    [InlineData("NT51926", "nt51926-general-merge-logical-candidate")]
    [InlineData("NT51927", "nt51927-general-merge-logical-candidate")]
    [InlineData("NT51928", "nt51928-general-merge-logical-candidate")]
    [InlineData("NT51950", "nt51950-general-merge-logical-candidate")]
    [InlineData("NT51951", "nt51951-general-merge-logical-candidate")]
    public async Task GeneralMergeV2DefaultRouteAppliesDeclaredSyntheticMappings(
        string icId,
        string candidateProfileId)
    {
        ArgumentNullException.ThrowIfNull(icId);
        ArgumentNullException.ThrowIfNull(candidateProfileId);

        using var workspace = TempWorkspace.Create();
        string firstSource = workspace.Write("first.bin", [0x10, 0x11, 0x12, 0x13]);
        string secondSource = workspace.Write("second.bin", [0x20, 0x21, 0x22]);
        AuthoringMappingState[] mappings =
        [
            Mapping("general-merge-map-1", firstSource, "0x1", "0x0", "0x3"),
            Mapping("general-merge-map-2", secondSource, "0x0", "0x6", "0x2"),
            Mapping("general-merge-map-3", firstSource, "0x0", "0x8", "0x1"),
        ];
        string output = workspace.PathFor("output.bin");
        CompositionRunResult result = await GeneralWorkflowTestSupport.RunGeneralMergeAsync(BootstrapTestHost.Canonical,
            icId,
            GeneralTestDraftFactory.CreateMergeDraft("0x10", mappings),
            build: true,
            TestContext.Current.CancellationToken,
            output);

        Assert.True(result.Succeeded);
        Assert.Equal(candidateProfileId, result.ProfileId);
        Assert.Equal(
            [0x11, 0x12, 0x13, 0, 0, 0, 0x20, 0x21, 0x10, 0, 0, 0, 0, 0, 0, 0],
            await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));

        using var report = JsonDocument.Parse(CompositionRunReportJson.Serialize(result));
        JsonElement root = report.RootElement;
        Assert.Equal(candidateProfileId, root.GetProperty("ProfileId").GetString());
        Assert.Equal("general-merge", root.GetProperty("ExperienceId").GetString());
        Assert.Equal("Merge", root.GetProperty("CompositionKind").GetString());
        Assert.Equal(3, root.GetProperty("Operations").GetArrayLength());
    }

    /// <summary>Verifies the typed default V2 route reports its registered profile identity.</summary>
    [Fact]
    public async Task GeneralMergeV2DefaultRouteReportsRegisteredProfileForCanonicalDraft()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10]);
        CompositionRunResult result = await GeneralWorkflowTestSupport.RunGeneralMergeAsync(BootstrapTestHost.Canonical,
            "NT51926",
            GeneralTestDraftFactory.CreateMergeDraft(
                "0x10",
                [Mapping("map-1", source, "0x0", "0x0", "0x1")]),
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal("nt51926-general-merge-logical-candidate", result.ProfileId);
    }

    /// <summary>Verifies the V2 default route rejects an unadmitted member rather than recreating a legacy profile.</summary>
    [Fact]
    public async Task GeneralMergeV2DefaultRouteRejectsUnadmittedMemberWithoutLegacyFallback()
    {
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10]);

        GeneralAuthoringSessionPreparation prepared =
            await GeneralWorkflowTestSupport.PrepareGeneralMergeAsync(BootstrapTestHost.Canonical,
            "NT51999",
            GeneralTestDraftFactory.CreateMergeDraft(
                "0x10",
                [Mapping("general-merge-map-1", source, "0x0", "0x0", "0x1")]),
            TestContext.Current.CancellationToken);

        Assert.False(prepared.Succeeded);
        Assert.Null(prepared.AcceptedSession);
        Assert.Equal(
            "general-merge.v2-candidate.member-not-admitted",
            Assert.Single(prepared.Issues).Code);

    }

    private static Task<CliRunResult> RunCliAsync(string[] args)
    {
        return CliTestHarness.RunAsync(args, TestContext.Current.CancellationToken);
    }

    private static AuthoringMappingState Mapping(
        string id,
        string path,
        string sourceStart,
        string targetStart,
        string length)
    {
        return GeneralAuthoringMappingUseCase.CreateGeneralMergeAuthoringState(
            id,
            path,
            sourceStart,
            targetStart,
            length,
            acceptedFileStamp: File.Exists(path)
                ? FileStamp.FromBytes(File.ReadAllBytes(path))
                : null);
    }
}
