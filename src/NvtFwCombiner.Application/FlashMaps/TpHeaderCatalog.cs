namespace NvtFwCombiner.Application.FlashMaps;

/// <summary>Stable TP header/postbuild write section identifiers used by planners and reports.</summary>
public static class TpHeaderSectionIds
{
    /// <summary>CRC/checksum fields in the TP flash header.</summary>
    public const string FlashHeaderCrc = "tp-flash-header-crc";

    /// <summary>FW config backup or FW config/register backup copy area.</summary>
    public const string FirmwareConfigBackup = "tp-fw-config-backup";

    /// <summary>Master TP header copy area.</summary>
    public const string HeaderCopyMaster = "tp-header-copy-master";

    /// <summary>Right/slave-R TP header copy area.</summary>
    public const string HeaderCopyRight = "tp-header-copy-right";

    /// <summary>Left/slave-L TP header copy area.</summary>
    public const string HeaderCopyLeft = "tp-header-copy-left";

    /// <summary>Final TP header copy area.</summary>
    public const string HeaderCopyFinal = "tp-header-copy-final";

    /// <summary>Final TP header backup copy area.</summary>
    public const string HeaderCopyFinalBackup = "tp-header-copy-final-backup";

    /// <summary>Generic TP header copy area when the postbuild source does not name a side.</summary>
    public const string HeaderCopy = "tp-header-copy";

    /// <summary>Right/slave-R TP copy window.</summary>
    public const string WindowCopyRight = "tp-window-copy-right";

    /// <summary>Left/slave-L TP copy window.</summary>
    public const string WindowCopyLeft = "tp-window-copy-left";

    /// <summary>Declared CtrlRAM replacement written by postbuild from a staged BIN.</summary>
    public const string CtrlRamReplacement = "tp-ctrlram-replacement";

    /// <summary>Other declared postbuild copy range.</summary>
    public const string PostbuildCopy = "postbuild-copy";
}

/// <summary>One TP header/write section category used for allowed writes and report grouping.</summary>
public sealed class TpHeaderSection
{
    /// <summary>Creates a TP header/write section category.</summary>
    public TpHeaderSection(string sectionId, string displayName, int priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        SectionId = sectionId;
        DisplayName = displayName;
        Priority = priority;
    }

    /// <summary>Stable machine-readable section id.</summary>
    public string SectionId { get; }

    /// <summary>Human-facing label for reports.</summary>
    public string DisplayName { get; }

    /// <summary>Priority used when overlapping allowed write declarations normalize to one label.</summary>
    public int Priority { get; }
}

/// <summary>TP header/write category catalog shared by postbuild planning and report rendering.</summary>
public static partial class TpHeaderCatalog
{
    private static readonly TpHeaderSection[] Sections =
    [
        Section(TpHeaderSectionIds.FlashHeaderCrc, "TP flash header / CRC fields", 100),
        Section(TpHeaderSectionIds.HeaderCopyMaster, "Header copy / master", 90),
        Section(TpHeaderSectionIds.HeaderCopyRight, "Header copy / slave R", 90),
        Section(TpHeaderSectionIds.HeaderCopyLeft, "Header copy / slave L", 90),
        Section(TpHeaderSectionIds.HeaderCopyFinal, "Header copy / final", 90),
        Section(TpHeaderSectionIds.HeaderCopyFinalBackup, "Header copy / final backup", 90),
        Section(TpHeaderSectionIds.HeaderCopy, "Header copy", 90),
        Section(TpHeaderSectionIds.FirmwareConfigBackup, "FW config backup", 80),
        Section(TpHeaderSectionIds.WindowCopyRight, "TP copy window / slave R", 60),
        Section(TpHeaderSectionIds.WindowCopyLeft, "TP copy window / slave L", 60),
        Section(TpHeaderSectionIds.CtrlRamReplacement, "CtrlRAM replacement", 50),
        Section(TpHeaderSectionIds.PostbuildCopy, "Postbuild copy", 10),
    ];

    private static readonly Dictionary<string, TpHeaderSection> SectionsById = Sections
        .ToDictionary(section => section.SectionId, StringComparer.Ordinal);

    /// <summary>All known TP header/write categories in stable priority order.</summary>
    public static IReadOnlyList<TpHeaderSection> All => Sections;

    /// <summary>Resolves a postbuild command block id into a TP header/write section id.</summary>
    public static string ResolvePostbuildBlockSectionId(string blockId, bool isStagedFileBlock)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blockId);

        return blockId switch
        {
            string value when Contains(value, "fw-config") =>
                TpHeaderSectionIds.FirmwareConfigBackup,
            string value when Contains(value, "final-header-backup") =>
                TpHeaderSectionIds.HeaderCopyFinalBackup,
            string value when Contains(value, "header-refresh-master") || Contains(value, "header-master") =>
                TpHeaderSectionIds.HeaderCopyMaster,
            string value when Contains(value, "header-refresh-right") || Contains(value, "header-right") =>
                TpHeaderSectionIds.HeaderCopyRight,
            string value when Contains(value, "header-refresh-left") || Contains(value, "header-left") =>
                TpHeaderSectionIds.HeaderCopyLeft,
            string value when Contains(value, "header-copy-final") =>
                TpHeaderSectionIds.HeaderCopyFinal,
            string value when Contains(value, "header-copy") || Contains(value, "header") =>
                TpHeaderSectionIds.HeaderCopy,
            string value when Contains(value, "copy-right-window") =>
                TpHeaderSectionIds.WindowCopyRight,
            string value when Contains(value, "copy-left-window") =>
                TpHeaderSectionIds.WindowCopyLeft,
            _ => isStagedFileBlock
                ? TpHeaderSectionIds.CtrlRamReplacement
                : TpHeaderSectionIds.PostbuildCopy,
        };
    }

    /// <summary>Returns the report display label for a section id.</summary>
    public static string GetDisplayName(string sectionId)
    {
        return SectionsById.TryGetValue(sectionId, out TpHeaderSection? section)
            ? section.DisplayName
            : "Postbuild write range";
    }

    /// <summary>Returns the overlap priority for a section id.</summary>
    public static int GetPriority(string sectionId)
    {
        return SectionsById.TryGetValue(sectionId, out TpHeaderSection? section)
            ? section.Priority
            : 0;
    }

    private static TpHeaderSection Section(string sectionId, string displayName, int priority)
    {
        return new TpHeaderSection(sectionId, displayName, priority);
    }

    private static bool Contains(string value, string token)
    {
        return value.Contains(token, StringComparison.OrdinalIgnoreCase);
    }
}
