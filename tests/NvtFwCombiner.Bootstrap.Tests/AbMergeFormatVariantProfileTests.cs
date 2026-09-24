using System.Buffers.Binary;
using System.Numerics;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Independent synthetic byte evidence; not real Combiner or certified Golden evidence.</summary>
public sealed class AbMergeFormatVariantProfileTests
{
    private const string BundleHash = "18b43352606ca744f499e328d5778c3b9e08307a97fd122ac38fd8762d37c8d1";
    private const int Capacity = 0x100000;
    private static readonly int[] HeaderOffsets = [0xA100, 0xA110, 0xA130];

    /// <summary>Every closed variant preserves seed bytes outside TP and imports only three header words.</summary>
    [Theory]
    [InlineData("NT51950", "desay", 1, 0x40000, false)]
    [InlineData("NT51950", "desay", 1, 0x40000, true)]
    [InlineData("NT51950", "desay", 2, 0x40000, false)]
    [InlineData("NT51950", "desay", 3, 0x40000, true)]
    [InlineData("NT51951", "desay", 0, 0x40000, false)]
    [InlineData("NT51951", "desay", 0, 0x40000, true)]
    [InlineData("NT51950", "cascade", 2, 0x80000, false)]
    [InlineData("NT51950", "cascade", 2, 0x80000, true)]
    [InlineData("NT51950", "cascade", 3, 0x80000, false)]
    [InlineData("NT51950", "cascade", 4, 0x80000, true)]
    public async Task ClosedVariantExecutesExactIndependentOutput(
        string ic, string format, int count, int bankLength, bool dummy)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-format-variant");
        CompiledComposition composition = Compile(workspace, ic, format, count, dummy);
        Assert.Equal(Capacity, composition.Plan.OutputInitialization.Capacity);
        Assert.True(composition.IsV2AbFunctionOpenCandidate);
        Assert.Null(composition.CapabilityFingerprint); // Deliberately not a published runtime route yet.
        Assert.Equal(new BigInteger(bankLength), composition.Plan.OrderedOperations
            .Single(operation => operation.OperationId == "relocate-tpb-diff-for-b-bank").ScalarTransform!.Addend);

        byte[] dp = Pattern(Capacity, 0x31);
        byte[] tpA = Pattern(0x37000, 0x57);
        byte[] tpB = Pattern(0x37000, 0xAB);
        BinaryPrimitives.WriteUInt32LittleEndian(tpB.AsSpan(0xA120, 4), 0x12345678);
        byte[][] originals = [[.. dp], [.. tpA], [.. tpB]];

