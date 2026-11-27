// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests.IcFiles;

/// <summary>Verifies the neutral comparison mechanism with synthetic plans.</summary>
public sealed class NeutralPlanComparisonTests
{
    /// <summary>Verifies every supplied route is compared in order without hiding earlier differences.</summary>
    [Fact]
    public void RouteRunnerPreservesEveryRouteAndItsCompleteDifferenceList()
    {
        CompositionPlan first = Plan(1);
        CompositionPlan second = Plan(2);
        Dictionary<string, CompositionPlan> existing = new(StringComparer.Ordinal)
        {
            ["synthetic-route-a"] = first,
            ["synthetic-route-b"] = second,
        };
        Dictionary<string, CompositionPlan> candidate = new(StringComparer.Ordinal)
        {
            ["synthetic-route-a"] = first,
            ["synthetic-route-b"] = Plan(3),
        };

        ImmutableArray<RoutePlanComparison> reports = NeutralPlanComparison.CompareRoutes(
            ["synthetic-route-b", "synthetic-route-a"], routeId => existing[routeId], routeId => candidate[routeId]);

        Assert.Collection(reports,
            changed =>
            {
                Assert.Equal("synthetic-route-b", changed.RouteId);
                Assert.Equal<PlanDifference>(
                    [new PlanDifference("operations[0].targetRange.endExclusive", "2", "3"),
                     new PlanDifference("operations[0].writeRanges[0].endExclusive", "2", "3")], changed.Differences);
            },
            unchanged =>
            {
                Assert.Equal("synthetic-route-a", unchanged.RouteId);
                Assert.Empty(unchanged.Differences);
            });
    }

    /// <summary>Verifies all requested fields retain the declared operation order.</summary>
    [Fact]
    public void ProjectionRetainsEveryRequestedFieldInInputOrder()
    {
        ImmutableArray<NeutralOperation> projected = NeutralPlanComparison.Project(Operations());

        Assert.Collection(projected,
            copy =>
            {
                Assert.Equal(CompositionOperationKind.CopyRange, copy.Kind);
                Assert.Equal("synthetic-input", copy.SourceSpaceId);
                Assert.Equal(new ByteRange(0, 4), copy.SourceRange);
                Assert.Equal("synthetic-output", copy.TargetSpaceId);
                Assert.Equal(new ByteRange(0x10, 4), copy.TargetRange);
                Assert.Equal(OverlapPolicy.Reject, copy.OverlapPolicy);
                Assert.Equal<ByteRange>([new ByteRange(0x10, 4)], copy.WriteRanges);
                Assert.Null(copy.ProcessorId);
            },
            fill =>
            {
                Assert.Equal(CompositionOperationKind.FillRange, fill.Kind);
                Assert.Null(fill.SourceSpaceId);
                Assert.Null(fill.SourceRange);
                Assert.Equal("synthetic-output", fill.TargetSpaceId);
                Assert.Equal(new ByteRange(0x20, 2), fill.TargetRange);
                Assert.Equal(OverlapPolicy.Reject, fill.OverlapPolicy);
                Assert.Equal<ByteRange>([new ByteRange(0x20, 2)], fill.WriteRanges);
                Assert.Null(fill.ProcessorId);
            },
            processor =>
            {
                Assert.Equal(CompositionOperationKind.RunExternalProcessor, processor.Kind);
                Assert.Null(processor.SourceSpaceId);
                Assert.Null(processor.SourceRange);
                Assert.Equal("synthetic-output", processor.TargetSpaceId);
                Assert.Equal(new ByteRange(0x40, 0x10), processor.TargetRange);
                Assert.Equal(OverlapPolicy.ReplaceExisting, processor.OverlapPolicy);
                Assert.Equal<ByteRange>([new ByteRange(0x40, 1), new ByteRange(0x48, 1)], processor.WriteRanges);
                Assert.Equal("synthetic-processor", processor.ProcessorId);
            });
    }

    /// <summary>Verifies separate projections compare by value, including write ranges.</summary>
    [Fact]
    public void SeparateProjectionsOfTheSameOperationsCompareEqual()
    {
        Assert.Empty(NeutralPlanComparison.Compare(
            NeutralPlanComparison.Project(Operations()), NeutralPlanComparison.Project(Operations())));
    }

