using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Strict bounded filesystem adapter for the one injected registry locator.</summary>
public sealed class FileSystemUpdateSourceRegistry : IUpdateSourceRegistry
{
    /// <summary>Recommended fixed registry file name.</summary>
    public const string RegistryFileName = "update-source-registry.json";

    /// <summary>Maximum raw registry bytes.</summary>
    public const int MaximumRegistryBytes =
        UpdateSourceRegistryDocumentParser.MaximumRegistryBytes;

    /// <summary>Maximum declared source entries.</summary>
    public const int MaximumEntries = UpdateSourceRegistryDocumentParser.MaximumEntries;
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
            if (!ManagedPathSafety.TryNormalizeExactAbsolutePath(_locator, out string path))
            {
                return Failure(UpdateSourceRegistryLoadIssue.UnsafeLocator);
            }
            if (ManagedPathSafety.HasReparseComponent(path))
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
            return stream.Length != admittedLength || stream.Position != admittedLength
                ? Failure(UpdateSourceRegistryLoadIssue.UnstableRead)
                : UpdateSourceRegistryDocumentParser.Parse(bytes);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (UnauthorizedAccessException exception)
        {
            return Failure(ClassifyReadFailure(exception));
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

    internal static UpdateSourceRegistryLoadIssue ClassifyReadFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            FileNotFoundException or DirectoryNotFoundException =>
                UpdateSourceRegistryLoadIssue.RegistryMissing,
            UnauthorizedAccessException => UpdateSourceRegistryLoadIssue.PermissionDenied,
            _ => UpdateSourceRegistryLoadIssue.RegistryUnavailable,
        };
    }

    private static UpdateSourceRegistryLoadResult Failure(UpdateSourceRegistryLoadIssue issue)
    {
        return new(null, issue);
    }

}
