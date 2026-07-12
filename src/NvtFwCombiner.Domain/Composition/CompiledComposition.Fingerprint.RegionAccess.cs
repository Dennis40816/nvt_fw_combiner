using System.Text;

namespace NvtFwCombiner.Domain.Composition;

public sealed partial class CompiledComposition
{
    private static void AppendRegionAccessContract(
        StringBuilder builder,
        CompiledRegionAccessContract contract)
    {
        AppendInteger(builder, "region-access.requirement.count", contract.Requirements.Count);
        for (int index = 0; index < contract.Requirements.Count; index++)
        {
            CompiledRegionAccessRequirement requirement = contract.Requirements[index];
            string prefix = FormattableString.Invariant($"region-access.requirement.{index}");
            AppendField(builder, $"{prefix}.region-id", requirement.RegionId);
            AppendEnum(builder, $"{prefix}.access", requirement.Access);
            AppendField(builder, $"{prefix}.reason", requirement.Reason);
            AppendStringList(builder, $"{prefix}.allowed-subregion", requirement.AllowedSubregionIds);
            AppendPhysicalRegionChain(builder, $"{prefix}.chain", requirement.GoverningRegionChain);
        }

        AppendInteger(builder, "region-access.view.count", contract.ResolvedViews.Count);
        for (int index = 0; index < contract.ResolvedViews.Count; index++)
        {
            CompiledResolvedPhysicalView view = contract.ResolvedViews[index];
            string prefix = FormattableString.Invariant($"region-access.view.{index}");
            AppendField(builder, $"{prefix}.id", view.ViewId);
            AppendField(builder, $"{prefix}.address-space", view.AddressSpaceId);
            AppendRange(builder, $"{prefix}.range", view.Range);
            AppendPhysicalRegionChain(builder, $"{prefix}.chain", view.GoverningRegionChain);
        }
    }

    private static void AppendPhysicalRegionChain(
        StringBuilder builder,
        string prefix,
        IReadOnlyList<CompiledPhysicalRegionConstraint> regionChain)
    {
        AppendInteger(builder, $"{prefix}.count", regionChain.Count);
        for (int index = 0; index < regionChain.Count; index++)
        {
            CompiledPhysicalRegionConstraint region = regionChain[index];
            string itemPrefix = FormattableString.Invariant($"{prefix}.{index}");
            AppendField(builder, $"{itemPrefix}.region-id", region.RegionId);
            AppendEnum(builder, $"{itemPrefix}.write-constraint", region.WriteConstraint);
            AppendInteger(builder, $"{itemPrefix}.alignment", region.Alignment);
        }
    }
}
