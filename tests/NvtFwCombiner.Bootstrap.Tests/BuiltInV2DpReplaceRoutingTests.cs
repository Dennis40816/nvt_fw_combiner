using System.Text.Json;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Runtime-routing evidence for the supported NT51950/NT51951 V2 DP Replace profiles.</summary>
public sealed class BuiltInV2DpReplaceRoutingTests
{
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
        bool compiled = WorkbenchCompositionService.TryCompileDpPerspectiveDpReplace(
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
        Assert.Equal("f36d750a4081ef95c23194227cc3aa2a05c711c3519640e2c8cc0d056cb921b0", details.Provenance.Bundle.ContentHash);
        Assert.Equal($"nt{icId[2..]}-dp-replace-dp-perspective", artifact.ProfileId);
        Assert.Equal(baseCapacity, artifact.Plan.OutputInitialization.Capacity);
    }

    /// <summary>Verifies unsupported base capacities fail at the V2 resolver and never fall back to legacy planning.</summary>
    [Fact]
    public void UnsupportedDpReplaceBaseCapacityFailsClosed()
    {
        bool registered = WorkbenchCompositionService.TryCompileDpPerspectiveDpReplace(
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

        WorkbenchRunResult result = await WorkbenchCompositionService.RunReplaceAsync(
            icId,
            "single",
            "DP",
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["replace-base"] = basePath,
                ["replace-dp"] = replacementPath,
            },
            build: false,
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ReportJson);
        Assert.Equal($"nt{icId[2..]}-dp-replace-dp-perspective", result.ProfileId);
        using var report = JsonDocument.Parse(result.ReportJson);
        Assert.Equal(
            ["base.bin", "replacement-dp.bin"],
            report.RootElement.GetProperty("Inputs")
                .EnumerateArray()
                .Select(static input => input.GetProperty("OriginalFileName").GetString())
                .Order(StringComparer.Ordinal));
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
}
