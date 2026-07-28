using System.Text.Json;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Runtime-routing evidence for the supported built-in V2 DP Replace profiles.</summary>
public sealed class BuiltInV2DpReplaceRoutingTests
{
    /// <summary>Every canonical Gen Flash map exposes only its owner-approved DP partition.</summary>
    [Theory]
    [InlineData("NT51917", 0x40000, "nt51917-dp-replace-gen-flash-alias", "2bb448b7a8ba8fb259d8f429ff386d6c0aa29fd47d163f334c1e119e455ebcad", 0x3C000, 0x4000)]
    [InlineData("NT51919", 0x40000, "nt51919-dp-replace-gen-flash-alias", "169b9334a57328504fbe463c96dda1e8d749109896ae8d0143088b747b0ab596", 0x00000, 0x6000)]
    [InlineData("NT51923", 0x40000, "nt51923-dp-replace-gen-flash", "9496b7d6296e06fac81f4ca73a49ac1d4154ef9edc4dcf078fe433efa046081e", 0x3E000, 0x2000)]
    [InlineData("NT51926", 0x40000, "nt51926-dp-replace-gen-flash", "9496b7d6296e06fac81f4ca73a49ac1d4154ef9edc4dcf078fe433efa046081e", 0x3E000, 0x2000)]
    [InlineData("NT51927", 0x40000, "nt51927-dp-replace-gen-flash", "2bb448b7a8ba8fb259d8f429ff386d6c0aa29fd47d163f334c1e119e455ebcad", 0x3C000, 0x4000)]
    [InlineData("NT51929", 0x40000, "nt51929-dp-replace-gen-flash", "169b9334a57328504fbe463c96dda1e8d749109896ae8d0143088b747b0ab596", 0x00000, 0x6000)]
    [InlineData("NT51932", 0x40000, "nt51932-dp-replace-gen-flash", "169b9334a57328504fbe463c96dda1e8d749109896ae8d0143088b747b0ab596", 0x00000, 0x6000)]
    public void GenFlashDpReplaceUsesCanonicalDpPartition(
        string icId,
        long baseCapacity,
        string profileId,
        string bundleContentHash,
        long dpStart,
        long dpLength)
    {
        bool registered = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            icId,
            baseCapacity,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(registered);
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal(profileId, artifact.ProfileId);
        Assert.Equal(bundleContentHash, artifact.V2Details!.Provenance.Bundle.ContentHash);
        Assert.Equal(
            [CompositionAddressSpaceIds.DpReplacement, CompositionAddressSpaceIds.ReferenceBase],
            artifact.Plan.RequiredInputAddressSpaceIds.Order(StringComparer.Ordinal));
        CompositionOperation operation = Assert.Single(artifact.Plan.OrderedOperations);
        Assert.Equal("replace-dp-code", operation.OperationId);
        Assert.Equal(CompositionAddressSpaceIds.DpReplacement, operation.SourceSpaceId);
        Assert.Equal(new ByteRange(dpStart, dpLength), operation.TargetRange);
    }

    /// <summary>NT51928 keeps DP and LDC as separate required inputs and non-overlapping writes.</summary>
    [Fact]
    public void Nt51928DpReplaceSeparatesDpAndLdcPartitions()
    {
        bool registered = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51928",
            0x80000,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(registered);
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal("nt51928-dp-replace-gen-flash", artifact.ProfileId);
        Assert.Equal(
            [CompositionAddressSpaceIds.DpReplacement, CompositionAddressSpaceIds.LdReplacement, CompositionAddressSpaceIds.ReferenceBase],
            artifact.Plan.RequiredInputAddressSpaceIds.Order(StringComparer.Ordinal));
        Assert.Collection(
            artifact.Plan.OrderedOperations,
            operation =>
            {
                Assert.Equal("replace-dp-code", operation.OperationId);
                Assert.Equal(CompositionAddressSpaceIds.DpReplacement, operation.SourceSpaceId);
                Assert.Equal(new ByteRange(0x3C000, 0x4000), operation.TargetRange);
            },
            operation =>
            {
                Assert.Equal("replace-ldc-code", operation.OperationId);
                Assert.Equal(CompositionAddressSpaceIds.LdReplacement, operation.SourceSpaceId);
                Assert.Equal(new ByteRange(0x40000, 0x22000), operation.TargetRange);
            });
    }

