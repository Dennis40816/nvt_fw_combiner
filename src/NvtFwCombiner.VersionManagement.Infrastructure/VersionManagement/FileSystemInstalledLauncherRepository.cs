using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Verifies one release-coupled launcher against its exact admitted owner manifest.</summary>
internal sealed class FileSystemInstalledLauncherRepository : IInstalledLauncherRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 32,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private static readonly VersionManagementJsonContext JsonContext = new(JsonOptions);

    public async ValueTask<InstalledLauncherResult> VerifyAsync(
        string managedRoot,
        ManagedVersionAdmission admission,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(managedRoot);
        ArgumentNullException.ThrowIfNull(admission);
        try
        {
            string versionsRoot = Path.Combine(
                Path.GetFullPath(managedRoot),
                FileSystemManagedVersionRepository.VersionsDirectoryName);
            string versionRoot = ManagedPathSafety.GetExactVersionDirectory(versionsRoot, admission.Version);
            if (!ManagedPathSafety.IsSafeOwnedTree(versionRoot))
            {
                return Failure(InstalledLauncherIssue.UnsafePath);
            }

            byte[]? manifestBytes = await ManagedPathSafety.ReadBoundedFileAsync(
                Path.Combine(versionRoot, "RELEASE-MANIFEST.json"),
                FileSystemManagedVersionRepository.MaximumManifestBytes,
                cancellationToken).ConfigureAwait(false);
            if (manifestBytes is null ||
                !string.Equals(
                    Convert.ToHexStringLower(SHA256.HashData(manifestBytes)),
                    admission.ReleaseManifestSha256,
                    StringComparison.Ordinal))
            {
                return Failure(InstalledLauncherIssue.InvalidManifest);
            }

            ReleaseManifestDocument? manifest;
            using (JsonDocument json = EmbeddedVersionManagementSchema.ParseStrict(manifestBytes, maximumDepth: 32))
            {
                if (!ReleaseManifestSchema.IsValid(json.RootElement))
                {
                    return Failure(InstalledLauncherIssue.InvalidManifest);
                }
                manifest = JsonSerializer.Deserialize(manifestBytes, JsonContext.ReleaseManifestDocument);
            }

            LauncherReleaseIdentityDocument? launcher = manifest?.Launcher;
            if (manifest?.SchemaVersion != "1.2" ||
                manifest.VersionManagementProtocolVersion != ManagedLauncherIdentity.SupportedProtocolVersion ||
                !string.Equals(manifest.Product, "NVT FW Combiner", StringComparison.Ordinal) ||
                !string.Equals(manifest.Version, admission.Version.ToString(), StringComparison.Ordinal) ||
                !string.Equals(manifest.RuntimeIdentifier, "win-x64", StringComparison.Ordinal) ||
                launcher?.ProtocolVersion != ManagedLauncherIdentity.SupportedProtocolVersion ||
                !ManagedAppVersion.TryParse(launcher.LauncherVersion, out ManagedAppVersion launcherVersion) ||
                !string.Equals(
                    launcher.ExecutableRelativePath,
                    ManagedLauncherIdentity.ExecutablePath,
                    StringComparison.Ordinal) ||
                launcher.Size is null or <= 0 or > ManagedLauncherIdentity.MaximumExecutableBytes ||
                !IsLowerSha256(launcher.Sha256))
            {
                return Failure(InstalledLauncherIssue.ProtocolMismatch);
            }

            ReleaseManifestFileDocument[] launcherFiles =
            [.. manifest.Files!.Where(file => string.Equals(file.Role, "launcher", StringComparison.Ordinal))];
            if (launcherFiles is not
                [{ Path: ManagedLauncherIdentity.ExecutablePath } launcherFile] ||
                launcherFile.Size != launcher.Size ||
                !string.Equals(launcherFile.Sha256, launcher.Sha256, StringComparison.Ordinal))
            {
                return Failure(InstalledLauncherIssue.InvalidManifest);
            }

            string launcherPath = ManagedPathSafety.ResolvePayloadPath(
                versionRoot,
                ManagedLauncherIdentity.ExecutablePath);
            byte[]? launcherBytes = await ManagedPathSafety.ReadBoundedFileAsync(
                launcherPath,
                checked((int)ManagedLauncherIdentity.MaximumExecutableBytes),
                cancellationToken).ConfigureAwait(false);
            return launcherBytes is null ||
                   launcherBytes.LongLength != launcher.Size ||
                   !string.Equals(
                       Convert.ToHexStringLower(SHA256.HashData(launcherBytes)),
                       launcher.Sha256,
                       StringComparison.Ordinal)
                ? Failure(InstalledLauncherIssue.Tampered)
                : new(
                ManagedLauncherIdentity.Create(
                    admission.Version,
                    admission.AdmissionIdentity,
                    admission.ReleaseManifestSha256,
                    launcherVersion,
                    launcher.ProtocolVersion.Value,
                    launcher.ExecutableRelativePath!,
                    launcher.Size.Value,
                    launcher.Sha256!),
                InstalledLauncherIssue.None);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            return Failure(InstalledLauncherIssue.Unavailable);
        }
    }

    private static InstalledLauncherResult Failure(InstalledLauncherIssue issue)
    {
        return new(null, issue);
    }

    private static bool IsLowerSha256(string? value)
    {
        return value is { Length: 64 } &&
               value.All(static character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }
}
