using NvtFwCombiner.Application.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

/// <summary>Selects the one strict transport for an injected Registry locator.</summary>
internal static class UpdateSourceRegistryAdapterFactory
{
    internal static IUpdateSourceRegistry Create(string locator)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(locator);
        return Uri.TryCreate(locator, UriKind.Absolute, out Uri? uri) && !uri.IsFile
            ? new HttpUpdateSourceRegistry(locator)
            : new FileSystemUpdateSourceRegistry(locator);
    }
}
