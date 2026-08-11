using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class AbMergeRuntimeAdmissionTests
{
    /// <summary>The Application plan contains only compiled delivery authority and no host input paths.</summary>
    [Fact]
    public void AFlashCodeDeliveryPlanIsPathFreeAndCompiled()
    {
        Assert.True(BootstrapTestHost.Canonical.Compiler.TryCompileAbMerge(
            "NT51929",
            out CompiledComposition? composition,
            out IReadOnlyList<CompositionIssue> issues),
            string.Join(',', issues.Select(static issue => issue.Code)));
        CompiledComposition compiledComposition = Assert.IsType<CompiledComposition>(composition);
        OutputNamingSummary outputNaming = CreateOutputNamingSummary("NT51929");

        CompositionAdditionalDeliveryPlan plan = Assert.IsType<CompositionAdditionalDeliveryPlan>(
            CompositionAdditionalDeliveryPlanner.TryCreate(
                compiledComposition,
                outputNaming,
                CompiledAdditionalDelivery.AbAFlashCodeKind));

        Assert.Equal(compiledComposition.V2Details.ProfileId, plan.ProfileId);
        Assert.Equal(CompiledAdditionalDelivery.AbAFlashCodeKind, plan.DeliveryKind);
        Assert.Equal(compiledComposition.V2Details.AdditionalDeliveries.Single().SourceRange, plan.SourceRange);
    }
}
