using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static readonly Dictionary<string, string> FixedInputOptionsByAddressSpace =
        new(StringComparer.Ordinal)
        {
            [CompositionAddressSpaceIds.ReferenceBase] = "--base",
            [CompositionAddressSpaceIds.DpReplacement] = "--dp",
            [CompositionAddressSpaceIds.LdReplacement] = "--ld",
            [CompositionAddressSpaceIds.CtrlRamReplacement] = "--ctrlram",
            [GeneralReplaceInputAddressSpaceId] = "--input",
        };

    private static bool TryCreateBindings(
        CompositionPlan plan,
        ParsedOptions options,
        TextWriter error,
        out IReadOnlyList<InputArtifactBinding> bindings)
    {
        List<InputArtifactBinding> items = [];
        HashSet<string> usedInputOptions = new(StringComparer.Ordinal);
        foreach (string addressSpaceId in plan.RequiredInputAddressSpaceIds.Order(StringComparer.Ordinal))
        {
            if (!FixedInputOptionsByAddressSpace.TryGetValue(addressSpaceId, out string? optionName))
            {
                error.WriteLine($"error: profile requires unsupported address space '{addressSpaceId}'");
                bindings = [];
                return false;
            }

            if (!RequireOption(options, optionName, error, out string? path))
            {
                bindings = [];
                return false;
            }

            string fullPath = Path.GetFullPath(path);
            items.Add(new InputArtifactBinding(addressSpaceId, addressSpaceId, fullPath));
            _ = usedInputOptions.Add(optionName);
        }

        foreach (string optionName in FixedInputOptionsByAddressSpace.Values.Order(StringComparer.Ordinal))
        {
            if (options.Values.ContainsKey(optionName) && !usedInputOptions.Contains(optionName))
            {
                error.WriteLine($"error: option '{optionName}' is not used by the selected replace profile");
                bindings = [];
                return false;
            }
        }

        bindings = items;
        return true;
    }
}
