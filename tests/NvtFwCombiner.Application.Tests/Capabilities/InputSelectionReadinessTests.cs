using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Capabilities;

/// <summary>Tests one Application-owned input-selection readiness projection.</summary>
public sealed class InputSelectionReadinessTests
{
    private const string InitialCode = "initial-code-replacement";
    private const string Ldc = "ldc-replacement";
    private const string LdcUnavailableReason = "Reference length does not include LDC";

    /// <summary>Before Reference inspection every dependent choice is pending and disabled.</summary>
    [Fact]
    public void MissingReferenceDisablesEveryDependentChoiceWithOneTypedNextAction()
    {
        InputSelectionReadinessSnapshot result = InputSelectionReadinessResolver.Resolve(
            new AuthoringRevision(7),
            [CreateNoLdcGroup()],
            [InitialCode],
            unresolvedApplicabilityPrerequisiteSlotId: "reference-base");

        InputSelectionMemberReadiness initial = Member(result, InitialCode);
        InputSelectionMemberReadiness ldc = Member(result, Ldc);
        Assert.Equal(ResolvedChildReadiness.PendingInput, initial.Readiness);
        Assert.Equal(ResolvedChildReadiness.PendingInput, ldc.Readiness);
        Assert.False(initial.CanSelect);
        Assert.False(ldc.CanSelect);
        Assert.Equal(
            new InputSelectionNextAction(
                InputSelectionNextActionKind.LoadArtifactFirst,
                "reference-base"),
            initial.NextAction);
        Assert.Equal(
            new InputSelectionNextAction(
                InputSelectionNextActionKind.LoadArtifactFirst,
                "reference-base"),
            ldc.NextAction);
        Assert.False(result.CanBuild);
        Assert.Equal("reference-base", result.PrimaryIssue!.NextAction.SubjectId);
        Assert.Equal(new AuthoringRevision(7), result.AuthoringRevision);
    }

    /// <summary>A 256-KiB Reference admits Initial Code and explains unavailable LDC.</summary>
    [Fact]
    public void NoLdcVariantMakesLdcNotApplicableAndInitialCodeSatisfiesGroup()
    {
        InputSelectionReadinessSnapshot result = InputSelectionReadinessResolver.Resolve(
            new AuthoringRevision(8),
            [CreateNoLdcGroup()],
            [InitialCode]);

        Assert.True(result.CanBuild);
        Assert.Empty(result.Issues);
        Assert.True(Member(result, InitialCode).CanSelect);
        Assert.False(Member(result, Ldc).CanSelect);
        Assert.Equal(ResolvedChildReadiness.NotApplicable, Member(result, Ldc).Readiness);
        Assert.Equal(LdcUnavailableReason, Member(result, Ldc).Reason);
    }

    /// <summary>A stale or manually supplied LDC selection cannot survive the 256-KiB variant.</summary>
    [Fact]
    public void NoLdcVariantRejectsSelectedLdcWithProfileOwnedReason()
    {
        InputSelectionReadinessSnapshot result = InputSelectionReadinessResolver.Resolve(
            new AuthoringRevision(9),
            [CreateNoLdcGroup()],
            [Ldc]);

        Assert.False(result.CanBuild);
        Assert.False(Member(result, Ldc).CanSelect);
        Assert.Equal(
            InputSelectionReadinessIssueCodes.SelectionNotApplicable,
            result.PrimaryIssue!.Code);
        Assert.Equal(LdcUnavailableReason, result.PrimaryIssue.Message);
    }

    /// <summary>The 512-KiB variant accepts Initial Code, LDC, or both but not neither.</summary>
    [Theory]
    [InlineData(InitialCode)]
    [InlineData(Ldc)]
    [InlineData(InitialCode, Ldc)]
    public void LdcVariantAcceptsEveryDeclaredOneOrTwoMemberSelection(params string[] selected)
    {
        ArgumentNullException.ThrowIfNull(selected);
        InputSelectionReadinessSnapshot result = InputSelectionReadinessResolver.Resolve(
            new AuthoringRevision(10),
            [CreateLdcGroup()],
            selected);

        Assert.True(result.CanBuild);
        Assert.Empty(result.Issues);
        Assert.Equal(selected.Length, Assert.Single(result.Groups).SelectedApplicableCount);
    }

    /// <summary>Neither selected is a typed pending selection rather than an NT51928-specific branch.</summary>
    [Fact]
    public void EmptySelectionUsesGenericGroupCardinality()
    {
        InputSelectionReadinessSnapshot result = InputSelectionReadinessResolver.Resolve(
            new AuthoringRevision(11),
            [CreateLdcGroup()],
            []);

        Assert.False(result.CanBuild);
        Assert.Equal(InputSelectionReadinessIssueCodes.SelectionPending, result.PrimaryIssue!.Code);
        Assert.Equal(
            InputSelectionNextActionKind.SelectMember,
            result.PrimaryIssue.NextAction.Kind);
    }

    /// <summary>Unknown selections fail closed without becoming profile members.</summary>
    [Fact]
    public void UnknownSelectionFailsClosed()
    {
        InputSelectionReadinessSnapshot result = InputSelectionReadinessResolver.Resolve(
            new AuthoringRevision(12),
            [CreateLdcGroup()],
            ["unknown-replacement"]);

        Assert.False(result.CanBuild);
        Assert.Equal(InputSelectionReadinessIssueCodes.SelectionUnknown, result.PrimaryIssue!.Code);
    }

    private static CompiledInputSelectionGroup CreateNoLdcGroup()
    {
        return new CompiledInputSelectionGroup(
            new InputSelectionGroupDefinition(
                "dp-replacement-selection",
                [InitialCode, Ldc],
                minimumSelected: 1,
                maximumSelected: 2),
            [InitialCode],
            [InitialCode],
            maximumSelected: 1,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [Ldc] = LdcUnavailableReason,
            });
    }

    private static CompiledInputSelectionGroup CreateLdcGroup()
    {
        return new CompiledInputSelectionGroup(
            new InputSelectionGroupDefinition(
                "dp-replacement-selection",
                [InitialCode, Ldc],
                minimumSelected: 1,
                maximumSelected: 2),
            [InitialCode, Ldc],
            [InitialCode],
            maximumSelected: 2);
    }

    private static InputSelectionMemberReadiness Member(
        InputSelectionReadinessSnapshot result,
        string slotId)
    {
        return Assert.Single(result.Groups).Members.Single(member =>
            StringComparer.Ordinal.Equals(member.SlotId, slotId));
    }
}
