using System.Security.Cryptography;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Infrastructure-only byte custody used by the fresh-root materializer.</summary>
internal interface IManagedDistributionPayloadContent
{
    /// <summary>Copies the already captured distribution Launcher bytes.</summary>
    ValueTask CopyDistributionLauncherAsync(
        string destination,
        CancellationToken cancellationToken);

    /// <summary>Copies the already captured immutable Root Bootstrap bytes.</summary>
    ValueTask CopyBootstrapAsync(string destination, CancellationToken cancellationToken);
}

/// <summary>Admits the running distribution Launcher and its embedded closed payload.</summary>
public sealed class EmbeddedManagedDistributionPayloadSource : IManagedDistributionPayloadSource
{
    private const int MaximumDescriptorBytes = 64 * 1024;
    private static readonly JsonSerializerOptions PayloadJsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 12,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private static readonly VersionManagementJsonContext PayloadJsonContext = new(PayloadJsonOptions);
    private readonly byte[]? _bootstrapBytes;
    private readonly byte[]? _descriptorBytes;
    private readonly string _launcherPath;

    /// <summary>Creates a source over exact host-provided embedded resources.</summary>
    public EmbeddedManagedDistributionPayloadSource(
        string launcherPath,
        ReadOnlyMemory<byte>? descriptorBytes,
        ReadOnlyMemory<byte>? bootstrapBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(launcherPath);
        _launcherPath = Path.GetFullPath(launcherPath);
        _descriptorBytes = descriptorBytes.HasValue ? descriptorBytes.Value.ToArray() : null;
        _bootstrapBytes = bootstrapBytes.HasValue ? bootstrapBytes.Value.ToArray() : null;
    }

    /// <summary>
    /// Projects only the descriptor-pinned Bootstrap identity for the healthy
    /// local entry path. This does not inspect or hash either payload binary.
    /// </summary>
    public bool TryProjectBootstrapIdentity(
        [NotNullWhen(true)] out ManagedImmutableBootstrapIdentity? identity)
    {
        DescriptorProjectionResult projection = ProjectDescriptor();
        if (!projection.IsSuccess)
        {
            identity = null;
            return false;
        }

        ManagedSetupEmbeddedBootstrapDocument bootstrap = projection.Descriptor!.Bootstrap;
        identity = new(
            bootstrap.InstalledFileName,
            bootstrap.Size,
            bootstrap.Sha256);
        return true;
    }

    /// <inheritdoc />
    public async ValueTask<ManagedDistributionPayloadInspectionResult> InspectAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DescriptorProjectionResult descriptor = ProjectEmbeddedPayload();
        if (!descriptor.IsSuccess)
        {
            return new(null, descriptor.Issue);
        }

