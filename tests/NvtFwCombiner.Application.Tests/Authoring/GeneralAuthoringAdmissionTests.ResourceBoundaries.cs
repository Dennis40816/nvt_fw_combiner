using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Application.Tests.Authoring;

public sealed partial class GeneralAuthoringAdmissionTests
{
    /// <summary>Resource envelopes reject ambiguous, out-of-range, or over-broad declarations.</summary>
    [Fact]
    public void ResourceLimitModelsRejectInvalidSlotDeclarations()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralSlotLengthLimits("slot", 1, 4, [0, 2]));
        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralSlotLengthLimits("slot", 1, 4, [2, 2]));
        var slot = new GeneralSlotLengthLimits("slot", 1, 4);
        _ = Assert.Throws<ArgumentException>(() => new GeneralResourceLimits(
            1,
            1,
            4,
            4,
            [slot, slot]));
        _ = Assert.Throws<ArgumentException>(() => new GeneralResourceLimits(
            1,
            1,
            3,
            4,
            [slot]));
        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralResourceResolutionResult(null, [null!]));
        GeneralResourceLimits limits = CreateLimits();
        _ = Assert.Throws<ArgumentNullException>(() =>
            new GeneralTrustedParentResourcePolicy(
                (SavedRuleParentIdentity)null!,
                limits));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new GeneralSavedRuleResourcePolicy(
                (SavedRuleExecutionIdentity)null!,
                limits));
        _ = Assert.Throws<ArgumentNullException>(() =>
            new GeneralSavedRuleResourcePolicy(
                (SavedRuleLifecycleSnapshot)null!,
                limits));
    }

    /// <summary>Every discrete-length intersection shape resolves through the same deterministic owner.</summary>
    [Fact]
    public void ResolvesAllDiscreteSlotIntersectionShapes()
    {
        Assert.Equal(
            [2L],
            ResolveSlot(
                new GeneralSlotLengthLimits("slot", 1, 4, [2]),
                new GeneralSlotLengthLimits("slot", 1, 4)).AllowedLengths);
        Assert.Equal(
            [3L],
            ResolveSlot(
                new GeneralSlotLengthLimits("slot", 1, 4),
                new GeneralSlotLengthLimits("slot", 1, 4, [3])).AllowedLengths);
        Assert.Empty(ResolveSlot(
            new GeneralSlotLengthLimits("slot", 1, 4),
            new GeneralSlotLengthLimits("slot", 2, 3)).AllowedLengths);

        GeneralResourceResolutionResult parentOnly =
            GeneralResourceLimitResolver.Resolve(
                new GeneralResourceLimits(4, 16, 3, 8),
                Parent(new GeneralResourceLimits(
                    4,
                    16,
                    8,
                    8,
                    [new GeneralSlotLengthLimits("slot", 1, 4, [1, 2, 4])])),
                savedRule: null);
        Assert.Equal(
            [1L, 2L],
            Assert.Single(parentOnly.EffectiveLimits!.SlotLimits).AllowedLengths);

        static GeneralSlotLengthLimits ResolveSlot(
            GeneralSlotLengthLimits technicalSlot,
            GeneralSlotLengthLimits parentSlot)
        {
            var technical = new GeneralResourceLimits(4, 16, 8, 8, [technicalSlot]);
            var parent = new GeneralResourceLimits(4, 16, 8, 8, [parentSlot]);
            GeneralResourceResolutionResult result = GeneralResourceLimitResolver.Resolve(
                technical,
                Parent(parent),
                savedRule: null);
            Assert.True(result.IsResolved);
            return Assert.Single(result.EffectiveLimits!.SlotLimits);
        }
    }

    /// <summary>Disjoint discrete Parent and technical lengths produce one typed empty-limit result.</summary>
    [Fact]
    public void RejectsEmptyParentDiscreteIntersection()
    {
        var technical = new GeneralResourceLimits(
            4,
            16,
            8,
            8,
            [new GeneralSlotLengthLimits("slot", 1, 4, [1])]);
        var parent = new GeneralResourceLimits(
            4,
            16,
            8,
            8,
            [new GeneralSlotLengthLimits("slot", 1, 4, [4])]);

        GeneralResourceResolutionResult result = GeneralResourceLimitResolver.Resolve(
            technical,
            Parent(parent),
            savedRule: null);

        Assert.False(result.IsResolved);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == GeneralAuthoringIssueCodes.EffectiveLimitsEmpty);
    }

    /// <summary>Saved Rule slot narrowing covers discrete, interval, and undeclared Parent shapes.</summary>
    [Fact]
    public void SavedRuleSlotSubsetChecksEveryCanonicalShape()
    {
        var technical = new GeneralResourceLimits(
            8,
            64,
            64,
            64,
            [new GeneralSlotLengthLimits("slot", 1, 32)]);
        GeneralResourceLimits parentInterval = new(
            8,
            64,
            64,
            64,
            [new GeneralSlotLengthLimits("slot", 4, 16)]);
        GeneralResourceLimits parentDiscrete = new(
            8,
            64,
            64,
            64,
            [new GeneralSlotLengthLimits("slot", 4, 16, [4, 8, 16])]);

        GeneralResourceResolutionResult interval = GeneralResourceLimitResolver.Resolve(
            technical,
            Parent(parentInterval),
            Saved(new GeneralResourceLimits(
                8,
                64,
                64,
                64,
                [new GeneralSlotLengthLimits("slot", 5, 12)])));
        GeneralResourceResolutionResult discretePoint = GeneralResourceLimitResolver.Resolve(
            technical,
            Parent(parentDiscrete),
            Saved(new GeneralResourceLimits(
                8,
                64,
                64,
                64,
                [new GeneralSlotLengthLimits("slot", 8, 8)])));
        GeneralResourceResolutionResult nonPoint = GeneralResourceLimitResolver.Resolve(
            technical,
            Parent(parentDiscrete),
            Saved(new GeneralResourceLimits(
                8,
                64,
                64,
                64,
                [new GeneralSlotLengthLimits("slot", 4, 8)])));
        GeneralResourceResolutionResult undeclared = GeneralResourceLimitResolver.Resolve(
            technical,
            Parent(parentInterval),
            Saved(new GeneralResourceLimits(
                8,
                64,
                64,
                64,
                [new GeneralSlotLengthLimits("other", 4, 8)])));

        Assert.True(interval.IsResolved);
        Assert.True(discretePoint.IsResolved);
        Assert.Contains(
            nonPoint.Issues,
            issue => issue.Code == GeneralAuthoringIssueCodes.SavedRuleBroadensParent);
        Assert.Contains(
            undeclared.Issues,
            issue => issue.Code == GeneralAuthoringIssueCodes.SavedRuleBroadensParent);
    }
}
