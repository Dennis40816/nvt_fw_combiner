using System.Text.RegularExpressions;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Closed artifact classes accepted by composition profile slots.</summary>
internal enum CompositionProfileArtifactClass
{
    TpFirmware,
    DpFirmware,
    ReferenceImage,
    CtrlRamReplacement,
    Auxiliary,
}

/// <summary>Closed input binding cardinality.</summary>
internal enum CompositionProfileSlotCardinality
{
    ExactlyOne,
    ZeroOrOne,
    OneOrMore,
}

/// <summary>Closed input length rule kind.</summary>
// Values mirror compiled fingerprint wire codes; retired values 3, 4, and 5 stay reserved.
internal enum CompositionProfileLengthRuleKind
{
    ExactBytes = 0,
    ExactResolvedMapCapacity = 1,
    Bounded = 2,
    SourceViewCoverage = 6,
}

/// <summary>Base value for one normalized input length rule.</summary>
internal abstract record CompositionProfileLengthRule
{
    protected CompositionProfileLengthRule(CompositionProfileLengthRuleKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown input length rule kind.");
        }

        Kind = kind;
    }

    internal CompositionProfileLengthRuleKind Kind { get; }
}

/// <summary>Requires one exact positive input length.</summary>
internal sealed record ExactBytesLengthRule : CompositionProfileLengthRule
{
    internal ExactBytesLengthRule(long bytes)
        : base(CompositionProfileLengthRuleKind.ExactBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        Bytes = bytes;
    }

    internal long Bytes { get; }
}

/// <summary>Requires the uniquely resolved map capacity.</summary>
internal sealed record ExactResolvedMapCapacityLengthRule()
    : CompositionProfileLengthRule(CompositionProfileLengthRuleKind.ExactResolvedMapCapacity);

/// <summary>Accepts a closed positive input length interval.</summary>
internal sealed record BoundedLengthRule : CompositionProfileLengthRule
{
    internal BoundedLengthRule(long minimumBytes, long maximumBytes)
        : base(CompositionProfileLengthRuleKind.Bounded)
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
        : base(CompositionProfileLengthRuleKind.SourceViewCoverage)
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

/// <summary>Closed transient input normalization kind.</summary>
internal enum CompositionProfileInputNormalizationKind
{
    None,
    PadShorter,
    TruncateCtrlRam,
}

/// <summary>Base value for one normalized transient input policy.</summary>
internal abstract record CompositionProfileInputNormalization
{
    protected CompositionProfileInputNormalization(CompositionProfileInputNormalizationKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown input normalization kind.");
        }

        Kind = kind;
    }

    internal CompositionProfileInputNormalizationKind Kind { get; }
}

/// <summary>Preserves immutable input bytes without transient normalization.</summary>
internal sealed record NoInputNormalization()
    : CompositionProfileInputNormalization(CompositionProfileInputNormalizationKind.None);

/// <summary>Pads a shorter transient DP replacement buffer with an evidenced byte.</summary>
internal sealed record PadShorterInputNormalization : CompositionProfileInputNormalization
{
    internal PadShorterInputNormalization(byte fillByte, string evidenceRef)
        : base(CompositionProfileInputNormalizationKind.PadShorter)
    {
        EvidenceRef = CompositionProfileValueRules.RequireId(evidenceRef, nameof(evidenceRef));
        FillByte = fillByte;
    }

    internal byte FillByte { get; }

    internal string EvidenceRef { get; }
}

/// <summary>Truncates only a transient CtrlRAM replacement buffer and emits a warning.</summary>
internal sealed record TruncateCtrlRamInputNormalization : CompositionProfileInputNormalization
{
    internal TruncateCtrlRamInputNormalization(string warningIssueCode, string evidenceRef)
        : base(CompositionProfileInputNormalizationKind.TruncateCtrlRam)
    {
        WarningIssueCode = CompositionProfileValueRules.RequireIssueCode(
            warningIssueCode,
            nameof(warningIssueCode));
        EvidenceRef = CompositionProfileValueRules.RequireId(evidenceRef, nameof(evidenceRef));
    }

