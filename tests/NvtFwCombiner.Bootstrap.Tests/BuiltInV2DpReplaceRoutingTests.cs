using System.Text.Json;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Runtime-routing evidence for the supported built-in V2 DP Replace profiles.</summary>
public sealed class BuiltInV2DpReplaceRoutingTests
{
    /// <summary>Every canonical Gen Flash map exposes only its owner-approved DP partition.</summary>
    [Theory]
    [InlineData("NT51917", 0x40000, "nt51917-dp-replace-gen-flash-alias", "d47faa5137c34e1f771ec1568f699f1c5301a9fb9235f243ca9ad467315d5db3", 0x3C000, 0x4000)]
    [InlineData("NT51919", 0x40000, "nt51919-dp-replace-gen-flash-alias", "31c545eb367ff902eb2e95bc0b90643c337ab26b4e5831169bfc1a31f060f3cd", 0x00000, 0x6000)]
    [InlineData("NT51923", 0x40000, "nt51923-dp-replace-gen-flash", "fd5ee9dda6de6b0ba2142adf0ddae9736282407fb96e53895e4cbfd505746df6", 0x3E000, 0x2000)]
    [InlineData("NT51926", 0x40000, "nt51926-dp-replace-gen-flash", "fd5ee9dda6de6b0ba2142adf0ddae9736282407fb96e53895e4cbfd505746df6", 0x3E000, 0x2000)]
    [InlineData("NT51927", 0x40000, "nt51927-dp-replace-gen-flash", "d47faa5137c34e1f771ec1568f699f1c5301a9fb9235f243ca9ad467315d5db3", 0x3C000, 0x4000)]
    [InlineData("NT51929", 0x40000, "nt51929-dp-replace-gen-flash", "31c545eb367ff902eb2e95bc0b90643c337ab26b4e5831169bfc1a31f060f3cd", 0x00000, 0x6000)]
    [InlineData("NT51932", 0x40000, "nt51932-dp-replace-gen-flash", "31c545eb367ff902eb2e95bc0b90643c337ab26b4e5831169bfc1a31f060f3cd", 0x00000, 0x6000)]
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
        Assert.Equal(bundleContentHash, artifact.V2Details.Provenance.Bundle.ContentHash);
        Assert.Equal(
            [CompositionAddressSpaceIds.DpReplacement, CompositionAddressSpaceIds.ReferenceBase],
            artifact.Plan.RequiredInputAddressSpaceIds.Order(StringComparer.Ordinal));
        CompositionOperation operation = Assert.Single(artifact.Plan.OrderedOperations);
        Assert.Equal("replace-dp-code", operation.OperationId);
        Assert.Equal(CompositionAddressSpaceIds.DpReplacement, operation.SourceSpaceId);
        Assert.Equal(new ByteRange(dpStart, dpLength), operation.TargetRange);
        Assert.Equal(operation.TargetRange, operation.SourceRange);
    }

    /// <summary>NT51928 lowers one selection group into only the selected non-overlapping replacement writes.</summary>
    [Theory]
    [InlineData(0x40000, "initial-code-replacement", "replace-dp-code")]
    [InlineData(0x80000, "initial-code-replacement", "replace-dp-code")]
    [InlineData(0x80000, "ldc-replacement", "replace-ldc-code")]
    [InlineData(0x80000, "initial-code-replacement,ldc-replacement", "replace-dp-code,replace-ldc-code")]
    public void Nt51928DpReplaceLowersOnlySelectedPartitions(
        long referenceCapacity,
        string selectedInputIds,
        string expectedOperationIds)
    {
        ArgumentNullException.ThrowIfNull(selectedInputIds);
        ArgumentNullException.ThrowIfNull(expectedOperationIds);
        bool registered = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51928",
            referenceCapacity,
            selectedInputIds.Split(','),
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(registered);
        Assert.Empty(issues);
        CompiledComposition artifact = Assert.IsType<CompiledComposition>(composition);
        Assert.Equal("nt51928-dp-replace-gen-flash", artifact.ProfileId);
        Assert.Equal(
            selectedInputIds.Split(',').Append(CompositionAddressSpaceIds.ReferenceBase).Order(StringComparer.Ordinal),
            artifact.Plan.RequiredInputAddressSpaceIds.Order(StringComparer.Ordinal));
        Assert.Equal(
            expectedOperationIds.Split(','),
            artifact.Plan.OrderedOperations.Select(static operation => operation.OperationId));
        Assert.All(artifact.Plan.OrderedOperations, operation =>
        {
            ByteRange expectedRange = operation.OperationId == "replace-dp-code"
                ? new ByteRange(0x3C000, 0x4000)
                : new ByteRange(0x40000, 0x22000);
            Assert.Equal(expectedRange, operation.TargetRange);
            Assert.Equal(operation.TargetRange, operation.SourceRange);
        });
        CompiledInputSelectionGroup group = Assert.Single(artifact.V2Details.InputContract.SelectionGroups);
        Assert.Equal(selectedInputIds.Split(',').Order(StringComparer.Ordinal), group.SelectedSlotIds);
        Assert.Equal(
            referenceCapacity == 0x40000
                ? [CompositionAddressSpaceIds.InitialCodeReplacement]
                : [CompositionAddressSpaceIds.InitialCodeReplacement, CompositionAddressSpaceIds.LdcReplacement],
            group.ApplicableMemberSlotIds);
        if (referenceCapacity == 0x40000)
        {
            Assert.Equal(
                "Reference length does not include LDC",
                group.NotApplicableReasons[CompositionAddressSpaceIds.LdcReplacement]);
        }
        else
        {
            Assert.Empty(group.NotApplicableReasons);
        }
    }

    /// <summary>NT51928 rejects LDC on a 256-KiB reference with the profile-owned readiness reason.</summary>
    [Fact]
    public void Nt51928DpReplaceRejectsLdcWhenReferenceHasNoLdcRegion()
    {
        bool registered = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51928",
            0x40000,
            [CompositionAddressSpaceIds.LdcReplacement],
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(registered);
        Assert.Null(composition);
        Assert.Contains(issues, issue =>
            issue.Code == "profile.v2.plan.input-selection-not-applicable" &&
            issue.Message == "Reference length does not include LDC");
    }

    /// <summary>NT51928 rejects an empty replacement selection instead of materializing a no-op route.</summary>
    [Fact]
    public void Nt51928DpReplaceRequiresAtLeastOneReplacement()
    {
        bool registered = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51928",
            0x80000,
            [],
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(registered);
        Assert.Null(composition);
        CompositionIssue issue = Assert.Single(issues);
        Assert.Equal("profile.v2.plan.input-selection-invalid", issue.Code);
    }

    /// <summary>The headless workbench consumes the same typed selection result as the CLI.</summary>
    [Theory]
    [InlineData(null, "initial-code-replacement", "ldc-replacement", "PendingInput")]
    [InlineData(0x40000L, "initial-code-replacement", "ldc-replacement", "NotApplicable")]
    [InlineData(0x80000L, "ldc-replacement", "initial-code-replacement", "Ready")]
    public void Nt51928DpReplaceProjectsApplicationOwnedSelectionReadiness(
        long? referenceCapacity,
        string selectedSlotId,
        string inspectedSlotId,
        string expectedReadiness)
    {
        bool resolved = WorkbenchCompositionService.TryResolveBuiltInV2DpReplaceInputSelection(
            "NT51928",
            referenceCapacity,
            [selectedSlotId],
            out InputSelectionReadinessSnapshot? readiness,
            out IReadOnlyList<CompositionIssue> issues);

        Assert.True(resolved);
        Assert.Empty(issues);
        InputSelectionMemberReadiness member = Assert.Single(readiness!.Groups)
            .Members.Single(candidate => candidate.SlotId == inspectedSlotId);
        Assert.Equal(
            Enum.Parse<ResolvedChildReadiness>(expectedReadiness),
            member.Readiness);
        if (referenceCapacity == 0x40000)
        {
            Assert.Equal("Reference length does not include LDC", member.Reason);
        }
    }

    /// <summary>The workbench rejects a stale LDC binding through the Application readiness result.</summary>
    [Fact]
    public async Task Nt51928WorkbenchRejectsSelectedLdcForNoLdcReferenceAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51928-stale-ldc-selection");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51928",
            WorkbenchIcNumberTokens.SingleChip,
            WorkbenchReplaceModes.Dp,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] =
                    workspace.Write("reference.bin", CreatePattern(0x40000, 0x21)),
                [WorkbenchSlotIds.ReplaceLdc] =
                    workspace.Write("ldc.bin", CreatePattern(0x80000, 0xB4)),
            },
            build: false,
            TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Contains(
            InputSelectionReadinessIssueCodes.SelectionNotApplicable,
            result.ReportJson,
            StringComparison.Ordinal);
        Assert.Contains(
            "Reference length does not include LDC",
            result.ReportJson,
            StringComparison.Ordinal);
    }

    /// <summary>Distinct sentinels prove every NT51928 byte outside selected Initial Code/LDC ranges stays Reference.</summary>
    [Theory]
    [InlineData(0x40000, true, false)]
    [InlineData(0x80000, true, false)]
    [InlineData(0x80000, false, true)]
    [InlineData(0x80000, true, true)]
    public async Task Nt51928DpReplacePreservesEveryUnselectedReferenceByteAsync(
        int referenceCapacity,
        bool selectInitialCode,
        bool selectLdc)
    {
        using var workspace = TempWorkspace.Create(
            $"nfc-nt51928-dp-replace-{referenceCapacity:X}-{selectInitialCode}-{selectLdc}");
        byte[] reference = CreatePattern(referenceCapacity, 0x21);
        byte[] initialCode = CreatePattern(referenceCapacity, 0x71);
        byte[] ldc = CreatePattern(referenceCapacity, 0xB4);
        string outputPath = workspace.PathFor("output.bin");
        var slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [WorkbenchSlotIds.ReplaceBase] = workspace.Write("reference.bin", reference),
        };
        if (selectInitialCode)
        {
            slotPaths[WorkbenchSlotIds.ReplaceDp] = workspace.Write("initial-code.bin", initialCode);
        }

        if (selectLdc)
        {
            slotPaths[WorkbenchSlotIds.ReplaceLdc] = workspace.Write("ldc.bin", ldc);
        }

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51928",
            WorkbenchIcNumberTokens.SingleChip,
            WorkbenchReplaceModes.Dp,
            slotPaths,
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        byte[] expected = [.. reference];
        if (selectInitialCode)
        {
            initialCode.AsSpan(0x3C000, 0x4000).CopyTo(expected.AsSpan(0x3C000, 0x4000));
        }

        if (selectLdc)
        {
            ldc.AsSpan(0x40000, 0x22000).CopyTo(expected.AsSpan(0x40000, 0x22000));
        }

        Assert.Equal(expected, File.ReadAllBytes(outputPath));
    }

    /// <summary>Uniform LDC content remains buildable and appears as one typed warning in the report.</summary>
    [Fact]
    public async Task Nt51928UniformLdcEmitsWarningWithoutBlockingBuildAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51928-uniform-ldc-warning");
        string outputPath = workspace.PathFor("output.bin");
        byte[] uniformLdc = new byte[0x80000];
        Array.Fill(uniformLdc, (byte)0xFF);
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51928",
            WorkbenchIcNumberTokens.SingleChip,
            WorkbenchReplaceModes.Dp,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] =
                    workspace.Write("reference.bin", CreatePattern(0x80000, 0x21)),
                [WorkbenchSlotIds.ReplaceLdc] =
                    workspace.Write("ldc.bin", uniformLdc),
            },
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue =>
                issue.GetProperty("Code").GetString() == "LDC_UNIFORM_CONTENT_WARNING" &&
                issue.GetProperty("Severity").GetString() == CompositionIssueSeverity.Warning);
    }

    /// <summary>Uniform Initial Code content remains buildable and uses the same warning-only validation contract.</summary>
    [Fact]
    public async Task Nt51928UniformInitialCodeEmitsWarningWithoutBlockingBuildAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51928-uniform-initial-warning");
        string outputPath = workspace.PathFor("output.bin");
        byte[] uniformInitialCode = new byte[0x80000];
        Array.Fill(uniformInitialCode, (byte)0xFF);
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51928",
            WorkbenchIcNumberTokens.SingleChip,
            WorkbenchReplaceModes.Dp,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] =
                    workspace.Write("reference.bin", CreatePattern(0x80000, 0x21)),
                [WorkbenchSlotIds.ReplaceDp] =
                    workspace.Write("initial-code.bin", uniformInitialCode),
            },
            build: true,
            TestContext.Current.CancellationToken,
            outputPath);

        Assert.True(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Contains(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue =>
                issue.GetProperty("Code").GetString() == "DP_UNIFORM_CONTENT_WARNING" &&
                issue.GetProperty("Severity").GetString() == CompositionIssueSeverity.Warning);
    }

    /// <summary>Non-uniform selected Initial Code and LDC inputs produce no plausibility warning.</summary>
    [Fact]
    public async Task Nt51928NonUniformReplacementInputsDoNotEmitPlausibilityWarningsAsync()
    {
        using var workspace = TempWorkspace.Create("nfc-nt51928-nonuniform-inputs");
        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            "NT51928",
            WorkbenchIcNumberTokens.SingleChip,
            WorkbenchReplaceModes.Dp,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [WorkbenchSlotIds.ReplaceBase] =
                    workspace.Write("reference.bin", CreatePattern(0x80000, 0x21)),
                [WorkbenchSlotIds.ReplaceDp] =
                    workspace.Write("initial-code.bin", CreatePattern(0x80000, 0x42)),
                [WorkbenchSlotIds.ReplaceLdc] =
                    workspace.Write("ldc.bin", CreatePattern(0x80000, 0x63)),
            },
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.DoesNotContain(
            report.RootElement.GetProperty("Issues").EnumerateArray(),
            issue => issue.GetProperty("Code").GetString() is
                "DP_UNIFORM_CONTENT_WARNING" or "LDC_UNIFORM_CONTENT_WARNING");
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
        V2CompiledCompositionDetails details = Assert.IsType<V2CompiledCompositionDetails>(artifact.V2Details);
        Assert.Equal("56e39af41aaed8abad5da0f49274053ad2fb619949b53efd9497ed31a10ee99b", details.Provenance.Bundle.ContentHash);
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
        string replacementPath = workspace.Write("replacement-dp.bin", CreatePattern(baseCapacity, 0xA7));
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
