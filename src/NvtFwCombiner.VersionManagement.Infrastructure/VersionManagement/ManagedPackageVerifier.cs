using System.Diagnostics.CodeAnalysis;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

internal sealed record ManagedPackagePlan(
    ReleaseManifestDocument Manifest,
    IReadOnlyDictionary<string, ZipArchiveEntry> Entries,
    byte[] ManifestBytes,
    byte[] ChecksumBytes,
    int FileCount,
    int ImplicitDirectoryCount,
    long ExpandedBytes)
{
    internal bool HasSupportedManagedLauncher =>
        string.Equals(Manifest.SchemaVersion, "1.2", StringComparison.Ordinal);
}

internal sealed record ManagedPackagePlanResult(
    ManagedPackagePlan? Plan,
    ManagedVersionInstallIssue Issue)
{
    internal bool IsSuccess => Plan is not null && Issue == ManagedVersionInstallIssue.None;
}

internal static class ManagedPackageVerifier
{
    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 32,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private static readonly VersionManagementJsonContext ManifestJsonContext = new(ManifestJsonOptions);

    internal static bool TryReadCanonicalManifest(
        byte[] manifestBytes,
        ManagedAppVersion version,
        IEnumerable<string>? archivePaths,
        [NotNullWhen(true)] out ReleaseManifestDocument? manifest)
    {
        manifest = null;
        ReleaseManifestDocument? parsed;
        try
        {
            using JsonDocument manifestJson = EmbeddedVersionManagementSchema.ParseStrict(
                manifestBytes,
                maximumDepth: 32);
            if (!ReleaseManifestSchema.IsValid(manifestJson.RootElement))
            {
                return false;
            }
            parsed = JsonSerializer.Deserialize(
                manifestBytes,
                ManifestJsonContext.ReleaseManifestDocument);
        }
        catch (JsonException)
        {
            return false;
        }
        if (parsed?.Files is null)
        {
            return false;
        }
        IEnumerable<string> expectedPaths = archivePaths ?? parsed.Files
            .Select(static file => file.Path)
            .Append("RELEASE-MANIFEST.json")
            .Append("SHA256SUMS.txt");
        if (!ValidateManifest(parsed, version, expectedPaths))
        {
            return false;
        }
        manifest = parsed;
        return true;
    }

    internal static async ValueTask<ManagedPackagePlanResult> CreatePlanAsync(
        ZipArchive archive,
        UpdateCatalogVersionSnapshot package,
        long maximumExpandedBytes,
        CancellationToken cancellationToken)
    {
        if (archive.Entries.Count is 0 or > FileSystemManagedVersionRepository.MaximumArchiveEntries)
        {
            return Failure(ManagedVersionInstallIssue.UnsafeArchive);
        }

        string packageRoot = $"NvtFwCombiner-v{package.Version}-win-x64";
        string prefix = packageRoot + "/";
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
        var directories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long expandedBytes = 0;
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (IsLink(entry) ||
                !entry.FullName.StartsWith(prefix, StringComparison.Ordinal) ||
                entry.FullName.Contains('\\', StringComparison.Ordinal))
            {
                return Failure(ManagedVersionInstallIssue.UnsafeArchive);
            }

            string relativePath = entry.FullName[prefix.Length..];
            bool isDirectory = entry.FullName.EndsWith('/');
            if (isDirectory)
            {
                if (entry.Length != 0 ||
                    (relativePath.Length > 0 && !ManagedPathSafety.IsSafeRelativePayloadPath(relativePath.TrimEnd('/'))))
                {
                    return Failure(ManagedVersionInstallIssue.UnsafeArchive);
                }
                continue;
            }
            if (!ManagedPathSafety.IsSafeRelativePayloadPath(relativePath) || !entries.TryAdd(relativePath, entry))
            {
                return Failure(ManagedVersionInstallIssue.UnsafeArchive);
            }

            string[] components = relativePath.Split('/');
            for (int count = 1; count < components.Length; count++)
            {
                _ = directories.Add(string.Join('/', components.AsSpan(0, count).ToArray()));
                if (directories.Count > FileSystemManagedVersionRepository.MaximumInstalledDirectories)
                {
                    return Failure(ManagedVersionInstallIssue.UnsafeArchive);
                }
            }

            if (entry.Length < 0 || entry.Length > maximumExpandedBytes - expandedBytes)
            {
                return Failure(ManagedVersionInstallIssue.UnsafeArchive);
            }
            expandedBytes += entry.Length;
        }

