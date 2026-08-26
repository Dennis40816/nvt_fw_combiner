using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Strict atomic launcher journal sharing the separate app-state lease identity.</summary>
internal sealed class JsonLauncherBootstrapStateStore : ILauncherBootstrapStateStore
{
    private const int MaximumStateBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 16,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };
    private static readonly VersionManagementJsonContext JsonContext = new(JsonOptions);
    private readonly string _path;

    internal JsonLauncherBootstrapStateStore(string versionManagerStatePath)
    {
        _path = DerivePath(versionManagerStatePath);
    }

    internal static string DerivePath(string versionManagerStatePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(versionManagerStatePath);
        return Path.GetFullPath(versionManagerStatePath) + ".launcher-bootstrap.v1.json";
    }

    public async ValueTask<LauncherBootstrapStateLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return Failure(LauncherBootstrapStateLoadIssue.Missing);
        }
        try
        {
            byte[]? bytes = await ManagedPathSafety.ReadBoundedFileAsync(
                _path,
                MaximumStateBytes,
                cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                return Failure(LauncherBootstrapStateLoadIssue.Invalid);
            }
            using JsonDocument json = EmbeddedVersionManagementSchema.ParseStrict(bytes, maximumDepth: 16);
            if (!LauncherBootstrapStateSchema.IsValid(json.RootElement))
            {
                return Failure(LauncherBootstrapStateLoadIssue.Invalid);
            }
            LauncherBootstrapStateDocument? document = JsonSerializer.Deserialize(
                json.RootElement,
                JsonContext.LauncherBootstrapStateDocument);
            LauncherBootstrapState? state = document is null ? null : Project(document);
            return state is null
                ? Failure(LauncherBootstrapStateLoadIssue.Invalid)
                : new(state, LauncherBootstrapStateLoadIssue.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            return Failure(LauncherBootstrapStateLoadIssue.Invalid);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(LauncherBootstrapStateLoadIssue.Unavailable);
        }
    }

    public async ValueTask<LauncherBootstrapStateSaveResult> TrySaveAsync(
        LauncherBootstrapState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        try
        {
            byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
                Project(state),
                JsonContext.LauncherBootstrapStateDocument);
            if (bytes.Length > MaximumStateBytes)
            {
                return new(LauncherBootstrapStateSaveIssue.Unavailable);
            }
            await WriteAtomicallyAsync(_path, bytes, cancellationToken).ConfigureAwait(false);
            return new(LauncherBootstrapStateSaveIssue.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            return new(LauncherBootstrapStateSaveIssue.Unavailable);
        }
    }

    private static LauncherBootstrapState? Project(LauncherBootstrapStateDocument document)
    {
        if (document.SchemaVersion != 1 || string.IsNullOrWhiteSpace(document.ManagedRootIdentity) ||
            !TryProject(document.Active, out ManagedLauncherIdentity? active) ||
            !TryProject(document.LastKnownGood, out ManagedLauncherIdentity? lastKnownGood) ||
            !TryProject(document.Failed, out ManagedLauncherIdentity? failed))
        {
            return null;
        }
        PendingLauncherActivation? pending = null;
        if (document.Pending is { } pendingDocument)
        {
            if (!TryProject(pendingDocument.Candidate, out ManagedLauncherIdentity? candidate) ||
                candidate is null ||
                !TryProject(pendingDocument.PreviousActive, out ManagedLauncherIdentity? previousActive) ||
                !TryProject(pendingDocument.PreviousLastKnownGood, out ManagedLauncherIdentity? previousLastKnownGood) ||
                !TryParsePhase(pendingDocument.Phase, out LauncherActivationPhase phase))
            {
                return null;
            }
            pending = PendingLauncherActivation.Create(candidate, previousActive, previousLastKnownGood, phase);
        }
        return LauncherBootstrapState.Create(
            document.ManagedRootIdentity,
            active,
            lastKnownGood,
            pending,
            failed);
    }

    private static LauncherBootstrapStateDocument Project(LauncherBootstrapState state)
    {
        PendingLauncherActivationDocument? pending = state.Pending is { } value
            ? new(
                Project(value.Candidate),
                value.PreviousActive is null ? null : Project(value.PreviousActive),
                value.PreviousLastKnownGood is null ? null : Project(value.PreviousLastKnownGood),
                FormatPhase(value.Phase))
            : null;
        return new(
            1,
            state.ManagedRootIdentity,
            state.Active is null ? null : Project(state.Active),
            state.LastKnownGood is null ? null : Project(state.LastKnownGood),
            pending,
            state.Failed is null ? null : Project(state.Failed));
    }

    private static LauncherIdentityDocument Project(ManagedLauncherIdentity identity)
    {
        return new(
            identity.OwnerAppVersion.ToString(),
            identity.OwnerAdmissionIdentity,
            identity.OwnerReleaseManifestSha256,
            identity.LauncherVersion.ToString(),
            identity.ProtocolVersion,
            identity.ExecutableRelativePath,
            identity.Size,
            identity.Sha256);
    }

    private static bool TryProject(
        LauncherIdentityDocument? document,
        out ManagedLauncherIdentity? identity)
    {
        if (document is null)
        {
            identity = null;
            return true;
        }
        try
        {
            identity = ManagedLauncherIdentity.Create(
                ManagedAppVersion.Parse(document.OwnerAppVersion ?? string.Empty),
                document.OwnerAdmissionIdentity ?? string.Empty,
                document.OwnerReleaseManifestSha256 ?? string.Empty,
                ManagedAppVersion.Parse(document.LauncherVersion ?? string.Empty),
                document.ProtocolVersion,
                document.ExecutableRelativePath ?? string.Empty,
                document.Size,
                document.Sha256 ?? string.Empty);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            identity = null;
            return false;
        }
    }

    private static bool TryParsePhase(string? value, out LauncherActivationPhase phase)
    {
        switch (value)
        {
            case "requested":
                phase = LauncherActivationPhase.Requested;
                return true;
            case "candidateLaunchRecorded":
                phase = LauncherActivationPhase.CandidateLaunchRecorded;
                return true;
            case "rollbackLaunchRecorded":
                phase = LauncherActivationPhase.RollbackLaunchRecorded;
                return true;
            case "activeLaunchRecorded":
                phase = LauncherActivationPhase.ActiveLaunchRecorded;
                return true;
            default:
                phase = default;
                return false;
        }
    }

    private static string FormatPhase(LauncherActivationPhase phase)
    {
        return phase switch
        {
            LauncherActivationPhase.Requested => "requested",
            LauncherActivationPhase.CandidateLaunchRecorded => "candidateLaunchRecorded",
            LauncherActivationPhase.RollbackLaunchRecorded => "rollbackLaunchRecorded",
            LauncherActivationPhase.ActiveLaunchRecorded => "activeLaunchRecorded",
            _ => throw new InvalidOperationException("Unknown launcher activation phase."),
        };
    }

    private static async ValueTask WriteAtomicallyAsync(
        string destination,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Launcher state has no parent directory.");
        }
        _ = Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporary, destination, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                try
                {
                    File.Delete(temporary);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }
    }

    private static LauncherBootstrapStateLoadResult Failure(LauncherBootstrapStateLoadIssue issue)
    {
        return new(null, issue);
    }
}

internal sealed record LauncherBootstrapStateDocument(
    int SchemaVersion,
    string? ManagedRootIdentity,
    LauncherIdentityDocument? Active,
    LauncherIdentityDocument? LastKnownGood,
    PendingLauncherActivationDocument? Pending,
    LauncherIdentityDocument? Failed);

internal sealed record PendingLauncherActivationDocument(
    LauncherIdentityDocument? Candidate,
    LauncherIdentityDocument? PreviousActive,
    LauncherIdentityDocument? PreviousLastKnownGood,
    string? Phase);

internal sealed record LauncherIdentityDocument(
    string? OwnerAppVersion,
    string? OwnerAdmissionIdentity,
    string? OwnerReleaseManifestSha256,
    string? LauncherVersion,
    int ProtocolVersion,
    string? ExecutableRelativePath,
    long Size,
    string? Sha256);
