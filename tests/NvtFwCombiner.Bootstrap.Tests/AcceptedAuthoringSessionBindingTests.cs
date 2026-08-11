using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Locks accepted-session bindings to compiler-owned slot identities.</summary>
public sealed class AcceptedAuthoringSessionBindingTests
{
    /// <summary>Accepted-session lookup maps the bound address space back to its compiled slot.</summary>
    [Fact]
    public void AcceptedBindingResolvesUnequalCompiledSlotIdentity()
    {
        var binding = new CompiledInputSpaceBinding(
            "dp-address-space",
            "compiled-dp-slot",
            CompiledInputInstancePolicy.Singleton);

        string definitionId = AcceptedSessionExecutionInputs.ResolveSlotDefinitionId(
            [binding],
            "dp-address-space");

        Assert.Equal("compiled-dp-slot", definitionId);
        Assert.NotEqual(binding.AddressSpaceId, definitionId);
        Assert.Equal(
            "dynamic-session-slot",
            AcceptedSessionExecutionInputs.ResolveSlotDefinitionId(
                [binding],
                "dp-address-space",
                "dynamic-session-slot"));
    }
}
