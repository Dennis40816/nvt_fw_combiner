using System.Text.RegularExpressions;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Base value for one normalized input length rule.</summary>
internal abstract record CompositionProfileLengthRule;

/// <summary>Requires one exact positive input length.</summary>
internal sealed record ExactBytesLengthRule : CompositionProfileLengthRule
{
    internal ExactBytesLengthRule(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        Bytes = bytes;
    }

    internal long Bytes { get; }
}

/// <summary>Requires the uniquely resolved map capacity.</summary>
internal sealed record ExactResolvedMapCapacityLengthRule()
    : CompositionProfileLengthRule;

/// <summary>Accepts a closed positive input length interval.</summary>
internal sealed record BoundedLengthRule : CompositionProfileLengthRule
{
    internal BoundedLengthRule(long minimumBytes, long maximumBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumBytes);
        if (maximumBytes < minimumBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumBytes),
                maximumBytes,
                "Maximum input length cannot be smaller than its minimum.");
        }

        MinimumBytes = minimumBytes;
        MaximumBytes = maximumBytes;
    }

    internal long MinimumBytes { get; }

    internal long MaximumBytes { get; }
}

/// <summary>
/// Accepts an immutable section source using compiled-read coverage or one
/// required end, with optional complete-container diagnostics and outer bound.
/// </summary>
internal sealed record SourceViewCoverageLengthRule : CompositionProfileLengthRule
{
    private readonly long[] _expectedOuterLengths;

    internal SourceViewCoverageLengthRule(
        IReadOnlyList<long>? expectedOuterLengths = null,
        string? unexpectedOuterLengthIssueCode = null,
        long? maximumOuterLength = null,
        long? requiredEndExclusive = null,
        string? shortInputIssueCode = null)
    {
        if (expectedOuterLengths is not null && unexpectedOuterLengthIssueCode is null)
        {
            throw new ArgumentException(
                "Expected outer lengths and their warning issue code must be declared together.");
        }

        _expectedOuterLengths = NormalizeExpectedOuterLengths(expectedOuterLengths);
        if ((requiredEndExclusive is null) != (shortInputIssueCode is null))
        {
            throw new ArgumentException(
                "Required end and its short-input issue code must be declared together.");
        }

        if (requiredEndExclusive is <= 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredEndExclusive),
                requiredEndExclusive,
                "Required end must fit the in-memory execution snapshot limit.");
        }

        if (requiredEndExclusive is { } requiredEnd &&
            (_expectedOuterLengths.Length == 0 ||
             _expectedOuterLengths.Any(length => length < requiredEnd || length > int.MaxValue)))
        {
            throw new ArgumentException(
                "Expected outer lengths must fit the in-memory limit and cover the required end.",
                nameof(expectedOuterLengths));
        }

        if (maximumOuterLength is <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumOuterLength),
                maximumOuterLength,
                "Maximum outer length must be positive when declared.");
        }

        if (maximumOuterLength is not null && requiredEndExclusive is not null)
        {
            throw new ArgumentException("Maximum outer length and required end cannot be combined.");
        }

        ExpectedOuterLengths = Array.AsReadOnly(_expectedOuterLengths);
        MaximumOuterLength = maximumOuterLength;
        RequiredEndExclusive = requiredEndExclusive;
        ShortInputIssueCode = shortInputIssueCode is null
            ? null
            : CompositionProfileValueRules.RequireIssueCode(shortInputIssueCode, nameof(shortInputIssueCode));
        UnexpectedOuterLengthIssueCode = unexpectedOuterLengthIssueCode is null
            ? null
            : CompositionProfileValueRules.RequireIssueCode(
                unexpectedOuterLengthIssueCode,
                nameof(unexpectedOuterLengthIssueCode));
    }

    internal IReadOnlyList<long> ExpectedOuterLengths { get; }

    internal string? UnexpectedOuterLengthIssueCode { get; }

    internal long? MaximumOuterLength { get; }

    internal long? RequiredEndExclusive { get; }

    internal string? ShortInputIssueCode { get; }

    private static long[] NormalizeExpectedOuterLengths(IReadOnlyList<long>? expectedOuterLengths)
    {
        if (expectedOuterLengths is null)
        {
            return [];
        }

        if (expectedOuterLengths.Count is 0 or
            > InputLengthPolicyLimits.MaximumExpectedInputLengths)
        {
            throw new ArgumentException(
                $"Expected outer lengths must contain between 1 and {InputLengthPolicyLimits.MaximumExpectedInputLengths} values.",
                nameof(expectedOuterLengths));
        }

        long[] normalized = new long[expectedOuterLengths.Count];
        long previous = 0;
        for (int index = 0; index < expectedOuterLengths.Count; index++)
        {
            long value = expectedOuterLengths[index];
            if (value <= 0 || (index > 0 && value <= previous))
            {
                throw new ArgumentException(
                    "Expected outer lengths must be positive and strictly ascending.",
                    nameof(expectedOuterLengths));
            }

            normalized[index] = value;
            previous = value;
        }

        return normalized;
    }
}

/// <summary>Immutable map-independent profile input slot and acceptance policy.</summary>
internal sealed partial class CompositionProfileInputSlot
{
    private readonly string[] _acceptedExtensions;

