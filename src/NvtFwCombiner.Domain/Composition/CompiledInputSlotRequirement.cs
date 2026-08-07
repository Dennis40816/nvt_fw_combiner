namespace NvtFwCombiner.Domain.Composition;

/// <summary>Closed source-artifact class accepted by one compiled input requirement.</summary>
public enum CompiledInputArtifactClass
{
    /// <inheritdoc/>
    TpFirmware,
    /// <inheritdoc/>
    DpFirmware,
    /// <inheritdoc/>
    ReferenceImage,
    /// <inheritdoc/>
    CtrlRamReplacement,
    /// <inheritdoc/>
    Auxiliary,
}

/// <summary>Closed cardinality accepted by one compiled input slot.</summary>
public enum CompiledInputSlotCardinality
{
    /// <inheritdoc/>
    ExactlyOne,
    /// <inheritdoc/>
    ZeroOrOne,
    /// <inheritdoc/>
    OneOrMore,
}

/// <summary>Closed immutable address-space instance policy for one compiled input-slot binding.</summary>
public enum CompiledInputInstancePolicy
{
    /// <inheritdoc/>
    Singleton,
    /// <inheritdoc/>
    PerBinding,
}

/// <summary>Base value for one immutable compiled input length requirement.</summary>
public abstract record CompiledInputLengthRequirement : InputLengthRequirementDefinition;

/// <summary>Accepts one immutable execution prefix while retaining full-source diagnostic authority.</summary>
public sealed record CompiledDeclaredPrefixWithWarningInputLengthRequirement : CompiledInputLengthRequirement
{
    private readonly long[] _expectedOuterLengths;

    /// <summary>Creates one checked declared-prefix requirement.</summary>
    public CompiledDeclaredPrefixWithWarningInputLengthRequirement(
        long requiredEndExclusive,
        IReadOnlyList<long> expectedOuterLengths,
        string shortInputIssueCode,
        string unexpectedOuterLengthIssueCode)
    {
        if (requiredEndExclusive is <= 0 or > int.MaxValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(requiredEndExclusive),
                requiredEndExclusive,
                "Required end must fit the in-memory execution snapshot limit.");
        }

        ArgumentNullException.ThrowIfNull(expectedOuterLengths);
        DomainInvariant.Reject(
            expectedOuterLengths.Count is 0 or > InputLengthPolicyLimits.MaximumExpectedInputLengths,
            $"Expected outer lengths must contain between 1 and {InputLengthPolicyLimits.MaximumExpectedInputLengths} values.",
            nameof(expectedOuterLengths));

        _expectedOuterLengths = new long[expectedOuterLengths.Count];
        long previous = 0;
        for (int index = 0; index < expectedOuterLengths.Count; index++)
        {
            long value = expectedOuterLengths[index];
            DomainInvariant.Reject(
                value < requiredEndExclusive || value > int.MaxValue || (index > 0 && value <= previous),
                "Expected outer lengths must fit the in-memory limit, cover the required end, and be strictly ascending.",
                nameof(expectedOuterLengths));

            _expectedOuterLengths[index] = value;
            previous = value;
        }

        ShortInputIssueCode = RequiredValue.NotBlank(shortInputIssueCode);
        UnexpectedOuterLengthIssueCode = RequiredValue.NotBlank(unexpectedOuterLengthIssueCode);
        RequiredEndExclusive = requiredEndExclusive;
        ExpectedOuterLengths = Array.AsReadOnly(_expectedOuterLengths);
    }

    /// <summary>First unavailable byte that makes a shorter source blocking.</summary>
    public long RequiredEndExclusive { get; }

    /// <summary>Complete source lengths that do not emit an outer-length warning.</summary>
    public IReadOnlyList<long> ExpectedOuterLengths { get; }

    /// <summary>Stable blocking issue code for a source shorter than the required end.</summary>
    public string ShortInputIssueCode { get; }

    /// <summary>Stable warning issue code for an accepted unexpected outer length.</summary>
    public string UnexpectedOuterLengthIssueCode { get; }
}

