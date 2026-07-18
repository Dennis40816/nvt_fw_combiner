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

/// <summary>Closed input length-rule kind retained by a compiled artifact.</summary>
public enum CompiledInputLengthRequirementKind
{
    /// <inheritdoc/>
    ExactBytes,
    /// <inheritdoc/>
    ExactResolvedMapCapacity,
    /// <inheritdoc/>
    Bounded,
    /// <inheritdoc/>
    NormalDpExtractWithWarning,
    /// <inheritdoc/>
    TpMaximum256K,
}

/// <summary>Base value for one immutable compiled input length requirement.</summary>
public abstract record CompiledInputLengthRequirement
{
    /// <summary>Creates a checked closed length requirement kind.</summary>
    protected CompiledInputLengthRequirement(CompiledInputLengthRequirementKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown compiled input length requirement kind.");
        }

        Kind = kind;
    }

    /// <summary>Closed requirement kind.</summary>
    public CompiledInputLengthRequirementKind Kind { get; }
}

/// <summary>Requires one exact positive source length.</summary>
public sealed record CompiledExactBytesInputLengthRequirement : CompiledInputLengthRequirement
{
    /// <summary>Creates an exact-length requirement.</summary>
    public CompiledExactBytesInputLengthRequirement(long bytes)
        : base(CompiledInputLengthRequirementKind.ExactBytes)
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
        : base(CompiledInputLengthRequirementKind.ExactResolvedMapCapacity)
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
        : base(CompiledInputLengthRequirementKind.Bounded)
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

/// <summary>Extracts declared Normal DP content and records a warning for a nonmatching container length.</summary>
public sealed record CompiledNormalDpExtractWithWarningInputLengthRequirement : CompiledInputLengthRequirement
{
    private readonly long[] _expectedInputLengths;

    /// <summary>Creates one fixed Normal-DP extraction requirement.</summary>
    public CompiledNormalDpExtractWithWarningInputLengthRequirement(
        string issueCode,
        IReadOnlyList<long> expectedInputLengths)
        : base(CompiledInputLengthRequirementKind.NormalDpExtractWithWarning)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCode);
        ArgumentNullException.ThrowIfNull(expectedInputLengths);
        IssueCode = issueCode;
        _expectedInputLengths = SnapshotExpectedInputLengths(expectedInputLengths);
        ExpectedInputLengths = Array.AsReadOnly(_expectedInputLengths);
    }

    /// <summary>Stable warning issue code emitted for an outer-container size mismatch.</summary>
    public string IssueCode { get; }

    /// <summary>Expected outer-container lengths that avoid the warning while preserving declared-range extraction.</summary>
    public IReadOnlyList<long> ExpectedInputLengths { get; }

    private static long[] SnapshotExpectedInputLengths(IReadOnlyList<long> expectedInputLengths)
    {
        if (expectedInputLengths.Count is 0 or
            > InputLengthPolicyLimits.MaximumExpectedInputLengths)
        {
            throw new ArgumentException(
                $"Expected input lengths must contain between 1 and {InputLengthPolicyLimits.MaximumExpectedInputLengths} values.",
                nameof(expectedInputLengths));
        }

        long[] snapshot = new long[expectedInputLengths.Count];
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

            snapshot[index] = value;
            previous = value;
        }

        return snapshot;
    }
}

/// <summary>Rejects TP firmware larger than the owner-approved 256 KiB limit.</summary>
public sealed record CompiledTpMaximum256KInputLengthRequirement()
    : CompiledInputLengthRequirement(CompiledInputLengthRequirementKind.TpMaximum256K)
{
    /// <summary>Owner-approved maximum TP input length.</summary>
    public const long MaximumBytes = 262144;
}

/// <summary>Closed transient input-normalization kind retained by a compiled artifact.</summary>
public enum CompiledInputNormalizationKind
{
    /// <inheritdoc/>
    None,
    /// <inheritdoc/>
    PadShorter,
    /// <inheritdoc/>
    TruncateCtrlRam,
}

/// <summary>Base value for one immutable compiled input-normalization policy.</summary>
public abstract record CompiledInputNormalization
{
    /// <summary>Creates a checked closed normalization kind.</summary>
    protected CompiledInputNormalization(CompiledInputNormalizationKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown compiled input normalization kind.");
        }

