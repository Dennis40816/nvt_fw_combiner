namespace NvtFwCombiner.Application.VersionManagement;

/// <summary>Managed-child startup handoff plus its durable version snapshot.</summary>
public sealed record ManagedApplicationStartupResult(
    ApplicationReadySignalOutcome ReadySignalOutcome,
    VersionManagementSnapshot Snapshot);

/// <summary>Application-owned startup seam for READY-qualified state reload.</summary>
public interface IManagedApplicationStartupCoordinator
{
    /// <summary>Reports READY once and loads the exact durable version state.</summary>
    ValueTask<ManagedApplicationStartupResult> CompleteStartupAsync(
        CancellationToken cancellationToken);
}

/// <summary>Coordinates the managed child READY handoff with the launcher's exact state-path lease.</summary>
public sealed class ManagedApplicationStartupCoordinator : IManagedApplicationStartupCoordinator
{
    private readonly ManagedAppVersion _applicationVersion;
    private readonly IApplicationReadySignal _readySignal;
    private readonly IVersionManagementExperience _versionManagement;

    /// <summary>Creates one managed-child startup coordinator.</summary>
    public ManagedApplicationStartupCoordinator(
        ManagedAppVersion applicationVersion,
        IApplicationReadySignal readySignal,
        IVersionManagementExperience versionManagement)
    {
        _applicationVersion = applicationVersion;
        _readySignal = readySignal ?? throw new ArgumentNullException(nameof(readySignal));
        _versionManagement = versionManagement ?? throw new ArgumentNullException(nameof(versionManagement));
    }

    /// <inheritdoc />
    public async ValueTask<ManagedApplicationStartupResult> CompleteStartupAsync(
        CancellationToken cancellationToken)
    {
        ApplicationReadySignalOutcome ready = await _readySignal.ReportReadyAsync(
            _applicationVersion,
            cancellationToken).ConfigureAwait(false);
        VersionManagementSnapshot snapshot = ready == ApplicationReadySignalOutcome.Reported
            ? await _versionManagement.InitializeAfterManagedReadyAsync(
                cancellationToken).ConfigureAwait(false)
            : await _versionManagement.InitializeAsync(cancellationToken).ConfigureAwait(false);
        return new(ready, snapshot);
    }
}