/// <summary>Requires one exact positive source length.</summary>
public sealed record CompiledExactBytesInputLengthRequirement : CompiledInputLengthRequirement
{
    /// <summary>Creates an exact-length requirement.</summary>
    public CompiledExactBytesInputLengthRequirement(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        Bytes = bytes;
    }

    /// <summary>Required source length in bytes.</summary>
    public long Bytes { get; }
}

/// <summary>Requires the resolved physical map capacity retained at compilation time.</summary>
public sealed record CompiledExactResolvedMapCapacityInputLengthRequirement : CompiledInputLengthRequirement
{
    /// <summary>Creates an exact resolved-map-capacity requirement.</summary>
    public CompiledExactResolvedMapCapacityInputLengthRequirement(long bytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bytes);
        Bytes = bytes;
    }

    /// <summary>Resolved physical map capacity in bytes.</summary>
    public long Bytes { get; }
}

/// <summary>Accepts a closed positive source-length interval.</summary>
public sealed record CompiledBoundedInputLengthRequirement : CompiledInputLengthRequirement
{
    /// <summary>Creates a bounded-length requirement.</summary>
    public CompiledBoundedInputLengthRequirement(long minimumBytes, long maximumBytes)
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

    /// <summary>Inclusive minimum accepted length.</summary>
    public long MinimumBytes { get; }

    /// <summary>Inclusive maximum accepted length.</summary>
    public long MaximumBytes { get; }
}

/// <summary>Rejects TP firmware larger than the owner-approved 256 KiB limit.</summary>
public sealed record CompiledTpMaximum256KInputLengthRequirement()
    : CompiledInputLengthRequirement
{
    /// <summary>Owner-approved maximum TP input length.</summary>
    public const long MaximumBytes = 262144;
}

/// <summary>
/// Accepts an immutable source that covers the compiled address-space reads,
/// with optional complete-container diagnostics.
/// </summary>
public sealed record CompiledSourceViewCoverageInputLengthRequirement :
    CompiledInputLengthRequirement
{
    private readonly long[] _expectedOuterLengths;

    /// <summary>Creates one checked source-view coverage requirement.</summary>
    public CompiledSourceViewCoverageInputLengthRequirement(
        IReadOnlyList<long>? expectedOuterLengths = null,
        string? unexpectedOuterLengthIssueCode = null)
    {
        DomainInvariant.Reject(
            (expectedOuterLengths is null) != (unexpectedOuterLengthIssueCode is null),
            "Expected outer lengths and their warning issue code must be declared together.");

        _expectedOuterLengths = expectedOuterLengths is null
            ? []
            : InputLengthPolicyLimits.SnapshotExpectedOuterLengths(
                expectedOuterLengths,
                nameof(expectedOuterLengths));
        if (unexpectedOuterLengthIssueCode is not null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(unexpectedOuterLengthIssueCode);
        }

        ExpectedOuterLengths = Array.AsReadOnly(_expectedOuterLengths);
        UnexpectedOuterLengthIssueCode = unexpectedOuterLengthIssueCode;
    }

    /// <summary>Known complete-container lengths that suppress an optional warning.</summary>
    public IReadOnlyList<long> ExpectedOuterLengths { get; }

    /// <summary>Optional stable warning code for an unexpected complete-container length.</summary>
    public string? UnexpectedOuterLengthIssueCode { get; }

}

/// <summary>Base value for one immutable compiled input-normalization policy.</summary>
public abstract record CompiledInputNormalization;

/// <summary>Preserves immutable source bytes without normalization.</summary>
public sealed record CompiledNoInputNormalization : CompiledInputNormalization;