        // This oracle follows the owner's ranges, not the compiled operations or profile JSON.
        byte[] expected = dummy ? [.. Enumerable.Repeat((byte)0xFF, Capacity)] : [.. dp];
        tpA.AsSpan(0xA000, 0x2D000).CopyTo(expected.AsSpan(0xA000));
        tpB.AsSpan(0xA000, 0x2D000).CopyTo(expected.AsSpan(bankLength + 0xA000));
        BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(bankLength + 0xA120, 4),
            0x12345678u + (uint)bankLength);
        byte[] expectedTransport = expected.AsSpan(0, bankLength * 2).ToArray();
        int calls = 0;
        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(
            composition.Plan, Inputs(dummy, dp, tpA, tpB),
            (invocation, bytes, sources, artifacts, _) =>
            {
                calls++;
                Assert.Empty(sources);
                Assert.Equal(expectedTransport, bytes.ToArray());
                Assert.Equal(["a-bank", "b-bank"], artifacts.Select(artifact => artifact.ArtifactId));
                Assert.Equal(expectedTransport.AsSpan(0, bankLength).ToArray(), artifacts[0].Bytes.ToArray());
                Assert.Equal(expectedTransport.AsSpan(bankLength, bankLength).ToArray(), artifacts[1].Bytes.ToArray());
                Assert.Equal(format == "desay"
                    ? "nfc-nt51950-ab-merge-combiner-v1"
                    : "nfc-nt51951-ab-merge-combiner-v1", invocation.ExternalProcessorInvocation!.ProcessorId);
                ExternalCombinerInvocationProfile command = Assert.Single(ExternalCombinerInvocationCatalog.All,
                    candidate => candidate.ProcessorId == invocation.ExternalProcessorInvocation.ProcessorId);
                Assert.Equal("legacy-combiner-1.13.0", command.ToolBindingId);
                Assert.Equal("input-output-file", command.InputMode);
                Assert.Equal(
                    ["NT51950BASED_MERGE_AB_MODE", "CRC8", "{staging.artifact.a-bank}",
                     "{staging.artifact.b-bank}", "{staging.outputBin}", format == "desay" ? "0x40000" : "0x80000"],
                    command.ArgumentTemplate);
                Assert.Equal(
                    HeaderOffsets.Select(offset => new ByteRange(bankLength + offset, 4)),
                    invocation.ExternalProcessorInvocation!.AllowedWriteRanges);
                byte[] transformed = bytes.ToArray();
                foreach (int offset in HeaderOffsets)
                {
                    BinaryPrimitives.WriteUInt32LittleEndian(transformed.AsSpan(bankLength + offset, 4), (uint)offset);
                    BinaryPrimitives.WriteUInt32LittleEndian(expected.AsSpan(bankLength + offset, 4), (uint)offset);
                }

                return ValueTask.FromResult(CompositionExternalProcessorResult.Success(transformed));
            }, CancellationToken.None);

        Assert.Equal(1, calls);
        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal(expected, result.OutputBytes.ToArray());
        Assert.Equal(originals[0], dp);
        Assert.Equal(originals[1], tpA);
        Assert.Equal(originals[2], tpB);
    }

    /// <summary>Cascade rejects a missing or single topology.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void CascadeRejectsSingleOrMissingTopology(int count)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-format-count");
        V2CompositionPlanCompileResult result = CompileResult(workspace, "NT51950", "cascade", count, false);
        Assert.False(result.IsCompiled);
        Assert.NotEmpty(result.Issues);
        Assert.DoesNotContain(result.Issues, issue => issue.Code == "profile.v2.selection.not-found");
    }

    /// <summary>NT51951 stays selector-free even when its bank geometry is shared with NT51950.</summary>
    [Fact]
    public void DesayNt51951RejectsHiddenTopology()
    {
        using var workspace = TempWorkspace.Create("nfc-ab-format-count");
        V2CompositionPlanCompileResult result = CompileResult(workspace, "NT51951", "desay", 1, false);
        Assert.False(result.IsCompiled);
        Assert.Contains(result.Issues, issue => issue.Code == "profile.v2.compile.topology-not-declared");
    }

    /// <summary>DP excess is not copied, while a short source cannot silently become padded output.</summary>
    [Theory]
    [InlineData("NT51950", 1, -1)]
    [InlineData("NT51950", 1, 1)]
    [InlineData("NT51951", 0, -1)]
    [InlineData("NT51951", 0, 1)]
    public async Task DesayDpCoverageRemainsFailClosed(string ic, int count, int delta)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-format-dp-coverage");
        CompiledComposition composition = Compile(workspace, ic, "desay", count, false);
        byte[] dp = Pattern(Capacity + delta, 0x61);
        CompositionExecutionResult result = await CompositionEngine.ExecuteAsync(composition.Plan,
            Inputs(false, dp, Pattern(0x37000, 0x21), Pattern(0x37000, 0x21)),
            (_, bytes, _, _, _) => ValueTask.FromResult(CompositionExternalProcessorResult.Success(bytes)),
            CancellationToken.None);
        if (delta < 0)
        {
            Assert.NotEqual(CompositionExecutionStatus.Succeeded, result.Status);
            Assert.Empty(result.OutputBytes.ToArray());
        }
        else
        {
            Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
            Assert.Equal(Capacity, result.OutputBytes.Length);
            Assert.Equal(dp.AsSpan(0x77000, Capacity - 0x77000).ToArray(), result.OutputBytes.Span[0x77000..].ToArray());
        }
    }

    /// <summary>The real host's diff policy rejects bytes adjacent to compiled header authority.</summary>
    [Theory]
    [InlineData("NT51950", "desay", 1, 0x40000)]
    [InlineData("NT51951", "desay", 0, 0x40000)]
    [InlineData("NT51950", "cascade", 2, 0x80000)]
    public void HostPolicyRejectsWritesOutsideCompiledHeaderSlices(string ic, string format, int count, int bankLength)
    {
        using var workspace = TempWorkspace.Create("nfc-ab-format-write-boundary");
        CompiledComposition composition = Compile(workspace, ic, format, count, false);
        ExternalProcessorInvocation invocation = composition.Plan.OrderedOperations
            .Single(operation => operation.ExternalProcessorInvocation is not null).ExternalProcessorInvocation!;
        var policy = new ChangedRangePolicy(invocation.AllowedWriteRanges);
        byte[] original = Pattern(bankLength * 2, 0x61);
        byte[] allowed = [.. original];
        foreach (int offset in HeaderOffsets)
        {
            allowed.AsSpan(bankLength + offset, 4).Fill(0xFE);
            foreach (int adjacentOffset in new[] { offset - 1, offset + 4 })
            {
                byte[] mutated = [.. original];
                mutated[bankLength + adjacentOffset] ^= 0xFF;
                ChangedRangeVerdict verdict = policy.Evaluate(ByteDiff.FindChangedRanges(original, mutated));
                Assert.False(verdict.IsAllowed);
                Assert.Equal([new ByteRange(bankLength + adjacentOffset, 1)], verdict.ViolatingRanges);
            }
        }

        Assert.True(policy.Evaluate(ByteDiff.FindChangedRanges(original, allowed)).IsAllowed);
        byte[] wrongBank = [.. original];
        wrongBank[0xA100] ^= 0xFF;
        Assert.False(policy.Evaluate(ByteDiff.FindChangedRanges(original, wrongBank)).IsAllowed);
    }

    private static CompiledComposition Compile(TempWorkspace workspace, string ic, string format, int count, bool dummy)
    {
        V2CompositionPlanCompileResult result = CompileResult(workspace, ic, format, count, dummy);
        Assert.True(result.IsCompiled, string.Join(" | ", result.Issues.Select(issue => $"{issue.Code}: {issue.Message}")));
        return Assert.IsType<CompiledComposition>(result.CompiledComposition);
    }

    private static V2CompositionPlanCompileResult CompileResult(
        TempWorkspace workspace, string ic, string format, int count, bool dummy)
    {
        return AbMergeCandidateTestSupport.LoadSourceCandidateCatalog(workspace, "nt51950-ab-merge", BundleHash).Compile(
            $"{ic.ToLowerInvariant()}-ab-merge-{format}", format == "desay" ? "0.2.1" : "0.4.0", ic, ExperienceIds.AbMerge, Capacity,
            count == 0 ? null : new TopologySelection(count, $"{count} IC", TopologySelectionSource.Requested, "test"),
            [], selectedInputSlotIds: dummy ? [] : ["dp-ab-input"]);
    }

    private static CompositionExecutionInput Inputs(bool dummy, byte[] dp, byte[] tpA, byte[] tpB)
    {
        var values = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["tp-a-input"] = tpA,
            ["tp-b-input"] = tpB,
        };
        if (!dummy)
        {
            values.Add("dp-ab-input", dp);
        }

        return new CompositionExecutionInput(values);
    }

    private static byte[] Pattern(int length, int seed)
    {
        byte[] result = new byte[length];
        for (int index = 0; index < result.Length; index++)
        {
            result[index] = (byte)(((index * 17) + seed) % 251);
        }

        return result;
    }
}
