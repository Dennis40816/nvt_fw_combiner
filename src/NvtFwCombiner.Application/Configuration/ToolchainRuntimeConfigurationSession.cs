using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Contracts.Configuration;

namespace NvtFwCombiner.Application.Configuration;

/// <summary>Serializes exact candidate admission, atomic persistence and immutable configuration publication.</summary>
internal sealed class ToolchainRuntimeConfigurationSession(
    IToolchainRuntimeConfigurationStorage storage,
    IToolchainRuntimeCandidateInspector inspector) : IToolchainRuntimeConfigurationSession, IDisposable
{
    private readonly IToolchainRuntimeConfigurationStorage _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    private readonly IToolchainRuntimeCandidateInspector _inspector = inspector ?? throw new ArgumentNullException(nameof(inspector));
    private readonly SemaphoreSlim _gate = new(1, 1);
    private ToolchainRuntimeConfigurationSnapshot _current = new(0, ToolchainRuntimeConfigurationStatus.NotLoaded, null, null, []);

    public ToolchainRuntimeConfigurationSnapshot Current => Volatile.Read(ref _current);

    public ValueTask<ToolchainRuntimeCandidateInspection> InspectAsync(string path, CancellationToken cancellationToken)
    {
        return _inspector.InspectAsync(path, cancellationToken);
    }

    public ValueTask<IReadOnlyList<ToolchainRuntimeCandidateInspection>> DetectAsync(CancellationToken cancellationToken)
    {
        return _inspector.DetectAsync(cancellationToken);
    }

    public ValueTask<ToolchainRuntimeCandidateInspection> InspectBundledAsync(CancellationToken cancellationToken)
    {
        return _inspector.InspectBundledAsync(cancellationToken);
    }

    public async ValueTask<ToolchainRuntimeConfigurationOperationResult> SaveAsync(
        ToolchainRuntimeSelection selection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(selection);
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            IReadOnlyList<ToolchainRuntimeConfigurationIssue> issues = await AdmitAsync(selection, cancellationToken).ConfigureAwait(false);
            if (issues.Count != 0)
            {
                return new(Current, false, issues);
            }

            await _storage.WriteAsync(new(1, selection.Source == ToolchainRuntimeSource.Bundled ? "bundled" : "user",
                selection.Path, selection.Sha256), cancellationToken).ConfigureAwait(false);
            // Storage has committed. Late cancellation cannot hide or roll back this publication.
            return Publish(selection, selection, []);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new(Current, false, [Issue(exception is ToolchainRuntimeConfigurationFormatException
                ? "configuration.invalid" : "configuration.save-failed", "Toolchain runtime configuration could not be saved.")]);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    public async ValueTask<ToolchainRuntimeConfigurationOperationResult> ReloadAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        ToolchainRuntimeSelection? requested = Current.RequestedSelection;
        try
        {
            ToolchainRuntimeConfigurationDocument? document = await _storage.ReadAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (document is null)
            {
                var bundled = new ToolchainRuntimeSelection(ToolchainRuntimeSource.Bundled);
                return Publish(bundled, bundled, []);
            }

            ToolchainRuntimeSelection? parsed = document.Source switch
            {
                "bundled" => new(ToolchainRuntimeSource.Bundled, document.Path, document.Sha256),
                "user" => new(ToolchainRuntimeSource.User, document.Path, document.Sha256),
                _ => null,
            };
            requested = parsed ?? requested;
            if (document.SchemaVersion != 1 || parsed is null)
            {
                return Publish(requested, null, [Issue("configuration.invalid", "Toolchain runtime configuration version or source is invalid.")]);
            }

            IReadOnlyList<ToolchainRuntimeConfigurationIssue> issues = await AdmitAsync(parsed, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return Publish(requested, issues.Count == 0 ? parsed : null, issues);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Publish(requested, null, [Issue(exception is ToolchainRuntimeConfigurationFormatException
                ? "configuration.invalid" : "configuration.read-failed", "Toolchain runtime configuration could not be loaded.")]);
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    private async ValueTask<IReadOnlyList<ToolchainRuntimeConfigurationIssue>> AdmitAsync(
        ToolchainRuntimeSelection selection, CancellationToken cancellationToken)
    {
        if (selection.Source == ToolchainRuntimeSource.Bundled)
        {
            return selection.Path is null && selection.Sha256 is null ? [] :
                [Issue("selection.invalid", "Bundled runtime selection cannot contain a user path or hash.")];
        }

        if (selection.Source != ToolchainRuntimeSource.User || string.IsNullOrWhiteSpace(selection.Path) ||
            !Path.IsPathFullyQualified(selection.Path) || !IsSha256(selection.Sha256))
        {
            return [Issue("selection.invalid", "User runtime selection requires an absolute path and a SHA-256 pin.")];
        }

        ToolchainRuntimeCandidateInspection candidate;
        try
        {
            candidate = await _inspector.InspectAsync(selection.Path, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [Issue("candidate.inspect-failed", "The selected runtime could not be inspected.")];
        }

        return candidate.Verification != ToolchainRuntimeCandidateVerification.Verified || candidate.Identity is null ||
            candidate.Issues.Count != 0 || string.IsNullOrWhiteSpace(candidate.Identity.FileVersion) ||
            string.IsNullOrWhiteSpace(candidate.Identity.Architecture)
            ? [.. candidate.Issues, Issue("candidate.unverified", "Complete runtime verification is required.")]
            : StringComparer.Ordinal.Equals(candidate.Identity.Path, selection.Path) &&
            StringComparer.OrdinalIgnoreCase.Equals(candidate.Identity.Sha256, selection.Sha256)
            ? [] : [Issue("candidate.identity-changed", "The inspected runtime does not match the selected path and SHA-256 pin.")];
    }

    private ToolchainRuntimeConfigurationOperationResult Publish(ToolchainRuntimeSelection? requested,
        ToolchainRuntimeSelection? selection, IReadOnlyList<ToolchainRuntimeConfigurationIssue> issues)
    {
        var next = new ToolchainRuntimeConfigurationSnapshot(checked(Current.Generation + 1),
            selection is null ? ToolchainRuntimeConfigurationStatus.Blocked : ToolchainRuntimeConfigurationStatus.Current,
            requested, selection, issues);
        Volatile.Write(ref _current, next);
        return new(next, selection is not null, issues);
    }

    private static bool IsSha256(string? value)
    {
        return value is { Length: 64 } && value.All(static character => char.IsAsciiHexDigit(character));
    }

    private static ToolchainRuntimeConfigurationIssue Issue(string suffix, string message)
    {
        return new("toolchain-runtime." + suffix, message);
    }

    /// <summary>The host must finish all operations before disposing this session.</summary>
    public void Dispose()
    {
        _gate.Dispose();
    }
}