/// <summary>Pads a shorter DP source with one evidenced byte.</summary>
public sealed record CompiledPadShorterInputNormalization : CompiledInputNormalization
{
    /// <summary>Creates a checked short-input padding policy.</summary>
    public CompiledPadShorterInputNormalization(byte fillByte, string evidenceRef)
    {
        EvidenceRef = RequiredValue.NotBlank(evidenceRef);
        FillByte = fillByte;
    }

    /// <summary>Byte appended to a shorter transient input.</summary>
    public byte FillByte { get; }

    /// <summary>Evidence binding authorizing the padding behavior.</summary>
    public string EvidenceRef { get; }
}

/// <summary>Truncates only an evidenced CtrlRAM replacement source and records a warning.</summary>
public sealed record CompiledTruncateCtrlRamInputNormalization : CompiledInputNormalization
{
    /// <summary>Creates a checked CtrlRAM truncation policy.</summary>
    public CompiledTruncateCtrlRamInputNormalization(string warningIssueCode, string evidenceRef)
    {
        WarningIssueCode = RequiredValue.NotBlank(warningIssueCode);
        EvidenceRef = RequiredValue.NotBlank(evidenceRef);
    }

    /// <summary>Stable warning issue code emitted when truncation occurs.</summary>
    public string WarningIssueCode { get; }

    /// <summary>Evidence binding authorizing the truncation behavior.</summary>
    public string EvidenceRef { get; }
}

/// <summary>One immutable compiled input-slot requirement referencing its canonical definition.</summary>
public sealed class CompiledInputSlotRequirement
{
    private readonly CompositionInputSlotDefinition _definition;

    internal CompiledInputSlotRequirement(
        CompositionInputSlotDefinition definition,
        CompiledInputLengthRequirement lengthRequirement,
        bool forceRequired = false)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(lengthRequirement);
        bool matchesDefinition = definition.LengthRequirement switch
        {
            ResolvedMapCapacityInputLengthDefinition =>
                lengthRequirement is CompiledExactResolvedMapCapacityInputLengthRequirement,
            SourceViewCoverageInputLengthDefinition =>
                lengthRequirement is CompiledSourceViewCoverageInputLengthRequirement,
            CompiledInputLengthRequirement fixedRequirement =>
                ReferenceEquals(fixedRequirement, lengthRequirement),
            _ => false,
        };
        DomainInvariant.Reject(
            !matchesDefinition,
            "Compiled input length must resolve the canonical slot definition.",
            nameof(lengthRequirement));

        _definition = definition;
        Required = definition.Required || forceRequired;
        Cardinality = forceRequired ? CompiledInputSlotCardinality.ExactlyOne : definition.Cardinality;
        LengthRequirement = lengthRequirement;
    }

    /// <summary>Profile-owned input slot identifier.</summary>
    public string SlotId => _definition.SlotId;

    /// <summary>Stable role identifier used for presentation and reports.</summary>
    public string Role => _definition.Role;

    /// <summary>Closed source-artifact class.</summary>
    public CompiledInputArtifactClass ArtifactClass => _definition.ArtifactClass;

    /// <summary>Whether the compiled plan requires this slot.</summary>
    public bool Required { get; }

    /// <summary>Closed source-binding cardinality.</summary>
    public CompiledInputSlotCardinality Cardinality { get; }

    /// <summary>Canonical accepted filename extensions.</summary>
    public IReadOnlyList<string> AcceptedExtensions => _definition.AcceptedExtensions;

    /// <summary>Typed resolved source length acceptance policy.</summary>
    public CompiledInputLengthRequirement LengthRequirement { get; }

    /// <summary>Typed transient source normalization policy.</summary>
    public CompiledInputNormalization Normalization => _definition.Normalization;
}

/// <summary>One immutable plan address-space binding supplied for one compiled input slot.</summary>
public sealed class CompiledInputSpaceBinding
{
    internal CompiledInputSpaceBinding(
        string addressSpaceId,
        string slotId,
        CompiledInputInstancePolicy instancePolicy)
    {
        AddressSpaceId = RequiredValue.NotBlank(addressSpaceId);
        SlotId = RequiredValue.NotBlank(slotId);
        ClosedEnum.ThrowIfUndefined(instancePolicy, "Unknown compiled input instance policy.");

        InstancePolicy = instancePolicy;
    }

