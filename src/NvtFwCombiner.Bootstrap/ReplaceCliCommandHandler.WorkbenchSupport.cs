using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static bool TryResolveWorkbenchIc(
        string selector,
        [NotNullWhen(true)] out string? icId)
    {
        string normalized = selector.Trim();
        icId = WorkbenchCompositionService.GetSupportedIcIds().FirstOrDefault(candidate =>
            string.Equals(candidate, normalized, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CliCompositionRunSupport.GetIcNumber(candidate), normalized, StringComparison.OrdinalIgnoreCase));
        return icId is not null;
    }

    private static InputArtifactBinding[] CreateWorkbenchBindings(IReadOnlyDictionary<string, string> slotPaths)
    {
        return [
            .. slotPaths
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => new InputArtifactBinding(
                    pair.Key == WorkbenchSlotIds.ReplaceBase ? CompositionAddressSpaceIds.ReferenceBase : pair.Key,
                    pair.Key,
                    pair.Value)),
        ];
    }
}
