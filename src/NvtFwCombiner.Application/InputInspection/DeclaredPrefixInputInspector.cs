using System.Security.Cryptography;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.InputInspection;

/// <summary>
/// Inspects an immutable source against one compiler-owned declared-prefix policy without retaining
/// source bytes. This substrate is not connected to runtime admission until the compiled contract
/// carries the corresponding firmware authority.
/// </summary>
internal static class DeclaredPrefixInputInspector
{
    /// <summary>Creates one deterministic, path-free inspection from a private source snapshot.</summary>
    internal static InputArtifactInspection Inspect(
        DeclaredPrefixInputInspectionPolicy policy,
        ReadOnlyMemory<byte> sourceBytes)
    {
        ArgumentNullException.ThrowIfNull(policy);

        byte[] actualSnapshot = sourceBytes.ToArray();
        InputArtifactContentIdentity actualIdentity = Identity(actualSnapshot);
        if (actualSnapshot.LongLength < policy.RequiredEndExclusive)
        {
            return new InputArtifactInspection(
                actualIdentity,
                policy.RequiredEndExclusive,
                policy.ExpectedOuterLengths,
                acceptedSnapshot: null,
                acceptedSnapshotRange: null,
                ignoredTrailingRange: null,
                InputArtifactInspectionSeverity.Blocking,
                policy.ShortInputIssueCode,
                InputArtifactBuildImpact.Blocked,
                InputArtifactInspectionNextAction.SelectCompatibleInput);
        }

        int acceptedLength = checked((int)policy.RequiredEndExclusive);
        var acceptedIdentity = new InputArtifactContentIdentity(
            acceptedLength,
            Hash(actualSnapshot.AsSpan(0, acceptedLength)));
        ByteRange acceptedRange = new(0, acceptedLength);
        ByteRange? ignoredRange = actualSnapshot.LongLength > policy.RequiredEndExclusive
            ? ByteRange.FromStartEndExclusive(policy.RequiredEndExclusive, actualSnapshot.LongLength)
            : null;
        bool expectedOuterLength = policy.ExpectedOuterLengths.Contains(actualSnapshot.LongLength);

        return new InputArtifactInspection(
            actualIdentity,
            policy.RequiredEndExclusive,
            policy.ExpectedOuterLengths,
            acceptedIdentity,
            acceptedRange,
            ignoredRange,
            expectedOuterLength ? InputArtifactInspectionSeverity.Valid : InputArtifactInspectionSeverity.Warning,
            expectedOuterLength ? InputArtifactInspectionIssueCodes.Ready : policy.UnexpectedOuterLengthIssueCode,
            InputArtifactBuildImpact.None,
            expectedOuterLength
                ? InputArtifactInspectionNextAction.None
                : ignoredRange.HasValue
                    ? InputArtifactInspectionNextAction.ReviewIgnoredTrailingBytes
                    : InputArtifactInspectionNextAction.ReviewUnexpectedOuterLength);
    }

    private static InputArtifactContentIdentity Identity(ReadOnlySpan<byte> bytes)
    {
        return new InputArtifactContentIdentity(bytes.Length, Hash(bytes));
    }

    private static string Hash(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }
}
