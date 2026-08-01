using System.Security.Cryptography;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>
/// Byte-level evidence that canonical address-bearing section views accept both their
/// minimum standalone shape and a compatible same-IC FlashCode without changing output.
/// </summary>
public sealed class CanonicalSourceProjectionByteShapeTests
{
    private const string Nt51929BundleDirectory = "nt51929-standard-merge";
    private const string Nt51929BundleHash = "c67e8ee68cd06f4e1a169abab7c900dc457bbd03f29da770fb7feefb848be380";
    private const string Nt51928StandardBundleDirectory = "nt51928-standard-merge";
    private const string Nt51928StandardBundleHash = "895ccc579907874af31e5a9f132e0ffb4c10e150f1ca8aad23a0f4f8bac317ca";

    /// <summary>
    /// Owner-approved NT51929 bytes prove both DP and TP slots produce the same full image
    /// from the minimum address-bearing source or the complete generated FlashCode.
    /// </summary>
    [Fact]
    public async Task Nt51929DpAndTpAcceptStandaloneOrSameIcFlashCodeAsync()
    {
        GoldenEvidence golden = ReadGoldenEvidence("51929");
        CompiledComposition composition = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
                Nt51929BundleDirectory,
                Nt51929BundleHash),
            "nt51929-standard-merge-gen-flash",
            "0.6.0",
            "NT51929");

        Assert.Equal(
            CompiledFirmwareArtifactKind.FlashCode,
            CompiledFirmwareArtifactClassifier.Classify(composition, golden.ExpectedOutput).Kind);
        await AssertEquivalentSourceShapesAsync(
            composition,
            golden.ExpectedOutput,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = golden.Inputs[CompositionAddressSpaceIds.DpInput],
                [CompositionAddressSpaceIds.TpInput] = golden.Inputs[CompositionAddressSpaceIds.TpInput],
            },
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.DpInput] = golden.ExpectedOutput,
                [CompositionAddressSpaceIds.TpInput] = golden.ExpectedOutput,
            },
            [
                ExpectedOperation.Copy(
                    "copy-tp",
                    100,
                    CompositionAddressSpaceIds.TpInput,
                    new ByteRange(0x7000, 0x39000)),
                ExpectedOperation.Copy(
                    "copy-dp",
                    200,
                    CompositionAddressSpaceIds.DpInput,
                    new ByteRange(0x0000, 0x6000)),
            ]);
    }

    /// <summary>
    /// Owner-approved NT51928 FlashCode proves selected Initial Code and LDC replacement
    /// sources can be either minimum address-bearing artifacts or the same complete FlashCode.
    /// </summary>
    [Theory]
    [InlineData(CompositionAddressSpaceIds.InitialCodeReplacement)]
    [InlineData(CompositionAddressSpaceIds.LdcReplacement)]
    public async Task Nt51928InitialCodeAndLdcAcceptStandaloneOrSameIcFlashCodeAsync(
        string replacementAddressSpaceId)
    {
        GoldenEvidence golden = ReadGoldenEvidence("51928");
        byte[] ownerFlashCode = golden.Inputs[
            replacementAddressSpaceId == CompositionAddressSpaceIds.InitialCodeReplacement
                ? CompositionAddressSpaceIds.DpInput
                : CompositionAddressSpaceIds.LdcInput];
        CompiledComposition classificationComposition = V2StandardMergeGoldenTestSupport.CompileV2(
            V2StandardMergeGoldenTestSupport.LoadDeployedCatalog(
                Nt51928StandardBundleDirectory,
                Nt51928StandardBundleHash),
            "nt51928-standard-merge-gen-flash",
            "0.8.0",
            "NT51928",
            requestedMapCapacity: golden.ExpectedOutput.LongLength,
            selectedInputSlotIds: [CompositionAddressSpaceIds.LdcInput]);
        Assert.Equal(
            CompiledFirmwareArtifactKind.FlashCode,
            CompiledFirmwareArtifactClassifier.Classify(classificationComposition, ownerFlashCode).Kind);

        bool registered = WorkbenchCompositionService.TryCompileBuiltInV2DpReplace(
            "NT51928",
            golden.ExpectedOutput.LongLength,
            [replacementAddressSpaceId],
            out CompiledComposition? compiledComposition,
            out ResolvedCapability? resolvedCapability,
            out IReadOnlyList<CompositionIssue> issues);
        Assert.True(registered);
        Assert.Empty(issues);
        CompiledComposition composition = Assert.IsType<CompiledComposition>(compiledComposition);
        ExpectedOperation expectedOperation = replacementAddressSpaceId switch
        {
            CompositionAddressSpaceIds.InitialCodeReplacement =>
                ExpectedOperation.Replace(
                    "replace-dp-code",
                    100,
                    CompositionAddressSpaceIds.InitialCodeReplacement,
                    new ByteRange(0x3C000, 0x4000)),
            CompositionAddressSpaceIds.LdcReplacement =>
                ExpectedOperation.Replace(
                    "replace-ldc-code",
                    200,
                    CompositionAddressSpaceIds.LdcReplacement,
                    new ByteRange(0x40000, 0x22000)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(replacementAddressSpaceId),
                replacementAddressSpaceId,
                "Unsupported NT51928 replacement source."),
        };
        Assert.True(
            ownerFlashCode.AsSpan(
                checked((int)expectedOperation.Range.Start),
                checked((int)expectedOperation.Range.Length))
            .SequenceEqual(golden.ExpectedOutput.AsSpan(
                checked((int)expectedOperation.Range.Start),
                checked((int)expectedOperation.Range.Length))));
        byte[] reference = CreateDistinctReference(
            golden.ExpectedOutput,
            expectedOperation.Range);

        await AssertEquivalentSourceShapesAsync(
            composition,
            golden.ExpectedOutput,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [replacementAddressSpaceId] = ownerFlashCode,
            },
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [replacementAddressSpaceId] = ownerFlashCode,
            },
            [expectedOperation],
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.ReferenceBase] = reference,
            },
            Assert.IsType<ResolvedCapability>(resolvedCapability));
    }

    private static async ValueTask AssertEquivalentSourceShapesAsync(
        CompiledComposition composition,
        byte[] expectedOutput,
        IReadOnlyDictionary<string, byte[]> standaloneSourceArtifacts,
        Dictionary<string, byte[]> compatibleFlashCodeArtifacts,
        IReadOnlyList<ExpectedOperation> expectedOperations,
        IReadOnlyDictionary<string, byte[]>? fixedInputs = null,
        ResolvedCapability? resolvedCapability = null)
    {
        string[] projectedAddressSpaceIds =
        [
            .. standaloneSourceArtifacts.Keys.Order(StringComparer.Ordinal),
        ];
        AssertCompiledOperations(composition, expectedOperations);
        int sourceShapeCount = 1 << projectedAddressSpaceIds.Length;
        for (int sourceShapeMask = 0; sourceShapeMask < sourceShapeCount; sourceShapeMask++)
        {
            var inputs = new Dictionary<string, byte[]>(
                fixedInputs ?? new Dictionary<string, byte[]>(StringComparer.Ordinal),
                StringComparer.Ordinal);
            for (int sourceIndex = 0; sourceIndex < projectedAddressSpaceIds.Length; sourceIndex++)
            {
                string addressSpaceId = projectedAddressSpaceIds[sourceIndex];
                AddressSpace addressSpace = composition.Plan.AddressSpaces.Single(space =>
                    StringComparer.Ordinal.Equals(space.AddressSpaceId, addressSpaceId));
                bool useFlashCode = (sourceShapeMask & (1 << sourceIndex)) != 0;
                byte[] source = useFlashCode
                    ? compatibleFlashCodeArtifacts[addressSpaceId]
                    : standaloneSourceArtifacts[addressSpaceId]
                        .AsSpan(0, checked((int)addressSpace.Length))
                        .ToArray();
                inputs[addressSpaceId] = source;
            }

            CompositionRunResult result = await PreviewAsync(
                composition,
                inputs,
                resolvedCapability);
            V2StandardMergeGoldenTestSupport.AssertSuccessfulGoldenOutput(
                result,
                composition,
                expectedOutput);
            AssertOperationTrace(expectedOperations, result);

            foreach (string addressSpaceId in projectedAddressSpaceIds)
            {
                AssertInputSnapshot(
                    composition,
                    result,
                    addressSpaceId,
                    inputs[addressSpaceId]);
            }
        }
    }

    private static void AssertInputSnapshot(
        CompiledComposition composition,
        CompositionRunResult result,
        string addressSpaceId,
        byte[] source)
    {
        AddressSpace addressSpace = composition.Plan.AddressSpaces.Single(space =>
            StringComparer.Ordinal.Equals(space.AddressSpaceId, addressSpaceId));
        InputArtifactSummary input = Assert.Single(
            result.Report.Inputs,
            candidate => StringComparer.Ordinal.Equals(candidate.AddressSpaceId, addressSpaceId));
        InputArtifactExecutionSnapshotSummary snapshot =
            Assert.IsType<InputArtifactExecutionSnapshotSummary>(input.ExecutionSnapshot);

        Assert.Equal(source.LongLength, input.Size);
        Assert.Equal(new ByteRange(0, addressSpace.Length), snapshot.AcceptedRange);
        Assert.Equal(
            Hash(source.AsSpan(0, checked((int)addressSpace.Length))),
            snapshot.AcceptedSha256);
        if (addressSpace.Length == source.LongLength)
        {
            Assert.Null(snapshot.IgnoredTrailingRange);
        }
        else
        {
            Assert.Equal(
                ByteRange.FromStartEndExclusive(addressSpace.Length, source.LongLength),
                snapshot.IgnoredTrailingRange);
        }
    }

    private static async ValueTask<CompositionRunResult> PreviewAsync(
        CompiledComposition composition,
        IReadOnlyDictionary<string, byte[]> inputs,
        ResolvedCapability? resolvedCapability)
    {
        var reader = new FakeArtifactReader(inputs.ToDictionary(
            item => $"{composition.ProfileId}:{item.Key}",
            static item => item.Value,
            StringComparer.Ordinal));
        InputArtifactBinding[] bindings =
        [
            .. inputs.Keys.Order(StringComparer.Ordinal).Select(addressSpaceId =>
                CreateInputBinding(composition.ProfileId, addressSpaceId)),
        ];
        var service = new CompositionRunService(
            reader,
            new FakeClock([
                new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 30, 0, 0, 1, TimeSpan.Zero),
            ]));
        IcNumberSelection? icNumberSelection =
            composition.CompositionKind == CompositionKind.Replace
                ? new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])
                : null;
        var request = new CompositionRunRequest(
            $"source-shape-{composition.IcId.ToLowerInvariant()}",
            composition,
            bindings,
            composition.DefaultOutputFileName,
            icNumberSelection: icNumberSelection,
            resolvedCapability: resolvedCapability);
        return await service.PreviewAsync(request, CancellationToken.None).ConfigureAwait(false);
    }

    private static InputArtifactBinding CreateInputBinding(string profileId, string addressSpaceId)
    {
        (string originalFileName, CompiledInputArtifactClass artifactClass) = addressSpaceId switch
        {
            CompositionAddressSpaceIds.DpInput =>
                ("dp.bin", CompiledInputArtifactClass.DpFirmware),
            CompositionAddressSpaceIds.TpInput =>
                ("tp.bin", CompiledInputArtifactClass.TpFirmware),
            CompositionAddressSpaceIds.ReferenceBase =>
                ("reference.bin", CompiledInputArtifactClass.ReferenceImage),
            CompositionAddressSpaceIds.InitialCodeReplacement =>
                ("initial-code.bin", CompiledInputArtifactClass.DpFirmware),
            CompositionAddressSpaceIds.LdcReplacement =>
                ("ldc.bin", CompiledInputArtifactClass.Auxiliary),
            _ => throw new ArgumentOutOfRangeException(
                nameof(addressSpaceId),
                addressSpaceId,
                "Unsupported canonical source projection input space."),
        };
        return new InputArtifactBinding(
            addressSpaceId,
            addressSpaceId,
            $"{profileId}:{addressSpaceId}",
            originalFileName,
            artifactClass);
    }

    private static void AssertOperationTrace(
        IReadOnlyList<ExpectedOperation> expectedOperations,
        CompositionRunResult result)
    {
        Assert.Equal(
            expectedOperations.Select(static operation =>
                (operation.OperationId, (string?)operation.SourceSpaceId, (ByteRange?)operation.Range,
                    operation.TargetSpaceId, operation.Range)),
            result.Report.Operations.Select(static operation =>
                (operation.OperationId, operation.SourceSpaceId, operation.SourceRange,
                    operation.TargetSpaceId, operation.TargetRange)));
    }

    private static void AssertCompiledOperations(
        CompiledComposition composition,
        IReadOnlyList<ExpectedOperation> expectedOperations)
    {
        Assert.Equal(
            expectedOperations.Select(static operation =>
                (operation.OperationId, operation.Sequence, operation.Kind,
                    (string?)operation.SourceSpaceId, (ByteRange?)operation.Range,
                    operation.TargetSpaceId, operation.Range)),
            composition.Plan.OrderedOperations.Select(static operation =>
                (operation.OperationId, operation.Sequence, operation.Kind,
                    operation.SourceSpaceId, operation.SourceRange,
                    operation.TargetSpaceId, operation.TargetRange)));
    }

    private static GoldenEvidence ReadGoldenEvidence(string ic)
    {
        System.Text.Json.JsonElement goldenCase =
            V2StandardMergeGoldenTestSupport.ReadGoldenCase(ic);
        return new GoldenEvidence(
            V2StandardMergeGoldenTestSupport.ReadInputs(goldenCase.GetProperty("inputs")),
            V2StandardMergeGoldenTestSupport.ReadManifestFile(
                goldenCase.GetProperty("expectedOutput")));
    }

    private static byte[] CreateDistinctReference(byte[] expectedOutput, ByteRange replacedRange)
    {
        byte[] reference = [.. expectedOutput];
        int first = checked((int)replacedRange.Start);
        int middle = checked((int)(replacedRange.Start + (replacedRange.Length / 2)));
        int last = checked((int)(replacedRange.EndExclusive - 1));
        reference[first] ^= 0xFF;
        reference[middle] ^= 0xFF;
        reference[last] ^= 0xFF;
        Assert.NotEqual(expectedOutput, reference);
        return reference;
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    private sealed record GoldenEvidence(
        IReadOnlyDictionary<string, byte[]> Inputs,
        byte[] ExpectedOutput);

    private sealed record ExpectedOperation(
        string OperationId,
        int Sequence,
        CompositionOperationKind Kind,
        string SourceSpaceId,
        ByteRange Range,
        string TargetSpaceId)
    {
        internal static ExpectedOperation Copy(
            string operationId,
            int sequence,
            string sourceSpaceId,
            ByteRange range)
        {
            return new ExpectedOperation(
                operationId,
                sequence,
                CompositionOperationKind.CopyRange,
                sourceSpaceId,
                range,
                CompositionAddressSpaceIds.OutputImage);
        }

        internal static ExpectedOperation Replace(
            string operationId,
            int sequence,
            string sourceSpaceId,
            ByteRange range)
        {
            return new ExpectedOperation(
                operationId,
                sequence,
                CompositionOperationKind.ReplaceRange,
                sourceSpaceId,
                range,
                CompositionAddressSpaceIds.OutputImage);
        }
    }
}
