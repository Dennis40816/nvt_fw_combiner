namespace NvtFwCombiner.Domain.Composition;

internal static class InputPolicyValueRules
{
    internal static string RequireCanonicalId(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value[0] is < 'a' or > 'z' ||
            value[^1] == '-' ||
            value.Where(static character => character != '-')
                .Any(static character => character is not (>= 'a' and <= 'z') and not (>= '0' and <= '9')) ||
            value.Contains("--", StringComparison.Ordinal)
            ? throw new ArgumentException("Identifier is not in canonical lowercase form.", parameterName)
            : value;
    }

    internal static string RequireIssueCode(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        return value.Length < 2 || value[0] is < 'A' or > 'Z' ||
            value.Skip(1).Any(static character =>
                character is not (>= 'A' and <= 'Z') and not (>= '0' and <= '9') and not '_')
            ? throw new ArgumentException("Value is not a canonical issue code.", parameterName)
            : value;
    }
}

/// <summary>Base value for one immutable canonical input length definition.</summary>
public abstract record InputLengthRequirementDefinition
{
    private protected InputLengthRequirementDefinition()
    {
    }
}

/// <summary>Requires the physical capacity selected during map resolution.</summary>
internal sealed record ResolvedMapCapacityInputLengthDefinition()
    : InputLengthRequirementDefinition;

/// <summary>Derives the immutable source snapshot from all compiled reads.</summary>
internal sealed record SourceViewCoverageInputLengthDefinition : InputLengthRequirementDefinition
{
    private readonly long[] _expectedOuterLengths;

    internal SourceViewCoverageInputLengthDefinition(
        IReadOnlyList<long>? expectedOuterLengths = null,
        string? unexpectedOuterLengthIssueCode = null)
    {
        if (expectedOuterLengths is not null && unexpectedOuterLengthIssueCode is null)
        {
            throw new ArgumentException(
                "Expected outer lengths and their warning issue code must be declared together.");
        }

        _expectedOuterLengths = expectedOuterLengths is null
            ? []
            : SnapshotExpectedOuterLengths(expectedOuterLengths);
        if (unexpectedOuterLengthIssueCode is not null)
        {
            _ = InputPolicyValueRules.RequireIssueCode(
                unexpectedOuterLengthIssueCode,
                nameof(unexpectedOuterLengthIssueCode));
        }

        ExpectedOuterLengths = Array.AsReadOnly(_expectedOuterLengths);
        UnexpectedOuterLengthIssueCode = unexpectedOuterLengthIssueCode;
    }

    internal IReadOnlyList<long> ExpectedOuterLengths { get; }

    internal string? UnexpectedOuterLengthIssueCode { get; }

    private static long[] SnapshotExpectedOuterLengths(IReadOnlyList<long> expectedOuterLengths)
    {
        if (expectedOuterLengths.Count is 0 or > InputLengthPolicyLimits.MaximumExpectedInputLengths)
        {
            throw new ArgumentException(
                $"Expected outer lengths must contain between 1 and {InputLengthPolicyLimits.MaximumExpectedInputLengths} values.",
                nameof(expectedOuterLengths));
        }

        long[] snapshot = new long[expectedOuterLengths.Count];
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

            snapshot[index] = value;
            previous = value;
        }

        return snapshot;
    }
}

/// <summary>Domain-owned canonical input-slot definition before map-dependent lowering.</summary>
internal sealed class CompositionInputSlotDefinition
{
    private readonly string[] _acceptedExtensions;

