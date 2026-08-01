namespace NvtFwCombiner.Application.ExternalTools;

/// <summary>Stable postbuild write-section identifiers used by compiled plans and reports.</summary>
public static class PostbuildWriteSectionIds
{
    /// <summary>TP Header integrity fields.</summary>
    public const string FlashHeaderCrc = "tp-flash-header-crc";
    /// <summary>Firmware-configuration backup.</summary>
    public const string FirmwareConfigBackup = "tp-fw-config-backup";
    /// <summary>Master Header copy.</summary>
    public const string HeaderCopyMaster = "tp-header-copy-master";
    /// <summary>Right Header copy.</summary>
    public const string HeaderCopyRight = "tp-header-copy-right";
    /// <summary>Left Header copy.</summary>
    public const string HeaderCopyLeft = "tp-header-copy-left";
    /// <summary>Final Header copy.</summary>
    public const string HeaderCopyFinal = "tp-header-copy-final";
    /// <summary>Final Header backup copy.</summary>
    public const string HeaderCopyFinalBackup = "tp-header-copy-final-backup";
    /// <summary>Unqualified Header copy.</summary>
    public const string HeaderCopy = "tp-header-copy";
    /// <summary>Right TP window copy.</summary>
    public const string WindowCopyRight = "tp-window-copy-right";
    /// <summary>Left TP window copy.</summary>
    public const string WindowCopyLeft = "tp-window-copy-left";
    /// <summary>Declared CtrlRAM replacement.</summary>
    public const string CtrlRamReplacement = "tp-ctrlram-replacement";
    /// <summary>Other declared postbuild copy.</summary>
    public const string PostbuildCopy = "postbuild-copy";
}

/// <summary>Closed presentation and overlap semantics for declared postbuild write sections.</summary>
public static class PostbuildWriteSectionSemantics
{
    private static readonly IReadOnlyDictionary<string, (string Label, int Priority)> Values =
        new Dictionary<string, (string Label, int Priority)>(StringComparer.Ordinal)
        {
            [PostbuildWriteSectionIds.FlashHeaderCrc] = ("TP flash header / CRC fields", 100),
            [PostbuildWriteSectionIds.HeaderCopyMaster] = ("Header copy / master", 90),
            [PostbuildWriteSectionIds.HeaderCopyRight] = ("Header copy / slave R", 90),
            [PostbuildWriteSectionIds.HeaderCopyLeft] = ("Header copy / slave L", 90),
            [PostbuildWriteSectionIds.HeaderCopyFinal] = ("Header copy / final", 90),
            [PostbuildWriteSectionIds.HeaderCopyFinalBackup] = ("Header copy / final backup", 90),
            [PostbuildWriteSectionIds.HeaderCopy] = ("Header copy", 90),
            [PostbuildWriteSectionIds.FirmwareConfigBackup] = ("FW config backup", 80),
            [PostbuildWriteSectionIds.WindowCopyRight] = ("TP copy window / slave R", 60),
            [PostbuildWriteSectionIds.WindowCopyLeft] = ("TP copy window / slave L", 60),
            [PostbuildWriteSectionIds.CtrlRamReplacement] = ("CtrlRAM replacement", 50),
            [PostbuildWriteSectionIds.PostbuildCopy] = ("Postbuild copy", 10),
        };
    /// <summary>All known stable ids.</summary>
    public static IReadOnlyCollection<string> KnownSectionIds { get; } =
        Array.AsReadOnly([.. Values.Keys]);

    /// <summary>Returns the report label for one declared section.</summary>
    public static string GetDisplayName(string sectionId)
    {
        return Values.TryGetValue(sectionId, out (string Label, int Priority) value)
            ? value.Label
            : "Postbuild write range";
    }

    /// <summary>Returns deterministic overlap precedence for one declared section.</summary>
    public static int GetOverlapPriority(string sectionId)
    {
        return Values.TryGetValue(sectionId, out (string Label, int Priority) value)
            ? value.Priority
            : 0;
    }

    /// <summary>Returns whether the section represents a TP Header structure.</summary>
    public static bool IsHeaderSection(string? sectionId)
    {
        return sectionId is PostbuildWriteSectionIds.FlashHeaderCrc or
            PostbuildWriteSectionIds.HeaderCopyMaster or
            PostbuildWriteSectionIds.HeaderCopyRight or
            PostbuildWriteSectionIds.HeaderCopyLeft or
            PostbuildWriteSectionIds.HeaderCopyFinal or
            PostbuildWriteSectionIds.HeaderCopyFinalBackup or
            PostbuildWriteSectionIds.HeaderCopy;
    }
}
