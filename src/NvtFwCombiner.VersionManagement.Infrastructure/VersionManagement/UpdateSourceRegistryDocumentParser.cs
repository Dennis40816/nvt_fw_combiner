using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Single strict parser shared by every admitted Registry transport.</summary>
internal static class UpdateSourceRegistryDocumentParser
{
    internal const int MaximumRegistryBytes = 64 * 1024;
    internal const int MaximumEntries = 16;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 16,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private static readonly VersionManagementJsonContext JsonContext = new(JsonOptions);

    internal static UpdateSourceRegistryLoadResult Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length == 0)
        {
            return Failure(UpdateSourceRegistryLoadIssue.InvalidManifest);
        }
        if (bytes.Length > MaximumRegistryBytes)
        {
            return Failure(UpdateSourceRegistryLoadIssue.RegistryTooLarge);
        }

        try
        {
            using JsonDocument json = EmbeddedVersionManagementSchema.ParseStrict(
                bytes,
                maximumDepth: 16);
            if (!UpdateSourceRegistrySchema.IsValid(json.RootElement))
            {
                return Failure(UpdateSourceRegistryLoadIssue.InvalidManifest);
            }

            UpdateSourceRegistryDocument? document = JsonSerializer.Deserialize(
                bytes,
                JsonContext.UpdateSourceRegistryDocument);
            if (document?.Entries is not { Count: >= 1 and <= MaximumEntries } entries ||
                document.SchemaVersion != 1 ||
                string.IsNullOrWhiteSpace(document.RegistryId) ||
                document.RegistryRevision <= 0 ||
                document.CatalogPublication is not { } catalogPublication ||
                !ManagedAppVersion.TryParse(catalogPublication.LatestVersion, out _) ||
                catalogPublication.CatalogSchemaVersion <= 0 ||
                !UpdateSourceRegistrySnapshot.IsLowerSha256(catalogPublication.CatalogSha256))
            {
                return Failure(UpdateSourceRegistryLoadIssue.InvalidManifest);
            }

            var projected = new List<UpdateSourceRegistryEntry>(entries.Count);
            var uniquePaths = new HashSet<string>(ManagedPathSafety.PathComparer);
            var uniqueRoots = new HashSet<string>(ManagedPathSafety.PathComparer);
            foreach (UpdateSourceRegistryEntryDocument? entry in entries)
            {
                if (entry is null ||
                    !TryParseStatus(entry.Status, out UpdateSourceRegistryEntryStatus status) ||
                    !ManagedPathSafety.TryNormalizeExactAbsolutePath(
                        entry.CatalogPath,
                        out string normalized) ||
                    string.IsNullOrWhiteSpace(Path.GetFileName(normalized)) ||
                    !uniquePaths.Add(normalized) ||
                    Path.GetDirectoryName(normalized) is not { } sourceRoot ||
                    !uniqueRoots.Add(sourceRoot))
                {
                    return Failure(UpdateSourceRegistryLoadIssue.InvalidManifest);
                }
                projected.Add(new(normalized, status));
            }

            try
            {
                return new(
                    new(
                        document.RegistryId,
                        document.RegistryRevision,
                        document.PublishedAtUtc,
                        new(
                            catalogPublication.LatestVersion!,
                            catalogPublication.CatalogSchemaVersion,
                            catalogPublication.CatalogSha256!),
                        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(),
                        projected),
                    UpdateSourceRegistryLoadIssue.None);
            }
            catch (Exception exception) when (exception is ArgumentException or FormatException)
            {
                return Failure(UpdateSourceRegistryLoadIssue.InvalidManifest);
            }
        }
        catch (JsonException)
        {
            return Failure(UpdateSourceRegistryLoadIssue.InvalidManifest);
        }
    }

    private static bool TryParseStatus(
        string? value,
        out UpdateSourceRegistryEntryStatus status)
    {
        switch (value)
        {
            case "latest":
                status = UpdateSourceRegistryEntryStatus.Latest;
                return true;
            case "available":
                status = UpdateSourceRegistryEntryStatus.Available;
                return true;
            case "deprecated":
                status = UpdateSourceRegistryEntryStatus.Deprecated;
                return true;
            default:
                status = default;
                return false;
        }
    }

    private static UpdateSourceRegistryLoadResult Failure(
        UpdateSourceRegistryLoadIssue issue)
    {
        return new(null, issue);
    }

}
