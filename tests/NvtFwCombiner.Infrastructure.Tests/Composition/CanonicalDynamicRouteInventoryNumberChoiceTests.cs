using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Composition;

namespace NvtFwCombiner.Infrastructure.Tests.Composition;

/// <summary>Protects the typed General Replace selector-choice projection.</summary>
public sealed class CanonicalDynamicRouteInventoryNumberChoiceTests
{
    /// <summary>Serializable topology facts and their canonical selector choices.</summary>
    public static TheoryData<TopologyRequirementKind, int?, int?, IcNumberInputMode?, string, string>
        TypedChoices => new()
        {
            { TopologyRequirementKind.SingleChip, null, null, IcNumberInputMode.SingleSelector, IcNumberSelectionTokens.SingleChip, "1 IC" },
            { TopologyRequirementKind.ExactCount, 3, 3, IcNumberInputMode.NumericSelector, "3", "3 IC" },
            { TopologyRequirementKind.Cascade, 2, 8, IcNumberInputMode.CascadeSelector, "cascade_2to8", "2–8 IC" },
            { TopologyRequirementKind.Cascade, 2, null, IcNumberInputMode.CascadeSelector, IcNumberSelectionTokens.Cascade, "Cascade" },
            { TopologyRequirementKind.None, null, null, IcNumberInputMode.SingleSelector, IcNumberSelectionTokens.SingleChip, "1 IC" },
            { TopologyRequirementKind.None, null, null, IcNumberInputMode.CascadeSelector, IcNumberSelectionTokens.Cascade, "Cascade" },
        };

    /// <summary>Projects only typed topology facts into UI selector choices.</summary>
    [Theory]
    [MemberData(nameof(TypedChoices))]
    public void GeneralReplaceChoiceProjectsOnlyTypedTopologyFacts(
        TopologyRequirementKind kind,
        int? minimum,
        int? maximum,
        IcNumberInputMode? inputMode,
        string expectedToken,
        string expectedLabel)
    {
        TopologyRequirement requirement = kind switch
        {
            TopologyRequirementKind.SingleChip => TopologyRequirement.RequireSingleChip(),
            TopologyRequirementKind.ExactCount => TopologyRequirement.RequireExactCount(minimum!.Value),
            TopologyRequirementKind.Cascade when maximum is not null =>
                TopologyRequirement.RequireCascade(minimum!.Value, maximum.Value),
            TopologyRequirementKind.Cascade => TopologyRequirement.RequireCascade(),
            TopologyRequirementKind.None => TopologyRequirement.NoTopologyConstraint(),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        CapabilityNumberChoice choice =
            CanonicalDynamicRouteInventory.ProjectGeneralReplaceNumberChoice(
                requirement,
                inputMode);

        Assert.Equal(expectedToken, choice.Token);
        Assert.Equal(expectedLabel, choice.DisplayLabel);
    }

    /// <summary>Fails closed when no typed fact selects one numeric choice.</summary>
    [Fact]
    public void GeneralReplaceChoiceRejectsUnfixedNumericSelector()
    {
        _ = Assert.Throws<InvalidDataException>(() =>
            CanonicalDynamicRouteInventory.ProjectGeneralReplaceNumberChoice(
                TopologyRequirement.NoTopologyConstraint(),
                IcNumberInputMode.NumericSelector));
    }
}
