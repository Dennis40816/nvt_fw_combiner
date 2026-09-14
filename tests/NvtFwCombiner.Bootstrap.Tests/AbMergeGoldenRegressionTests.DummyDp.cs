using System.Buffers.Binary;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Contracts.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeGoldenRegressionTests
{
    /// <summary>Independent accepted map and MPEG-2 expectation versus the real pinned processor; not certified Dummy Golden.</summary>
    [Theory]
    [InlineData("NT51950", 1, 0x80000, 0x40000)]
    [InlineData("NT51950", 2, 0x100000, 0x40000)]
    [InlineData("NT51951", 0, 0x100000, 0x80000)]
    public async Task DummyProcessorOutputMatchesCompleteIndependentMap(string icId, int count, int capacity, int bankOffset)
    {
        var adapter = new BuiltInV2DynamicCompilationAdapter();
        var identity = new CapabilityRouteIdentity(icId, ExperienceIds.AbMerge,
            count == 0 ? "selector-free" : count == 1 ? "1-ic" : "2-plus-ic",
            icId == "NT51950" ? "nt51950-ab-merge-maps" : "nt51951-ab-merge-1024k");
        adapter.Compile(identity, capacity, [],
            out CompiledComposition? composition, out _, out IReadOnlyList<CompositionIssue> issues,
            count == 0 ? null : new TopologySelection(count, "test", TopologySelectionSource.Requested, "test"));
        Assert.Empty(issues);
        Assert.NotNull(composition);
        byte[] tpA = CreateAddressSensitivePattern(0x37000, 0x23);
        byte[] tpB = CreateAddressSensitivePattern(0x37000, 0x61);
        WriteHeaderPointers(tpA);
        WriteHeaderPointers(tpB);
        byte[] originalA = [.. tpA];
        byte[] originalB = [.. tpB];

        // Accepted map constants and independent scalar/CRC arithmetic, never candidate plan/output.
        byte[] expected = new byte[capacity];
        Array.Fill(expected, (byte)0xFF);
        tpA.AsSpan(0xA000, 0x2D000).CopyTo(expected.AsSpan(0xA000));
        tpB.AsSpan(0xA000, 0x2D000).CopyTo(expected.AsSpan(bankOffset + 0xA000));
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(bankOffset + 0xA100, 4), (uint)(bankOffset + 0xC000));
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(bankOffset + 0xA110, 4), (uint)(bankOffset + 0x11000));
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(bankOffset + 0xA120, 4), (uint)(bankOffset + 0x1A000));
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(bankOffset + 0xA130, 4),
            CalculateCrc32Mpeg2(expected.AsSpan(bankOffset + 0xA100, 0x30)));

        using var workspace = TempWorkspace.Create("dummy-ab-real-processor");
        string repository = RepositoryPaths.FindRepositoryRoot();
        ExternalCombinerToolManifest manifest = LoadManifest(Path.Combine(repository,
            "external-tools", "legacy-combiner", "1.13.0", "manifest.json"));
        var processor = new ExternalCombinerProcessor(new ExternalCombinerToolRegistry([manifest]),
            Path.Combine(repository, "external-tools"), workspace.PathFor("staging"),
            new SystemExternalProcessRunner(), ExternalCombinerInvocationCatalog.All);
        ExternalProcessorResult? postbuild = null;
        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(composition.Plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["tp-a-input"] = tpA,
                ["tp-b-input"] = tpB,
            }),
            async (operation, input, sources, artifacts, cancellationToken) =>
            {
                Assert.Empty(sources);
                ExternalProcessorInvocation invocation = Assert.IsType<ExternalProcessorInvocation>(operation.ExternalProcessorInvocation);
                postbuild = await processor.TransformAsync(new ExternalProcessorRequest(
                    "dummy-map-expected", invocation.ProcessorId, invocation.ToolBindingId,
                    input, invocation.AllowedWriteRanges, stagedArtifacts: artifacts), cancellationToken);
                return postbuild.Succeeded ? CompositionExternalProcessorResult.Success(postbuild.OutputBytes)
                    : CompositionExternalProcessorResult.Failed(postbuild.Issues);
            }, TestContext.Current.CancellationToken);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Empty(result.Issues);
        Assert.Equal(expected, result.OutputBytes.ToArray());
        Assert.Equal(originalA, tpA);
        Assert.Equal(originalB, tpB);
        Assert.NotNull(postbuild);
        Assert.True(postbuild.Succeeded);
        Assert.Equal([new ByteRange(bankOffset + 0xA102, 1), new ByteRange(bankOffset + 0xA112, 1), new ByteRange(bankOffset + 0xA130, 4)],
            postbuild.ChangedRanges);
        _ = Assert.Single(postbuild.ExecutedCommands);
        Assert.Equal([new ByteRange(bankOffset + 0xA100, 4), new ByteRange(bankOffset + 0xA110, 4), new ByteRange(bankOffset + 0xA130, 4)],
            result.Mutations.Where(mutation => mutation.OperationId.StartsWith("import-postbuild-", StringComparison.Ordinal))
                .Select(mutation => mutation.TargetRange));
    }
}
