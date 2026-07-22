using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Typed profile-owned guidance rendered beside a workbench firmware slot.</summary>
public enum WorkbenchFirmwareSlotHint
{
    /// <summary>No additional profile guidance.</summary>
    None,

    /// <summary>The DP container includes both Initial Code and LDC.</summary>
    InitialCodeAndLdc,
}

public static partial class WorkbenchCompositionService
{
    /// <summary>Gets guidance from the registered workflow profile rather than UI-owned IC rules.</summary>
    public static WorkbenchFirmwareSlotHint GetFirmwareSlotHint(string icId, string slotId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);

        string normalizedIc = IcSupportCatalog.NormalizeIcId(icId);
        string? profileId = slotId switch
        {
            WorkbenchSlotIds.MergeDp =>
                BuiltInV2RegistrationRegistry.StandardMergeByIc.GetValueOrDefault(normalizedIc)?.ProfileId,
            WorkbenchSlotIds.ReplaceDp =>
                BuiltInV2RegistrationRegistry.DpReplaceByIc.Value.GetValueOrDefault(normalizedIc)?.ProfileId,
            _ => null,
        };

        return profileId is "nt51951-standard-merge-dp-perspective" or "nt51951-dp-replace-dp-perspective"
            ? WorkbenchFirmwareSlotHint.InitialCodeAndLdc
            : WorkbenchFirmwareSlotHint.None;
    }
}