    /// <summary>Verifies DP Perspective classification remains a map-shape fact, not generic DP Replace availability.</summary>
    [Theory]
    [InlineData("51950")]
    [InlineData("nt51951")]
    [InlineData("NT51950")]
    public void DpPerspectiveClassificationNormalizesRegisteredV2IcIds(string icId)
    {
        Assert.True(WorkbenchCompositionService.IsDpPerspectiveIc(icId));
    }

    /// <summary>Verifies empty ids and registered non-DP-Perspective ICs remain outside the DP Perspective family.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("NT51929")]
    public void DpPerspectiveClassificationReturnsFalseOutsidePerspectiveFamily(string icId)
    {
        Assert.False(WorkbenchCompositionService.IsDpPerspectiveIc(icId));
    }

    /// <summary>Verifies each supported IC/capacity resolves an executable trusted V2 artifact without legacy fallback.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51950", 0x80000)]
    [InlineData("NT51950", 0x100000)]
    [InlineData("NT51951", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    [InlineData("NT51951", 0x100000)]
    public void SupportedDpReplaceUsesCapacitySelectedTrustedV2Artifact(string icId, long baseCapacity)
    {
        bool compiled = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            icId,
            baseCapacity,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(compiled, string.Join(Environment.NewLine, issues.Select(static issue => issue.Message)));
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal(CompiledCompositionEligibility.V2RuntimeExecutable, artifact.Eligibility);
        _ = Assert.IsType<ProfileBundleV2CompilationAuthority>(artifact.Authority);
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(artifact.V2Details);
        Assert.Equal("4511e59f2f573f80554df55e0e825d65885a4fb1661f78c44f48bc57859640e2", details.Provenance.Bundle.ContentHash);
        Assert.Equal($"nt{icId[2..]}-dp-replace-dp-perspective", artifact.ProfileId);
        Assert.Equal(baseCapacity, artifact.Plan.OutputInitialization.Capacity);
        WorkbenchProfileSummary summary = WorkbenchCompositionService.GetReplaceProfileSummaries()
            .Single(profile => string.Equals(profile.IcId, icId, StringComparison.Ordinal));
        Assert.Equal(summary.ProfileId, artifact.ProfileId);
        Assert.Equal(summary.CompositionKind, artifact.CompositionKind);
        Assert.Equal(summary.RequiredInputAddressSpaceIds, artifact.Plan.RequiredInputAddressSpaceIds);
        Assert.Equal(summary.DefaultOutputFileName, artifact.DefaultOutputFileName);
        Assert.Equal(summary.IcNumberPolicy, artifact.IcNumberPolicy);
    }

    /// <summary>Verifies unsupported base capacities fail at the V2 resolver and never fall back to legacy planning.</summary>
    [Fact]
    public void UnsupportedDpReplaceBaseCapacityFailsClosed()
    {
        bool registered = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51950",
            0x40001,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(registered);
        Assert.Null(composition);
        CompositionIssue issue = Assert.Single(issues);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, issue.Code);
        Assert.Contains("0x40000 / 0x80000 / 0x100000", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies the workbench route satisfies the V2 typed-input and filename trace contract.</summary>
    [Theory]
    [InlineData("NT51950", 0x40000)]
    [InlineData("NT51951", 0x80000)]
    public async Task DpReplaceWorkbenchPreviewUsesTrustedV2InputBindings(string icId, int baseCapacity)
    {
        using var workspace = TempWorkspace.Create($"nfc-v2-dp-replace-{icId}-{baseCapacity:X}");
        string basePath = workspace.Write("base.bin", CreatePattern(baseCapacity, 0x31));
        string replacementPath = workspace.Write("replacement-dp.bin", CreatePattern(baseCapacity - 0x1000, 0xA7));
        var progress = new CompositionRunProgressFeed();

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceWithProgressAsync(
            icId,
            "single",
            "DP",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["replace-base"] = basePath,
                ["replace-dp"] = replacementPath,
            },
            [],
            [],
            build: false,
            progress,
            TestContext.Current.CancellationToken);
        List<CompositionRunProgressSnapshot> snapshots = [];
        await foreach (CompositionRunProgressSnapshot snapshot in
            progress.ReadAllAsync(TestContext.Current.CancellationToken))
        {
            snapshots.Add(snapshot);
        }

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.True(progress.IsAttached);
        Assert.Equal(
            [
                CompositionRunPhase.Preparing,
                CompositionRunPhase.ReadingInputs,
                CompositionRunPhase.ExecutingComposition,
                CompositionRunPhase.ValidatingOutput,
                CompositionRunPhase.PreparingReport,
            ],
            snapshots.Select(static snapshot => snapshot.CurrentPhase));
        Assert.Equal($"nt{icId[2..]}-dp-replace-dp-perspective", result.ProfileId);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal(
            ["base.bin", "replacement-dp.bin"],
            report.RootElement.GetProperty("Inputs")
                .EnumerateArray()
                .Select(static input => input.GetProperty("OriginalFileName").GetString())
                .Order(StringComparer.Ordinal));
    }

    /// <summary>Verifies selected DP Replace display ranges are projections of the executable V2 plan.</summary>
    [Theory]
    [InlineData("NT51950", 0x80000)]
    [InlineData("NT51951", 0x40000)]
    public void DpReplaceDisplayProjectsSelectedV2Plan(string icId, int baseCapacity)
    {
        _ = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            icId,
            baseCapacity,
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        CompositionOperation replacement = Assert.Single(
            artifact.Plan.OrderedOperations,
            operation => string.Equals(operation.SourceSpaceId, CompositionAddressSpaceIds.DpReplacement, StringComparison.Ordinal));
        CompositionOperation restore = Assert.Single(
            artifact.Plan.OrderedOperations,
            operation => string.Equals(operation.SourceSpaceId, CompositionAddressSpaceIds.ReferenceBase, StringComparison.Ordinal));

        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetReplaceMemoryDisplay(
            icId,
            "single",
            WorkbenchReplaceModes.Dp,
            baseCapacity);

        WorkbenchMemoryMapRow replacementRow = Assert.Single(display.MemoryMapRows, row => row.ActionLabel == "Replace");
        Assert.Equal(FormatRange(replacement.TargetRange), replacementRow.RangeLabel);
        Assert.Equal("Base flash", replacementRow.BeforeSource);
        Assert.Equal("DP replacement", replacementRow.AfterSource);
        WorkbenchMemoryMapRow restoreRow = Assert.Single(display.MemoryMapRows, row => row.ActionLabel == "Restore");
        Assert.Equal(FormatRange(restore.TargetRange), restoreRow.RangeLabel);
        Assert.Equal("DP replacement", restoreRow.BeforeSource);
        Assert.Equal("Base TP", restoreRow.AfterSource);
        Assert.Equal(
            FormatRange(new ByteRange(0, artifact.Plan.OutputInitialization.Capacity)),
            display.RangeLabel);
        Assert.Contains(display.CoverageSegments, segment =>
            segment.SourceLabel == "Base flash" && segment.RangeLabel == FormatRange(restore.TargetRange));
        Assert.Contains(display.CoverageSegments, segment => segment.SourceLabel == "Changed DP BIN");
    }

    /// <summary>Verifies pending DP Replace display uses V2 map capacities without selecting an arbitrary output range.</summary>
    [Theory]
    [InlineData("NT51950")]
    [InlineData("NT51951")]
    public void DpReplaceDisplayPendingBaseUsesV2MapCapacities(string icId)
    {
        WorkbenchMemoryDisplay display = WorkbenchCompositionService.GetReplaceMemoryDisplay(
            icId,
            "single",
            WorkbenchReplaceModes.Dp);

        WorkbenchMemoryMapRow row = Assert.Single(display.MemoryMapRows);
        Assert.Equal("Reference FlashCode length: 0x40000 / 0x80000 / 0x100000", row.RangeLabel);
        Assert.Equal("Select", row.ActionLabel);
        WorkbenchMemoryCoverageSegment segment = Assert.Single(display.CoverageSegments);
        Assert.Equal("Reference length pending", segment.RangeLabel);
        Assert.Contains("0x40000 / 0x80000 / 0x100000", segment.Detail, StringComparison.Ordinal);
        Assert.Equal(
            "Reference FlashCode length: 0x40000 / 0x80000 / 0x100000",
            display.RangeLabel);
    }

    private static byte[] CreatePattern(int length, byte salt)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = unchecked((byte)(salt + (index * 37)));
        }

        return bytes;
    }

    private static string FormatRange(ByteRange range)
    {
        return FormattableString.Invariant($"0x{range.Start:X5}-0x{range.EndExclusive - 1:X5} (len 0x{range.Length:X})");
    }
}
