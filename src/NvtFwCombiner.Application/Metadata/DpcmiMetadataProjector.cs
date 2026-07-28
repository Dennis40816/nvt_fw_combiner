using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Application.Metadata;

/// <summary>Stable logical DPCMI vocabulary shared by declarations and projections.</summary>
public static class DpcmiMetadataContract
{
    /// <summary>One Initial Code structure at CMD1 Page 0 registers 16h-18h.</summary>
    public const string StructureId = "dpcmi";

    /// <summary>First CMD1 Page 0 register in the structure.</summary>
    public const int FirstRegister = 0x16;

    /// <summary>Jira bits 7:0 in register 16h.</summary>
    public const string JiraLowFieldId = "jira-low";

    /// <summary>DP major version byte in register 17h.</summary>
    public const string MajorVersionFieldId = "dp-major";

    /// <summary>DP minor version nibble in register 18h bits 7:4.</summary>
    public const string MinorVersionFieldId = "dp-minor";

    /// <summary>Jira bits 11:8 in register 18h bits 3:0.</summary>
    public const string JiraHighFieldId = "jira-high";
}

/// <summary>Derived DP Version and Jira facts from one successfully decoded DPCMI structure.</summary>
public sealed record DpcmiMetadataFacts(
    byte MajorVersion,
    byte MinorVersion,
    ushort JiraNumber,
    ByteRange ResolvedRange)
{
    /// <summary>Four uppercase hexadecimal digits used by output naming.</summary>
    public string VersionToken =>
        FormattableString.Invariant($"{MajorVersion:X2}{MinorVersion:X2}");

    /// <summary>Technical Jira badge, absent only when the declared value is zero.</summary>
    public string? JiraBadge =>
        JiraNumber == 0 ? null : $"AUTO_PRJ-{JiraNumber}";
}

/// <summary>Projects DPCMI raw fields into their accepted derived semantic facts.</summary>
public static class DpcmiMetadataProjector
{
    /// <summary>Projects exactly one successful DPCMI result.</summary>
    public static bool TryProject(
        MetadataInspectionSnapshot snapshot,
        out DpcmiMetadataFacts facts)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        facts = null!;
        MetadataInspectionResult[] matches =
        [
            .. snapshot.Results.Where(result =>
                StringComparer.Ordinal.Equals(
                    result.PlanEntry.Definition.StructureDefinition.Definition.DefinitionId,
                    DpcmiMetadataContract.StructureId)),
        ];
        if (matches.Length != 1 ||
            matches[0].State != MetadataInspectionState.Value ||
            matches[0].Resolution?.Resolved is not { } resolved)
        {
            return false;
        }

        var values =
            resolved.DecodedStructure.Facts.ToDictionary(
                static fact => fact.FieldId,
                static fact => fact.Value,
                StringComparer.Ordinal);
        if (!TryReadByte(
                values,
                DpcmiMetadataContract.JiraLowFieldId,
                out byte jiraLow) ||
            !TryReadByte(
                values,
                DpcmiMetadataContract.MajorVersionFieldId,
                out byte major) ||
            !TryReadByte(
                values,
                DpcmiMetadataContract.MinorVersionFieldId,
                out byte minor) ||
            !TryReadByte(
                values,
                DpcmiMetadataContract.JiraHighFieldId,
                out byte jiraHigh) ||
            minor > 0x0F ||
            jiraHigh > 0x0F)
        {
            return false;
        }

        facts = new DpcmiMetadataFacts(
            major,
            minor,
            checked((ushort)(jiraLow | (jiraHigh << 8))),
            resolved.LocatorOutcome.ResolvedRange.Range);
        return true;
    }

    private static bool TryReadByte(
        Dictionary<string, FirmwareMetadataValue> values,
        string fieldId,
        out byte value)
    {
        value = 0;
        if (!values.TryGetValue(
                fieldId,
                out FirmwareMetadataValue? metadata) ||
            metadata.Kind != FirmwareMetadataValueKind.UnsignedInteger ||
            metadata.UnsignedIntegerValue is not { } unsigned ||
            unsigned > byte.MaxValue)
        {
            return false;
        }

        value = (byte)unsigned;
        return true;
    }
}
