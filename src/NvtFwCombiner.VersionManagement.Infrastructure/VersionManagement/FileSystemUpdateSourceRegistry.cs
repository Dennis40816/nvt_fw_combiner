using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Strict bounded filesystem adapter for the one injected registry locator.</summary>
public sealed class FileSystemUpdateSourceRegistry : IUpdateSourceRegistry
{
    /// <summary>Recommended fixed registry file name.</summary>
    public const string RegistryFileName = "update-source-registry.v1.json";

    /// <summary>Maximum raw registry bytes.</summary>
    public const int MaximumRegistryBytes = 64 * 1024;

    /// <summary>Maximum declared source entries.</summary>
    public const int MaximumEntries = 16;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 16,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private static readonly VersionManagementJsonContext JsonContext = new(JsonOptions);
    private readonly string? _locator;

    /// <summary>Creates one reader for an injected absolute file locator.</summary>
    public FileSystemUpdateSourceRegistry(string? locator)
    {
        _locator = locator;
    }

    /// <inheritdoc />
    public async ValueTask<UpdateSourceRegistryLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(_locator))
        {
            return Failure(UpdateSourceRegistryLoadIssue.NotConfigured);
        }

        try
        {
            if (!TryNormalizeAbsoluteFile(_locator, out string path))
            {
                return Failure(UpdateSourceRegistryLoadIssue.UnsafeLocator);
            }
            if (!File.Exists(path))
            {
                return Failure(UpdateSourceRegistryLoadIssue.RegistryMissing);
            }
            if (HasReparseComponent(path))
            {
                return Failure(UpdateSourceRegistryLoadIssue.UnsafeLocator);
            }

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: MaximumRegistryBytes,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            long admittedLength = stream.Length;
            if (admittedLength is < 1 or > MaximumRegistryBytes)
            {
                return admittedLength > MaximumRegistryBytes
                    ? Failure(UpdateSourceRegistryLoadIssue.RegistryTooLarge)
                    : Failure(UpdateSourceRegistryLoadIssue.InvalidManifest);
            }
            byte[] bytes = new byte[checked((int)admittedLength)];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (stream.Length != admittedLength || stream.Position != admittedLength)
            {
                return Failure(UpdateSourceRegistryLoadIssue.UnstableRead);
            }

            using JsonDocument json = EmbeddedVersionManagementSchema.ParseStrict(bytes, maximumDepth: 16);
            if (!UpdateSourceRegistrySchema.IsValid(json.RootElement))
            {
                return Failure(UpdateSourceRegistryLoadIssue.InvalidManifest);
            }
            UpdateSourceRegistryDocument? document = JsonSerializer.Deserialize(
                bytes,
                JsonContext.UpdateSourceRegistryDocument);
            if (document?.Entries is not { Count: >= 1 and <= MaximumEntries } entries ||
                document.SchemaVersion != 1 ||
                document.Revision <= 0)
            {
                return Failure(UpdateSourceRegistryLoadIssue.InvalidManifest);
            }

            var projected = new List<UpdateSourceRegistryEntry>(entries.Count);
            var unique = new HashSet<string>(PathComparer);
            foreach (UpdateSourceRegistryEntryDocument? entry in entries)
            {
                if (entry is null ||
                    !TryParseStatus(entry.Status, out UpdateSourceRegistryEntryStatus status) ||
                    !TryNormalizeAbsoluteRoot(entry.Path, out string normalized) ||
                    !unique.Add(normalized))
                {
                    return Failure(UpdateSourceRegistryLoadIssue.InvalidManifest);
                }
                projected.Add(new(normalized, status));
            }

            try
            {
                return new(
                    new(document.Revision, Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant(), projected),
                    UpdateSourceRegistryLoadIssue.None);
            }
            catch (ArgumentException)
            {
                return Failure(UpdateSourceRegistryLoadIssue.InvalidManifest);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(ClassifyReadFailure(exception));
        }
        catch (JsonException)
        {
            return Failure(UpdateSourceRegistryLoadIssue.InvalidManifest);
        }
        catch (IOException exception)
        {
            return Failure(ClassifyReadFailure(exception));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return Failure(UpdateSourceRegistryLoadIssue.UnsafeLocator);
        }
    }

    private static bool TryNormalizeAbsoluteRoot(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) ||
            !Path.IsPathFullyQualified(value) ||
            IsDeviceExtendedOrAlternateStream(value))
        {
            return false;
        }
        try
        {
            normalized = Path.TrimEndingDirectorySeparator(Path.GetFullPath(value));
            return normalized.Length > 0 && PathComparer.Equals(normalized, value);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool TryNormalizeAbsoluteFile(string value, out string normalized)
    {
        normalized = string.Empty;
        if (!Path.IsPathFullyQualified(value) || IsDeviceExtendedOrAlternateStream(value))
        {
            return false;
        }
        try
        {
            normalized = Path.GetFullPath(value);
            return PathComparer.Equals(normalized, value);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException)
        {
            return false;
        }
    }

    private static bool HasReparseComponent(string filePath)
    {
        string? current = filePath;
        while (!string.IsNullOrEmpty(current))
        {
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                return true;
            }
            current = Path.GetDirectoryName(current);
        }
        return false;
    }

    private static bool IsDeviceExtendedOrAlternateStream(string path)
    {
        if (path.StartsWith("\\\\?\\", StringComparison.Ordinal) ||
            path.StartsWith("\\\\.\\", StringComparison.Ordinal))
        {
            return true;
        }
        string? root = Path.GetPathRoot(path);
        return root is null || path.AsSpan(root.Length).Contains(':');
    }

    internal static UpdateSourceRegistryLoadIssue ClassifyReadFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is UnauthorizedAccessException
            ? UpdateSourceRegistryLoadIssue.PermissionDenied
            : UpdateSourceRegistryLoadIssue.RegistryUnavailable;
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

    private static UpdateSourceRegistryLoadResult Failure(UpdateSourceRegistryLoadIssue issue)
    {
        return new(null, issue);
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
}
