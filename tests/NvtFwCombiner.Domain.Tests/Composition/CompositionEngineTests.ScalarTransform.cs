using System.Numerics;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompositionEngineTests
{
    /// <summary>Verifies little-endian transforms read and write the declared unsigned value.</summary>
    [Fact]
    public void ScalarTransformAppliesLittleEndianAddend()
    {
        CompositionPlan plan = CreateTransformPlan(
            new ScalarTransform(
                ScalarTransformWidth.TwoBytes,
                ScalarTransformByteOrder.LittleEndian,
                0x100,
                expectedBefore: 0x1234,
                ScalarTransformOverflowPolicy.Reject));

        CompositionExecutionResult result = CompositionEngine.Execute(
            plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["source"] = [0x34, 0x12],
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x34, 0x13], result.OutputBytes.ToArray());
        MutationRecord mutation = Assert.Single(result.Mutations);
        Assert.Equal(CompositionOperationKind.TransformScalar, mutation.OperationKind);
        Assert.Equal([new ByteRange(0, 2)], mutation.ChangedRanges);
    }

    /// <summary>Verifies big-endian transforms preserve the declared byte order.</summary>
    [Fact]
    public void ScalarTransformAppliesBigEndianAddend()
    {
        CompositionPlan plan = CreateTransformPlan(
            new ScalarTransform(
                ScalarTransformWidth.TwoBytes,
                ScalarTransformByteOrder.BigEndian,
                -0x100,
                expectedBefore: 0x1234,
                ScalarTransformOverflowPolicy.Reject));

        CompositionExecutionResult result = CompositionEngine.Execute(
            plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["source"] = [0x12, 0x34],
            }));

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x11, 0x34], result.OutputBytes.ToArray());
    }

    /// <summary>Verifies four-byte carry and eight-byte boundary values use the declared byte order.</summary>
    [Fact]
    public void ScalarTransformAppliesFourAndEightByteBoundaryValues()
    {
        CompositionExecutionResult fourByte = ExecuteTransform(
            new ScalarTransform(
                ScalarTransformWidth.FourBytes,
                ScalarTransformByteOrder.LittleEndian,
                1,
                expectedBefore: 0x00FFFFFF,
                ScalarTransformOverflowPolicy.Reject),
            [0xFF, 0xFF, 0xFF, 0x00]);
        CompositionExecutionResult eightByteLittleEndian = ExecuteTransform(
            new ScalarTransform(
                ScalarTransformWidth.EightBytes,
                ScalarTransformByteOrder.LittleEndian,
                1,
                expectedBefore: ulong.MaxValue - 1,
                ScalarTransformOverflowPolicy.Reject),
            [0xFE, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);
        CompositionExecutionResult eightByteBigEndian = ExecuteTransform(
            new ScalarTransform(
                ScalarTransformWidth.EightBytes,
                ScalarTransformByteOrder.BigEndian,
                1,
                expectedBefore: ulong.MaxValue - 1,
                ScalarTransformOverflowPolicy.Reject),
            [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFE]);

        Assert.Equal([0x00, 0x00, 0x00, 0x01], fourByte.OutputBytes.ToArray());
        Assert.Equal([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], eightByteLittleEndian.OutputBytes.ToArray());
        Assert.Equal([0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF], eightByteBigEndian.OutputBytes.ToArray());
    }

    /// <summary>Verifies a transform can read the current mutable buffer and rewrite the same scalar range.</summary>
    [Fact]
    public void ScalarTransformReadsMutableSourceBeforeWritingSameRange()
    {
        var transform = new ScalarTransform(
            ScalarTransformWidth.OneByte,
            ScalarTransformByteOrder.LittleEndian,
            1,
            expectedBefore: 0x7F,
            ScalarTransformOverflowPolicy.Reject);
        CompositionPlan plan = CreateBlankPlan(
            1,
            CompositionOperation.PatchScalar(
                "seed-scalar",
                10,
                "output-image",
                new ByteRange(0, 1),
                [0x7F],
                OverlapPolicy.Reject,
                "seed mutable scalar"),
            CompositionOperation.TransformScalar(
                "relocate-scalar",
                20,
                "output-image",
                new ByteRange(0, 1),
                "output-image",
                new ByteRange(0, 1),
                transform,
                OverlapPolicy.ReplaceExisting,
                "adjust mutable scalar"));

        CompositionExecutionResult result = CompositionEngine.Execute(plan, EmptyInput());

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        Assert.Equal([0x80], result.OutputBytes.ToArray());
        Assert.Equal(["seed-scalar", "relocate-scalar"], result.Mutations.Select(static mutation => mutation.OperationId));
    }

    /// <summary>Verifies an expected-before mismatch fails without exposing an output or partial mutation trace.</summary>
    [Fact]
    public void ScalarTransformExpectedValueMismatchFailsClosed()
    {
        CompositionPlan plan = CreateTransformPlan(
            new ScalarTransform(
                ScalarTransformWidth.OneByte,
                ScalarTransformByteOrder.LittleEndian,
                1,
                expectedBefore: 2,
                ScalarTransformOverflowPolicy.Reject));

        CompositionExecutionResult result = CompositionEngine.Execute(
            plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["source"] = [1],
            }));

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.True(result.OutputBytes.IsEmpty);
        Assert.Empty(result.Mutations);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(CompositionIssueCodes.ExecutionScalarTransformExpectedValueMismatch, issue.Code);
        Assert.Equal("transform-scalar", issue.OperationId);
    }

    /// <summary>Verifies unsigned scalar overflows and underflows fail closed without writing the target.</summary>
    [Theory]
    [InlineData(0xFF, 1)]
    [InlineData(0x00, -1)]
    public void ScalarTransformOverflowFailsClosed(byte sourceValue, int addend)
    {
        CompositionPlan plan = CreateTransformPlan(
            new ScalarTransform(
                ScalarTransformWidth.OneByte,
                ScalarTransformByteOrder.LittleEndian,
                addend,
                expectedBefore: null,
                ScalarTransformOverflowPolicy.Reject));

        CompositionExecutionResult result = CompositionEngine.Execute(
            plan,
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["source"] = [sourceValue],
            }));

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.True(result.OutputBytes.IsEmpty);
        CompositionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(CompositionIssueCodes.ExecutionScalarTransformOverflow, issue.Code);
        Assert.Equal("transform-scalar", issue.OperationId);
    }

    /// <summary>Verifies eight-byte overflows and underflows reject both byte orders without converting past ulong.</summary>
    [Fact]
    public void EightByteScalarTransformOverflowAndUnderflowFailClosed()
    {
        foreach (ScalarTransformByteOrder byteOrder in Enum.GetValues<ScalarTransformByteOrder>())
        {
            CompositionExecutionResult overflow = ExecuteTransform(
                new ScalarTransform(
                    ScalarTransformWidth.EightBytes,
                    byteOrder,
                    1,
                    expectedBefore: null,
                    ScalarTransformOverflowPolicy.Reject),
                [0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF]);
            CompositionExecutionResult underflow = ExecuteTransform(
                new ScalarTransform(
                    ScalarTransformWidth.EightBytes,
                    byteOrder,
                    -1,
                    expectedBefore: null,
                    ScalarTransformOverflowPolicy.Reject),
                [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00]);

            Assert.Equal(CompositionExecutionStatus.Failed, overflow.Status);
            Assert.Equal(CompositionIssueCodes.ExecutionScalarTransformOverflow, Assert.Single(overflow.Issues).Code);
            Assert.True(overflow.OutputBytes.IsEmpty);
            Assert.Equal(CompositionExecutionStatus.Failed, underflow.Status);
            Assert.Equal(CompositionIssueCodes.ExecutionScalarTransformOverflow, Assert.Single(underflow.Issues).Code);
            Assert.True(underflow.OutputBytes.IsEmpty);
        }
    }

    /// <summary>Verifies partially overlapping mutable source and target ranges read the full source before writing.</summary>
    [Fact]
    public void ScalarTransformHandlesPartialMutableRangeOverlapInBothDirections()
    {
        CompositionExecutionResult forward = ExecuteMutableTransform(
            [0x01, 0x00, 0x00],
            new ByteRange(0, 2),
            new ByteRange(1, 2));
        CompositionExecutionResult reverse = ExecuteMutableTransform(
            [0x00, 0x01, 0x00],
            new ByteRange(1, 2),
            new ByteRange(0, 2));

        Assert.Equal(CompositionExecutionStatus.Succeeded, forward.Status);
        Assert.Equal([0x01, 0x02, 0x00], forward.OutputBytes.ToArray());
        Assert.Equal(CompositionExecutionStatus.Succeeded, reverse.Status);
        Assert.Equal([0x02, 0x00, 0x00], reverse.OutputBytes.ToArray());
    }

    /// <summary>Verifies a failed transform discards output and mutation trace even after an earlier mutation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ScalarTransformFailureAfterPriorMutationFailsAtomically(bool overflow)
    {
        ScalarTransform transform = overflow
            ? new ScalarTransform(
                ScalarTransformWidth.OneByte,
                ScalarTransformByteOrder.LittleEndian,
                1,
                expectedBefore: null,
                ScalarTransformOverflowPolicy.Reject)
            : new ScalarTransform(
                ScalarTransformWidth.OneByte,
                ScalarTransformByteOrder.LittleEndian,
                1,
                expectedBefore: 2,
                ScalarTransformOverflowPolicy.Reject);
        byte seed = overflow ? byte.MaxValue : (byte)1;
        CompositionPlan plan = CreateBlankPlan(
            1,
            CompositionOperation.PatchScalar(
                "seed-scalar",
                10,
                "output-image",
                new ByteRange(0, 1),
                [seed],
                OverlapPolicy.Reject,
                "seed scalar"),
            CompositionOperation.TransformScalar(
                "failing-transform",
                20,
                "output-image",
                new ByteRange(0, 1),
                "output-image",
                new ByteRange(0, 1),
                transform,
                OverlapPolicy.ReplaceExisting,
                "fail after seed"));

        CompositionExecutionResult result = CompositionEngine.Execute(plan, EmptyInput());

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        Assert.True(result.OutputBytes.IsEmpty);
        Assert.Empty(result.Mutations);
        Assert.Equal(
            overflow
                ? CompositionIssueCodes.ExecutionScalarTransformOverflow
                : CompositionIssueCodes.ExecutionScalarTransformExpectedValueMismatch,
            Assert.Single(result.Issues).Code);
    }

    /// <summary>Verifies impossible scalar declarations fail before a plan can be created.</summary>
    [Fact]
    public void ScalarTransformRejectsImpossibleDeclaredGeometryAndValues()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new ScalarTransform(
            ScalarTransformWidth.OneByte,
            ScalarTransformByteOrder.LittleEndian,
            new BigInteger(256),
            expectedBefore: null,
            ScalarTransformOverflowPolicy.Reject));
        _ = Assert.Throws<ArgumentException>(() => new ScalarTransform(
            ScalarTransformWidth.OneByte,
            ScalarTransformByteOrder.LittleEndian,
            1,
            expectedBefore: byte.MaxValue,
            ScalarTransformOverflowPolicy.Reject));
        ScalarTransform transform = new(
            ScalarTransformWidth.OneByte,
            ScalarTransformByteOrder.LittleEndian,
            1,
            expectedBefore: null,
            ScalarTransformOverflowPolicy.Reject);
        _ = Assert.Throws<ArgumentException>(() => CompositionOperation.TransformScalar(
            "invalid-transform",
            10,
            "source",
            new ByteRange(0, 2),
            "output-image",
            new ByteRange(0, 2),
            transform,
            OverlapPolicy.Reject,
            "invalid width"));
    }

    private static CompositionPlan CreateTransformPlan(ScalarTransform transform)
    {
        long width = transform.WidthBytes;
        return CreateBlankPlan(
            width,
            new AddressSpace("source", width, AddressSpaceMutability.Immutable),
            CompositionOperation.TransformScalar(
                "transform-scalar",
                10,
                "source",
                new ByteRange(0, width),
                "output-image",
                new ByteRange(0, width),
                transform,
                OverlapPolicy.Reject,
                "adjust scalar"));
    }

    private static CompositionExecutionResult ExecuteTransform(ScalarTransform transform, byte[] source)
    {
        return CompositionEngine.Execute(
            CreateTransformPlan(transform),
            new CompositionExecutionInput(new Dictionary<string, byte[]>
            {
                ["source"] = source,
            }));
    }

    private static CompositionExecutionResult ExecuteMutableTransform(
        byte[] seed,
        ByteRange sourceRange,
        ByteRange targetRange)
    {
        var transform = new ScalarTransform(
            ScalarTransformWidth.TwoBytes,
            ScalarTransformByteOrder.LittleEndian,
            1,
            expectedBefore: 1,
            ScalarTransformOverflowPolicy.Reject);
        CompositionPlan plan = CreateBlankPlan(
            seed.Length,
            CompositionOperation.PatchScalar(
                "seed-buffer",
                10,
                "output-image",
                new ByteRange(0, seed.Length),
                seed,
                OverlapPolicy.Reject,
                "seed buffer"),
            CompositionOperation.TransformScalar(
                "transform-overlap",
                20,
                "output-image",
                sourceRange,
                "output-image",
                targetRange,
                transform,
                OverlapPolicy.ReplaceExisting,
                "adjust overlapping scalar"));

        return CompositionEngine.Execute(plan, EmptyInput());
    }
}
