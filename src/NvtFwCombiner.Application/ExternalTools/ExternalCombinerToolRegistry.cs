using NvtFwCombiner.Contracts.ExternalTools;

namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Read-only registry for approved external combiner tool manifests.</summary>
public sealed class ExternalCombinerToolRegistry
{
    private readonly Dictionary<string, ExternalCombinerToolManifest> _byBindingId;

    /// <summary>Creates a registry after validating all manifests.</summary>
    public ExternalCombinerToolRegistry(IEnumerable<ExternalCombinerToolManifest> manifests)
    {
        ArgumentNullException.ThrowIfNull(manifests);

        Dictionary<string, ExternalCombinerToolManifest> byBindingId = new(StringComparer.Ordinal);
        foreach (ExternalCombinerToolManifest manifest in manifests)
        {
            IReadOnlyList<string> errors = ExternalCombinerToolManifestValidator.Validate(manifest);
            if (errors.Count > 0)
            {
                throw new ArgumentException(
                    $"External combiner manifest '{manifest.ToolBindingId}' is invalid: {string.Join("; ", errors)}",
                    nameof(manifests));
            }

            if (!byBindingId.TryAdd(manifest.ToolBindingId, manifest))
            {
                throw new ArgumentException(
                    $"External combiner tool binding '{manifest.ToolBindingId}' is declared more than once.",
                    nameof(manifests));
            }
        }

        _byBindingId = byBindingId;
    }

    /// <summary>Finds a manifest by exact tool binding id.</summary>
    public ExternalCombinerToolManifest Resolve(string toolBindingId)
    {
        return _byBindingId.TryGetValue(toolBindingId, out ExternalCombinerToolManifest? manifest)
            ? manifest
            : throw new KeyNotFoundException($"External combiner tool binding '{toolBindingId}' is not registered.");
    }
}
