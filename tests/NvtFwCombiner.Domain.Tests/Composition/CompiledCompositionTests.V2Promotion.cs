using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Domain.Tests.Composition;

public sealed partial class CompiledCompositionTests
{
    /// <summary>Verifies the canonical promotion owner snapshots, orders, and rejects ambiguous blockers.</summary>
    [Fact]
    public void V2PromotionOwnsImmutableBlockerPolicy()
    {
        List<string> evidence = ["z-evidence", "a-evidence"];
        var second = new CompiledProfilePromotionBlocker(
            "second",
            CompiledProfilePromotionBlockerKind.Golden,
            "Golden evidence is pending.",
            evidence);
        var first = new CompiledProfilePromotionBlocker(
            "first",
            CompiledProfilePromotionBlockerKind.HumanReview,
            "Owner review is pending.",
            []);
        var blockers = new List<CompiledProfilePromotionBlocker> { second, first };

        var promotion = new CompiledProfilePromotion(
            CompiledProfilePromotionStage.ExecutableCandidate,
            blockers);
        evidence.Add("late-evidence");
        blockers.Clear();

        Assert.Equal(["first", "second"], promotion.Blockers.Select(static blocker => blocker.BlockerId));
        Assert.Equal(["a-evidence", "z-evidence"], promotion.Blockers[1].EvidenceRefs);
        _ = Assert.Throws<ArgumentException>(() => new CompiledProfilePromotion(
            CompiledProfilePromotionStage.Known,
            [first, first]));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompiledProfilePromotion(
            (CompiledProfilePromotionStage)99,
            []));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new CompiledProfilePromotionBlocker(
            "blocker",
            (CompiledProfilePromotionBlockerKind)99,
            "reason",
            []));
    }
}