        if (!entries.TryGetValue("RELEASE-MANIFEST.json", out ZipArchiveEntry? manifestEntry) ||
            manifestEntry.Length is < 1 or > FileSystemManagedVersionRepository.MaximumManifestBytes)
        {
            return Failure(ManagedVersionInstallIssue.InvalidPayload);
        }

        var verificationBudget = new ExpandedByteBudget(maximumExpandedBytes);
        using var manifestBuffer = new MemoryStream(
            capacity: checked((int)Math.Min(
                manifestEntry.Length,
                FileSystemManagedVersionRepository.MaximumManifestBytes)));
        BoundedArchiveReadResult manifestRead;
        await using (Stream input = manifestEntry.Open())
        {
            manifestRead = await BoundedArchiveReader.ReadAtMostAndHashAsync(
                input,
                FileSystemManagedVersionRepository.MaximumManifestBytes,
                verificationBudget,
                manifestBuffer,
                cancellationToken).ConfigureAwait(false);
        }
        if (!manifestRead.IsSuccess)
        {
            return Failure(manifestRead.Issue == BoundedArchiveReadIssue.AggregateLengthExceeded
                ? ManagedVersionInstallIssue.UnsafeArchive
                : ManagedVersionInstallIssue.InvalidPayload);
        }
        byte[] manifestBytes = manifestBuffer.ToArray();
        if (!string.Equals(
                manifestRead.Sha256,
                package.ReleaseManifestSha256,
                StringComparison.Ordinal))
        {
            return Failure(ManagedVersionInstallIssue.InvalidPayload);
        }

        if (!TryReadCanonicalManifest(
                manifestBytes,
                package.Version,
                entries.Keys,
                out ReleaseManifestDocument? manifest))
        {
            return Failure(ManagedVersionInstallIssue.InvalidPayload);
        }

        if (!entries.TryGetValue("SHA256SUMS.txt", out ZipArchiveEntry? checksumEntry) ||
            checksumEntry.Length is < 1 or > FileSystemManagedVersionRepository.MaximumManifestBytes)
        {
            return Failure(ManagedVersionInstallIssue.InvalidPayload);
        }
        using var checksumBuffer = new MemoryStream(
            capacity: checked((int)checksumEntry.Length));
        BoundedArchiveReadResult checksumRead;
        await using (Stream input = checksumEntry.Open())
        {
            checksumRead = await BoundedArchiveReader.ReadAtMostAndHashAsync(
                input,
                FileSystemManagedVersionRepository.MaximumManifestBytes,
                verificationBudget,
                checksumBuffer,
                cancellationToken).ConfigureAwait(false);
        }
        if (!checksumRead.IsSuccess)
        {
            return Failure(checksumRead.Issue == BoundedArchiveReadIssue.AggregateLengthExceeded
                ? ManagedVersionInstallIssue.UnsafeArchive
                : ManagedVersionInstallIssue.InvalidPayload);
        }
        byte[] checksumBytes = checksumBuffer.ToArray();
        if (!VerifyChecksumDocument(checksumBytes, manifestBytes, manifest.Files!))
        {
            return Failure(ManagedVersionInstallIssue.InvalidPayload);
        }

