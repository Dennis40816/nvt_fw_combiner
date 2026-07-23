using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.InputInspection;

/// <summary>Stable health priority for one input inspected against a compiled contract.</summary>
public enum CompiledInputArtifactInspectionSeverity
{
    /// <summary>The selected source satisfies the compiled input policy.</summary>
    Valid,

    /// <summary>The source is accepted, but the diagnostic must remain visible.</summary>
    Warning,

    /// <summary>The source cannot be used by Build.</summary>
    Blocking,
}

/// <summary>Typed corrective action for a compiled input diagnostic.</summary>
public enum CompiledInputArtifactInspectionNextAction
{
    /// <summary>No corrective action is required.</summary>
    None,

    /// <summary>Select an input that reaches the compiled required end.</summary>
    SelectCompatibleInput,

    /// <summary>Review the ignored half-open trailing range before Build.</summary>
    ReviewIgnoredTrailingBytes,

    /// <summary>Review an unexpected accepted outer length.</summary>
    ReviewUnexpectedOuterLength,
}

/// <summary>
/// Path-free diagnostic for one immutable source inspected against one compiled input-space binding.
/// A prior inspection is display evidence only; Build re-reads and revalidates its own binding.
/// </summary>
public sealed record CompiledInputArtifactInspectionResult(
    string AddressSpaceId,
    string SlotId,
    long ActualLength,
    string ActualSha256,
    long RequiredEndExclusive,
    IReadOnlyList<long> ExpectedOuterLengths,
    ByteRange? AcceptedSnapshotRange,
    string? AcceptedSnapshotSha256,
    ByteRange? IgnoredTrailingRange,
    CompiledInputArtifactInspectionSeverity Severity,
    string IssueCode,
    bool BlocksBuild,
    CompiledInputArtifactInspectionNextAction NextAction)
{
    /// <summary>Number of immutable source bytes excluded from the execution snapshot.</summary>
    public long IgnoredTrailingBytes => IgnoredTrailingRange?.Length ?? 0;
}

/// <summary>
/// Inspects an immutable source using only one compiler-owned input contract. Informational IC,
/// filename, PID, version, and hash values cannot select or modify the policy.
/// </summary>
public static class CompiledInputArtifactInspectionService
{
    /// <summary>Creates one deterministic diagnostic from a compiled declared-prefix requirement.</summary>
    public static CompiledInputArtifactInspectionResult InspectDeclaredPrefix(
        CompiledInputContract inputContract,
        string addressSpaceId,
        ReadOnlyMemory<byte> sourceBytes)
    {
        ArgumentNullException.ThrowIfNull(inputContract);
        ArgumentException.ThrowIfNullOrWhiteSpace(addressSpaceId);

        CompiledInputSpaceBinding binding = inputContract.SpaceBindings.SingleOrDefault(candidate =>
            StringComparer.Ordinal.Equals(candidate.AddressSpaceId, addressSpaceId)) ??
            throw new ArgumentException(
                $"Compiled input contract does not declare address space '{addressSpaceId}'.",
                nameof(addressSpaceId));
        CompiledInputSlotRequirement slot = inputContract.Slots.Single(candidate =>
            StringComparer.Ordinal.Equals(candidate.SlotId, binding.SlotId));
        if (slot.LengthRequirement is not CompiledDeclaredPrefixWithWarningInputLengthRequirement requirement)
        {
            throw new ArgumentException(
                $"Compiled input address space '{addressSpaceId}' does not use declared-prefix inspection.",
                nameof(addressSpaceId));
        }

        InputArtifactInspection inspection = DeclaredPrefixInputInspector.Inspect(
            new DeclaredPrefixInputInspectionPolicy(
                requirement.RequiredEndExclusive,
                requirement.ExpectedOuterLengths,
                requirement.ShortInputIssueCode,
                requirement.UnexpectedOuterLengthIssueCode),
            sourceBytes);

        return new CompiledInputArtifactInspectionResult(
            binding.AddressSpaceId,
            slot.SlotId,
            inspection.ActualSource.Length,
            inspection.ActualSource.Sha256,
            inspection.RequiredEndExclusive,
            inspection.ExpectedOuterLengths,
            inspection.AcceptedSnapshotRange,
            inspection.AcceptedSnapshot?.Sha256,
            inspection.IgnoredTrailingRange,
            MapSeverity(inspection.Severity),
            inspection.IssueCode,
            inspection.BuildImpact == InputArtifactBuildImpact.Blocked,
            MapNextAction(inspection.NextAction));
    }

    private static CompiledInputArtifactInspectionSeverity MapSeverity(InputArtifactInspectionSeverity severity)
    {
        return severity switch
        {
            InputArtifactInspectionSeverity.Valid => CompiledInputArtifactInspectionSeverity.Valid,
            InputArtifactInspectionSeverity.Warning => CompiledInputArtifactInspectionSeverity.Warning,
            InputArtifactInspectionSeverity.Blocking => CompiledInputArtifactInspectionSeverity.Blocking,
            _ => throw new ArgumentOutOfRangeException(nameof(severity), severity, null),
        };
    }

    private static CompiledInputArtifactInspectionNextAction MapNextAction(
        InputArtifactInspectionNextAction nextAction)
    {
        return nextAction switch
        {
            InputArtifactInspectionNextAction.None => CompiledInputArtifactInspectionNextAction.None,
            InputArtifactInspectionNextAction.SelectCompatibleInput =>
                CompiledInputArtifactInspectionNextAction.SelectCompatibleInput,
            InputArtifactInspectionNextAction.ReviewIgnoredTrailingBytes =>
                CompiledInputArtifactInspectionNextAction.ReviewIgnoredTrailingBytes,
            InputArtifactInspectionNextAction.ReviewUnexpectedOuterLength =>
                CompiledInputArtifactInspectionNextAction.ReviewUnexpectedOuterLength,
            _ => throw new ArgumentOutOfRangeException(nameof(nextAction), nextAction, null),
        };
    }
}