        Kind = kind;
    }

    /// <summary>Closed normalization kind.</summary>
    public CompiledInputNormalizationKind Kind { get; }
}

/// <summary>Preserves immutable source bytes without normalization.</summary>
public sealed record CompiledNoInputNormalization()
    : CompiledInputNormalization(CompiledInputNormalizationKind.None);

/// <summary>Pads a shorter DP source with one evidenced byte.</summary>
public sealed record CompiledPadShorterInputNormalization : CompiledInputNormalization
{
    /// <summary>Creates a checked short-input padding policy.</summary>
    public CompiledPadShorterInputNormalization(byte fillByte, string evidenceRef)
        : base(CompiledInputNormalizationKind.PadShorter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceRef);
        FillByte = fillByte;
        EvidenceRef = evidenceRef;
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
        : base(CompiledInputNormalizationKind.TruncateCtrlRam)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(warningIssueCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(evidenceRef);
        WarningIssueCode = warningIssueCode;
        EvidenceRef = evidenceRef;
    }

    /// <summary>Stable warning issue code emitted when truncation occurs.</summary>
    public string WarningIssueCode { get; }

    /// <summary>Evidence binding authorizing the truncation behavior.</summary>
    public string EvidenceRef { get; }
}

/// <summary>One immutable input-slot acceptance, normalization, and address-space binding requirement.</summary>
public sealed class CompiledInputSlotRequirement
{
    private readonly string[] _acceptedExtensions;

    internal CompiledInputSlotRequirement(
        string slotId,
        string role,
        CompiledInputArtifactClass artifactClass,
        bool required,
        CompiledInputSlotCardinality cardinality,
        IEnumerable<string> acceptedExtensions,
        CompiledInputLengthRequirement lengthRequirement,
        CompiledInputNormalization normalization)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);
        if (!Enum.IsDefined(artifactClass))
        {
            throw new ArgumentOutOfRangeException(nameof(artifactClass), artifactClass, "Unknown compiled input artifact class.");
        }

        if (!Enum.IsDefined(cardinality))
        {
            throw new ArgumentOutOfRangeException(nameof(cardinality), cardinality, "Unknown compiled input slot cardinality.");
        }

        ArgumentNullException.ThrowIfNull(lengthRequirement);
        ArgumentNullException.ThrowIfNull(normalization);
        ValidateArtifactPolicy(artifactClass, lengthRequirement, normalization);
        _acceptedExtensions = SnapshotExtensions(acceptedExtensions);

        SlotId = slotId;
        Role = role;
        ArtifactClass = artifactClass;
        Required = required;
        Cardinality = cardinality;
        AcceptedExtensions = Array.AsReadOnly(_acceptedExtensions);
        LengthRequirement = lengthRequirement;
        Normalization = normalization;
    }

    /// <summary>Profile-owned input slot identifier.</summary>
    public string SlotId { get; }

    /// <summary>Stable role identifier used for presentation and reports.</summary>
    public string Role { get; }

    /// <summary>Closed source-artifact class.</summary>
    public CompiledInputArtifactClass ArtifactClass { get; }

    /// <summary>Whether the profile requires this slot.</summary>
    public bool Required { get; }

    /// <summary>Closed source-binding cardinality.</summary>
    public CompiledInputSlotCardinality Cardinality { get; }

    /// <summary>Canonical accepted filename extensions.</summary>
    public IReadOnlyList<string> AcceptedExtensions { get; }

    /// <summary>Typed source length acceptance policy.</summary>
    public CompiledInputLengthRequirement LengthRequirement { get; }

    /// <summary>Typed transient source normalization policy.</summary>
    public CompiledInputNormalization Normalization { get; }

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

    private static void ValidateArtifactPolicy(
        CompiledInputArtifactClass artifactClass,
        CompiledInputLengthRequirement lengthRequirement,
        CompiledInputNormalization normalization)
    {
        if (artifactClass == CompiledInputArtifactClass.TpFirmware &&
            (!IsApprovedTpLengthRequirement(lengthRequirement) ||
             normalization.Kind != CompiledInputNormalizationKind.None))
        {
            throw new ArgumentException(
                "TP firmware requires an unnormalized maximum-256-KiB or exact-within-256-KiB length rule.");
        }

        if (lengthRequirement.Kind == CompiledInputLengthRequirementKind.TpMaximum256K &&
            artifactClass != CompiledInputArtifactClass.TpFirmware)
        {
            throw new ArgumentException("The fixed 256 KiB rule is restricted to TP firmware.");
        }

        if (artifactClass == CompiledInputArtifactClass.DpFirmware &&
            lengthRequirement.Kind is not CompiledInputLengthRequirementKind.ExactResolvedMapCapacity and
                not CompiledInputLengthRequirementKind.NormalDpExtractWithWarning)
        {
            throw new ArgumentException("DP firmware requires an approved DP length rule.");
        }

        if (artifactClass == CompiledInputArtifactClass.ReferenceImage &&
            (lengthRequirement.Kind != CompiledInputLengthRequirementKind.ExactResolvedMapCapacity ||
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
            lengthRequirement.Kind != CompiledInputLengthRequirementKind.ExactResolvedMapCapacity)
        {
            throw new ArgumentException("Short-input padding requires exact resolved-map capacity.");
        }

        if (normalization.Kind == CompiledInputNormalizationKind.TruncateCtrlRam &&
            artifactClass != CompiledInputArtifactClass.CtrlRamReplacement)
        {
            throw new ArgumentException("CtrlRAM truncation requires a CtrlRAM replacement artifact.");
        }

        if (lengthRequirement.Kind == CompiledInputLengthRequirementKind.NormalDpExtractWithWarning &&
            (artifactClass != CompiledInputArtifactClass.DpFirmware ||
             normalization.Kind != CompiledInputNormalizationKind.None))
        {
            throw new ArgumentException("Normal DP extraction warnings cannot normalize input bytes.");
        }
    }

    private static bool IsApprovedTpLengthRequirement(CompiledInputLengthRequirement lengthRequirement)
    {
        return lengthRequirement is CompiledTpMaximum256KInputLengthRequirement or
            CompiledExactBytesInputLengthRequirement
        {
            Bytes: <= CompiledTpMaximum256KInputLengthRequirement.MaximumBytes,
        };
    }

}

