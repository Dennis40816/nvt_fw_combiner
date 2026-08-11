using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Infrastructure.Composition;

internal static class CtrlRamV2FirmwareVersionAdapter
{
    internal static V2RuntimeReferenceReplaceFirmwareVersionEdit? Create(FirmwareConfigVersionWritePlan? plan)
    {
        return plan is null ? null : new(plan.SourceFirmwareVersionAndBarRange, plan.SourceFirmwareSubVersionRange,
            plan.FirmwareVersion, plan.FirmwareSubVersion,
            CompositionPlanningIssueCodes.ReplaceCtrlRamFirmwareVersionOutputInvalid,
            CompositionPlanningIssueCodes.ReplaceCtrlRamFirmwareVersionOutputMismatch);
    }
}
