// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Diagnostics;
using System.Security.Cryptography;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

/// <summary>Exercises explicit output-image write authority independently of adapter command coverage.</summary>
public sealed class StagedExternalProcessorHostTests
{
    /// <summary>The start, interior and last included byte of [2,5) are accepted for both Legacy protocols.</summary>
    [Theory]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(false, 4)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    [InlineData(true, 4)]
    public async Task ExplicitWriteRangeAcceptsEveryIncludedBoundary(bool postbuild, int changedOffset)
    {
        using TempWorkspace workspace = TempWorkspace.Create("nfc-staged-host");
        MutationRunner runner = new(changedOffset);
        StagedExternalProcessorHost host = CreateHost(workspace, runner);
        ExternalProcessorRequest request = CreateRequest(postbuild, []);
        byte[] expected = new byte[8];
        expected[changedOffset] = 0x7B;

        ExternalProcessorResult result = await host.TransformAsync(
            "legacy-combiner", request, [new ByteRange(2, 3)], TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.OutputBytes.ToArray());
        Assert.Equal([new ByteRange(changedOffset, 1)], result.ChangedRanges);
        Assert.Empty(result.Issues);
        _ = Assert.Single(result.ExecutedCommands);
        Assert.Equal(new byte[8], request.InputBytes.ToArray());
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "staging", request.RunId)));
    }

    /// <summary>The preceding byte and endExclusive byte are rejected even when the request permits the full image.</summary>
    [Theory]
    [InlineData(false, 1, "External processor changed bytes outside declared write ranges.")]
    [InlineData(false, 5, "External processor changed bytes outside declared write ranges.")]
    [InlineData(true, 1, "External processor changed bytes outside declared write ranges: 0x1-0x1 (len 0x1).")]
    [InlineData(true, 5, "External processor changed bytes outside declared write ranges: 0x5-0x5 (len 0x1).")]
    public async Task ExplicitWriteRangeRejectsOneByteOutsideEitherBoundary(bool postbuild, int changedOffset, string message)
    {
        using TempWorkspace workspace = TempWorkspace.Create("nfc-staged-host");
        MutationRunner runner = new(changedOffset);
        StagedExternalProcessorHost host = CreateHost(workspace, runner);
        ExternalProcessorRequest request = CreateRequest(postbuild, [new ByteRange(0, 8)]);

        ExternalProcessorResult result = await host.TransformAsync(
            "legacy-combiner", request, [new ByteRange(2, 3)], TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(result.OutputBytes.IsEmpty);
        Assert.Empty(result.ChangedRanges);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.write-range.violation", issue.Code);
        Assert.Equal(message, issue.Message);
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
        Assert.Null(issue.OperationId);
        _ = Assert.Single(result.ExecutedCommands);
        Assert.Equal(new byte[8], request.InputBytes.ToArray());
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "staging", request.RunId)));
    }

    /// <summary>Disjoint explicit ranges may include the first and last image bytes without authorizing the gap.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task ExplicitWriteRangesAcceptImageEdges(int changedOffset)
    {
        using TempWorkspace workspace = TempWorkspace.Create("nfc-staged-host");
        MutationRunner runner = new(changedOffset);
        StagedExternalProcessorHost host = CreateHost(workspace, runner);
        byte[] expected = new byte[8];
        expected[changedOffset] = 0x7B;

        ExternalProcessorResult result = await host.TransformAsync(
            "legacy-combiner",
            CreateRequest(false, []),
            [new ByteRange(0, 1), new ByteRange(7, 1)],
            TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.OutputBytes.ToArray());
        Assert.Equal([new ByteRange(changedOffset, 1)], result.ChangedRanges);
        Assert.Empty(result.Issues);
    }

    /// <summary>An empty authority list still accepts an unchanged image.</summary>
    [Fact]
    public async Task EmptyExplicitWriteRangesAcceptUnchangedImage()
    {
        using TempWorkspace workspace = TempWorkspace.Create("nfc-staged-host");
        MutationRunner runner = new(null);
        StagedExternalProcessorHost host = CreateHost(workspace, runner);

        ExternalProcessorResult result = await host.TransformAsync(
            "legacy-combiner", CreateRequest(false, [new ByteRange(0, 8)]), [], TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(new byte[8], result.OutputBytes.ToArray());
        Assert.Empty(result.ChangedRanges);
        Assert.Empty(result.Issues);
    }

    /// <summary>Both Legacy protocols inherit the parent environment and retain the manifest timeout.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HostLeavesLegacyEnvironmentInheritedAndPreservesManifestTimeout(bool postbuild)
    {
        using TempWorkspace workspace = TempWorkspace.Create("nfc-staged-host");
        MutationRunner runner = new(null);
        StagedExternalProcessorHost host = CreateHost(workspace, runner);

        ExternalProcessorResult result = await host.TransformAsync(CreateRequest(postbuild, []), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        ExternalProcessStartInfo invocation = Assert.IsType<ExternalProcessStartInfo>(runner.StartInfo);
        Assert.Equal(TimeSpan.FromSeconds(5), invocation.Timeout);
        ProcessStartInfo actual = SystemExternalProcessRunner.CreateProcessStartInfo(invocation);
        ProcessStartInfo inherited = new();
        Assert.Equal(
            inherited.Environment.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            actual.Environment.OrderBy(pair => pair.Key, StringComparer.Ordinal));
    }

    /// <summary>The shared host selects the compiled protocol without inferring a route from the processor id.</summary>
    [Theory]
    [InlineData(false, "future-ab-profile", "work.bin")]
    [InlineData(false, "legacy-postbuild", "work.bin")]
    [InlineData(true, "legacy-postbuild", "firmware.bin")]
    [InlineData(true, "future-processor", "firmware.bin")]
    public async Task HostSelectsLegacyProtocolOnlyFromCompiledPlan(bool postbuild, string processorId, string targetFileName)
    {
        using TempWorkspace workspace = TempWorkspace.Create("nfc-staged-host");
        MutationRunner runner = new(null);
        StagedExternalProcessorHost host = CreateHost(workspace, runner);
        ExternalProcessorRequest request = CreateRequest(postbuild, [], processorId: processorId);

        ExternalProcessorResult result = await host.TransformAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.Equal(new byte[8], result.OutputBytes.ToArray());
        Assert.Empty(result.ChangedRanges);
        Assert.Empty(result.Issues);
        ExternalProcessInvocation command = Assert.Single(result.ExecutedCommands);
        Assert.Equal(Path.Combine(workspace.Root, "staging", request.RunId), command.WorkingDirectory);
        Assert.Equal(targetFileName, Path.GetFileName(Assert.Single(command.Arguments)));
    }

    /// <summary>An unknown compiled protocol fails before any staging or manifest fallback can run.</summary>
    [Fact]
    public async Task HostRejectsUnknownCompiledProtocolWithoutAdapterFallback()
    {
        using TempWorkspace workspace = TempWorkspace.Create("nfc-staged-host");
        MutationRunner runner = new(null);
        StagedExternalProcessorHost host = CreateHost(workspace, runner);
        ExternalProcessorProtocolPlan unknown = new(
            "unknown-processor-protocol-v1",
            "firmware.bin",
            [new ExternalProcessorProtocolCommand("unknown", [ExternalProcessorProtocolArgumentTokens.TargetFile], [], false)]);
        ExternalProcessorRequest request = new(
            "unknown-protocol", "future-processor", "legacy-combiner-1.0", new byte[8], [], protocolPlan: unknown);

        ExternalProcessorResult result = await host.TransformAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(result.OutputBytes.IsEmpty);
        Assert.Empty(result.ChangedRanges);
        Assert.Empty(result.ExecutedCommands);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-processor.protocol.unsupported", issue.Code);
        Assert.Equal("No external-processor adapter is registered for compiled protocol 'unknown-processor-protocol-v1'.", issue.Message);
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
        Assert.Null(issue.OperationId);
        Assert.Null(runner.StartInfo);
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "staging", request.RunId)));
    }

    /// <summary>A manifest run id starting with two dots is rejected before the host creates staging.</summary>
    [Fact]
    public async Task ManifestRunIdStartingWithTwoDotsFailsWithStagingPathEscape()
    {
        using TempWorkspace workspace = TempWorkspace.Create("nfc-staged-host");
        MutationRunner runner = new(null);
        StagedExternalProcessorHost host = CreateHost(workspace, runner);
        ExternalProcessorRequest request = CreateRequest(false, [], runId: "..manifest-run");

        ExternalProcessorResult result = await host.TransformAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.True(result.OutputBytes.IsEmpty);
        Assert.Empty(result.ChangedRanges);
        Assert.Empty(result.ExecutedCommands);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal("external-tool.staging.path-escape", issue.Code);
        Assert.Equal("External processor staging directory escapes the approved staging root.", issue.Message);
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
        Assert.Null(issue.OperationId);
        Assert.Null(runner.StartInfo);
        Assert.Equal(new byte[8], request.InputBytes.ToArray());
        Assert.False(Directory.Exists(Path.Combine(workspace.Root, "staging")));
    }

    private static ExternalProcessorRequest CreateRequest(
        bool postbuild,
        IReadOnlyList<ByteRange> requestRanges,
        string processorId = "synthetic",
        string runId = "host-range-test")
    {
        ExternalProcessorProtocolPlan? plan = postbuild
            ? new ExternalProcessorProtocolPlan(
                LegacyCombinerPostbuildPlanCompiler.ProtocolId,
                "firmware.bin",
                [new ExternalProcessorProtocolCommand("synthetic", [ExternalProcessorProtocolArgumentTokens.TargetFile], [], false)])
            : null;
        return new ExternalProcessorRequest(runId, processorId, "legacy-combiner-1.0", new byte[8], requestRanges, protocolPlan: plan);
    }

    private static StagedExternalProcessorHost CreateHost(TempWorkspace workspace, IExternalProcessRunner runner)
    {
        byte[] executable = [0x4D, 0x5A, 0x01];
        _ = workspace.Write("tools/legacy-combiner/1.0/combiner.exe", executable);
        ExternalCombinerToolManifest manifest = new(
            "1.0", "legacy-combiner-1.0", "legacy-combiner", "1.0", "Synthetic", "win-x64", "combiner.exe",
            Convert.ToHexString(SHA256.HashData(executable)).ToLowerInvariant(),
            "legacy-combiner-inplace-v1", "in-place", ["{staging.workBin}"], "staging-directory", 5, []);
        return new StagedExternalProcessorHost(
            new ExternalCombinerToolRegistry([manifest]),
            Path.Combine(workspace.Root, "tools"),
            Path.Combine(workspace.Root, "staging"),
            runner,
            []);
    }

    private sealed class MutationRunner(int? changedOffset) : IExternalProcessRunner
    {
        // Written only by this test's sequential host invocation.
        internal ExternalProcessStartInfo? StartInfo { get; private set; }

        public async ValueTask<ExternalProcessResult> RunAsync(ExternalProcessStartInfo startInfo, CancellationToken cancellationToken)
        {
            StartInfo = startInfo;
            string target = Assert.Single(startInfo.Arguments);
            byte[] bytes = await File.ReadAllBytesAsync(target, cancellationToken);
            Assert.Equal(new byte[8], bytes);
            if (changedOffset is { } offset)
            {
                bytes[offset] = 0x7B;
                await File.WriteAllBytesAsync(target, bytes, cancellationToken);
            }

            return new ExternalProcessResult(0, false, string.Empty, string.Empty);
        }
    }
}