        StableManagedExecutableMeasurementResult launcher = await StableManagedExecutableLaunchLease
            .TryMeasureAsync(_launcherPath, cancellationToken).ConfigureAwait(false);
        return launcher.IsMeasured
            ? new(
                CreateIdentity(descriptor, launcher.Length, launcher.Sha256),
                ManagedDistributionPayloadIssue.None)
            : new(null, MapMeasurementIssue(launcher.Issue));
    }

    /// <inheritdoc />
    [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification =
        "Successful stable-custody ownership transfers into the returned payload capture.")]
    public async ValueTask<ManagedDistributionPayloadCaptureResult> CaptureExactAsync(
        ManagedDistributionPayloadIdentity expected,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(expected);
        cancellationToken.ThrowIfCancellationRequested();
        DescriptorProjectionResult descriptor = ProjectEmbeddedPayload();
        if (!descriptor.IsSuccess)
        {
            return new(null, descriptor.Issue);
        }
        ManagedDistributionPayloadIdentity embedded = CreateIdentity(
            descriptor,
            expected.LauncherSize,
            expected.LauncherSha256);
        if (embedded != expected)
        {
            return new(null, ManagedDistributionPayloadIssue.Changed);
        }

        ManagedExecutableLaunchLeaseResult acquired = await StableManagedExecutableLaunchLease
            .TryAcquireAsync(
                _launcherPath,
                expected.LauncherSize,
                expected.LauncherSha256,
                cancellationToken).ConfigureAwait(false);
        if (!acquired.IsAcquired)
        {
            return new(null, acquired.Issue switch
            {
                ManagedExecutableLaunchIssue.Tampered => ManagedDistributionPayloadIssue.Changed,
                ManagedExecutableLaunchIssue.UnsafePath => ManagedDistributionPayloadIssue.Invalid,
                ManagedExecutableLaunchIssue.Unavailable => ManagedDistributionPayloadIssue.Unavailable,
                ManagedExecutableLaunchIssue.None => throw new InvalidOperationException(
                    "A successful Launcher capture did not return custody."),
                _ => throw new InvalidOperationException(
                    "Launcher custody returned an undefined issue."),
            });
        }
        if (acquired.Lease is not StableManagedExecutableLaunchLease stable)
        {
            acquired.Lease!.Dispose();
            return new(null, ManagedDistributionPayloadIssue.Invalid);
        }
        return new(
            new EmbeddedPayloadCapture(expected, stable, _bootstrapBytes!),
            ManagedDistributionPayloadIssue.None);
    }

    private DescriptorProjectionResult ProjectEmbeddedPayload()
    {
        DescriptorProjectionResult projection = ProjectDescriptor();
        if (!projection.IsSuccess)
        {
            return projection;
        }
        if (_bootstrapBytes is not { Length: > 0 } bootstrapBytes)
        {
            return DescriptorProjectionResult.Failure(
                _bootstrapBytes is null
                    ? ManagedDistributionPayloadIssue.Unavailable
                    : ManagedDistributionPayloadIssue.Invalid);
        }
        ManagedSetupPayloadAdmissionDescriptorDocument descriptor = projection.Descriptor!;
        return bootstrapBytes.LongLength == descriptor.Bootstrap.Size &&
            string.Equals(
                Convert.ToHexStringLower(SHA256.HashData(bootstrapBytes)),
                descriptor.Bootstrap.Sha256,
                StringComparison.Ordinal)
                ? projection
                : DescriptorProjectionResult.Failure(ManagedDistributionPayloadIssue.Invalid);
    }

    private DescriptorProjectionResult ProjectDescriptor()
    {
        if (_descriptorBytes is not { Length: > 0 and <= MaximumDescriptorBytes } descriptorBytes)
        {
            return DescriptorProjectionResult.Failure(
                _descriptorBytes is null
                    ? ManagedDistributionPayloadIssue.Unavailable
                    : ManagedDistributionPayloadIssue.Invalid);
        }
        try
        {
            using JsonDocument json = EmbeddedVersionManagementSchema.ParseStrict(
                descriptorBytes,
                maximumDepth: 12);
            if (!ManagedSetupPayloadAdmissionSchema.IsValid(json.RootElement))
            {
                return DescriptorProjectionResult.Failure(ManagedDistributionPayloadIssue.Invalid);
            }
            ManagedSetupPayloadAdmissionDescriptorDocument? descriptor = JsonSerializer.Deserialize(
                json.RootElement,
                PayloadJsonContext.ManagedSetupPayloadAdmissionDescriptorDocument);
            return descriptor is not null &&
                ManagedAppVersion.TryParse(
                    descriptor.LauncherVersion,
                    out ManagedAppVersion version) &&
                string.Equals(
                    descriptor.SourceCommit,
                    descriptor.Bootstrap.SourceCommit,
                    StringComparison.Ordinal)
                    ? new(
                        descriptor,
                        version,
                        descriptorBytes.LongLength,
                        Convert.ToHexStringLower(SHA256.HashData(descriptorBytes)),
                        ManagedDistributionPayloadIssue.None)
                    : DescriptorProjectionResult.Failure(
                        ManagedDistributionPayloadIssue.Invalid);
        }
        catch (Exception exception) when (exception is
            JsonException or ArgumentException or InvalidOperationException)
        {
            return DescriptorProjectionResult.Failure(ManagedDistributionPayloadIssue.Invalid);
        }
    }

    private static ManagedDistributionPayloadIdentity CreateIdentity(
        DescriptorProjectionResult projection,
        long launcherSize,
        string launcherSha256)
    {
        ManagedSetupPayloadAdmissionDescriptorDocument descriptor = projection.Descriptor!;
        return new(
            projection.LauncherVersion,
            descriptor.SourceCommit,
            launcherSize,
            launcherSha256,
            projection.DescriptorSize,
            projection.DescriptorSha256,
            new(
                descriptor.Bootstrap.InstalledFileName,
                descriptor.Bootstrap.Size,
                descriptor.Bootstrap.Sha256));
    }

    private static ManagedDistributionPayloadIssue MapMeasurementIssue(
        ManagedExecutableLaunchIssue issue)
    {
        return issue switch
        {
            ManagedExecutableLaunchIssue.Tampered or ManagedExecutableLaunchIssue.UnsafePath =>
                ManagedDistributionPayloadIssue.Invalid,
            ManagedExecutableLaunchIssue.Unavailable => ManagedDistributionPayloadIssue.Unavailable,
            ManagedExecutableLaunchIssue.None => throw new InvalidOperationException(
                "A successful Launcher measurement returned no identity."),
            _ => throw new InvalidOperationException(
                "Launcher measurement returned an undefined issue."),
        };
    }

    private sealed record DescriptorProjectionResult(
        ManagedSetupPayloadAdmissionDescriptorDocument? Descriptor,
        ManagedAppVersion LauncherVersion,
        long DescriptorSize,
        string DescriptorSha256,
        ManagedDistributionPayloadIssue Issue)
    {
        internal bool IsSuccess => Descriptor is not null && Issue == ManagedDistributionPayloadIssue.None;

        internal static DescriptorProjectionResult Failure(ManagedDistributionPayloadIssue issue)
        {
            return new(null, default, 0, string.Empty, issue);
        }
    }

    private sealed class EmbeddedPayloadCapture(
        ManagedDistributionPayloadIdentity identity,
        StableManagedExecutableLaunchLease launcher,
        byte[] bootstrapBytes)
        : IManagedDistributionPayloadCapture, IManagedDistributionPayloadContent
    {
        public ManagedDistributionPayloadIdentity Identity { get; } = identity;

        public ValueTask CopyDistributionLauncherAsync(
            string destination,
            CancellationToken cancellationToken)
        {
            return launcher.CopyToAsync(destination, cancellationToken);
        }

        public async ValueTask CopyBootstrapAsync(
            string destination,
            CancellationToken cancellationToken)
        {
            await using var output = new FileStream(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            await output.WriteAsync(bootstrapBytes, cancellationToken).ConfigureAwait(false);
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Flush(flushToDisk: true);
        }

        public void Dispose()
        {
            launcher.Dispose();
        }
    }
}

