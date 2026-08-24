using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

internal static partial class ReplaceCliCommandHandler
{
    private static bool TryParseCtrlRamSlotArguments(
        ParsedCliOptions options,
        TextWriter error,
        [NotNullWhen(true)] out IReadOnlyList<CtrlRamSlotArgument>? arguments)
    {
        List<string> values = options.GetValues("--ctrlram");
        if (values.Count == 0)
        {
            error.WriteLine("error: at least one --ctrlram <slot-id=path> value is required for real IC CtrlRAM Replace");
            arguments = null;
            return false;
        }

        var parsed = new List<CtrlRamSlotArgument>(values.Count);
        foreach (string value in values)
        {
            int separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
            string token = separatorIndex > 0
                ? value[..separatorIndex].Trim()
                : string.Empty;
            string path = separatorIndex >= 0 && separatorIndex < value.Length - 1
                ? value[(separatorIndex + 1)..].Trim()
                : string.Empty;
            if (token.Length == 0 || path.Length == 0)
            {
                error.WriteLine(
                    $"error: real IC CtrlRAM Replace expects --ctrlram <slot-id=path>; example: --ctrlram {CompositionAddressSpaceIds.DynamicCtrlRamReplacementPrefix}vn=C:\\path\\vn.bin");
                arguments = null;
                return false;
            }

            parsed.Add(new(token, path));
        }

        arguments = parsed;
        return true;
    }

    private static bool TryCreateCtrlRamSlotPaths(
        ICtrlRamAuthoring authoring,
        string icId,
        string icNumber,
        string basePath,
        ReadOnlyMemory<byte> acceptedBaseBytes,
        IReadOnlyList<CtrlRamSlotArgument> arguments,
        TextWriter error,
        [NotNullWhen(true)] out Dictionary<string, string>? slotPaths)
    {
        slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = Path.GetFullPath(basePath),
        };
        Dictionary<string, ReplaceInputSlot> slotsByToken = CreateCtrlRamSlotLookup(
            authoring,
            icId,
            icNumber,
            acceptedBaseBytes);
        if (slotsByToken.Count == 0)
        {
            error.WriteLine($"error: no CtrlRAM replacement slots are available for {icId} / {icNumber}");
            slotPaths = null;
            return false;
        }

        foreach (CtrlRamSlotArgument argument in arguments)
        {
            if (!slotsByToken.TryGetValue(argument.Token, out ReplaceInputSlot? slot))
            {
                error.WriteLine($"error: unknown CtrlRAM slot '{argument.Token}' for {icId} / {icNumber}");
                error.WriteLine($"available slots: {FormatAvailableSlotIds(slotsByToken)}");
                slotPaths = null;
                return false;
            }

            if (!slotPaths.TryAdd(slot.SlotId, Path.GetFullPath(argument.Path)))
            {
                error.WriteLine($"error: duplicate CtrlRAM slot '{slot.SlotId}'");
                slotPaths = null;
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, ReplaceInputSlot> CreateCtrlRamSlotLookup(
        ICtrlRamAuthoring authoring,
        string icId,
        string icNumber,
        ReadOnlyMemory<byte> acceptedBaseBytes)
    {
        Dictionary<string, ReplaceInputSlot> slotsByToken = new(StringComparer.OrdinalIgnoreCase);
        foreach (ReplaceInputSlot slot in authoring.GetDiscoveryDisplayFromAcceptedBase(
                     icId,
                     icNumber,
                     acceptedBaseBytes).InputSlots)
        {
            slotsByToken[slot.SlotId] = slot;
            if (!string.IsNullOrWhiteSpace(slot.RegionId))
            {
                slotsByToken[slot.RegionId] = slot;
            }
        }

        return slotsByToken;
    }

    private static string FormatAvailableSlotIds(Dictionary<string, ReplaceInputSlot> slotsByToken)
    {
        return string.Join(
            ", ",
            slotsByToken.Values
                .Select(slot => slot.SlotId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
    }

    private sealed record CtrlRamSlotArgument(string Token, string Path);
}