    internal CompositionInputSlotDefinition(
        string slotId,
        string role,
        CompiledInputArtifactClass artifactClass,
        bool required,
        CompiledInputSlotCardinality cardinality,
        IEnumerable<string> acceptedExtensions,
        InputLengthRequirementDefinition lengthRequirement,
        CompiledInputNormalization normalization,
        string? notApplicableReason = null,
        bool validateCanonicalPolicy = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        if (!Enum.IsDefined(artifactClass))
        {
            throw new ArgumentOutOfRangeException(nameof(artifactClass), artifactClass, "Unknown input artifact class.");
        }

        if (!Enum.IsDefined(cardinality))
        {
            throw new ArgumentOutOfRangeException(nameof(cardinality), cardinality, "Unknown input slot cardinality.");
        }

        ArgumentNullException.ThrowIfNull(lengthRequirement);
        ArgumentNullException.ThrowIfNull(normalization);
        ValidateArtifactPolicy(artifactClass, lengthRequirement, normalization);
        if (validateCanonicalPolicy)
        {
            ValidateCanonicalPolicy(lengthRequirement, normalization);
        }

        _acceptedExtensions = SnapshotExtensions(acceptedExtensions);
        if (notApplicableReason is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(notApplicableReason);
        }

        SlotId = slotId;
        Role = role;
        ArtifactClass = artifactClass;
        Required = required;
        Cardinality = cardinality;
        AcceptedExtensions = Array.AsReadOnly(_acceptedExtensions);
        LengthRequirement = lengthRequirement;
        Normalization = normalization;
        NotApplicableReason = notApplicableReason;
    }

    internal string SlotId { get; }

    internal string Role { get; }

    internal CompiledInputArtifactClass ArtifactClass { get; }

    internal bool Required { get; }

    internal CompiledInputSlotCardinality Cardinality { get; }

    internal IReadOnlyList<string> AcceptedExtensions { get; }

    internal InputLengthRequirementDefinition LengthRequirement { get; }

    internal CompiledInputNormalization Normalization { get; }

    internal string? NotApplicableReason { get; }

    private static string[] SnapshotExtensions(IEnumerable<string> acceptedExtensions)
    {
        ArgumentNullException.ThrowIfNull(acceptedExtensions);
        string[] snapshot = [.. acceptedExtensions];
        if (snapshot.Length == 0 || snapshot.Any(static extension =>
                extension.Length < 2 || extension[0] != '.' ||
                extension.Skip(1).Any(static character => !char.IsAsciiLetterOrDigit(character))))
        {
            throw new ArgumentException(
                "Accepted extensions must use canonical dot-prefixed alphanumeric form.",
                nameof(acceptedExtensions));
        }

        if (snapshot.Distinct(StringComparer.Ordinal).Count() != snapshot.Length)
        {
            throw new ArgumentException("Accepted extensions must be ordinally unique.", nameof(acceptedExtensions));
        }

        Array.Sort(snapshot, StringComparer.Ordinal);
        return snapshot;
    }

    private static void ValidateCanonicalPolicy(
        InputLengthRequirementDefinition lengthRequirement,
        CompiledInputNormalization normalization)
    {
        switch (lengthRequirement)
        {
            case CompiledDeclaredPrefixWithWarningInputLengthRequirement declaredPrefix:
                _ = InputPolicyValueRules.RequireIssueCode(
                    declaredPrefix.ShortInputIssueCode,
                    nameof(declaredPrefix.ShortInputIssueCode));
                _ = InputPolicyValueRules.RequireIssueCode(
                    declaredPrefix.UnexpectedOuterLengthIssueCode,
                    nameof(declaredPrefix.UnexpectedOuterLengthIssueCode));
                break;
            case CompiledSourceViewCoverageInputLengthRequirement sourceView
                when sourceView.UnexpectedOuterLengthIssueCode is { } issueCode:
                _ = InputPolicyValueRules.RequireIssueCode(issueCode, nameof(issueCode));
                break;
            default:
                break;
        }

        switch (normalization)
        {
            case CompiledPadShorterInputNormalization padded:
                _ = InputPolicyValueRules.RequireCanonicalId(padded.EvidenceRef, nameof(padded.EvidenceRef));
                break;
            case CompiledTruncateCtrlRamInputNormalization truncated:
                _ = InputPolicyValueRules.RequireIssueCode(
                    truncated.WarningIssueCode,
                    nameof(truncated.WarningIssueCode));
                _ = InputPolicyValueRules.RequireCanonicalId(
                    truncated.EvidenceRef,
                    nameof(truncated.EvidenceRef));
                break;
            default:
                break;
        }
    }

