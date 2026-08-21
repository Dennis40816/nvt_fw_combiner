using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

internal sealed record ManagedPackagePlan(
    ReleaseManifestDocument Manifest,
    IReadOnlyDictionary<string, ZipArchiveEntry> Entries,
    byte[] ManifestBytes);

internal sealed record ManagedPackagePlanResult(
    ManagedPackagePlan? Plan,
    ManagedVersionInstallIssue Issue)
{
    internal bool IsSuccess => Plan is not null && Issue == ManagedVersionInstallIssue.None;
}

internal static class ManagedPackageVerifier
{
    private static readonly string[] FixedPayloadFiles =
    [
        "NvtFwCombiner.exe",
        "external-tools/crc-worker/0.1.0/Nfc.CrcWorker.exe",
        "THIRD-PARTY-NOTICES.txt",
        "LICENSE.txt",
        "README.txt",
    ];

    private static readonly HashSet<string> Roles = new(StringComparer.Ordinal)
    {
        "application",
        "crcWorker",
        "notices",
        "license",
        "readme",
        "externalTool",
        "reference",
        "goldenFixture",
    };

    private static readonly JsonSerializerOptions ManifestJsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 32,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private static readonly VersionManagementJsonContext ManifestJsonContext = new(ManifestJsonOptions);

    internal static async ValueTask<ManagedPackagePlanResult> CreatePlanAsync(
        ZipArchive archive,
        UpdateCatalogVersionSnapshot package,
        CancellationToken cancellationToken)
    {
        if (archive.Entries.Count is 0 or > FileSystemManagedVersionRepository.MaximumArchiveEntries)
        {
            return Failure(ManagedVersionInstallIssue.UnsafeArchive);
        }

        string packageRoot = $"NvtFwCombiner-v{package.Version}-win-x64";
        string prefix = packageRoot + "/";
        var entries = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
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

            expandedBytes = checked(expandedBytes + entry.Length);
            if (expandedBytes > FileSystemManagedVersionRepository.MaximumExpandedBytes)
            {
                return Failure(ManagedVersionInstallIssue.UnsafeArchive);
            }
        }

        if (!entries.TryGetValue("RELEASE-MANIFEST.json", out ZipArchiveEntry? manifestEntry) ||
            manifestEntry.Length is < 1 or > FileSystemManagedVersionRepository.MaximumManifestBytes)
        {
            return Failure(ManagedVersionInstallIssue.InvalidPayload);
        }

        byte[] manifestBytes = new byte[checked((int)manifestEntry.Length)];
        await using (Stream input = manifestEntry.Open())
        {
            await input.ReadExactlyAsync(manifestBytes, cancellationToken).ConfigureAwait(false);
        }
        if (!string.Equals(
                Convert.ToHexStringLower(SHA256.HashData(manifestBytes)),
                package.ReleaseManifestSha256,
                StringComparison.Ordinal))
        {
            return Failure(ManagedVersionInstallIssue.InvalidPayload);
        }

        ReleaseManifestDocument? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize(
                manifestBytes,
                ManifestJsonContext.ReleaseManifestDocument);
        }
        catch (JsonException)
        {
            return Failure(ManagedVersionInstallIssue.InvalidPayload);
        }
        return manifest is not null && ValidateManifest(manifest, package.Version, entries.Keys)
            ? new(new(manifest, entries, manifestBytes), ManagedVersionInstallIssue.None)
            : Failure(ManagedVersionInstallIssue.InvalidPayload);
    }

    internal static async ValueTask ExtractAsync(
        ManagedPackagePlan plan,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        foreach ((string relativePath, ZipArchiveEntry entry) in plan.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string target = ManagedPathSafety.ResolvePayloadPath(stagingDirectory, relativePath);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using Stream source = entry.Open();
            await using var destination = new FileStream(
                target,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            await source.CopyToAsync(destination, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async ValueTask<ManagedVersionDamageReason?> VerifyInstalledAsync(
        string versionRoot,
        ManagedVersionAdmission admission,
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

            ReleaseManifestDocument? manifest = JsonSerializer.Deserialize(
                manifestBytes,
                ManifestJsonContext.ReleaseManifestDocument);
            if (manifest?.Files is null ||
                !ValidateManifest(
                    manifest,
                    admission.Version,
                    manifest.Files.Select(file => file.Path).Append("RELEASE-MANIFEST.json")))
            {
                return ManagedVersionDamageReason.ManifestMismatch;
            }

            HashSet<string> expected = new(StringComparer.OrdinalIgnoreCase)
            {
                "RELEASE-MANIFEST.json",
                FileSystemManagedVersionRepository.AdmissionFileName,
            };
            foreach (ReleaseManifestFileDocument file in manifest.Files)
            {
                _ = expected.Add(file.Path);
                string path = ManagedPathSafety.ResolvePayloadPath(versionRoot, file.Path);
                if (!File.Exists(path) || ManagedPathSafety.IsReparsePoint(path))
                {
                    return ManagedVersionDamageReason.MissingFile;
                }
                var info = new FileInfo(path);
                if (info.Length != file.Size ||
                    !string.Equals(await HashFileAsync(path, cancellationToken).ConfigureAwait(false), file.Sha256, StringComparison.Ordinal))
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
        if (!string.Equals(manifest.SchemaVersion, "1.1", StringComparison.Ordinal) ||
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
            manifest.Files.Count < FixedPayloadFiles.Length ||
            !IsSafeFileName(manifest.SbomAsset) ||
            !IsSafeFileName(manifest.ProvenanceAsset))
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
                !Roles.Contains(file.Role) ||
                !declared.Add(file.Path))
            {
                return false;
            }
        }
        if (FixedPayloadFiles.Any(required => !declared.Contains(required)))
        {
            return false;
        }

        var expectedArchive = new HashSet<string>(declared, StringComparer.OrdinalIgnoreCase)
        {
            "RELEASE-MANIFEST.json",
        };
        return expectedArchive.SetEquals(archivePaths);
    }

    private static async ValueTask<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 64 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = GC.AllocateUninitializedArray<byte>(64 * 1024);
        int read;
        while ((read = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            hash.AppendData(buffer, 0, read);
        }
        return Convert.ToHexStringLower(hash.GetHashAndReset());
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
    string? ProvenanceAsset);

internal sealed record ReleaseManifestFileDocument(
    string Path,
    long Size,
    string Sha256,
    string Role,
    string? SigningIdentity = null);
