using System.Security.Cryptography;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Behavior evidence for the Application-owned declared-prefix diagnostic substrate.</summary>
public sealed class DeclaredPrefixInputInspectorTests
{
    private const string ShortCode = "TEST_INPUT_REQUIRED_PREFIX_MISSING";
    private const string OuterLengthCode = "TEST_INPUT_OUTER_LENGTH_UNEXPECTED";

    /// <summary>A source one byte short is blocking and cannot claim an accepted snapshot.</summary>
    [Fact]
    public void OneByteShortBlocksWithoutAcceptedSnapshot()
    {
        DeclaredPrefixInputInspectionPolicy policy = Policy(requiredEndExclusive: 16, expectedOuterLengths: [16]);

        InputArtifactInspection result = DeclaredPrefixInputInspector.Inspect(policy, Sequence(15));

        Assert.Equal(InputArtifactInspectionLifecycle.Inspected, result.Lifecycle);
        Assert.Equal(15, result.ActualSource.Length);
        Assert.Equal(16, result.RequiredEndExclusive);
        Assert.Equal([16L], result.ExpectedOuterLengths);
        Assert.Null(result.AcceptedSnapshot);
        Assert.Null(result.AcceptedSnapshotRange);
        Assert.Null(result.IgnoredTrailingRange);
        Assert.Equal(0, result.IgnoredTrailingBytes);
        Assert.Equal(InputArtifactInspectionSeverity.Blocking, result.Severity);
        Assert.Equal(ShortCode, result.IssueCode);
        Assert.Equal(InputArtifactBuildImpact.Blocked, result.BuildImpact);
        Assert.Equal(InputArtifactInspectionNextAction.SelectCompatibleInput, result.NextAction);
    }

    /// <summary>An exact source retains identical full-source and accepted-prefix identities.</summary>
    [Fact]
    public void ExactLengthProducesReadyAcceptedPrefixIdentity()
    {
        byte[] source = Sequence(16);

        InputArtifactInspection result = DeclaredPrefixInputInspector.Inspect(
            Policy(requiredEndExclusive: 16, expectedOuterLengths: [16]),
            source);

        InputArtifactContentIdentity accepted = Assert.IsType<InputArtifactContentIdentity>(result.AcceptedSnapshot);
        Assert.Equal(result.ActualSource, accepted);
        Assert.Equal(new ByteRange(0, 16), result.AcceptedSnapshotRange);
        Assert.Null(result.IgnoredTrailingRange);
        Assert.Equal(InputArtifactInspectionSeverity.Valid, result.Severity);
        Assert.Equal(InputArtifactInspectionIssueCodes.Ready, result.IssueCode);
        Assert.Equal(InputArtifactBuildImpact.None, result.BuildImpact);
        Assert.Equal(InputArtifactInspectionNextAction.None, result.NextAction);
    }

    /// <summary>A one-byte unexpected tail is accepted, identified, and reported as half-open.</summary>
    [Fact]
    public void OneByteTailProducesWarningAndIgnoredHalfOpenRange()
    {
        byte[] source = Sequence(17);

        InputArtifactInspection result = DeclaredPrefixInputInspector.Inspect(
            Policy(requiredEndExclusive: 16, expectedOuterLengths: [16]),
            source);

        Assert.Equal(new ByteRange(0, 16), result.AcceptedSnapshotRange);
        Assert.Equal(ByteRange.FromStartEndExclusive(16, 17), result.IgnoredTrailingRange);
        Assert.Equal(1, result.IgnoredTrailingBytes);
        Assert.Equal(InputArtifactInspectionSeverity.Warning, result.Severity);
        Assert.Equal(OuterLengthCode, result.IssueCode);
        Assert.Equal(InputArtifactBuildImpact.None, result.BuildImpact);
        Assert.Equal(InputArtifactInspectionNextAction.ReviewIgnoredTrailingBytes, result.NextAction);
    }

