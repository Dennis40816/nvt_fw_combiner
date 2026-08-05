using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks DP Replace projection to compiler-owned slot identities.</summary>
public sealed class DpReplaceInputSlotProjectionTests
{
    /// <summary>Selection compilation uses the compiler slot identity, never UI or address-space aliases.</summary>
    [Fact]
    public void SelectedGroupMemberKeepsCompiledSlotIdentityDistinctFromAddressSpace()
    {
        var slot = new WorkbenchReplaceInputSlot(
            SlotId: "workbench-dp",
            Title: "DP",
            Description: "synthetic unequal identity",
            IsOptional: true,
            AddressSpaceId: "dp-address-space",
            RegionId: "dp-region",
            CompiledSlotId: "compiled-dp-slot",
            SelectionGroupId: "dp-choice",
            InputRole: WorkbenchReplaceInputRole.Dp);

        string[] selected = DpReplaceInputSlotProjection.GetSelectedCompiledSlotIds(
            [slot],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["workbench-dp"] = "selected.bin",
            });

        Assert.Equal(["compiled-dp-slot"], selected);
        Assert.DoesNotContain(slot.AddressSpaceId, selected);
        Assert.DoesNotContain(slot.SlotId, selected);
    }

    /// <summary>Accepted-session lookup maps the bound address space back to its compiled slot.</summary>
    [Fact]
    public void AcceptedBindingResolvesUnequalCompiledSlotIdentity()
    {
        var binding = new CompiledInputSpaceBinding(
            "dp-address-space",
            "compiled-dp-slot",
            CompiledInputInstancePolicy.Singleton);

        string definitionId = AcceptedAuthoringSessionBinding.ResolveSlotDefinitionId(
            [binding],
            "dp-address-space");

        Assert.Equal("compiled-dp-slot", definitionId);
        Assert.NotEqual(binding.AddressSpaceId, definitionId);
        Assert.Equal(
            "dynamic-session-slot",
            AcceptedAuthoringSessionBinding.ResolveSlotDefinitionId(
                [binding],
                "dp-address-space",
                "dynamic-session-slot"));
    }
}
