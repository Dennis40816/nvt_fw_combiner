using System.Security.Cryptography;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Behavior evidence for compiler-owned declared-prefix input inspection.</summary>
public sealed class DeclaredPrefixInputInspectorTests
{
    private const string ShortCode = "TEST_INPUT_REQUIRED_PREFIX_MISSING";
    private const string OuterLengthCode = "TEST_INPUT_OUTER_LENGTH_UNEXPECTED";

    /// <summary>A source one byte short is blocking and cannot claim an accepted snapshot.</summary>
    [Fact]
    public void OneByteShortBlocksWithoutAcceptedSnapshot()
    {
        CompiledInputArtifactInspectionResult result = Inspect(
            PrefixRequirement(requiredEndExclusive: 16, expectedOuterLengths: [16]),
            Sequence(15));

        Assert.Equal(15, result.ActualLength);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Sequence(15))), result.ActualSha256);
        Assert.Equal(16, result.RequiredEndExclusive);
        Assert.Equal([16L], result.ExpectedOuterLengths);
        Assert.Null(result.AcceptedSnapshotRange);
        Assert.Null(result.AcceptedSnapshotSha256);
        Assert.Null(result.IgnoredTrailingRange);
        Assert.Equal(0, result.IgnoredTrailingBytes);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Blocking, result.Severity);
        Assert.Equal(ShortCode, result.IssueCode);
        Assert.True(result.BlocksBuild);
        Assert.Equal(CompiledInputArtifactInspectionNextAction.SelectCompatibleInput, result.NextAction);
    }

    /// <summary>An exact source retains identical full-source and accepted-prefix identities.</summary>
    [Fact]
    public void ExactLengthProducesReadyAcceptedPrefixIdentity()
    {
        byte[] source = Sequence(16);

        CompiledInputArtifactInspectionResult result = Inspect(
            PrefixRequirement(requiredEndExclusive: 16, expectedOuterLengths: [16]),
            source);

        Assert.Equal(result.ActualSha256, result.AcceptedSnapshotSha256);
        Assert.Equal(new ByteRange(0, 16), result.AcceptedSnapshotRange);
        Assert.Null(result.IgnoredTrailingRange);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Valid, result.Severity);
        Assert.Equal(InputArtifactInspectionIssueCodes.Ready, result.IssueCode);
        Assert.False(result.BlocksBuild);
        Assert.Equal(CompiledInputArtifactInspectionNextAction.None, result.NextAction);
    }

    /// <summary>A one-byte unexpected tail is accepted, identified, and reported as half-open.</summary>
    [Fact]
    public void OneByteTailProducesWarningAndIgnoredHalfOpenRange()
    {
        byte[] source = Sequence(17);

        CompiledInputArtifactInspectionResult result = Inspect(
            PrefixRequirement(requiredEndExclusive: 16, expectedOuterLengths: [16]),
            source);

        Assert.Equal(new ByteRange(0, 16), result.AcceptedSnapshotRange);
        Assert.Equal(ByteRange.FromStartEndExclusive(16, 17), result.IgnoredTrailingRange);
        Assert.Equal(1, result.IgnoredTrailingBytes);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Warning, result.Severity);
        Assert.Equal(OuterLengthCode, result.IssueCode);
        Assert.False(result.BlocksBuild);
        Assert.Equal(CompiledInputArtifactInspectionNextAction.ReviewIgnoredTrailingBytes, result.NextAction);
    }

    /// <summary>A large tail cannot influence the accepted snapshot identity.</summary>
    [Fact]
    public void LargeTailPreservesPrefixIdentityAndFullSourceEvidence()
    {
        byte[] exact = Sequence(16);
        byte[] tailed = new byte[checked(16 + (1024 * 1024))];
        exact.CopyTo(tailed, 0);
        Array.Fill(tailed, (byte)0xA5, 16, tailed.Length - 16);

        CompiledInputArtifactInspectionResult exactResult = Inspect(
            PrefixRequirement(requiredEndExclusive: 16, expectedOuterLengths: [16]),
            exact);
        CompiledInputArtifactInspectionResult tailedResult = Inspect(
            PrefixRequirement(requiredEndExclusive: 16, expectedOuterLengths: [16]),
            tailed);

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(tailed)), tailedResult.ActualSha256);
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(tailed.AsSpan(0, 16))),
            tailedResult.AcceptedSnapshotSha256);
        Assert.Equal(exactResult.AcceptedSnapshotSha256, tailedResult.AcceptedSnapshotSha256);
        Assert.NotEqual(exactResult.ActualSha256, tailedResult.ActualSha256);
        Assert.Equal(1024 * 1024, tailedResult.IgnoredTrailingBytes);
        Assert.Equal(
            ByteRange.FromStartEndExclusive(16, tailed.LongLength),
            tailedResult.IgnoredTrailingRange);
    }

    /// <summary>Known larger containers remain valid while still exposing their ignored tail.</summary>
    [Fact]
    public void ExpectedLargerOuterLengthIsValidAndStillReportsIgnoredTail()
    {
        CompiledInputArtifactInspectionResult result = Inspect(
            PrefixRequirement(requiredEndExclusive: 16, expectedOuterLengths: [16, 32]),
            Sequence(32));

        Assert.Equal(CompiledInputArtifactInspectionSeverity.Valid, result.Severity);
        Assert.Equal(InputArtifactInspectionIssueCodes.Ready, result.IssueCode);
        Assert.Equal(16, result.IgnoredTrailingBytes);
        Assert.Equal(ByteRange.FromStartEndExclusive(16, 32), result.IgnoredTrailingRange);
        Assert.Equal(CompiledInputArtifactInspectionNextAction.None, result.NextAction);
    }

    /// <summary>An accepted required prefix with no tail still reports an unexpected outer container.</summary>
    [Fact]
    public void UnexpectedRequiredLengthWithoutTailRequestsOuterLengthReview()
    {
        CompiledInputArtifactInspectionResult result = Inspect(
            PrefixRequirement(requiredEndExclusive: 16, expectedOuterLengths: [32]),
            Sequence(16));

        Assert.Equal(new ByteRange(0, 16), result.AcceptedSnapshotRange);
        Assert.Null(result.IgnoredTrailingRange);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Warning, result.Severity);
        Assert.Equal(OuterLengthCode, result.IssueCode);
        Assert.False(result.BlocksBuild);
        Assert.Equal(
            CompiledInputArtifactInspectionNextAction.ReviewUnexpectedOuterLength,
            result.NextAction);
    }

    /// <summary>Inspection snapshots identities without changing caller-owned bytes.</summary>
    [Fact]
    public void InspectionIsDeterministicAndDoesNotMutateCallerBytes()
    {
        byte[] source = Sequence(64);
        byte[] before = [.. source];
        CompiledSourceViewCoverageInputLengthRequirement requirement = PrefixRequirement(16, [16]);

        CompiledInputArtifactInspectionResult first = Inspect(requirement, source);
        CompiledInputArtifactInspectionResult second = Inspect(requirement, source);

        Assert.Equal(before, source);
        Assert.Equal(first.ActualSha256, second.ActualSha256);
        Assert.Equal(first.AcceptedSnapshotSha256, second.AcceptedSnapshotSha256);
        Assert.Equal(first.IssueCode, second.IssueCode);
        Assert.Equal(first.IgnoredTrailingRange, second.IgnoredTrailingRange);
    }

    /// <summary>Reinspection observes a changed source instead of treating stale diagnostics as Build authority.</summary>
    [Fact]
    public void ReinspectionDetectsChangedSourceIdentity()
    {
        byte[] source = Sequence(16);
        CompiledSourceViewCoverageInputLengthRequirement requirement = PrefixRequirement(16, [16]);
        CompiledInputArtifactInspectionResult loadInspection = Inspect(requirement, source);
        string loadActualHash = loadInspection.ActualSha256;
        string loadAcceptedHash = loadInspection.AcceptedSnapshotSha256!;

        source[0] ^= 0xFF;
        CompiledInputArtifactInspectionResult buildReinspection = Inspect(requirement, source);

        Assert.Equal(loadActualHash, loadInspection.ActualSha256);
        Assert.Equal(loadAcceptedHash, loadInspection.AcceptedSnapshotSha256);
        Assert.NotEqual(loadInspection.ActualSha256, buildReinspection.ActualSha256);
        Assert.NotEqual(loadInspection.AcceptedSnapshotSha256, buildReinspection.AcceptedSnapshotSha256);
        Assert.False(buildReinspection.BlocksBuild);
    }

    /// <summary>Policy rejects invalid, descending, duplicate, and runtime-overflow geometry.</summary>
    [Fact]
    public void CompiledRequirementRejectsInvalidOrOverflowGeometry()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => PrefixRequirement(0, [1]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            PrefixRequirement((long)int.MaxValue + 1, [(long)int.MaxValue + 1]));
        _ = Assert.Throws<ArgumentException>(() => PrefixRequirement(16, [(long)int.MaxValue + 1]));
        _ = Assert.Throws<ArgumentException>(() => PrefixRequirement(16, []));
        _ = Assert.Throws<ArgumentException>(() => PrefixRequirement(16, [15]));
        _ = Assert.Throws<ArgumentException>(() => PrefixRequirement(16, [32, 16]));
        _ = Assert.Throws<ArgumentException>(() => PrefixRequirement(16, [16, 16]));
    }

    /// <summary>The generic inspector accepts no IC, filename, PID, version, or hash routing input.</summary>
    [Fact]
    public void InspectionAuthorityHasNoInformationalRoutingParameters()
    {
        System.Reflection.MethodInfo inspect = Assert.Single(
            typeof(CompiledInputArtifactInspectionService)
                .GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public),
            static method => method.Name == "Inspect");
        Type[] parameters =
        [
            .. inspect.GetParameters().Select(static parameter => parameter.ParameterType),
        ];

        Assert.Equal([typeof(CompiledComposition), typeof(string), typeof(ReadOnlyMemory<byte>)], parameters);
    }

    /// <summary>The public use case obtains geometry and issue codes only from the compiled contract.</summary>
    [Fact]
    public void CompiledContractProjectionPreservesTypedHealthAndEvidence()
    {
        CompiledInputArtifactInspectionResult result = Inspect(
            PrefixRequirement(requiredEndExclusive: 16, expectedOuterLengths: [16]),
            Sequence(17));

        Assert.Equal("source-input", result.AddressSpaceId);
        Assert.Equal("source-input-slot", result.SlotId);
        Assert.Equal(17, result.ActualLength);
        Assert.Equal(16, result.RequiredEndExclusive);
        Assert.Equal([16L], result.ExpectedOuterLengths);
        var mutableExpectedLengths = (IList<long>)result.ExpectedOuterLengths;
        Assert.True(mutableExpectedLengths.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => mutableExpectedLengths[0] = 32);
        Assert.Equal(new ByteRange(0, 16), result.AcceptedSnapshotRange);
        Assert.NotNull(result.AcceptedSnapshotSha256);
        Assert.Equal(ByteRange.FromStartEndExclusive(16, 17), result.IgnoredTrailingRange);
        Assert.Equal(1, result.IgnoredTrailingBytes);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Warning, result.Severity);
        Assert.Equal(OuterLengthCode, result.IssueCode);
        Assert.False(result.BlocksBuild);
        Assert.Equal(
            CompiledInputArtifactInspectionNextAction.ReviewIgnoredTrailingBytes,
            result.NextAction);
    }

    /// <summary>Exact compiled inputs block both a short and a tailed source without prefix truncation.</summary>
    [Fact]
    public void CompiledContractProjectionRejectsExactLengthMismatch()
    {
        CompiledInputArtifactInspectionResult result = Inspect(
            new CompiledExactResolvedMapCapacityInputLengthRequirement(16),
            Sequence(17));

        Assert.Equal(16, result.RequiredEndExclusive);
        Assert.Equal([16L], result.ExpectedOuterLengths);
        Assert.Null(result.AcceptedSnapshotRange);
        Assert.Null(result.IgnoredTrailingRange);
        Assert.Equal(CompiledInputArtifactInspectionSeverity.Blocking, result.Severity);
        Assert.Equal(CompositionIssueCodes.InputAddressSpaceLengthMismatch, result.IssueCode);
        Assert.True(result.BlocksBuild);
        Assert.Equal(
            CompiledInputArtifactInspectionNextAction.SelectCompatibleInput,
            result.NextAction);
    }

    /// <summary>Unknown spaces cannot select a compiled inspection policy.</summary>
    [Fact]
    public void CompiledContractProjectionRejectsUnownedPolicySelection()
    {
        CompiledComposition declaredPrefix = Composition(PrefixRequirement(16, [16]));
        _ = Assert.Throws<ArgumentException>(() =>
            CompiledInputArtifactInspectionService.Inspect(
                declaredPrefix,
                "other-input",
                Sequence(16)));
    }

    private static CompiledSourceViewCoverageInputLengthRequirement PrefixRequirement(
        long requiredEndExclusive,
        IReadOnlyList<long> expectedOuterLengths)
    {
        return new CompiledSourceViewCoverageInputLengthRequirement(
            expectedOuterLengths,
            OuterLengthCode,
            requiredEndExclusive,
            ShortCode);
    }

    private static CompiledInputArtifactInspectionResult Inspect(
        CompiledInputLengthRequirement requirement,
        ReadOnlyMemory<byte> sourceBytes)
    {
        return CompiledInputArtifactInspectionService.Inspect(
            Composition(requirement),
            "source-input",
            sourceBytes);
    }

    private static CompiledComposition Composition(CompiledInputLengthRequirement requirement)
    {
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 16, 0),
            [
                new AddressSpace(
                    "source-input",
                    16,
                    AddressSpaceMutability.Immutable,
                    inputOversizePolicy: InputOversizePolicy.ExtractDeclaredRange),
                new AddressSpace("output-image", 16, AddressSpaceMutability.Mutable),
            ],
            [
                CompositionOperation.CopyRange(
                    "copy-source",
                    100,
                    "source-input",
                    new ByteRange(0, 16),
                    "output-image",
                    new ByteRange(0, 16),
                    OverlapPolicy.Reject,
                    "Copy the synthetic source."),
            ]);
        return CompiledCompositionTestFactory.Create(
            plan,
            new TestCompiledCompositionIdentity(
                "declared-prefix-test",
                "1.0.0",
                "NT-TEST",
                "standard",
                ExperienceIds.StandardMerge,
                CompositionKind.Merge),
            "declared-prefix.bin",
            inputLengthRequirement: requirement);
    }

    private static byte[] Sequence(int length)
    {
        byte[] bytes = new byte[length];
        for (int index = 0; index < bytes.Length; index++)
        {
            bytes[index] = checked((byte)(index % 251));
        }

        return bytes;
    }
}