    /// <summary>Verifies the two requested mutations produce exactly two differences.</summary>
    [Fact]
    public void OneMovedRangeEndAndOneRemovedOperationReportExactlyThoseDifferences()
    {
        ImmutableArray<NeutralOperation> original = NeutralPlanComparison.Project(Operations());
        ImmutableArray<NeutralOperation> mutated =
            [original[0] with { SourceRange = new ByteRange(0, 5) }, original[1]];

        Assert.Equal<PlanDifference>(
            [new PlanDifference("operations[0].sourceRange.endExclusive", "4", "5"),
             new PlanDifference("operations[2].operation", "RunExternalProcessor", null)],
            NeutralPlanComparison.Compare(original, mutated));
    }

    /// <summary>Verifies comparison reports every differing neutral field.</summary>
    [Fact]
    public void EveryDifferingFieldIsReportedWithoutStoppingAtTheFirst()
    {
        NeutralOperation original = NeutralPlanComparison.Project(Operations())[0];
        NeutralOperation mutated = original with
        {
            Kind = CompositionOperationKind.ReplaceRange,
            SourceSpaceId = "other-input",
            SourceRange = new ByteRange(1, 3),
            TargetSpaceId = "other-output",
            TargetRange = new ByteRange(0x11, 3),
            OverlapPolicy = OverlapPolicy.AllowDeclared,
            WriteRanges = [new ByteRange(0x12, 2)],
            ProcessorId = "other-processor",
        };

        Assert.Equal<PlanDifference>(
            [new PlanDifference("operations[0].kind", "CopyRange", "ReplaceRange"),
             new PlanDifference("operations[0].sourceSpaceId", "synthetic-input", "other-input"),
             new PlanDifference("operations[0].sourceRange.start", "0", "1"),
             new PlanDifference("operations[0].targetSpaceId", "synthetic-output", "other-output"),
             new PlanDifference("operations[0].targetRange.start", "16", "17"),
             new PlanDifference("operations[0].overlapPolicy", "Reject", "AllowDeclared"),
             new PlanDifference("operations[0].writeRanges[0].start", "16", "18"),
             new PlanDifference("operations[0].processorId", null, "other-processor")],
            NeutralPlanComparison.Compare([original], [mutated]));
    }

    /// <summary>Verifies extra operations and ranges are reported with their exact positions.</summary>
    [Fact]
    public void ExtraOperationAndMissingWriteRangeAreReported()
    {
        ImmutableArray<NeutralOperation> original = NeutralPlanComparison.Project(Operations());
        ImmutableArray<NeutralOperation> mutated =
            [original[0] with { WriteRanges = [] }, original[1], original[2], original[1]];

        Assert.Equal<PlanDifference>(
            [new PlanDifference("operations[0].writeRanges[0]", "[16, 20)/4", null),
             new PlanDifference("operations[3].operation", null, "FillRange")],
            NeutralPlanComparison.Compare(original, mutated));
    }

    /// <summary>Verifies operation ordering contributes to comparison.</summary>
    [Fact]
    public void ReorderingOperationsCannotCompareEqual()
    {
        ImmutableArray<NeutralOperation> original = NeutralPlanComparison.Project(Operations());

        Assert.NotEmpty(NeutralPlanComparison.Compare(original, [original[1], original[0], original[2]]));
    }

    private static CompositionOperation[] Operations()
    {
        return
        [
            CompositionOperation.CopyRange("synthetic-copy", 0, "synthetic-input", new ByteRange(0, 4),
                "synthetic-output", new ByteRange(0x10, 4), OverlapPolicy.Reject, "Synthetic comparison fixture"),
            CompositionOperation.FillRange("synthetic-fill", 1, "synthetic-output", new ByteRange(0x20, 2),
                0xA5, OverlapPolicy.Reject, "Synthetic comparison fixture"),
            CompositionOperation.RunExternalProcessor("synthetic-run", 2, "synthetic-output", new ByteRange(0x40, 0x10),
                new ExternalProcessorInvocation("synthetic-processor", "synthetic-binding", [new ByteRange(0x40, 0x10)],
                    [new ByteRange(0x40, 1), new ByteRange(0x48, 1)]),
                OverlapPolicy.ReplaceExisting, "Synthetic comparison fixture"),
        ];
    }

    private static CompositionPlan Plan(long length)
    {
        return new(
            ImageInitialization.Blank("synthetic-output", 0x100, 0xFF),
            [new AddressSpace("synthetic-output", 0x100, AddressSpaceMutability.Mutable)],
            [CompositionOperation.FillRange("synthetic-fill", 0, "synthetic-output", new ByteRange(0, length),
                0xA5, OverlapPolicy.Reject, "Synthetic route runner fixture")]);
    }
}
