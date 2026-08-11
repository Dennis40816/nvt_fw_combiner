using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

internal static partial class ReplaceCliCommandHandler
{
    private static bool TryCreateCtrlRamSlotPaths(
        ICtrlRamAuthoring authoring,
        string icId,
        string icNumber,
        string basePath,
        ParsedCliOptions options,
        TextWriter error,
        [NotNullWhen(true)] out Dictionary<string, string>? slotPaths)
    {
        slotPaths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = Path.GetFullPath(basePath),
        };
        List<string> ctrlRamValues = options.GetValues("--ctrlram");
        if (ctrlRamValues.Count == 0)
        {
            error.WriteLine("error: at least one --ctrlram <slot-id=path> value is required for real IC CtrlRAM Replace");
            slotPaths = null;
            return false;
        }

        Dictionary<string, ReplaceInputSlot> slotsByToken = CreateCtrlRamSlotLookup(
            authoring,
            icId,
            icNumber,
            basePath);
        if (slotsByToken.Count == 0)
        {
            error.WriteLine($"error: no CtrlRAM replacement slots are available for {icId} / {icNumber}");
            slotPaths = null;
            return false;
        }

        foreach (string value in ctrlRamValues)
        {
            int separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
            if (separatorIndex <= 0 || separatorIndex == value.Length - 1)
            {
                error.WriteLine(
                    $"error: real IC CtrlRAM Replace expects --ctrlram <slot-id=path>; example: --ctrlram {CompositionAddressSpaceIds.DynamicCtrlRamReplacementPrefix}vn=C:\\path\\vn.bin");
                slotPaths = null;
                return false;
            }

            string token = value[..separatorIndex].Trim();
            string path = value[(separatorIndex + 1)..].Trim();
            if (!slotsByToken.TryGetValue(token, out ReplaceInputSlot? slot))
            {
                error.WriteLine($"error: unknown CtrlRAM slot '{token}' for {icId} / {icNumber}");
                error.WriteLine($"available slots: {FormatAvailableSlotIds(slotsByToken)}");
                slotPaths = null;
                return false;
            }

            if (!slotPaths.TryAdd(slot.SlotId, Path.GetFullPath(path)))
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
        string basePath)
    {
        Dictionary<string, ReplaceInputSlot> slotsByToken = new(StringComparer.OrdinalIgnoreCase);
        foreach (ReplaceInputSlot slot in authoring.GetDiscoveryDisplay(
                     icId,
                     icNumber,
                     basePath).InputSlots)
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
}
