using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// NVT-END-FLAG-1113-01 (owner decisions 30/37): NT51950/NT51951 read a Standard Merge TP input, and the retired DP
/// Replace Reference locator, only at the layout-declared NVT end flag [0x36FFC, 0x37000). A complete marker elsewhere
/// is neither counted nor rejected; an end flag without the marker is rejected for the existing reason, exactly as an
/// input without any marker, even when a complete Backup ends elsewhere. The AB TP A/B cases are in
/// <see cref="AbMergeGoldenRegressionTests"/> (they reuse its Combiner harness).
/// </summary>
public sealed class NvtEndFlagTpInputTests
{
    private const int EndFlagStart = 0x36FFC;
    private const int BackupStart = 0x36000;

    // Below the TP overlay [0xA000, 0x37000): never copied into the output.
    private const int UncopiedTpMarker = 0x1FFC;

    // Inside the TP overlay: copied 1:1 into the output.
    private const int CopiedTpMarker = 0x20000;
    private static readonly byte[] Marker = [0x00, 0x4E, 0x56, 0x54];

    /// <summary>
    /// Off-end-flag markers in the TP input are neither counted nor rejected: the Backup, IC Count and T token come
    /// from the end flag, and the complete output is the owner Golden with only the copied TP byte change.
    /// </summary>
    [Theory]
    [InlineData("51950-dp-256k", "NT51950")]
    [InlineData("51951-dp-512k", "NT51951")]
    public async Task StandardMergeTpMarkerAwayFromTheEndFlagIsNeitherCountedNorRejectedAsync(string caseId, string icId)
    {
        (byte[] dp, byte[] tp, byte[] expected) = ReadStandardCase(caseId);
        byte[] tpWithMarkers = [.. tp];
        Marker.CopyTo(tpWithMarkers.AsSpan(UncopiedTpMarker));
        Marker.CopyTo(tpWithMarkers.AsSpan(CopiedTpMarker));
        byte[] expectedWithMarker = [.. expected];
        Marker.CopyTo(expectedWithMarker.AsSpan(CopiedTpMarker));

        CompiledAuthoringSessionPreparation prepared = PrepareStandard(icId, dp, tpWithMarkers);
        Assert.True(prepared.Succeeded, CompositionExecutionTestSupport.FormatIssues(prepared.Issues));
        FirmwareNvtEndFlagResolution endFlag = prepared.Snapshot!.ExactCapability!.CompiledComposition.V2Details
            .Provenance.ResolvedMap.NvtEndFlagResolution;
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(tpWithMarkers, endFlag, out FirmwareConfigMetadata metadata,
            out int markerCount));
        Assert.Equal(1, markerCount);
        Assert.Equal(BackupStart, metadata.StructureStart);
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(tp, endFlag, out FirmwareConfigMetadata original, out _));
        Assert.Equal(original, metadata);
        // The previous whole-image rule counted all three markers and rejected the TP.
        Assert.False(FirmwareConfigMetadataReader.TryReadBackup(tpWithMarkers, out _, out int wholeImageCount));
        Assert.Equal(3, wholeImageCount);

        using var workspace = TempWorkspace.Create("nfc-nvt-end-flag-standard-tp");
        (CompositionRunResult baseline, byte[] baselineOutput) = await BuildStandardAsync(workspace, icId, dp, tp, "baseline");
        (CompositionRunResult result, byte[] output) = await BuildStandardAsync(workspace, icId, dp, tpWithMarkers, "markers");

        Assert.Equal(expected, baselineOutput);
        Assert.Equal(expectedWithMarker, output);
        Assert.Equal(NamingTokens(baseline), NamingTokens(result));
        Assert.Contains(NamingTokens(result), static token => token.TokenId == "tp-version" && token.IsKnown);
    }

    /// <summary>
    /// A TP whose end flag lacks the marker is blocked with the existing IC Count reason, the same issues as the TP
    /// without it, although a complete Backup ending at 0x1FFC is what the previous whole-image rule accepted.
    /// </summary>
    [Theory]
    [InlineData("51950-dp-256k", "NT51950")]
    [InlineData("51951-dp-512k", "NT51951")]
    public void StandardMergeTpWithoutTheEndFlagMarkerIsRejectedEvenWithABackupElsewhere(string caseId, string icId)
    {
        (byte[] dp, byte[] tp, _) = ReadStandardCase(caseId);
        byte[] missing = [.. tp];
        missing[EndFlagStart + 1] ^= 0xFF;
        byte[] moved = [.. missing];
        tp.AsSpan(BackupStart, 0x1000).CopyTo(moved.AsSpan(0x1000));
        Assert.True(FirmwareConfigMetadataReader.TryReadBackup(moved, out FirmwareConfigMetadata wholeImage, out int count));
        Assert.Equal(1, count);
        Assert.Equal(0x1000, wholeImage.StructureStart);

        CompiledAuthoringSessionPreparation movedResult = PrepareStandard(icId, dp, moved);
        CompiledAuthoringSessionPreparation missingResult = PrepareStandard(icId, dp, missing);

        Assert.False(movedResult.Succeeded);
        Assert.False(missingResult.Succeeded);
        Assert.Contains(movedResult.Issues, static issue =>
            issue.Code == FirmwareConfigChipCountDiagnostics.UnreadableIssueCode &&
            issue.OperationId == CompositionAddressSpaceIds.TpInput);
        Assert.Equal(IssueCodes(missingResult.Issues), IssueCodes(movedResult.Issues));
    }

    /// <summary>
    /// The retired DP Replace Reference locator resolves only the declared end flag: beside an extra marker it
    /// resolves at 0x36000 with one match; with only a misplaced marker it has no match.
    /// </summary>
    [Theory]
    [InlineData("51950-dp-256k", "NT51950", "nt51950-standard-merge-256k")]
    [InlineData("51951-dp-512k", "NT51951", "nt51951-standard-merge-512k")]
    public void RetiredDpReplaceReferenceLocatorReadsOnlyTheDeclaredEndFlag(string caseId, string icId, string mapId)
    {
        (_, _, byte[] reference) = ReadStandardCase(caseId);
        FirmwareFamilyResolutionDefinition family = BuiltInV2BundleRegistry.All["nt51950-nt51951-standard-merge"]
            .GetFirmwareFamily($"nt{icId[2..]}-standard-merge-dp-perspective", "0.8.0");
        byte[] withExtraMarker = [.. reference];
        Marker.CopyTo(withExtraMarker.AsSpan(CopiedTpMarker));

        FirmwareMetadataStructureResolution resolved = ResolveDpReplaceReference(family, icId, mapId, withExtraMarker);

        FirmwareResolvedMetadataStructure structure = Assert.IsType<FirmwareResolvedMetadataStructure>(resolved.Resolved);
        Assert.Equal(BackupStart, structure.LocatorOutcome.ResolvedRange.Range.Start);
        Assert.Equal(1, structure.LocatorOutcome.MarkerMatchCount);

        byte[] misplacedOnly = [.. withExtraMarker];
        misplacedOnly[EndFlagStart + 1] ^= 0xFF;
        FirmwareMetadataStructureResolution rejected = ResolveDpReplaceReference(family, icId, mapId, misplacedOnly);

        Assert.Null(rejected.Resolved);
        Assert.Equal(FirmwareMetadataStructureResolutionFailure.MarkerCardinalityMismatch, rejected.Failure);
        Assert.Equal(0, rejected.ObservedMarkerMatchCount);
    }

    /// <summary>DP Replace stays retired for both members after the declared locator change.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public async Task DpReplaceRetirementIsUnchangedAsync(string icId)
    {
        using var workspace = TempWorkspace.Create("nfc-nvt-end-flag-retired-dp");
        string outputPath = workspace.PathFor("must-not-exist.bin");

        CliRunResult result = await CliTestHarness.RunAsync(
        [
            "dp-replace", "build", "--profile", icId, "--ic-num", "single",
            "--base", "\0must-not-be-read.bin", "--dp", "\0must-not-be-read-either.bin", "--output", outputPath,
        ], TestContext.Current.CancellationToken);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("cli.retired-experience", result.Error, StringComparison.Ordinal);
        Assert.False(File.Exists(outputPath));
    }

    /// <summary>
    /// Owner decision 37: a saved General Merge rule bound to the previous dp-perspective 1.4.1 identity no longer
    /// admits, while the same rule bound to the current identity passes the parent check.
    /// </summary>
    [Fact]
    public async Task SavedGeneralMergeRuleBoundToThePreviousFamilyNoLongerAdmitsAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nvt-end-flag-saved-rule");
        JsonObject current = SavedRuleCliCommandTests.ValidGeneralMergeV2RuleObject();
        JsonObject previous = SavedRuleCliCommandTests.ValidGeneralMergeV2RuleObject();
        JsonObject parent = previous["parentBinding"]!.AsObject();
        parent["bundleVersion"] = "1.1.10-full-image-metadata.1";
        parent["bundleContentHash"] = "101a97d0101abae37e27568f21b7af9c491ef47af3d26b0a6d5d4f5cb0945a05";
        parent["profileContentHash"] = "af9b5f2d3270cbcd920b69dca30361f2509587d33b26070f301419dab5b3d795";
        parent["familyVersion"] = "1.4.1";
        parent["familyContentHash"] = "17b04684520efb5297e8096dec5fe20e57532e1ae1cc80ac26d6b86c27cdfb54";
        string source = workspace.Write("source.bin", [0x10]);

        CliRunResult stale = await PreviewSavedRuleAsync(workspace.Write("previous.json", Json(previous)), source);
        CliRunResult admitted = await PreviewSavedRuleAsync(workspace.Write("current.json", Json(current)), source);

        Assert.Equal(64, stale.ExitCode);
        Assert.Contains("saved-rule.v2.parent-narrowing-invalid", stale.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("saved-rule.v2.parent-narrowing-invalid", admitted.Error, StringComparison.Ordinal);
        Assert.Contains("saved-rule.lifecycle.execution-not-trusted-published", admitted.Error, StringComparison.Ordinal);
    }

    private static (byte[] Dp, byte[] Tp, byte[] Expected) ReadStandardCase(string caseId)
    {
        JsonElement goldenCase = CanonicalGoldenTestData.LoadDirectCase(ExperienceIds.StandardMerge, caseId);
        byte[] Read(string artifactId)
        {
            return File.ReadAllBytes(CanonicalGoldenTestData.ArtifactPath(
                CanonicalGoldenTestData.Artifact(goldenCase, artifactId)));
        }

        return (Read(CompositionAddressSpaceIds.DpInput), Read(CompositionAddressSpaceIds.TpInput), Read("expected-output"));
    }

    private static CompiledAuthoringSessionPreparation PrepareStandard(string icId, byte[] dp, byte[] tp)
    {
        return BootstrapTestHost.Services.StandardMergeAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.StandardMerge),
            icId,
            [new(CompositionAddressSpaceIds.DpInput, "dp.bin", dp), new(CompositionAddressSpaceIds.TpInput, "tp.bin", tp)]);
    }

    private static async Task<(CompositionRunResult Result, byte[] Output)> BuildStandardAsync(
        TempWorkspace workspace, string icId, byte[] dp, byte[] tp, string name)
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.DpInput] = workspace.Write($"{name}-dp.bin", dp),
            [CompositionAddressSpaceIds.TpInput] = workspace.Write($"{name}-tp.bin", tp),
        };
        string outputPath = workspace.PathFor($"{name}-output.bin");
        CompositionRunResult result = await StandardMergeTestSupport.RunAsync(
            BootstrapTestHost.Services, icId, paths, build: true, TestContext.Current.CancellationToken, outputPath);
        Assert.True(result.Succeeded, CompositionRunReportJson.Serialize(result));
        return (result, File.ReadAllBytes(outputPath));
    }

    private static (string TokenId, string Value, bool IsKnown)[] NamingTokens(CompositionRunResult result)
    {
        return
        [
            .. Assert.IsType<OutputNamingSummary>(result.Report.OutputNaming).Tokens
                .Where(static token => token.TokenId != "date")
                .Select(static token => (token.TokenId, token.Value, token.IsKnown)),
        ];
    }

    private static FirmwareMetadataStructureResolution ResolveDpReplaceReference(
        FirmwareFamilyResolutionDefinition family, string icId, string mapId, byte[] reference)
    {
        return family.ResolveMetadataStructure(
            mapId,
            "firmware-config-dp-replace",
            new FirmwareMapResolutionInputs(
                icId,
                ExperienceIds.DpReplace,
                reference.LongLength,
                requestedTopology: null,
                [new FirmwareArtifactPayload("reference-base", reference)]));
    }

    private static string[] IssueCodes(IEnumerable<CompositionIssue> issues)
    {
        return [.. issues.Select(static issue => issue.Code).Order(StringComparer.Ordinal)];
    }

    private static Task<CliRunResult> PreviewSavedRuleAsync(string rulePath, string sourcePath)
    {
        return CliTestHarness.RunAsync(
        [
            "general-merge", "preview", "--profile", "NT51950", "--rule", rulePath, "--slot", $"source-bin={sourcePath}",
        ], TestContext.Current.CancellationToken);
    }

    private static byte[] Json(JsonObject json)
    {
        return System.Text.Encoding.UTF8.GetBytes(json.ToJsonString());
    }
}
