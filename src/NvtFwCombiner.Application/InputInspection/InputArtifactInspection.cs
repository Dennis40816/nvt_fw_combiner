using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.InputInspection;

/// <summary>Selection/inspection lifecycle kept separate from any selected host path.</summary>
internal enum InputArtifactInspectionLifecycle
{
    /// <summary>The selected artifact has a current typed inspection.</summary>
    Inspected,
}

/// <summary>Deterministic health priority for one completed input inspection.</summary>
internal enum InputArtifactInspectionSeverity
{
    /// <summary>The inspected source satisfies the policy without a diagnostic.</summary>
    Valid,
    /// <summary>The source is accepted, but the diagnostic must remain visible.</summary>
    Warning,
    /// <summary>The source cannot be used by Build.</summary>
    Blocking,
}

/// <summary>Whether the completed inspection permits Build to continue validation.</summary>
internal enum InputArtifactBuildImpact
{
    /// <summary>The inspection itself does not block Build.</summary>
    None,
    /// <summary>The inspection blocks Build.</summary>
    Blocked,
}

/// <summary>Typed next action; display text remains a Presentation concern.</summary>
internal enum InputArtifactInspectionNextAction
{
    /// <summary>No corrective action is required.</summary>
    None,
    /// <summary>Select an input that reaches the required exclusive end.</summary>
    SelectCompatibleInput,
    /// <summary>Review the ignored half-open trailing range before Build.</summary>
    ReviewIgnoredTrailingBytes,
    /// <summary>Review an unexpected outer length that has no ignored trailing bytes.</summary>
    ReviewUnexpectedOuterLength,
}

/// <summary>Stable generic issue codes emitted by the Application inspection substrate.</summary>
internal static class InputArtifactInspectionIssueCodes
{
    /// <summary>The source matches one compiler-owned expected outer length.</summary>
    public const string Ready = "input.inspection.ready";
}

/// <summary>Immutable content identity for a full selected source or accepted execution snapshot.</summary>
internal sealed record InputArtifactContentIdentity
{
    /// <summary>Creates a checked byte-length and lowercase SHA-256 identity.</summary>
    internal InputArtifactContentIdentity(long length, string sha256)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentException.ThrowIfNullOrWhiteSpace(sha256);
        if (sha256.Length != 64 || sha256.Any(static character =>
                character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("SHA-256 must be 64 lowercase hexadecimal characters.", nameof(sha256));
        }

        Length = length;
        Sha256 = sha256;
    }

    /// <summary>Byte length represented by this identity.</summary>
    public long Length { get; }

    /// <summary>Lowercase SHA-256 of the represented bytes.</summary>
    public string Sha256 { get; }
}

/// <summary>
/// Immutable, path-free result for one completed declared-prefix input inspection.
/// This result is diagnostic evidence only: Build must inspect its current source again or bind an
/// independently immutable accepted snapshot; a prior result never grants execution authority.
/// </summary>
internal sealed class InputArtifactInspection
{
    private readonly long[] _expectedOuterLengths;

    internal InputArtifactInspection(
        InputArtifactContentIdentity actualSource,
        long requiredEndExclusive,
        IEnumerable<long> expectedOuterLengths,
        InputArtifactContentIdentity? acceptedSnapshot,
        ByteRange? acceptedSnapshotRange,
        ByteRange? ignoredTrailingRange,
        InputArtifactInspectionSeverity severity,
        string issueCode,
        InputArtifactBuildImpact buildImpact,
        InputArtifactInspectionNextAction nextAction)
    {
        ArgumentNullException.ThrowIfNull(actualSource);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(requiredEndExclusive);
        ArgumentNullException.ThrowIfNull(expectedOuterLengths);
        ArgumentException.ThrowIfNullOrWhiteSpace(issueCode);
        if (!Enum.IsDefined(severity))
        {
            throw new ArgumentOutOfRangeException(nameof(severity), severity, "Unknown inspection severity.");
        }

        if (!Enum.IsDefined(buildImpact))
        {
            throw new ArgumentOutOfRangeException(nameof(buildImpact), buildImpact, "Unknown Build impact.");
        }

        if (!Enum.IsDefined(nextAction))
        {
            throw new ArgumentOutOfRangeException(nameof(nextAction), nextAction, "Unknown inspection next action.");
        }

        _expectedOuterLengths = [.. expectedOuterLengths];
        bool hasAcceptedSnapshot = acceptedSnapshot is not null;
        if (hasAcceptedSnapshot != acceptedSnapshotRange.HasValue ||
            (acceptedSnapshot is not null &&
             (acceptedSnapshot.Length != requiredEndExclusive ||
              acceptedSnapshotRange != new ByteRange(0, requiredEndExclusive))))
        {
            throw new ArgumentException(
                "An accepted snapshot identity and its exact declared-prefix range must be supplied together.",
                nameof(acceptedSnapshot));
        }

        if (ignoredTrailingRange is { } ignored &&
            (ignored.Start != requiredEndExclusive || ignored.EndExclusive != actualSource.Length))
        {
            throw new ArgumentException(
                "The ignored trailing range must be [requiredEndExclusive, actualLength).",
                nameof(ignoredTrailingRange));
        }

        ActualSource = actualSource;
        Lifecycle = InputArtifactInspectionLifecycle.Inspected;
        RequiredEndExclusive = requiredEndExclusive;
        ExpectedOuterLengths = Array.AsReadOnly(_expectedOuterLengths);
        AcceptedSnapshot = acceptedSnapshot;
        AcceptedSnapshotRange = acceptedSnapshotRange;
        IgnoredTrailingRange = ignoredTrailingRange;
        Severity = severity;
        IssueCode = issueCode;
        BuildImpact = buildImpact;
        NextAction = nextAction;
    }

    /// <summary>Completed typed inspection lifecycle; selected path state remains external.</summary>
    public InputArtifactInspectionLifecycle Lifecycle { get; }

    /// <summary>Identity of every byte supplied by the selected source.</summary>
    public InputArtifactContentIdentity ActualSource { get; }

    /// <summary>First unavailable byte that would make this source blocking.</summary>
    public long RequiredEndExclusive { get; }

    /// <summary>Compiler-owned complete source lengths that avoid a warning.</summary>
    public IReadOnlyList<long> ExpectedOuterLengths { get; }

    /// <summary>Identity of the accepted immutable prefix, or null when the required end is unavailable.</summary>
    public InputArtifactContentIdentity? AcceptedSnapshot { get; }

    /// <summary>Accepted half-open execution-snapshot span, or null for a blocking short source.</summary>
    public ByteRange? AcceptedSnapshotRange { get; }

    /// <summary>Ignored half-open source tail, or null when no trailing bytes exist.</summary>
    public ByteRange? IgnoredTrailingRange { get; }

    /// <summary>Number of ignored trailing bytes.</summary>
    public long IgnoredTrailingBytes => IgnoredTrailingRange?.Length ?? 0;

    /// <summary>Typed health severity independent from file selection.</summary>
    public InputArtifactInspectionSeverity Severity { get; }

    /// <summary>Stable issue code for localization and deterministic ordering.</summary>
    public string IssueCode { get; }

    /// <summary>Typed Build effect of this diagnostic.</summary>
    public InputArtifactBuildImpact BuildImpact { get; }

    /// <summary>Typed follow-up action without presentation text.</summary>
    public InputArtifactInspectionNextAction NextAction { get; }
}