/// <summary>One immutable plan address-space binding supplied for one compiled input slot.</summary>
public sealed class CompiledInputSpaceBinding
{
    internal CompiledInputSpaceBinding(
        string addressSpaceId,
        string slotId,
        CompiledInputInstancePolicy instancePolicy)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        if (!Enum.IsDefined(instancePolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(instancePolicy),
                instancePolicy,
                "Unknown compiled input instance policy.");
        }

        AddressSpaceId = addressSpaceId;
        SlotId = slotId;
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

    internal CompiledInputContract(
        IEnumerable<CompiledInputSlotRequirement> slots,
        IEnumerable<CompiledInputSpaceBinding> spaceBindings)
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

        var slotIds = _slots.Select(static slot => slot.SlotId).ToHashSet(StringComparer.Ordinal);
        if (_spaceBindings.Any(binding => !slotIds.Contains(binding.SlotId)) ||
            _slots.Any(slot => !_spaceBindings.Any(binding =>
                StringComparer.Ordinal.Equals(binding.SlotId, slot.SlotId))))
        {
            throw new ArgumentException(
                "Every input space binding must name one slot and every slot must bind one or more spaces.",
                nameof(spaceBindings));
        }

        Array.Sort(_slots, static (left, right) => StringComparer.Ordinal.Compare(left.SlotId, right.SlotId));
        Array.Sort(_spaceBindings, static (left, right) =>
        {
            int space = StringComparer.Ordinal.Compare(left.AddressSpaceId, right.AddressSpaceId);
            return space != 0 ? space : StringComparer.Ordinal.Compare(left.SlotId, right.SlotId);
        });
        Slots = Array.AsReadOnly(_slots);
        SpaceBindings = Array.AsReadOnly(_spaceBindings);
    }

    /// <summary>Canonical slot declarations by ordinal slot id.</summary>
    public IReadOnlyList<CompiledInputSlotRequirement> Slots { get; }

    /// <summary>Canonical immutable plan-space bindings by address space then slot id.</summary>
    public IReadOnlyList<CompiledInputSpaceBinding> SpaceBindings { get; }
}
