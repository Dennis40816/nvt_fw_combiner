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
    private readonly Func<Stream?> _openBootstrap;
    private readonly Func<Stream?> _openDescriptor;
    private readonly string _launcherPath;

    /// <summary>Creates a source over exact, independently reopenable embedded resources.</summary>
    public EmbeddedManagedDistributionPayloadSource(
        string launcherPath,
        Func<Stream?> openDescriptor,
        Func<Stream?> openBootstrap)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(launcherPath);
        _launcherPath = Path.GetFullPath(launcherPath);
        _openDescriptor = openDescriptor ?? throw new ArgumentNullException(nameof(openDescriptor));
        _openBootstrap = openBootstrap ?? throw new ArgumentNullException(nameof(openBootstrap));
    }

    /// <summary>Creates a compatibility source over already captured test resources.</summary>
    internal EmbeddedManagedDistributionPayloadSource(
        string launcherPath,
        ReadOnlyMemory<byte>? descriptorBytes,
        ReadOnlyMemory<byte>? bootstrapBytes)
        : this(
            launcherPath,
            CreateMemoryFactory(descriptorBytes),
            CreateMemoryFactory(bootstrapBytes))
    {
    }

    /// <inheritdoc />
    public async ValueTask<ManagedDistributionPayloadEntryAdmissionResult> AdmitEntryAsync(
        CancellationToken cancellationToken)
    {
        EntryProjectionResult admitted = await AdmitEntryCoreAsync(cancellationToken)
            .ConfigureAwait(false);
        return admitted.IsSuccess
            ? new(
                admitted.Descriptor!.LauncherVersion,
                admitted.Bootstrap,
                ManagedDistributionPayloadIssue.None)
            : new(default, null, admitted.Issue);
    }

    /// <inheritdoc />
    public async ValueTask<ManagedDistributionPayloadInspectionResult> InspectAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EntryProjectionResult admitted = await AdmitEntryCoreAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!admitted.IsSuccess)
        {
            return new(null, admitted.Issue);
        }
        BootstrapReadResult bootstrap = await ReadBootstrapExactAsync(
            admitted.Bootstrap!,
            captureBytes: false,
            cancellationToken).ConfigureAwait(false);
        if (!bootstrap.IsSuccess)
        {
            return new(null, bootstrap.Issue);
        }

        StableManagedExecutableMeasurementResult launcher = await StableManagedExecutableLaunchLease
            .TryMeasureAsync(_launcherPath, cancellationToken).ConfigureAwait(false);
        return launcher.IsMeasured
            ? new(
                CreateIdentity(admitted.Descriptor!, launcher.Length, launcher.Sha256),
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
        EntryProjectionResult admitted = await AdmitEntryCoreAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!admitted.IsSuccess)
        {
            return new(
                null,
                admitted.Issue == ManagedDistributionPayloadIssue.Invalid
                    ? ManagedDistributionPayloadIssue.Changed
                    : admitted.Issue);
        }
        ManagedDistributionPayloadIdentity embedded = CreateIdentity(
            admitted.Descriptor!,
            expected.LauncherSize,
            expected.LauncherSha256);
        if (embedded != expected)
        {
            return new(null, ManagedDistributionPayloadIssue.Changed);
        }

        BootstrapReadResult bootstrap = await ReadBootstrapExactAsync(
            admitted.Bootstrap!,
            captureBytes: true,
            cancellationToken).ConfigureAwait(false);
        if (!bootstrap.IsSuccess)
        {
            return new(
                null,
                bootstrap.Issue == ManagedDistributionPayloadIssue.Invalid
                    ? ManagedDistributionPayloadIssue.Changed
                    : bootstrap.Issue);
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
            new EmbeddedPayloadCapture(expected, stable, bootstrap.Bytes!),
            ManagedDistributionPayloadIssue.None);
    }

    private async ValueTask<EntryProjectionResult> AdmitEntryCoreAsync(
        CancellationToken cancellationToken)
    {
        DescriptorProjectionResult projection = await ProjectDescriptorAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!projection.IsSuccess)
        {
            return EntryProjectionResult.Failure(projection.Issue);
        }
        ManagedSetupEmbeddedBootstrapDocument bootstrap = projection.Descriptor!.Bootstrap;
        ManagedImmutableBootstrapIdentity identity;
        try
        {
            identity = new(
                bootstrap.InstalledFileName,
                bootstrap.Size,
                bootstrap.Sha256);
        }
        catch (ArgumentException)
        {
            return EntryProjectionResult.Failure(ManagedDistributionPayloadIssue.Invalid);
        }

        ResourceLengthResult resource = ObserveBootstrapLength();
        return resource.Issue == ManagedDistributionPayloadIssue.None &&
            resource.Length == identity.Length
                ? new(projection, identity, ManagedDistributionPayloadIssue.None)
                : EntryProjectionResult.Failure(
                    resource.Issue == ManagedDistributionPayloadIssue.None
                        ? ManagedDistributionPayloadIssue.Invalid
                        : resource.Issue);
    }

    private async ValueTask<DescriptorProjectionResult> ProjectDescriptorAsync(
        CancellationToken cancellationToken)
    {
        Stream? descriptorStream;
        try
        {
            descriptorStream = _openDescriptor();
        }
        catch (Exception exception) when (IsResourceUnavailable(exception))
        {
            return DescriptorProjectionResult.Failure(ManagedDistributionPayloadIssue.Unavailable);
        }
        if (descriptorStream is null)
        {
            return DescriptorProjectionResult.Failure(ManagedDistributionPayloadIssue.Unavailable);
        }

        await using (descriptorStream)
        {
            try
            {
                if (!descriptorStream.CanRead)
                {
                    return DescriptorProjectionResult.Failure(ManagedDistributionPayloadIssue.Invalid);
                }
                var buffer = new byte[MaximumDescriptorBytes + 1];
                int count = 0;
                while (count < buffer.Length)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int read = await descriptorStream.ReadAsync(
                        buffer.AsMemory(count, buffer.Length - count),
                        cancellationToken).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }
                    count += read;
                }
                if (count is 0 or > MaximumDescriptorBytes)
                {
                    return DescriptorProjectionResult.Failure(ManagedDistributionPayloadIssue.Invalid);
                }
                byte[] descriptorBytes = buffer.AsSpan(0, count).ToArray();
                using JsonDocument json = EmbeddedVersionManagementSchema.ParseStrict(
                    descriptorBytes,
                    maximumDepth: 12);
                if (!ManagedSetupPayloadAdmissionSchema.IsValid(json.RootElement))
                {
                    return DescriptorProjectionResult.Failure(ManagedDistributionPayloadIssue.Invalid);
                }
                ManagedSetupPayloadAdmissionDescriptorDocument? document = JsonSerializer.Deserialize(
                    json.RootElement,
                    PayloadJsonContext.ManagedSetupPayloadAdmissionDescriptorDocument);
                return document is not null &&
                    ManagedAppVersion.TryParse(
                        document.LauncherVersion,
                        out ManagedAppVersion version) &&
                    string.Equals(
                        document.SourceCommit,
                        document.Bootstrap.SourceCommit,
                        StringComparison.Ordinal)
                        ? new(
                            document,
                            version,
                            descriptorBytes.LongLength,
                            Convert.ToHexStringLower(SHA256.HashData(descriptorBytes)),
                            ManagedDistributionPayloadIssue.None)
                        : DescriptorProjectionResult.Failure(
                            ManagedDistributionPayloadIssue.Invalid);
            }
            catch (Exception exception) when (IsResourceUnavailable(exception))
            {
                return DescriptorProjectionResult.Failure(ManagedDistributionPayloadIssue.Unavailable);
            }
            catch (Exception exception) when (exception is
                JsonException or ArgumentException or InvalidOperationException)
            {
                return DescriptorProjectionResult.Failure(ManagedDistributionPayloadIssue.Invalid);
            }
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

    private ResourceLengthResult ObserveBootstrapLength()
    {
        Stream? bootstrap;
        try
        {
            bootstrap = _openBootstrap();
        }
        catch (Exception exception) when (IsResourceUnavailable(exception))
        {
            return new(0, ManagedDistributionPayloadIssue.Unavailable);
        }
        if (bootstrap is null)
        {
            return new(0, ManagedDistributionPayloadIssue.Unavailable);
        }

        using (bootstrap)
        {
            try
            {
                return bootstrap.CanRead
                    ? new(bootstrap.Length, ManagedDistributionPayloadIssue.None)
                    : new(0, ManagedDistributionPayloadIssue.Invalid);
            }
            catch (Exception exception) when (IsResourceUnavailable(exception))
            {
                return new(0, ManagedDistributionPayloadIssue.Unavailable);
            }
        }
    }

    private async ValueTask<BootstrapReadResult> ReadBootstrapExactAsync(
        ManagedImmutableBootstrapIdentity expected,
        bool captureBytes,
        CancellationToken cancellationToken)
    {
        Stream? bootstrap;
        try
        {
            bootstrap = _openBootstrap();
        }
        catch (Exception exception) when (IsResourceUnavailable(exception))
        {
            return BootstrapReadResult.Failure(ManagedDistributionPayloadIssue.Unavailable);
        }
        if (bootstrap is null)
        {
            return BootstrapReadResult.Failure(ManagedDistributionPayloadIssue.Unavailable);
        }

        await using (bootstrap)
        {
            try
            {
                if (!bootstrap.CanRead || bootstrap.Length != expected.Length)
                {
                    return BootstrapReadResult.Failure(ManagedDistributionPayloadIssue.Invalid);
                }

                byte[]? captured = captureBytes ? new byte[checked((int)expected.Length)] : null;
                byte[] readBuffer = captureBytes ? captured! : new byte[64 * 1024];
                using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long remaining = expected.Length;
                int capturedOffset = 0;
                while (remaining > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    int requested = (int)Math.Min(remaining, readBuffer.Length);
                    Memory<byte> destination = captureBytes
                        ? readBuffer.AsMemory(capturedOffset, requested)
                        : readBuffer.AsMemory(0, requested);
                    int read = await bootstrap.ReadAsync(destination, cancellationToken)
                        .ConfigureAwait(false);
                    if (read == 0)
                    {
                        return BootstrapReadResult.Failure(ManagedDistributionPayloadIssue.Invalid);
                    }
                    hash.AppendData(destination.Span[..read]);
                    remaining -= read;
                    capturedOffset += read;
                }

                var probe = new byte[1];
                if (await bootstrap.ReadAsync(probe, cancellationToken).ConfigureAwait(false) != 0)
                {
                    return BootstrapReadResult.Failure(ManagedDistributionPayloadIssue.Invalid);
                }
                string sha256 = Convert.ToHexStringLower(hash.GetHashAndReset());
                return string.Equals(sha256, expected.Sha256, StringComparison.Ordinal)
                    ? new(captured, ManagedDistributionPayloadIssue.None)
                    : BootstrapReadResult.Failure(ManagedDistributionPayloadIssue.Invalid);
            }
            catch (Exception exception) when (IsResourceUnavailable(exception))
            {
                return BootstrapReadResult.Failure(ManagedDistributionPayloadIssue.Unavailable);
            }
        }
    }

    private static Func<Stream?> CreateMemoryFactory(ReadOnlyMemory<byte>? bytes)
    {
        byte[]? captured = bytes?.ToArray();
        return () => captured is null ? null : new MemoryStream(captured, writable: false);
    }

    private static bool IsResourceUnavailable(Exception exception)
    {
        return exception is IOException or UnauthorizedAccessException or NotSupportedException or
            ObjectDisposedException;
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

    private sealed record EntryProjectionResult(
        DescriptorProjectionResult? Descriptor,
        ManagedImmutableBootstrapIdentity? Bootstrap,
        ManagedDistributionPayloadIssue Issue)
    {
        internal bool IsSuccess =>
            Descriptor?.IsSuccess == true && Bootstrap is not null &&
            Issue == ManagedDistributionPayloadIssue.None;

        internal static EntryProjectionResult Failure(ManagedDistributionPayloadIssue issue)
        {
            return new(null, null, issue);
        }
    }

    private readonly record struct ResourceLengthResult(
        long Length,
        ManagedDistributionPayloadIssue Issue);

    private sealed record BootstrapReadResult(
        byte[]? Bytes,
        ManagedDistributionPayloadIssue Issue)
    {
        internal bool IsSuccess => Issue == ManagedDistributionPayloadIssue.None;

        internal static BootstrapReadResult Failure(ManagedDistributionPayloadIssue issue)
        {
            return new(null, issue);
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