    private static void ValidateArtifactPolicy(
        CompiledInputArtifactClass artifactClass,
        InputLengthRequirementDefinition lengthRequirement,
        CompiledInputNormalization normalization)
    {
        if (artifactClass == CompiledInputArtifactClass.TpFirmware &&
            (!IsApprovedTpLengthRequirement(lengthRequirement) ||
             normalization.Kind != CompiledInputNormalizationKind.None))
        {
            throw new ArgumentException(
                "TP firmware requires one approved unnormalized section or exact length rule.");
        }

        if (lengthRequirement is CompiledTpMaximum256KInputLengthRequirement &&
            artifactClass != CompiledInputArtifactClass.TpFirmware)
        {
            throw new ArgumentException("The fixed 256 KiB rule is restricted to TP firmware.");
        }

        if (artifactClass == CompiledInputArtifactClass.DpFirmware &&
            lengthRequirement is not (ResolvedMapCapacityInputLengthDefinition or
                CompiledExactResolvedMapCapacityInputLengthRequirement or
                CompiledDeclaredPrefixWithWarningInputLengthRequirement or
                SourceViewCoverageInputLengthDefinition or
                CompiledSourceViewCoverageInputLengthRequirement))
        {
            throw new ArgumentException("DP firmware requires an approved DP length rule.");
        }

        if (artifactClass == CompiledInputArtifactClass.ReferenceImage &&
            (lengthRequirement is not (ResolvedMapCapacityInputLengthDefinition or
                 CompiledExactResolvedMapCapacityInputLengthRequirement) ||
             normalization.Kind != CompiledInputNormalizationKind.None))
        {
            throw new ArgumentException("Reference images require exact map capacity without normalization.");
        }

        if (normalization.Kind == CompiledInputNormalizationKind.PadShorter &&
            artifactClass != CompiledInputArtifactClass.DpFirmware)
        {
            throw new ArgumentException("Short-input padding is restricted to DP firmware.");
        }

        if (normalization.Kind == CompiledInputNormalizationKind.PadShorter &&
            lengthRequirement is not (ResolvedMapCapacityInputLengthDefinition or
                CompiledExactResolvedMapCapacityInputLengthRequirement))
        {
            throw new ArgumentException("Short-input padding requires exact resolved-map capacity.");
        }

        if (normalization.Kind == CompiledInputNormalizationKind.TruncateCtrlRam &&
            artifactClass != CompiledInputArtifactClass.CtrlRamReplacement)
        {
            throw new ArgumentException("CtrlRAM truncation requires a CtrlRAM replacement artifact.");
        }

        if (lengthRequirement is CompiledDeclaredPrefixWithWarningInputLengthRequirement &&
            (artifactClass is CompiledInputArtifactClass.ReferenceImage or
                CompiledInputArtifactClass.CtrlRamReplacement ||
             normalization.Kind != CompiledInputNormalizationKind.None))
        {
            throw new ArgumentException(
                "Declared-prefix input authority is restricted to unnormalized immutable Merge sources.");
        }

        if (lengthRequirement is SourceViewCoverageInputLengthDefinition or
                CompiledSourceViewCoverageInputLengthRequirement &&
            (artifactClass is CompiledInputArtifactClass.ReferenceImage or
                CompiledInputArtifactClass.CtrlRamReplacement ||
             normalization.Kind != CompiledInputNormalizationKind.None))
        {
            throw new ArgumentException(
                "Source-view coverage is restricted to unnormalized immutable section sources.");
        }
    }

    private static bool IsApprovedTpLengthRequirement(InputLengthRequirementDefinition lengthRequirement)
    {
        return lengthRequirement is CompiledTpMaximum256KInputLengthRequirement or
            SourceViewCoverageInputLengthDefinition or
            CompiledSourceViewCoverageInputLengthRequirement or
            CompiledDeclaredPrefixWithWarningInputLengthRequirement
        {
            RequiredEndExclusive: <= CompiledTpMaximum256KInputLengthRequirement.MaximumBytes,
        } or
            CompiledExactBytesInputLengthRequirement
        {
            Bytes: <= CompiledTpMaximum256KInputLengthRequirement.MaximumBytes,
        };
    }
}