    /// <summary>Immutable plan address space bound to the slot.</summary>
    public string AddressSpaceId { get; }

    /// <summary>Compiled input slot identifier.</summary>
    public string SlotId { get; }

    /// <summary>Closed source instance policy.</summary>
    public CompiledInputInstancePolicy InstancePolicy { get; }
}

/// <summary>Complete immutable compiled input slot policy and its plan-space bindings.</summary>
public sealed class CompiledInputContract
{
    private readonly CompiledInputSlotRequirement[] _slots;
    private readonly CompiledInputSpaceBinding[] _spaceBindings;
    private readonly CompiledInputSelectionGroup[] _selectionGroups;

    internal CompiledInputContract(
        IEnumerable<CompiledInputSlotRequirement> slots,
        IEnumerable<CompiledInputSpaceBinding> spaceBindings,
        IEnumerable<CompiledInputSelectionGroup>? selectionGroups = null)
    {
        _slots = ImmutableReferenceSnapshot.CreateUnique(
            slots,
            static slot => slot.SlotId,
            "Input slots must be non-null, non-empty, and ordinally unique.",
            "Input slots must be non-null, non-empty, and ordinally unique.",
            StringComparer.Ordinal,
            requireValue: true);
        _spaceBindings = ImmutableReferenceSnapshot.CreateUnique(
            spaceBindings,
            static binding => binding.AddressSpaceId,
            "Input space bindings must be non-null, non-empty, and ordinally unique by space.",
            "Input space bindings must be non-null, non-empty, and unique by address space.",
            StringComparer.Ordinal,
            requireValue: true);
        _selectionGroups = ImmutableReferenceSnapshot.CreateUnique(
            selectionGroups ?? [],
            static group => group.GroupId,
            "Input selection groups must be non-null and ordinally unique.",
            "Input selection groups must be non-null and ordinally unique.",
            StringComparer.Ordinal,
            requireValue: false);

        var slotIds = _slots.Select(static slot => slot.SlotId).ToHashSet(StringComparer.Ordinal);
        DomainInvariant.Reject(
            _spaceBindings.Any(binding => !slotIds.Contains(binding.SlotId)) ||
            _slots.Any(slot => !_spaceBindings.Any(binding =>
                StringComparer.Ordinal.Equals(binding.SlotId, slot.SlotId))),
            "Every input space binding must name one slot and every slot must bind one or more spaces.",
            nameof(spaceBindings));

        Array.Sort(_slots, static (left, right) => StringComparer.Ordinal.Compare(left.SlotId, right.SlotId));
        Array.Sort(_spaceBindings, static (left, right) =>
        {
            int space = StringComparer.Ordinal.Compare(left.AddressSpaceId, right.AddressSpaceId);
            return space != 0 ? space : StringComparer.Ordinal.Compare(left.SlotId, right.SlotId);
        });
        Array.Sort(_selectionGroups, static (left, right) =>
            StringComparer.Ordinal.Compare(left.GroupId, right.GroupId));
        Slots = Array.AsReadOnly(_slots);
        SpaceBindings = Array.AsReadOnly(_spaceBindings);
        SelectionGroups = Array.AsReadOnly(_selectionGroups);
    }

    /// <summary>Canonical slot declarations by ordinal slot id.</summary>
    public IReadOnlyList<CompiledInputSlotRequirement> Slots { get; }

    /// <summary>Canonical immutable plan-space bindings by address space then slot id.</summary>
    public IReadOnlyList<CompiledInputSpaceBinding> SpaceBindings { get; }

    /// <summary>Resolved selection-group definitions and selected state.</summary>
    public IReadOnlyList<CompiledInputSelectionGroup> SelectionGroups { get; }
}
