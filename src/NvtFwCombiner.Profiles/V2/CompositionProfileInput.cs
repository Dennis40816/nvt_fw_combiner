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
internal enum CompositionProfileLengthRuleKind
{
    ExactBytes,
    ExactResolvedMapCapacity,
    Bounded,
    NormalDpExtractWithWarning,
    TpMaximum256K,
    DeclaredPrefixWithWarning,
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

/// <summary>Accepts one immutable declared prefix and retains full-source diagnostic authority.</summary>
internal sealed record DeclaredPrefixWithWarningLengthRule : CompositionProfileLengthRule
{
    private readonly long[] _expectedOuterLengths;

    internal DeclaredPrefixWithWarningLengthRule(
        long requiredEndExclusive,
        IReadOnlyList<long> expectedOuterLengths,
        string shortInputIssueCode,
        string unexpectedOuterLengthIssueCode)
        : base(CompositionProfileLengthRuleKind.DeclaredPrefixWithWarning)
    {
        if (requiredEndExclusive is <= 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredEndExclusive),
                requiredEndExclusive,
                "Required end must fit the in-memory execution snapshot limit.");
        }

        ArgumentNullException.ThrowIfNull(expectedOuterLengths);
        if (expectedOuterLengths.Count is 0 or > InputLengthPolicyLimits.MaximumExpectedInputLengths)
        {
            throw new ArgumentException(
                $"Expected outer lengths must contain between 1 and {InputLengthPolicyLimits.MaximumExpectedInputLengths} values.",
                nameof(expectedOuterLengths));
        }

        _expectedOuterLengths = new long[expectedOuterLengths.Count];
        long previous = 0;
        for (int index = 0; index < expectedOuterLengths.Count; index++)
        {
            long value = expectedOuterLengths[index];
            if (value < requiredEndExclusive || value > int.MaxValue || (index > 0 && value <= previous))
            {
                throw new ArgumentException(
                    "Expected outer lengths must fit the in-memory limit, cover the required end, and be strictly ascending.",
                    nameof(expectedOuterLengths));
            }

            _expectedOuterLengths[index] = value;
            previous = value;
        }

        RequiredEndExclusive = requiredEndExclusive;
        ExpectedOuterLengths = Array.AsReadOnly(_expectedOuterLengths);
        ShortInputIssueCode = CompositionProfileValueRules.RequireIssueCode(
            shortInputIssueCode,
            nameof(shortInputIssueCode));
        UnexpectedOuterLengthIssueCode = CompositionProfileValueRules.RequireIssueCode(
            unexpectedOuterLengthIssueCode,
            nameof(unexpectedOuterLengthIssueCode));
    }

    internal long RequiredEndExclusive { get; }

    internal IReadOnlyList<long> ExpectedOuterLengths { get; }

    internal string ShortInputIssueCode { get; }

    internal string UnexpectedOuterLengthIssueCode { get; }
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

/// <summary>Extracts declared Normal DP views and warns when the outer file length differs.</summary>
internal sealed record NormalDpExtractWithWarningLengthRule : CompositionProfileLengthRule
{
    private readonly long[] _expectedInputLengths;

    internal NormalDpExtractWithWarningLengthRule(
        string issueCode,
        IReadOnlyList<long>? expectedInputLengths = null)
        : base(CompositionProfileLengthRuleKind.NormalDpExtractWithWarning)
    {
        IssueCode = CompositionProfileValueRules.RequireIssueCode(issueCode, nameof(issueCode));
        _expectedInputLengths = NormalizeExpectedInputLengths(expectedInputLengths);
        ExpectedInputLengths = Array.AsReadOnly(_expectedInputLengths);
    }

    internal string IssueCode { get; }

    /// <summary>Optional outer-container lengths that suppress the profile-owned extraction warning.</summary>
    internal IReadOnlyList<long> ExpectedInputLengths { get; }

    private static long[] NormalizeExpectedInputLengths(IReadOnlyList<long>? expectedInputLengths)
    {
        if (expectedInputLengths is null)
        {
            return [];
        }

        if (expectedInputLengths.Count is 0 or
            > InputLengthPolicyLimits.MaximumExpectedInputLengths)
        {
            throw new ArgumentException(
                $"Expected input lengths must contain between 1 and {InputLengthPolicyLimits.MaximumExpectedInputLengths} values.",
                nameof(expectedInputLengths));
        }

        long[] normalized = new long[expectedInputLengths.Count];
        long previous = 0;
        for (int index = 0; index < expectedInputLengths.Count; index++)
        {
            long value = expectedInputLengths[index];
            if (value <= 0 || (index > 0 && value <= previous))
            {
                throw new ArgumentException(
                    "Expected input lengths must be positive and strictly ascending.",
                    nameof(expectedInputLengths));
            }

            normalized[index] = value;
            previous = value;
        }

        return normalized;
    }
}

/// <summary>Rejects TP firmware larger than the fixed 256 KiB owner limit.</summary>
internal sealed record TpMaximum256KLengthRule()
    : CompositionProfileLengthRule(CompositionProfileLengthRuleKind.TpMaximum256K)
{
    internal const long MaximumBytes = 262144;
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
        CompositionProfileInputNormalization normalization)
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
    }

    internal string SlotId { get; }

    internal string Role { get; }

    internal CompositionProfileArtifactClass ArtifactClass { get; }

    internal bool Required { get; }

    internal CompositionProfileSlotCardinality Cardinality { get; }

    internal IReadOnlyList<string> AcceptedExtensions { get; }

    internal CompositionProfileLengthRule LengthRule { get; }

    internal CompositionProfileInputNormalization Normalization { get; }

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
                "TP firmware requires an unnormalized maximum-256-KiB or exact-within-256-KiB length rule.");
        }

        if (lengthRule.Kind == CompositionProfileLengthRuleKind.TpMaximum256K &&
            artifactClass != CompositionProfileArtifactClass.TpFirmware)
        {
            throw new ArgumentException("The fixed 256 KiB rule is restricted to TP firmware.");
        }

        if (artifactClass == CompositionProfileArtifactClass.DpFirmware &&
            lengthRule.Kind is not CompositionProfileLengthRuleKind.ExactResolvedMapCapacity and
                not CompositionProfileLengthRuleKind.NormalDpExtractWithWarning and
                not CompositionProfileLengthRuleKind.DeclaredPrefixWithWarning)
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

        if (lengthRule.Kind == CompositionProfileLengthRuleKind.NormalDpExtractWithWarning &&
            (artifactClass != CompositionProfileArtifactClass.DpFirmware ||
             normalization.Kind != CompositionProfileInputNormalizationKind.None))
        {
            throw new ArgumentException("Normal DP extraction warnings cannot normalize input bytes.");
        }

        if (lengthRule.Kind == CompositionProfileLengthRuleKind.DeclaredPrefixWithWarning &&
            (artifactClass is CompositionProfileArtifactClass.ReferenceImage or
                CompositionProfileArtifactClass.CtrlRamReplacement ||
             normalization.Kind != CompositionProfileInputNormalizationKind.None))
        {
            throw new ArgumentException(
                "Declared-prefix input authority is restricted to unnormalized immutable Merge sources.");
        }
    }

    private static bool IsApprovedTpLengthRule(CompositionProfileLengthRule lengthRule)
    {
        return lengthRule is TpMaximum256KLengthRule or
            DeclaredPrefixWithWarningLengthRule
        {
            RequiredEndExclusive: <= TpMaximum256KLengthRule.MaximumBytes,
        } or
            ExactBytesLengthRule { Bytes: <= TpMaximum256KLengthRule.MaximumBytes };
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
