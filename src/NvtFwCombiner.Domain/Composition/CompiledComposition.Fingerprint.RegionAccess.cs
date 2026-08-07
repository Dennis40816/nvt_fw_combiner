using System.Text;
using NvtFwCombiner.Domain.Firmware;
using static NvtFwCombiner.Domain.Firmware.FirmwareFingerprintWriter;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private static void AppendRegionAccessContract(
        StringBuilder builder,
        CompiledRegionAccessContract contract)
    {
        AppendList(builder, "region-access.requirement", contract.Requirements, AppendRegionAccessRequirement);
        AppendList(builder, "region-access.view", contract.ResolvedViews, AppendResolvedPhysicalView);
    }

    private static void AppendRegionAccessRequirement(
        StringBuilder builder,
        string prefix,
        CompiledRegionAccessRequirement requirement)
    {
        AppendField(builder, $"{prefix}.region-id", requirement.RegionId);
        AppendEnum(builder, $"{prefix}.access", requirement.Access);
        AppendField(builder, $"{prefix}.reason", requirement.Reason);
        AppendStringList(builder, $"{prefix}.allowed-subregion", requirement.AllowedSubregionIds);
        AppendPhysicalRegionChain(builder, $"{prefix}.chain", requirement.GoverningRegionChain);
    }

    private static void AppendResolvedPhysicalView(
        StringBuilder builder,
        string prefix,
        CompiledResolvedPhysicalView view)
    {
        AppendField(builder, $"{prefix}.id", view.ViewId);
        AppendField(builder, $"{prefix}.address-space", view.AddressSpaceId);
        AppendRange(builder, $"{prefix}.range", view.Range);
        AppendPhysicalRegionChain(builder, $"{prefix}.chain", view.GoverningRegionChain);
    }

    private static void AppendPhysicalRegionChain(
        StringBuilder builder,
        string prefix,
        IReadOnlyList<FirmwareRegion> regionChain)
    {
        AppendList(builder, prefix, regionChain, static (target, itemPrefix, region) =>
        {
            AppendField(target, $"{itemPrefix}.region-id", region.RegionId);
            AppendEnum(target, $"{itemPrefix}.write-constraint", region.WriteConstraint);
            AppendInteger(target, $"{itemPrefix}.alignment", region.Alignment);
        });
    }
}
