using System.Security.Cryptography;
using NvtFwCombiner.Application.Capabilities;
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
    private const string Nt51929BundleHash = "e043dad07ffd7670c96b07b7732a9889b33a4b00b143eba56ad02cac1bb59cb5";

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
            "0.7.0",
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
        if (StringComparer.Ordinal.Equals(
                composition.V2Details.ExperienceId,
                ExperienceIds.StandardMerge))
        {
            return await V2StandardMergeGoldenTestSupport.PreviewAsync(composition, inputs);
        }

        var reader = new FakeArtifactReader(inputs.ToDictionary(
            item => $"{composition.V2Details.ProfileId}:{item.Key}",
            static item => item.Value,
            StringComparer.Ordinal));
        InputArtifactBinding[] bindings =
        [
            .. inputs.Keys.Order(StringComparer.Ordinal).Select(addressSpaceId =>
                CreateInputBinding(composition.V2Details.ProfileId, addressSpaceId)),
        ];
        var service = new CompositionRunService(
            reader,
            new FakeClock([
                new DateTimeOffset(2026, 7, 30, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 7, 30, 0, 0, 1, TimeSpan.Zero),
            ]));
        IcNumberSelection? icNumberSelection =
            composition.V2Details.CompositionKind == CompositionKind.Replace
                ? new IcNumberSelection(IcNumberInputMode.SingleSelector, ["single"])
                : null;
        var request = new CompositionRunRequest(
            $"source-shape-{composition.V2Details.Provenance.Context.MemberId.ToLowerInvariant()}",
            composition,
            bindings,
            composition.V2Details.OutputNamingRequirement.FileNameTemplate,
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
