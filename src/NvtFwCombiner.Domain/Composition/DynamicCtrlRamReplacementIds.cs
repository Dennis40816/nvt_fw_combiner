namespace NvtFwCombiner.Domain.Composition;

/// <summary>Helpers for dynamic CtrlRAM replacement address-space identifiers.</summary>
public static class DynamicCtrlRamReplacementIds
{
    /// <summary>Creates one dynamic CtrlRAM replacement address-space id.</summary>
    public static string Create(string regionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        return string.Concat(
            CompositionAddressSpaceIds.DynamicCtrlRamReplacementPrefix,
            regionId);
    }

    /// <summary>Formats a dynamic CtrlRAM replacement id into a human-readable report label.</summary>
    public static bool TryFormatDisplayLabel(string addressSpaceId, out string label)
    {
        label = string.Empty;
        if (string.IsNullOrWhiteSpace(addressSpaceId))
        {
            return false;
        }

        string normalized = addressSpaceId.Trim();
        if (!normalized.StartsWith(CompositionAddressSpaceIds.DynamicCtrlRamReplacementPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string regionId = normalized[CompositionAddressSpaceIds.DynamicCtrlRamReplacementPrefix.Length..];
        if (string.IsNullOrWhiteSpace(regionId) ||
            regionId.All(character => character == '-'))
        {
            return false;
        }

        label = FormatRegionDisplayLabel(regionId);
        return true;
    }

    /// <summary>Formats a CtrlRAM region id into a human-readable report label.</summary>
    public static string FormatRegionDisplayLabel(string regionId)
    {
        string[] parts = SplitRegionId(regionId);
        string region = $"{FormatRegionToken(parts[0])} CtrlRAM";
        string side = FormatSide(parts);
        return string.IsNullOrWhiteSpace(side) ? region : $"{region} ({side})";
    }

    /// <summary>Formats the stable CtrlRAM family token without topology decoration.</summary>
    public static string FormatRegionBaseDisplayLabel(string regionId)
    {
        return $"{FormatRegionToken(GetRegionFamilyToken(regionId))} CtrlRAM";
    }

    /// <summary>Returns the stable uppercase family token from one canonical CtrlRAM region id.</summary>
    public static string GetRegionFamilyToken(string regionId)
    {
        return SplitRegionId(regionId)[0].ToUpperInvariant();
    }

    private static string[] SplitRegionId(string regionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(regionId);
        string[] parts = regionId.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        DomainInvariant.Reject(parts.Length == 0, "CtrlRAM region id must include a region token.", nameof(regionId));
        return parts;
    }

    private static string FormatRegionToken(string token)
    {
        return token.ToUpperInvariant() switch
        {
            "NF" => "NF",
            "MP" => "MP",
            "VN" => "VN",
            "NORMAL" => "Normal",
            _ => token,
        };
    }

    private static string FormatSide(string[] parts)
    {
        return parts switch
        {
            [_, string side, ..] when string.Equals(side, "master", StringComparison.OrdinalIgnoreCase) => "Master",
            [_, string side, string direction, ..] when string.Equals(side, "slave", StringComparison.OrdinalIgnoreCase) => $"Slave {direction.ToUpperInvariant()}",
            _ => string.Empty,
        };
    }
}