    internal CompositionProfileInputSlot(
        string slotId,
        string role,
        CompiledInputArtifactClass artifactClass,
        bool required,
        CompiledInputSlotCardinality cardinality,
        IEnumerable<string> acceptedExtensions,
        CompositionProfileLengthRule lengthRule,
        CompiledInputNormalization normalization,
        string? notApplicableReason = null)
    {
        SlotId = CompositionProfileValueRules.RequireId(slotId, nameof(slotId));
        Role = CompositionProfileValueRules.RequireId(role, nameof(role));
        if (!Enum.IsDefined(artifactClass))
        {
            throw new ArgumentOutOfRangeException(nameof(artifactClass), artifactClass, "Unknown artifact class.");
        }

        if (!Enum.IsDefined(cardinality))
        {
            throw new ArgumentOutOfRangeException(nameof(cardinality), cardinality, "Unknown slot cardinality.");
        }

        ArgumentNullException.ThrowIfNull(lengthRule);
        ArgumentNullException.ThrowIfNull(normalization);
        ValidateFirmwarePolicy(artifactClass, lengthRule, normalization);
        _acceptedExtensions = SnapshotExtensions(acceptedExtensions);

        ArtifactClass = artifactClass;
        Required = required;
        Cardinality = cardinality;
        AcceptedExtensions = Array.AsReadOnly(_acceptedExtensions);
        LengthRule = lengthRule;
        Normalization = normalization;
        if (notApplicableReason is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(notApplicableReason);
        }

        NotApplicableReason = notApplicableReason;
    }

    internal string SlotId { get; }

    internal string Role { get; }

    internal CompiledInputArtifactClass ArtifactClass { get; }

    internal bool Required { get; }

    internal CompiledInputSlotCardinality Cardinality { get; }

    internal IReadOnlyList<string> AcceptedExtensions { get; }

    internal CompositionProfileLengthRule LengthRule { get; }

    internal CompiledInputNormalization Normalization { get; }

    internal string? NotApplicableReason { get; }

    private static void ValidateFirmwarePolicy(
        CompiledInputArtifactClass artifactClass,
        CompositionProfileLengthRule lengthRule,
        CompiledInputNormalization normalization)
    {
        if (normalization is CompiledPadShorterInputNormalization padded)
        {
            _ = CompositionProfileValueRules.RequireId(padded.EvidenceRef, nameof(padded.EvidenceRef));
        }

        if (normalization is CompiledTruncateCtrlRamInputNormalization truncated)
        {
            _ = CompositionProfileValueRules.RequireIssueCode(
                truncated.WarningIssueCode,
                nameof(truncated.WarningIssueCode));
            _ = CompositionProfileValueRules.RequireId(truncated.EvidenceRef, nameof(truncated.EvidenceRef));
        }

        if (artifactClass == CompiledInputArtifactClass.TpFirmware &&
            (!IsApprovedTpLengthRule(lengthRule) ||
             normalization is not CompiledNoInputNormalization))
        {
            throw new ArgumentException(
                "TP firmware requires one approved unnormalized section or exact length rule.");
        }

        if (artifactClass == CompiledInputArtifactClass.DpFirmware &&
            lengthRule is not (ExactResolvedMapCapacityLengthRule or SourceViewCoverageLengthRule))
        {
            throw new ArgumentException("DP firmware requires an approved DP length rule.");
        }

        if (artifactClass == CompiledInputArtifactClass.ReferenceImage &&
            (lengthRule is not ExactResolvedMapCapacityLengthRule ||
             normalization is not CompiledNoInputNormalization))
        {
            throw new ArgumentException("Reference images require exact map capacity without normalization.");
        }

        if (normalization is CompiledPadShorterInputNormalization &&
            artifactClass != CompiledInputArtifactClass.DpFirmware)
        {
            throw new ArgumentException("Short-input padding is restricted to DP firmware.");
        }

        if (normalization is CompiledTruncateCtrlRamInputNormalization &&
            artifactClass != CompiledInputArtifactClass.CtrlRamReplacement)
        {
            throw new ArgumentException("CtrlRAM truncation requires a CtrlRAM replacement artifact.");
        }

        if (lengthRule is SourceViewCoverageLengthRule { RequiredEndExclusive: not null } &&
            (artifactClass is CompiledInputArtifactClass.ReferenceImage or
                CompiledInputArtifactClass.CtrlRamReplacement ||
             normalization is not CompiledNoInputNormalization))
        {
            throw new ArgumentException(
                "Declared-prefix input authority is restricted to unnormalized immutable Merge sources.");
        }

        if (lengthRule is SourceViewCoverageLengthRule sourceView &&
            (artifactClass is CompiledInputArtifactClass.ReferenceImage or
                CompiledInputArtifactClass.CtrlRamReplacement ||
             normalization is not CompiledNoInputNormalization ||
             (sourceView.MaximumOuterLength is not null &&
              artifactClass != CompiledInputArtifactClass.TpFirmware)))
        {
            throw new ArgumentException(
                "Source-view coverage is restricted to unnormalized immutable section sources.");
        }
    }

    private static bool IsApprovedTpLengthRule(CompositionProfileLengthRule lengthRule)
    {
        return lengthRule is SourceViewCoverageLengthRule
        {
            RequiredEndExclusive: null or <= 262144,
        } or
            ExactBytesLengthRule { Bytes: <= 262144 };
    }

    private static string[] SnapshotExtensions(IEnumerable<string> acceptedExtensions)
    {
        ArgumentNullException.ThrowIfNull(acceptedExtensions);
        string[] snapshot = [.. acceptedExtensions];
        if (snapshot.Length == 0)
        {
            throw new ArgumentException("Input slots require an accepted extension.", nameof(acceptedExtensions));
        }

        if (snapshot.Any(extension => extension is null || !ExtensionPattern().IsMatch(extension)))
        {
            throw new ArgumentException(
                "Accepted extensions must use canonical dot-prefixed form.",
                nameof(acceptedExtensions));
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Accepted extensions must be ordinally unique.", nameof(acceptedExtensions));
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }

    [GeneratedRegex("^\\.[A-Za-z0-9]+$", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking)]
    private static partial Regex ExtensionPattern();
}
