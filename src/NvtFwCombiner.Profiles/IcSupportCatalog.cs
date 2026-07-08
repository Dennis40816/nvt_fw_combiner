namespace NvtFwCombiner.Profiles;

/// <summary>Supported workflow identifiers used by IC onboarding policy.</summary>
public static class IcWorkflowIds
{
    /// <summary>Profile-backed normal Standard Merge.</summary>
    public const string StandardMerge = "standard-merge";

    /// <summary>DP Replace workflow.</summary>
    public const string DpReplace = "dp-replace";

    /// <summary>CtrlRAM Replace workflow.</summary>
    public const string CtrlRamReplace = "ctrlram-replace";

    /// <summary>General Merge workbench workflow.</summary>
    public const string GeneralMerge = "general-merge";

    /// <summary>General Replace workbench workflow.</summary>
    public const string GeneralReplace = "general-replace";
}

/// <summary>One IC onboarding entry that defines workflow exposure and alias/family facts.</summary>
public sealed class IcSupportEntry
{
    /// <summary>Creates an IC onboarding entry.</summary>
    public IcSupportEntry(
        string icId,
        IReadOnlyList<string> workflowIds,
        string? standardMergeSourceIcId = null,
        string? ctrlRamPostbuildSourceIcId = null,
        string? notes = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(workflowIds);

        IcId = NormalizeIcId(icId);
        WorkflowIds = [.. workflowIds.Distinct(StringComparer.Ordinal)];
        StandardMergeSourceIcId = string.IsNullOrWhiteSpace(standardMergeSourceIcId)
            ? null
            : NormalizeIcId(standardMergeSourceIcId);
        CtrlRamPostbuildSourceIcId = string.IsNullOrWhiteSpace(ctrlRamPostbuildSourceIcId)
            ? null
            : NormalizeIcId(ctrlRamPostbuildSourceIcId);
        Notes = notes;
    }

    /// <summary>NT-prefixed IC id.</summary>
    public string IcId { get; }

    /// <summary>Workflow ids that may be surfaced for this IC.</summary>
    public IReadOnlyList<string> WorkflowIds { get; }

    /// <summary>Reference IC when Standard Merge is an owner-approved alias.</summary>
    public string? StandardMergeSourceIcId { get; }

    /// <summary>Reference IC when CtrlRAM postbuild is an owner-approved alias.</summary>
    public string? CtrlRamPostbuildSourceIcId { get; }

    /// <summary>Short human-readable evidence note.</summary>
    public string? Notes { get; }

    /// <summary>Returns true when this IC exposes the selected workflow.</summary>
    public bool SupportsWorkflow(string workflowId)
    {
        return WorkflowIds.Contains(workflowId, StringComparer.Ordinal);
    }

    private static string NormalizeIcId(string icId)
    {
        string trimmed = icId.Trim();
        return trimmed.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? $"NT{trimmed[2..]}"
            : $"NT{trimmed}";
    }
}

/// <summary>Single C# entry point for IC workflow support and alias facts.</summary>
public static class IcSupportCatalog
{
    private static readonly string[] DefaultWorkflowIds =
    [
        IcWorkflowIds.StandardMerge,
        IcWorkflowIds.CtrlRamReplace,
        IcWorkflowIds.GeneralMerge,
        IcWorkflowIds.GeneralReplace,
    ];

    private static readonly IcSupportEntry[] Entries =
    [
        Entry("NT51917", standardMergeSourceIcId: "NT51927", ctrlRamPostbuildSourceIcId: "NT51927"),
        Entry("NT51919", standardMergeSourceIcId: "NT51929", ctrlRamPostbuildSourceIcId: "NT51929"),
        Entry("NT51920"),
        Entry("NT51923"),
        Entry("NT51926", notes: "CtrlRAM postbuild category is selected by Common FW version."),
        Entry("NT51927"),
        Entry("NT51928", ctrlRamPostbuildSourceIcId: "NT51927", notes: "NT51928 NB is not covered."),
        Entry("NT51929", ctrlRamPostbuildSourceIcId: "NT51932"),
        Entry("NT51930", notes: "CtrlRAM postbuild category is selected by Common FW version."),
        Entry("NT51931"),
        Entry("NT51932"),
        Entry(
            "NT51950",
            workflowIds: [.. DefaultWorkflowIds, IcWorkflowIds.DpReplace]),
        Entry(
            "NT51951",
            workflowIds: [.. DefaultWorkflowIds, IcWorkflowIds.DpReplace],
            standardMergeSourceIcId: "NT51950",
            ctrlRamPostbuildSourceIcId: "NT51950"),
    ];

    private static readonly Dictionary<string, IcSupportEntry> EntriesByIc = Entries
        .ToDictionary(entry => entry.IcId, StringComparer.Ordinal);

    /// <summary>All current workbench IC entries in stable display order.</summary>
    public static IReadOnlyList<IcSupportEntry> All => Entries;

    /// <summary>Initial IC selected by shell/workbench surfaces.</summary>
    public static string DefaultIcId => "NT51950";

    /// <summary>All selectable workbench IC ids.</summary>
    public static IReadOnlyList<string> IcIds => [.. Entries.Select(entry => entry.IcId)];

    /// <summary>Returns true when the catalog contains an entry for the selected IC.</summary>
    public static bool TryFind(string icId, out IcSupportEntry? entry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        return EntriesByIc.TryGetValue(NormalizeIcId(icId), out entry);
    }

    /// <summary>Returns true when an IC exposes the selected workflow.</summary>
    public static bool SupportsWorkflow(string icId, string workflowId)
    {
        return TryFind(icId, out IcSupportEntry? entry) &&
            entry is not null &&
            entry.SupportsWorkflow(workflowId);
    }

    private static IcSupportEntry Entry(
        string icId,
        IReadOnlyList<string>? workflowIds = null,
        string? standardMergeSourceIcId = null,
        string? ctrlRamPostbuildSourceIcId = null,
        string? notes = null)
    {
        return new IcSupportEntry(
            icId,
            workflowIds ?? DefaultWorkflowIds,
            standardMergeSourceIcId,
            ctrlRamPostbuildSourceIcId,
            notes);
    }

    private static string NormalizeIcId(string icId)
    {
        string trimmed = icId.Trim();
        return trimmed.StartsWith("NT", StringComparison.OrdinalIgnoreCase)
            ? $"NT{trimmed[2..]}"
            : $"NT{trimmed}";
    }
}