    internal string WarningIssueCode { get; }

    internal string EvidenceRef { get; }
}

/// <summary>Immutable map-independent profile input slot and acceptance policy.</summary>
internal sealed partial class CompositionProfileInputSlot
{
    private readonly string[] _acceptedExtensions;

    internal CompositionProfileInputSlot(
        string slotId,
        string role,
        CompositionProfileArtifactClass artifactClass,
        bool required,
        CompositionProfileSlotCardinality cardinality,
        IEnumerable<string> acceptedExtensions,
        CompositionProfileLengthRule lengthRule,
        CompositionProfileInputNormalization normalization,
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

    internal CompositionProfileArtifactClass ArtifactClass { get; }

    internal bool Required { get; }

    internal CompositionProfileSlotCardinality Cardinality { get; }

    internal IReadOnlyList<string> AcceptedExtensions { get; }

    internal CompositionProfileLengthRule LengthRule { get; }

    internal CompositionProfileInputNormalization Normalization { get; }

    internal string? NotApplicableReason { get; }

    private static void ValidateFirmwarePolicy(
        CompositionProfileArtifactClass artifactClass,
        CompositionProfileLengthRule lengthRule,
        CompositionProfileInputNormalization normalization)
    {
        if (artifactClass == CompositionProfileArtifactClass.TpFirmware &&
            (!IsApprovedTpLengthRule(lengthRule) ||
             normalization.Kind != CompositionProfileInputNormalizationKind.None))
        {
            throw new ArgumentException(
                "TP firmware requires one approved unnormalized section or exact length rule.");
        }

        if (artifactClass == CompositionProfileArtifactClass.DpFirmware &&
            lengthRule.Kind is not CompositionProfileLengthRuleKind.ExactResolvedMapCapacity and
                not CompositionProfileLengthRuleKind.SourceViewCoverage)
        {
            throw new ArgumentException("DP firmware requires an approved DP length rule.");
        }

        if (artifactClass == CompositionProfileArtifactClass.ReferenceImage &&
            (lengthRule.Kind != CompositionProfileLengthRuleKind.ExactResolvedMapCapacity ||
             normalization.Kind != CompositionProfileInputNormalizationKind.None))
        {
            throw new ArgumentException("Reference images require exact map capacity without normalization.");
        }

        if (normalization.Kind == CompositionProfileInputNormalizationKind.PadShorter &&
            artifactClass != CompositionProfileArtifactClass.DpFirmware)
        {
            throw new ArgumentException("Short-input padding is restricted to DP firmware.");
        }

        if (normalization.Kind == CompositionProfileInputNormalizationKind.TruncateCtrlRam &&
            artifactClass != CompositionProfileArtifactClass.CtrlRamReplacement)
        {
            throw new ArgumentException("CtrlRAM truncation requires a CtrlRAM replacement artifact.");
        }

        if (lengthRule is SourceViewCoverageLengthRule { RequiredEndExclusive: not null } &&
            (artifactClass is CompositionProfileArtifactClass.ReferenceImage or
                CompositionProfileArtifactClass.CtrlRamReplacement ||
             normalization.Kind != CompositionProfileInputNormalizationKind.None))
        {
            throw new ArgumentException(
                "Declared-prefix input authority is restricted to unnormalized immutable Merge sources.");
        }

        if (lengthRule is SourceViewCoverageLengthRule sourceView &&
            (artifactClass is CompositionProfileArtifactClass.ReferenceImage or
                CompositionProfileArtifactClass.CtrlRamReplacement ||
             normalization.Kind != CompositionProfileInputNormalizationKind.None ||
             (sourceView.MaximumOuterLength is not null &&
              artifactClass != CompositionProfileArtifactClass.TpFirmware)))
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
