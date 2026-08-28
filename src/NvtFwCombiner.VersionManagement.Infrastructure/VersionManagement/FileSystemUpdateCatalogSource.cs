using System.Security.Cryptography;
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
            return ManagedPathSafety.HasReparseComponent(fullRoot)
                    ? Failure(UpdateCatalogLoadIssue.UnsafeSource)
                    : await LoadValidatedPathAsync(
                    Path.Combine(fullRoot, CatalogFileName),
                    cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(ClassifyReadFailure(exception));
        }
        catch (IOException exception)
        {
            return Failure(ClassifyReadFailure(exception));
        }
        catch (Exception exception) when (exception is NotSupportedException or ArgumentException)
        {
            return Failure(UpdateCatalogLoadIssue.UnsafeSource);
        }
    }

    /// <inheritdoc />
    public async ValueTask<UpdateCatalogLoadResult> LoadCatalogAsync(
        string catalogPath,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(catalogPath);
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            if (!ManagedPathSafety.TryNormalizeExactAbsolutePath(
                    catalogPath,
                    out string fullPath))
            {
                return Failure(UpdateCatalogLoadIssue.UnsafeSource);
            }
            string? parent = Path.GetDirectoryName(fullPath);
            return string.IsNullOrWhiteSpace(parent)
                ? Failure(UpdateCatalogLoadIssue.SourceMissing)
                : ManagedPathSafety.HasReparseComponent(parent)
                    ? Failure(UpdateCatalogLoadIssue.UnsafeSource)
                    : await LoadValidatedPathAsync(fullPath, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(ClassifyReadFailure(exception));
        }
        catch (IOException exception)
        {
            return Failure(ClassifyReadFailure(exception));
        }
        catch (Exception exception) when (exception is NotSupportedException or ArgumentException)
        {
            return Failure(UpdateCatalogLoadIssue.UnsafeSource);
        }
    }

    private static async ValueTask<UpdateCatalogLoadResult> LoadValidatedPathAsync(
        string catalogPath,
        CancellationToken cancellationToken)
    {
        try
        {
            if (ManagedPathSafety.HasReparseComponent(catalogPath))
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
                ? new(
                    validation.Snapshot,
                    UpdateCatalogLoadIssue.None,
                    new(
                        document.SchemaVersion,
                        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant()))
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
        catch (Exception exception) when (exception is NotSupportedException or ArgumentException)
        {
            return Failure(UpdateCatalogLoadIssue.UnsafeSource);
        }
    }

    internal static UpdateCatalogLoadIssue ClassifyReadFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            FileNotFoundException or DirectoryNotFoundException =>
                UpdateCatalogLoadIssue.SourceMissing,
            UnauthorizedAccessException => UpdateCatalogLoadIssue.PermissionDenied,
            _ => UpdateCatalogLoadIssue.SourceUnavailable,
        };
    }

    private static UpdateCatalogLoadResult Failure(UpdateCatalogLoadIssue issue)
    {
        return new(null, issue);
    }

}