/// <summary>Performs one bounded, local-only observation of a managed installation root.</summary>
public sealed class FileSystemManagedInstallationRootProbe : IManagedInstallationRootProbe
{
    internal const string TransactionMarkerSuffix = ".managed-setup-transaction.v1.json";
    internal const string StagingContainerSuffix = ".managed-setup-staging";
    private readonly Func<string, ManagedInstallationRootObservation> _observe;

    /// <summary>Creates the production bounded local filesystem probe.</summary>
    public FileSystemManagedInstallationRootProbe()
        : this(ObserveCore)
    {
    }

    internal FileSystemManagedInstallationRootProbe(
        Func<string, ManagedInstallationRootObservation> observe)
    {
        _observe = observe ?? throw new ArgumentNullException(nameof(observe));
    }

    /// <inheritdoc />
    public async ValueTask<ManagedInstallationRootObservation> ObserveAsync(
        string managedRoot,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Task<ManagedInstallationRootObservation> observation = Task.Factory.StartNew(
            () => _observe(managedRoot),
            CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskScheduler.Default);
        return await observation.WaitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static ManagedInstallationRootObservation ObserveCore(string managedRoot)
    {
        ManagedInstallationRootStatus admission = AdmitRoot(managedRoot, out string root);
        if (admission != ManagedInstallationRootStatus.Absent)
        {
            return new(admission);
        }

        try
        {
            FileSystemEntryState marker = ObserveExactEntry(GetTransactionMarkerPath(root));
            FileSystemEntryState staging = ObserveExactEntry(GetStagingContainerPath(root));
            if (marker != FileSystemEntryState.Absent || staging != FileSystemEntryState.Absent)
            {
                return new(ManagedInstallationRootStatus.Residue);
            }

            FileSystemEntryState destination = ObserveExactEntry(root);
            return new(destination switch
            {
                FileSystemEntryState.Absent => ManagedInstallationRootStatus.Absent,
                FileSystemEntryState.Directory => ManagedInstallationRootStatus.Present,
                FileSystemEntryState.File or FileSystemEntryState.Unsafe =>
                    ManagedInstallationRootStatus.InvalidDestination,
                _ => throw new InvalidOperationException("Undefined filesystem entry state."),
            });
        }
        catch (UnauthorizedAccessException)
        {
            return new(ManagedInstallationRootStatus.PermissionDenied);
        }
        catch (Exception exception) when (exception is IOException or NotSupportedException)
        {
            return new(ManagedInstallationRootStatus.Unavailable);
        }
    }

    internal static string GetTransactionMarkerPath(string managedRoot)
    {
        return NormalizeRoot(managedRoot) + TransactionMarkerSuffix;
    }

    internal static string GetStagingContainerPath(string managedRoot)
    {
        return NormalizeRoot(managedRoot) + StagingContainerSuffix;
    }

    internal static ManagedInstallationRootStatus AdmitRoot(
        string? value,
        out string normalized)
    {
        normalized = string.Empty;
        if (!ManagedPathSafety.TryNormalizeExactAbsolutePath(value, out string root))
        {
            return ManagedInstallationRootStatus.InvalidDestination;
        }

        try
        {
            string? driveRoot = Path.GetPathRoot(root);
            if (string.IsNullOrWhiteSpace(driveRoot) ||
                ManagedPathSafety.PathComparer.Equals(NormalizeRoot(driveRoot), NormalizeRoot(root)) ||
                new DriveInfo(driveRoot).DriveType != DriveType.Fixed)
            {
                return ManagedInstallationRootStatus.InvalidDestination;
            }

            string? existing = root;
            while (existing is not null && !EntryExists(existing))
            {
                existing = Path.GetDirectoryName(existing);
            }
            if (existing is null ||
                (File.GetAttributes(existing) & FileAttributes.Directory) == 0 ||
                ManagedPathSafety.HasReparseComponent(existing))
            {
                return ManagedInstallationRootStatus.InvalidDestination;
            }

            normalized = NormalizeRoot(root);
            return ManagedInstallationRootStatus.Absent;
        }
        catch (UnauthorizedAccessException)
        {
            return ManagedInstallationRootStatus.PermissionDenied;
        }
        catch (IOException)
        {
            return ManagedInstallationRootStatus.Unavailable;
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return ManagedInstallationRootStatus.InvalidDestination;
        }
    }

    private static bool EntryExists(string path)
    {
        try
        {
            _ = File.GetAttributes(path);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
        catch (DirectoryNotFoundException)
        {
            return false;
        }
    }

    private static FileSystemEntryState ObserveExactEntry(string path)
    {
        try
        {
            FileAttributes attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.ReparsePoint) != 0
                ? FileSystemEntryState.Unsafe
                : (attributes & FileAttributes.Directory) != 0
                    ? FileSystemEntryState.Directory
                    : FileSystemEntryState.File;
        }
        catch (FileNotFoundException)
        {
            return FileSystemEntryState.Absent;
        }
        catch (DirectoryNotFoundException)
        {
            return FileSystemEntryState.Absent;
        }
    }

    private static string NormalizeRoot(string managedRoot)
    {
        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(managedRoot));
    }

    private enum FileSystemEntryState
    {
        Absent,
        File,
        Directory,
        Unsafe,
    }
}
