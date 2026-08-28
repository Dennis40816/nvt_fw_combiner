using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Strict atomic JSON adapter for separate launcher state v1.</summary>
public sealed class JsonVersionManagerStateStore : IVersionManagerStateStore
{
    /// <summary>The launcher-state file name under per-user local application data.</summary>
    public const string StateFileName = "version-manager.v1.json";

    private const int LegacySchemaVersion = 1;
    private const int RegistrySchemaVersion = 2;
    private const int MaximumStateBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 32,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };
    private static readonly VersionManagementJsonContext JsonContext = new(JsonOptions);
    private readonly bool _allowUnboundSeedTemplate;
    private readonly string _path;

    /// <summary>Creates a state store at an explicit executable-owned location.</summary>
    /// <param name="path">Exact state-file path.</param>
    /// <param name="allowUnboundSeedTemplate">Whether this store is exclusively writing a packaged unbound seed template.</param>
    public JsonVersionManagerStateStore(string path, bool allowUnboundSeedTemplate = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = Path.GetFullPath(path);
        _allowUnboundSeedTemplate = allowUnboundSeedTemplate;
    }

    /// <summary>Gets the canonical per-user launcher state path.</summary>
    /// <returns>The full default state path.</returns>
    public static string GetDefaultPath()
    {
        string localApplicationData = Environment.GetEnvironmentVariable("LOCALAPPDATA") ??
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        ArgumentException.ThrowIfNullOrWhiteSpace(localApplicationData);
        return Path.Combine(
            Path.GetFullPath(localApplicationData),
            "NvtFwCombiner",
            StateFileName);
    }

    /// <inheritdoc />
    public ValueTask<VersionManagerWriteLeaseResult> TryAcquireWriteLeaseAsync(
        TimeSpan waitTimeout,
        CancellationToken cancellationToken)
    {
        return FileSystemVersionManagerWriteLease.TryAcquireAsync(
            _path,
            waitTimeout,
            cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<VersionManagerStateLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
        {
            return Failure(VersionManagerStateLoadIssue.Missing);
        }
        try
        {
            byte[]? bytes = await ManagedPathSafety.ReadBoundedFileAsync(
                _path,
                MaximumStateBytes,
                cancellationToken).ConfigureAwait(false);
            if (bytes is null)
            {
                return Failure(VersionManagerStateLoadIssue.Invalid);
            }
            using JsonDocument stateJson = EmbeddedVersionManagementSchema.ParseStrict(
                bytes,
                maximumDepth: 32);
            VersionManagerStateDocument? document = JsonSerializer.Deserialize(
                stateJson.RootElement,
                JsonContext.VersionManagerStateDocument);
            if (document is null ||
                document.SchemaVersion is not (LegacySchemaVersion or RegistrySchemaVersion) ||
                (document.SchemaVersion == LegacySchemaVersion && document.SourceRegistryState is not null) ||
                (document.SchemaVersion == RegistrySchemaVersion && document.SourceRegistryState is null))
            {
                return Failure(VersionManagerStateLoadIssue.Invalid);
            }

            VersionManagerState? state = Project(document);
            return state is null
                ? Failure(VersionManagerStateLoadIssue.Invalid)
                : new(state, VersionManagerStateLoadIssue.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            return Failure(VersionManagerStateLoadIssue.Invalid);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return Failure(VersionManagerStateLoadIssue.Unavailable);
        }
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        VersionManagerState state,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.ManagedRootIdentity is null && !_allowUnboundSeedTemplate)
        {
            throw new InvalidOperationException(
                "Durable version-manager state must be bound to one managed root.");
        }
        VersionManagerStateDocument document = Project(state);
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(
            document,
            JsonContext.VersionManagerStateDocument);
        if (bytes.Length > MaximumStateBytes)
        {
            throw new InvalidOperationException("Version-manager state exceeds its bounded size.");
        }
        await WriteAtomicallyAsync(_path, bytes, cancellationToken).ConfigureAwait(false);
    }

    private static VersionManagerState? Project(VersionManagerStateDocument document)
    {
        IReadOnlyList<ManagedVersionAdmissionDocument?> admissions = document.Admissions ?? [];
        var installed = new List<ManagedVersionAdmission>(admissions.Count);
        foreach (ManagedVersionAdmissionDocument? admission in admissions)
        {
            if (admission is null ||
                !ManagedAppVersion.TryParse(admission.Version, out ManagedAppVersion version) ||
                string.IsNullOrWhiteSpace(admission.AdmissionIdentity) ||
                !IsLowerSha256(admission.ReleaseManifestSha256))
            {
                return null;
            }
            installed.Add(new(version, admission.AdmissionIdentity, admission.ReleaseManifestSha256!));
        }

        if (!TryParseOptional(document.ActiveVersion, out ManagedAppVersion? active) ||
            !TryParseOptional(document.LastKnownGoodVersion, out ManagedAppVersion? lastKnownGood) ||
            !TryParseOptional(document.FailedActivationVersion, out ManagedAppVersion? failed))
        {
            return null;
        }

        PendingVersionActivation? pending = null;
        if (document.PendingActivation is { } pendingDocument)
        {
            if (!ManagedAppVersion.TryParse(pendingDocument.CandidateVersion, out ManagedAppVersion candidate) ||
                string.IsNullOrWhiteSpace(pendingDocument.CandidateAdmissionIdentity) ||
                !TryParseOptional(pendingDocument.PreviousActiveVersion, out ManagedAppVersion? previousActive) ||
                !TryParseOptional(pendingDocument.PreviousLastKnownGoodVersion, out ManagedAppVersion? previousLastKnownGood) ||
                !TryParseActivationPhase(pendingDocument.Phase, out VersionActivationPhase phase))
            {
                return null;
            }
            pending = new(
                candidate,
                pendingDocument.CandidateAdmissionIdentity,
                previousActive,
                previousLastKnownGood,
                phase);
        }

        PendingManagedVersionMutation? pendingMutation = null;
        if (document.PendingMutation is { } mutationDocument)
        {
            if (!TryParseMutationKind(mutationDocument.Kind, out ManagedVersionMutationKind kind) ||
                mutationDocument.Admission is not { } admissionDocument ||
                !ManagedAppVersion.TryParse(admissionDocument.Version, out ManagedAppVersion mutationVersion) ||
                string.IsNullOrWhiteSpace(admissionDocument.AdmissionIdentity) ||
                !IsLowerSha256(admissionDocument.ReleaseManifestSha256))
            {
                return null;
            }
            pendingMutation = new(
                kind,
                new(
                    mutationVersion,
                    admissionDocument.AdmissionIdentity,
                    admissionDocument.ReleaseManifestSha256!));
        }

        VersionSourceRegistryState? sourceRegistryState = null;
        if (document.SourceRegistryState is { } registryDocument)
        {
            try
            {
                sourceRegistryState = new(
                    registryDocument.AcceptedRevision,
                    registryDocument.AcceptedDigest,
                    registryDocument.IsManualPin);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        VersionManagerState state = VersionManagerState.Create(
            document.UpdateSource,
            active,
            lastKnownGood,
            installed,
            pending,
            failed,
            document.RetentionReviewDue,
            pendingMutation,
            document.ManagedRootIdentity,
            sourceRegistryState);
        return document.ManagedRootIdentity is null || string.Equals(
            document.ManagedRootIdentity,
            state.ManagedRootIdentity,
            StringComparison.Ordinal)
                ? state
                : null;
    }

    private static VersionManagerStateDocument Project(VersionManagerState state)
    {
        PendingVersionActivationDocument? pending = state.PendingActivation is { } value
            ? new(
                value.CandidateVersion.ToString(),
                value.CandidateAdmissionIdentity,
                value.PreviousActiveVersion?.ToString(),
                value.PreviousLastKnownGoodVersion?.ToString(),
                FormatActivationPhase(value.Phase))
            : null;
        PendingManagedVersionMutationDocument? pendingMutation = state.PendingMutation is { } mutation
            ? new(
                FormatMutationKind(mutation.Kind),
                new(
                    mutation.Admission.Version.ToString(),
                    mutation.Admission.AdmissionIdentity,
                    mutation.Admission.ReleaseManifestSha256))
            : null;
        VersionSourceRegistryStateDocument? sourceRegistryState = state.SourceRegistryState is { } registry
            ? new(registry.AcceptedRevision, registry.AcceptedDigest, registry.IsManualPin)
            : null;
        return new(
            sourceRegistryState is null ? LegacySchemaVersion : RegistrySchemaVersion,
            state.UpdateSource,
            state.ActiveVersion?.ToString(),
            state.LastKnownGoodVersion?.ToString(),
            [.. state.Admissions.Select(admission => new ManagedVersionAdmissionDocument(
                admission.Version.ToString(),
                admission.AdmissionIdentity,
                admission.ReleaseManifestSha256))],
            pending,
            state.FailedActivationVersion?.ToString(),
            state.RetentionReviewDue,
            pendingMutation,
            state.ManagedRootIdentity,
            sourceRegistryState);
    }

    private static bool TryParseActivationPhase(string? value, out VersionActivationPhase phase)
    {
        switch (value)
        {
            case null:
            case "requested":
                phase = VersionActivationPhase.Requested;
                return true;
            case "candidateLaunchRecorded":
                phase = VersionActivationPhase.CandidateLaunchRecorded;
                return true;
            case "rollbackLaunchRecorded":
                phase = VersionActivationPhase.RollbackLaunchRecorded;
                return true;
            case "activeLaunchRecorded":
                phase = VersionActivationPhase.ActiveLaunchRecorded;
                return true;
            default:
                phase = default;
                return false;
        }
    }

    private static string? FormatActivationPhase(VersionActivationPhase phase)
    {
        return phase switch
        {
            VersionActivationPhase.Requested => null,
            VersionActivationPhase.CandidateLaunchRecorded => "candidateLaunchRecorded",
            VersionActivationPhase.RollbackLaunchRecorded => "rollbackLaunchRecorded",
            VersionActivationPhase.ActiveLaunchRecorded => "activeLaunchRecorded",
            _ => throw new InvalidOperationException("Unknown activation transaction phase."),
        };
    }

    private static bool TryParseMutationKind(string? value, out ManagedVersionMutationKind kind)
    {
        switch (value)
        {
            case "install":
                kind = ManagedVersionMutationKind.Install;
                return true;
            case "delete":
                kind = ManagedVersionMutationKind.Delete;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    private static string FormatMutationKind(ManagedVersionMutationKind kind)
    {
        return kind switch
        {
            ManagedVersionMutationKind.Install => "install",
            ManagedVersionMutationKind.Delete => "delete",
            _ => throw new InvalidOperationException("Unknown managed-version mutation kind."),
        };
    }

    private static bool TryParseOptional(string? value, out ManagedAppVersion? version)
    {
        if (value is null)
        {
            version = null;
            return true;
        }
        if (ManagedAppVersion.TryParse(value, out ManagedAppVersion parsed))
        {
            version = parsed;
            return true;
        }
        version = null;
        return false;
    }

    private static bool IsLowerSha256(string? value)
    {
        return value is { Length: 64 } &&
               value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    private static async ValueTask WriteAtomicallyAsync(
        string destination,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(destination);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException("Version-manager state has no parent directory.");
        }
        _ = Directory.CreateDirectory(directory);
        string temporary = Path.Combine(
            directory,
            $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
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

    private static VersionManagerStateLoadResult Failure(VersionManagerStateLoadIssue issue)
    {
        return new(null, issue);
    }
}
