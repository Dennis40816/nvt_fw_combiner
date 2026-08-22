using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Bounded non-recursive filesystem adapter for update catalog v1.</summary>
public sealed class FileSystemUpdateCatalogSource : IUpdateCatalogSource
{
    /// <summary>The exact configured-root catalog name.</summary>
    public const string CatalogFileName = "update-catalog.v1.json";

    /// <summary>The maximum raw catalog length.</summary>
    public const int MaximumCatalogBytes = 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        MaxDepth = 16,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
    private static readonly VersionManagementJsonContext JsonContext = new(JsonOptions);

    /// <inheritdoc />
    public async ValueTask<UpdateCatalogLoadResult> LoadAsync(
        string sourceRoot,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRoot);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            string fullRoot = Path.GetFullPath(sourceRoot);
            if (!Directory.Exists(fullRoot))
            {
                return Failure(UpdateCatalogLoadIssue.SourceMissing);
            }
            if (IsReparsePoint(fullRoot))
            {
                return Failure(UpdateCatalogLoadIssue.UnsafeSource);
            }

            string catalogPath = Path.Combine(fullRoot, CatalogFileName);
            if (!File.Exists(catalogPath))
            {
                return Failure(UpdateCatalogLoadIssue.SourceMissing);
            }
            if (IsReparsePoint(catalogPath))
            {
                return Failure(UpdateCatalogLoadIssue.UnsafeSource);
            }

            await using var stream = new FileStream(
                catalogPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            long admittedLength = stream.Length;
            if (admittedLength is < 1 or > MaximumCatalogBytes)
            {
                return admittedLength > MaximumCatalogBytes
                    ? Failure(UpdateCatalogLoadIssue.CatalogTooLarge)
                    : Failure(UpdateCatalogLoadIssue.InvalidManifest);
            }

            byte[] bytes = new byte[checked((int)admittedLength)];
            await stream.ReadExactlyAsync(bytes, cancellationToken).ConfigureAwait(false);
            if (stream.Length != admittedLength || stream.Position != admittedLength)
            {
                return Failure(UpdateCatalogLoadIssue.UnstableRead);
            }

            using JsonDocument catalogJson = EmbeddedVersionManagementSchema.ParseStrict(bytes, maximumDepth: 16);
            if (!UpdateCatalogSchema.IsValid(catalogJson.RootElement))
            {
                return Failure(UpdateCatalogLoadIssue.InvalidManifest);
            }
            UpdateCatalogDocument? document = JsonSerializer.Deserialize(
                bytes,
                JsonContext.UpdateCatalogDocument);
            if (document is null)
            {
                return Failure(UpdateCatalogLoadIssue.InvalidManifest);
            }

            UpdateCatalogValidationResult validation = UpdateCatalogValidator.Validate(document);
            return validation.IsValid
                ? new(validation.Snapshot, UpdateCatalogLoadIssue.None)
                : Failure(UpdateCatalogLoadIssue.InvalidManifest);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(ClassifyReadFailure(exception));
        }
        catch (JsonException)
        {
            return Failure(UpdateCatalogLoadIssue.InvalidManifest);
        }
        catch (IOException exception)
        {
            return Failure(ClassifyReadFailure(exception));
        }
        catch (NotSupportedException)
        {
            return Failure(UpdateCatalogLoadIssue.UnsafeSource);
        }
        catch (ArgumentException)
        {
            return Failure(UpdateCatalogLoadIssue.UnsafeSource);
        }
    }

    private static bool IsReparsePoint(string path)
    {
        return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
    }

    internal static UpdateCatalogLoadIssue ClassifyReadFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception is UnauthorizedAccessException
            ? UpdateCatalogLoadIssue.PermissionDenied
            : UpdateCatalogLoadIssue.SourceUnavailable;
    }

    private static UpdateCatalogLoadResult Failure(UpdateCatalogLoadIssue issue)
    {
        return new(null, issue);
    }
}