    /// <summary>A large tail cannot influence the accepted snapshot identity.</summary>
    [Fact]
    public void LargeTailPreservesPrefixIdentityAndFullSourceEvidence()
    {
        byte[] exact = Sequence(16);
        byte[] tailed = new byte[checked(16 + (1024 * 1024))];
        exact.CopyTo(tailed, 0);
        Array.Fill(tailed, (byte)0xA5, 16, tailed.Length - 16);

        InputArtifactInspection exactResult = DeclaredPrefixInputInspector.Inspect(
            Policy(requiredEndExclusive: 16, expectedOuterLengths: [16]),
            exact);
        InputArtifactInspection tailedResult = DeclaredPrefixInputInspector.Inspect(
            Policy(requiredEndExclusive: 16, expectedOuterLengths: [16]),
            tailed);

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(tailed)), tailedResult.ActualSource.Sha256);
        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(tailed.AsSpan(0, 16))),
            tailedResult.AcceptedSnapshot!.Sha256);
        Assert.Equal(exactResult.AcceptedSnapshot, tailedResult.AcceptedSnapshot);
        Assert.NotEqual(exactResult.ActualSource, tailedResult.ActualSource);
        Assert.Equal(1024 * 1024, tailedResult.IgnoredTrailingBytes);
        Assert.Equal(
            ByteRange.FromStartEndExclusive(16, tailed.LongLength),
            tailedResult.IgnoredTrailingRange);
    }

    /// <summary>Known larger containers remain valid while still exposing their ignored tail.</summary>
    [Fact]
    public void ExpectedLargerOuterLengthIsValidAndStillReportsIgnoredTail()
    {
        InputArtifactInspection result = DeclaredPrefixInputInspector.Inspect(
            Policy(requiredEndExclusive: 16, expectedOuterLengths: [16, 32]),
            Sequence(32));

        Assert.Equal(InputArtifactInspectionSeverity.Valid, result.Severity);
        Assert.Equal(InputArtifactInspectionIssueCodes.Ready, result.IssueCode);
        Assert.Equal(16, result.IgnoredTrailingBytes);
        Assert.Equal(ByteRange.FromStartEndExclusive(16, 32), result.IgnoredTrailingRange);
        Assert.Equal(InputArtifactInspectionNextAction.None, result.NextAction);
    }

    /// <summary>Inspection snapshots identities without changing caller-owned bytes.</summary>
    [Fact]
    public void InspectionIsDeterministicAndDoesNotMutateCallerBytes()
    {
        byte[] source = Sequence(64);
        byte[] before = [.. source];
        DeclaredPrefixInputInspectionPolicy policy = Policy(16, [16]);

        InputArtifactInspection first = DeclaredPrefixInputInspector.Inspect(policy, source);
        InputArtifactInspection second = DeclaredPrefixInputInspector.Inspect(policy, source);

        Assert.Equal(before, source);
        Assert.Equal(first.ActualSource, second.ActualSource);
        Assert.Equal(first.AcceptedSnapshot, second.AcceptedSnapshot);
        Assert.Equal(first.IssueCode, second.IssueCode);
        Assert.Equal(first.IgnoredTrailingRange, second.IgnoredTrailingRange);
    }

    /// <summary>Reinspection observes a changed source instead of treating stale diagnostics as Build authority.</summary>
    [Fact]
    public void ReinspectionDetectsChangedSourceIdentity()
    {
        byte[] source = Sequence(16);
        DeclaredPrefixInputInspectionPolicy policy = Policy(16, [16]);
        InputArtifactInspection loadInspection = DeclaredPrefixInputInspector.Inspect(policy, source);
        string loadActualHash = loadInspection.ActualSource.Sha256;
        string loadAcceptedHash = loadInspection.AcceptedSnapshot!.Sha256;

        source[0] ^= 0xFF;
        InputArtifactInspection buildReinspection = DeclaredPrefixInputInspector.Inspect(policy, source);

        Assert.Equal(loadActualHash, loadInspection.ActualSource.Sha256);
        Assert.Equal(loadAcceptedHash, loadInspection.AcceptedSnapshot!.Sha256);
        Assert.NotEqual(loadInspection.ActualSource.Sha256, buildReinspection.ActualSource.Sha256);
        Assert.NotEqual(loadInspection.AcceptedSnapshot!.Sha256, buildReinspection.AcceptedSnapshot!.Sha256);
        Assert.Equal(InputArtifactBuildImpact.None, buildReinspection.BuildImpact);
    }

    /// <summary>Policy rejects invalid, descending, duplicate, and runtime-overflow geometry.</summary>
    [Fact]
    public void PolicyRejectsInvalidOrOverflowGeometry()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Policy(0, [1]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => Policy((long)int.MaxValue + 1, [(long)int.MaxValue + 1]));
        _ = Assert.Throws<ArgumentException>(() => Policy(16, [(long)int.MaxValue + 1]));
        _ = Assert.Throws<ArgumentException>(() => Policy(16, []));
        _ = Assert.Throws<ArgumentException>(() => Policy(16, [15]));
        _ = Assert.Throws<ArgumentException>(() => Policy(16, [32, 16]));
        _ = Assert.Throws<ArgumentException>(() => Policy(16, [16, 16]));
    }

    /// <summary>The generic inspector accepts no IC, filename, PID, version, or hash routing input.</summary>
    [Fact]
    public void InspectionAuthorityHasNoInformationalRoutingParameters()
    {
        Type[] parameters =
        [
            .. typeof(DeclaredPrefixInputInspector)
            .GetMethod("Inspect", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
            .GetParameters()
            .Select(static parameter => parameter.ParameterType),
        ];

        Assert.Equal([typeof(DeclaredPrefixInputInspectionPolicy), typeof(ReadOnlyMemory<byte>)], parameters);
    }

    /// <summary>The public use case obtains geometry and issue codes only from the compiled contract.</summary>
    [Fact]
    public void CompiledContractProjectionPreservesTypedHealthAndEvidence()
    {
        CompiledInputContract contract = Contract(requiredEndExclusive: 16, expectedOuterLengths: [16]);

        CompiledInputArtifactInspectionResult result =
            CompiledInputArtifactInspectionService.Inspect(
                contract,
                "source-input",
                Sequence(17));

        Assert.Equal("source-input", result.AddressSpaceId);
        Assert.Equal("source-slot", result.SlotId);
        Assert.Equal(17, result.ActualLength);
        Assert.Equal(16, result.RequiredEndExclusive);
        Assert.Equal([16L], result.ExpectedOuterLengths);
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
        CompiledInputSlotRequirement slot = CompiledInputSlotTestFactory.Create(
            "source-slot",
            "source",
            CompiledInputArtifactClass.Auxiliary,
            required: true,
            CompiledInputSlotCardinality.ExactlyOne,
            [".bin"],
            new CompiledExactResolvedMapCapacityInputLengthRequirement(16),
            new CompiledNoInputNormalization());
        var contract = new CompiledInputContract(
            [slot],
            [new CompiledInputSpaceBinding(
                "source-input",
                slot.SlotId,
                CompiledInputInstancePolicy.Singleton)]);

        CompiledInputArtifactInspectionResult result =
            CompiledInputArtifactInspectionService.Inspect(contract, "source-input", Sequence(17));

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
        CompiledInputContract declaredPrefix = Contract(16, [16]);
        _ = Assert.Throws<ArgumentException>(() =>
            CompiledInputArtifactInspectionService.Inspect(
                declaredPrefix,
                "other-input",
                Sequence(16)));
    }

    private static DeclaredPrefixInputInspectionPolicy Policy(
        long requiredEndExclusive,
        IEnumerable<long> expectedOuterLengths)
    {
        return new DeclaredPrefixInputInspectionPolicy(
            requiredEndExclusive,
            expectedOuterLengths,
            ShortCode,
            OuterLengthCode);
    }

    private static CompiledInputContract Contract(
        long requiredEndExclusive,
        IReadOnlyList<long> expectedOuterLengths)
    {
        CompiledInputSlotRequirement slot = CompiledInputSlotTestFactory.Create(
            "source-slot",
            "source",
            CompiledInputArtifactClass.Auxiliary,
            required: true,
            CompiledInputSlotCardinality.ExactlyOne,
            [".bin"],
            new CompiledSourceViewCoverageInputLengthRequirement(
                expectedOuterLengths,
                OuterLengthCode,
                requiredEndExclusive,
                ShortCode),
            new CompiledNoInputNormalization());
        return new CompiledInputContract(
            [slot],
            [new CompiledInputSpaceBinding(
                "source-input",
                slot.SlotId,
                CompiledInputInstancePolicy.Singleton)]);
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