        ArchiveContentVerification content = await VerifyArchiveContentAsync(
            manifest,
            entries,
            verificationBudget,
            cancellationToken).ConfigureAwait(false);
        return content switch
        {
            ArchiveContentVerification.Valid =>
                new(new(
                    manifest,
                    entries,
                    manifestBytes,
                    checksumBytes,
                    entries.Count,
                    directories.Count,
                    expandedBytes), ManagedVersionInstallIssue.None),
            ArchiveContentVerification.Invalid => Failure(ManagedVersionInstallIssue.InvalidPayload),
            ArchiveContentVerification.Unsafe => Failure(ManagedVersionInstallIssue.UnsafeArchive),
            _ => throw new InvalidOperationException("Unknown archive verification result."),
        };
    }

    internal static async ValueTask ExtractAsync(
        ManagedPackagePlan plan,
        Func<string, FileStream> createDestination,
        long maximumExpandedBytes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(createDestination);
        var extractionBudget = new ExpandedByteBudget(maximumExpandedBytes);
        var expectedFiles =
            plan.Manifest.Files!.ToDictionary(file => file.Path, StringComparer.OrdinalIgnoreCase);
        foreach ((string relativePath, ZipArchiveEntry entry) in plan.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            long expectedLength;
            string expectedHash;
            if (relativePath.Equals("RELEASE-MANIFEST.json", StringComparison.OrdinalIgnoreCase))
            {
                expectedLength = plan.ManifestBytes.LongLength;
                expectedHash = Convert.ToHexStringLower(SHA256.HashData(plan.ManifestBytes));
            }
            else if (relativePath.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
            {
                expectedLength = plan.ChecksumBytes.LongLength;
                expectedHash = Convert.ToHexStringLower(SHA256.HashData(plan.ChecksumBytes));
            }
            else if (expectedFiles.TryGetValue(relativePath, out ReleaseManifestFileDocument? expected))
            {
                expectedLength = expected.Size;
                expectedHash = expected.Sha256;
            }
            else
            {
                throw new InvalidDataException("Archive entry is absent from the admitted manifest.");
            }

            await using Stream source = entry.Open();
            FileStream? destination = null;
            try
            {
#pragma warning disable CA2000 // Ownership is transferred to the explicit async-dispose finally below.
                destination = createDestination(relativePath);
#pragma warning restore CA2000
                BoundedArchiveReadResult extracted = await BoundedArchiveReader.CopyAndHashAsync(
                    source,
                    expectedLength,
                    extractionBudget,
                    destination,
                    cancellationToken).ConfigureAwait(false);
                if (!extracted.IsSuccess ||
                    !string.Equals(extracted.Sha256, expectedHash, StringComparison.Ordinal))
                {
                    throw new InvalidDataException("Archive content changed after admission.");
                }
            }
            finally
            {
                if (destination is not null)
                {
                    await destination.DisposeAsync().ConfigureAwait(false);
                }
            }
        }
    }

    internal static async ValueTask<ManagedVersionDamageReason?> VerifyInstalledAsync(
        string versionRoot,
        ManagedVersionAdmission admission,
        long maximumExpandedBytes,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ManagedPathSafety.IsSafeOwnedTree(versionRoot))
            {
                return Directory.Exists(versionRoot)
                    ? ManagedVersionDamageReason.UnexpectedPath
                    : ManagedVersionDamageReason.MissingFile;
            }

            ManagedVersionAdmission? storedAdmission =
                await FileSystemManagedVersionRepository.ReadAdmissionAsync(
                    versionRoot,
                    cancellationToken).ConfigureAwait(false);
            if (storedAdmission != admission)
            {
                return ManagedVersionDamageReason.ManifestMismatch;
            }

            string manifestPath = Path.Combine(versionRoot, "RELEASE-MANIFEST.json");
            byte[]? manifestBytes = await ManagedPathSafety.ReadBoundedFileAsync(
                manifestPath,
                FileSystemManagedVersionRepository.MaximumManifestBytes,
                cancellationToken).ConfigureAwait(false);
            if (manifestBytes is null)
            {
                return File.Exists(manifestPath)
                    ? ManagedVersionDamageReason.ManifestMismatch
                    : ManagedVersionDamageReason.MissingFile;
            }
            if (!string.Equals(
                    Convert.ToHexStringLower(SHA256.HashData(manifestBytes)),
                    admission.ReleaseManifestSha256,
                    StringComparison.Ordinal))
            {
                return ManagedVersionDamageReason.ManifestMismatch;
            }

            if (!TryReadCanonicalManifest(
                    manifestBytes,
                    admission.Version,
                    archivePaths: null,
                    out ReleaseManifestDocument? manifest))
            {
                return ManagedVersionDamageReason.ManifestMismatch;
            }
            string checksumPath = Path.Combine(versionRoot, "SHA256SUMS.txt");
            byte[]? checksumBytes = await ManagedPathSafety.ReadBoundedFileAsync(
                checksumPath,
                FileSystemManagedVersionRepository.MaximumManifestBytes,
                cancellationToken).ConfigureAwait(false);
            if (checksumBytes is null ||
                !VerifyChecksumDocument(checksumBytes, manifestBytes, manifest.Files!))
            {
                return ManagedVersionDamageReason.ManifestMismatch;
            }

            var installedBudget = new ExpandedByteBudget(maximumExpandedBytes);
            if (!installedBudget.Consume(manifestBytes.Length) ||
                !installedBudget.Consume(checksumBytes.Length))
            {
                return ManagedVersionDamageReason.ContentMismatch;
            }

            HashSet<string> expected = new(StringComparer.OrdinalIgnoreCase)
            {
                "RELEASE-MANIFEST.json",
                "SHA256SUMS.txt",
                FileSystemManagedVersionRepository.AdmissionFileName,
            };
            foreach (ReleaseManifestFileDocument file in manifest.Files!)
            {
                _ = expected.Add(file.Path);
                string path = ManagedPathSafety.ResolvePayloadPath(versionRoot, file.Path);
                if (!File.Exists(path) || ManagedPathSafety.IsReparsePoint(path))
                {
                    return ManagedVersionDamageReason.MissingFile;
                }
                var info = new FileInfo(path);
                if (info.Length != file.Size)
                {
                    return ManagedVersionDamageReason.ContentMismatch;
                }
                BoundedArchiveReadResult read = await BoundedArchiveReader.ReadFileAndHashAsync(
                    path,
                    file.Size,
                    installedBudget,
                    cancellationToken).ConfigureAwait(false);
                if (!read.IsSuccess ||
                    !string.Equals(read.Sha256, file.Sha256, StringComparison.Ordinal))
                {
                    return ManagedVersionDamageReason.ContentMismatch;
                }
            }

            string[] actual = [.. Directory.EnumerateFiles(versionRoot, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(versionRoot, path).Replace('\\', '/'))];
            return actual.Length == expected.Count && actual.All(expected.Contains)
                ? null
                : ManagedVersionDamageReason.UnexpectedPath;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            return ManagedVersionDamageReason.Unreadable;
        }
    }

    private static bool ValidateManifest(
        ReleaseManifestDocument manifest,
        ManagedAppVersion version,
        IEnumerable<string> archivePaths)
    {
        if (manifest.SchemaVersion is not ("1.1" or "1.2") ||
            !string.Equals(manifest.Product, "NVT FW Combiner", StringComparison.Ordinal) ||
            !string.Equals(manifest.Version, version.ToString(), StringComparison.Ordinal) ||
            !string.Equals(manifest.SourceTag, $"v{version}", StringComparison.Ordinal) ||
            !string.Equals(manifest.RuntimeIdentifier, "win-x64", StringComparison.Ordinal) ||
            !string.Equals(manifest.LicenseSpdx, "MIT", StringComparison.Ordinal) ||
            !IsLowerHex(manifest.SourceCommit, 40) ||
            !IsLowerHex(manifest.ProcessorBundleSha256, 64) ||
            !IsLowerHex(manifest.EmbeddedProfileCatalogSha256, 64) ||
            !IsLowerHex(manifest.EmbeddedSchemaBundleSha256, 64) ||
            manifest.WorkerProtocolVersions is null ||
            manifest.WorkerProtocolVersions.Count == 0 ||
            manifest.Files is null ||
            !IsSafeFileName(manifest.SbomAsset) ||
            !IsSafeFileName(manifest.ProvenanceAsset))
        {
            return false;
        }

        if (!ValidateLauncherContract(manifest))
        {
            return false;
        }

        var declared = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ReleaseManifestFileDocument file in manifest.Files)
        {
            if (!ManagedPathSafety.IsSafeRelativePayloadPath(file.Path) ||
                file.Path.Equals(FileSystemManagedVersionRepository.AdmissionFileName, StringComparison.OrdinalIgnoreCase) ||
                file.Size <= 0 ||
                !IsLowerHex(file.Sha256, 64) ||
                !declared.Add(file.Path))
            {
                return false;
            }
        }
        var expectedArchive = new HashSet<string>(declared, StringComparer.OrdinalIgnoreCase)
        {
            "RELEASE-MANIFEST.json",
            "SHA256SUMS.txt",
        };
        return expectedArchive.SetEquals(archivePaths);
    }

    private static bool ValidateLauncherContract(ReleaseManifestDocument manifest)
    {
        if (string.Equals(manifest.SchemaVersion, "1.1", StringComparison.Ordinal))
        {
            return manifest.VersionManagementProtocolVersion is null && manifest.Launcher is null;
        }

        LauncherReleaseIdentityDocument? launcher = manifest.Launcher;
        if (manifest.VersionManagementProtocolVersion != ManagedLauncherIdentity.SupportedProtocolVersion ||
            launcher?.ProtocolVersion != ManagedLauncherIdentity.SupportedProtocolVersion ||
            !ManagedAppVersion.TryParse(launcher.LauncherVersion, out _) ||
            !string.Equals(
                launcher.ExecutableRelativePath,
                ManagedLauncherIdentity.ExecutablePath,
                StringComparison.Ordinal) ||
            launcher.Size is null or <= 0 or > ManagedLauncherIdentity.MaximumExecutableBytes ||
            !IsLowerHex(launcher.Sha256, 64))
        {
            return false;
        }

        ReleaseManifestFileDocument[] launcherFiles =
        [.. manifest.Files!.Where(file => string.Equals(file.Role, "launcher", StringComparison.Ordinal))];
        return launcherFiles is
        [
        { Path: ManagedLauncherIdentity.ExecutablePath } file,
        ] &&
        file.Size == launcher.Size &&
        string.Equals(file.Sha256, launcher.Sha256, StringComparison.Ordinal);
    }

    private static async ValueTask<ArchiveContentVerification> VerifyArchiveContentAsync(
        ReleaseManifestDocument manifest,
        Dictionary<string, ZipArchiveEntry> entries,
        ExpandedByteBudget verificationBudget,
        CancellationToken cancellationToken)
    {
        foreach (ReleaseManifestFileDocument file in manifest.Files!)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entries.TryGetValue(file.Path, out ZipArchiveEntry? entry) || entry.Length != file.Size)
            {
                return ArchiveContentVerification.Invalid;
            }
            await using Stream stream = entry.Open();
            BoundedArchiveReadResult read = await BoundedArchiveReader.ReadAndHashAsync(
                stream,
                file.Size,
                verificationBudget,
                cancellationToken).ConfigureAwait(false);
            if (read.Issue == BoundedArchiveReadIssue.AggregateLengthExceeded)
            {
                return ArchiveContentVerification.Unsafe;
            }
            if (!read.IsSuccess || !string.Equals(read.Sha256, file.Sha256, StringComparison.Ordinal))
            {
                return ArchiveContentVerification.Invalid;
            }
        }
        return ArchiveContentVerification.Valid;
    }

    internal static bool VerifyChecksumDocument(
        byte[] checksumBytes,
        byte[] manifestBytes,
        IReadOnlyList<ReleaseManifestFileDocument> files)
    {
        string text;
        try
        {
            text = new UTF8Encoding(
                encoderShouldEmitUTF8Identifier: false,
                throwOnInvalidBytes: true).GetString(checksumBytes);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
        if (text.Contains('\r') && !text.Contains("\r\n", StringComparison.Ordinal))
        {
            return false;
        }

        var expected = files.ToDictionary(file => file.Path, file => file.Sha256, StringComparer.Ordinal);
        expected.Add(
            "RELEASE-MANIFEST.json",
            Convert.ToHexStringLower(SHA256.HashData(manifestBytes)));
        var actual = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in text.Replace("\r\n", "\n", StringComparison.Ordinal)
                     .Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            if (line.Length < 67 || line[64] != ' ' || line[65] != ' ')
            {
                return false;
            }
            string hash = line[..64];
            string path = line[66..];
            if (!IsLowerHex(hash, 64) ||
                !ManagedPathSafety.IsSafeRelativePayloadPath(path) ||
                !actual.TryAdd(path, hash))
            {
                return false;
            }
        }
        return expected.Count == actual.Count && expected.All(pair =>
            actual.TryGetValue(pair.Key, out string? hash) &&
            string.Equals(hash, pair.Value, StringComparison.Ordinal));
    }

    private static bool IsLink(ZipArchiveEntry entry)
    {
        int unixMode = (entry.ExternalAttributes >> 16) & 0xF000;
        int windowsAttributes = entry.ExternalAttributes & 0xFFFF;
        return unixMode == 0xA000 ||
               (windowsAttributes & (int)FileAttributes.ReparsePoint) != 0;
    }

    private static bool IsLowerHex(string? value, int length)
    {
        return value is not null &&
               value.Length == length &&
               value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    private static bool IsSafeFileName(string? value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Length <= 200 &&
               !value.Contains('/') &&
               !value.Contains('\\');
    }

    private static ManagedPackagePlanResult Failure(ManagedVersionInstallIssue issue)
    {
        return new(null, issue);
    }

    private enum ArchiveContentVerification
    {
        Valid,
        Invalid,
        Unsafe,
    }
}

internal sealed record ReleaseManifestDocument(
    string? SchemaVersion,
    string? Product,
    string? Version,
    string? SourceCommit,
    string? SourceTag,
    string? RuntimeIdentifier,
    string? LicenseSpdx,
    IReadOnlyList<string>? WorkerProtocolVersions,
    IReadOnlyList<string>? ApprovedProcessorIds,
    string? ProcessorBundleSha256,
    string? EmbeddedProfileCatalogSha256,
    string? EmbeddedSchemaBundleSha256,
    IReadOnlyList<ReleaseManifestFileDocument>? Files,
    string? SbomAsset,
    string? ProvenanceAsset,
    int? VersionManagementProtocolVersion = null,
    LauncherReleaseIdentityDocument? Launcher = null);

internal sealed record LauncherReleaseIdentityDocument(
    string? LauncherVersion,
    int? ProtocolVersion,
    string? ExecutableRelativePath,
    long? Size,
    string? Sha256);

internal sealed record ReleaseManifestFileDocument(
    string Path,
    long Size,
    string Sha256,
    string Role,
    string? SigningIdentity = null);
