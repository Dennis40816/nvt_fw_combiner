// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using System.Globalization;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests.IcFiles;

internal sealed record NeutralOperation(
    CompositionOperationKind Kind,
    string? SourceSpaceId,
    ByteRange? SourceRange,
    string TargetSpaceId,
    ByteRange TargetRange,
    OverlapPolicy OverlapPolicy,
    ImmutableArray<ByteRange> WriteRanges,
    string? ProcessorId);

internal sealed record PlanDifference(string Path, string? Expected, string? Actual);

internal sealed record RoutePlanComparison(string RouteId, ImmutableArray<PlanDifference> Differences);

internal enum RouteRuntimeInputReason
{
    AuthoringRequest,
    ReferenceFile,
}

internal sealed record RouteRuntimeInputRequirement(string RouteId, RouteRuntimeInputReason Reason);

internal sealed record CanonicalRouteComparisonReport(
    ImmutableArray<RoutePlanComparison> ComparedRoutes,
    ImmutableArray<RouteRuntimeInputRequirement> RequiresRuntimeInput);

internal static class NeutralPlanComparison
{
    internal static CanonicalRouteComparisonReport CompareCanonicalRoutes(
        IReadOnlyList<CanonicalCapabilityPolicyRoute> routes)
    {
        Func<CapabilityRouteIdentity, CanonicalDynamicRoute> resolveDynamic =
            CanonicalDynamicRouteInventory.CreateResolver();
        ImmutableArray<RoutePlanComparison>.Builder comparisons = ImmutableArray.CreateBuilder<RoutePlanComparison>();
        ImmutableArray<RouteRuntimeInputRequirement>.Builder requirements =
            ImmutableArray.CreateBuilder<RouteRuntimeInputRequirement>();
        foreach (CanonicalCapabilityPolicyRoute route in routes)
        {
            CapabilityRouteIdentity identity = route.Identity;
            if (CanonicalDynamicRouteInventory.IsDynamic(identity))
            {
                CanonicalDynamicRoute definition = resolveDynamic(identity);
                requirements.Add(new RouteRuntimeInputRequirement(identity.RouteId,
                    ProjectRuntimeInputReason(definition.CompilationContract.CompilerSemanticId)));
                continue;
            }

            CompositionPlan plan = CanonicalCompiledRouteInventory.Resolve(identity).Composition.Plan;
            comparisons.AddRange(CompareRoutes([identity.RouteId], _ => plan, _ => plan));
        }

        return new CanonicalRouteComparisonReport(comparisons.ToImmutable(), requirements.ToImmutable());
    }

    private static RouteRuntimeInputReason ProjectRuntimeInputReason(string compilerSemanticId)
    {
        return compilerSemanticId switch
        {
            CapabilityDefinitionFingerprint.MapBoundCompilerSemanticId or
            CapabilityDefinitionFingerprint.LogicalOutputCompilerSemanticId => RouteRuntimeInputReason.AuthoringRequest,
            CapabilityDefinitionFingerprint.RuntimeReferenceReplaceCompilerSemanticId or
            CapabilityDefinitionFingerprint.RuntimeBankReplaceCompilerSemanticId => RouteRuntimeInputReason.ReferenceFile,
            _ => throw new InvalidOperationException("Unknown canonical compiler runtime-input contract."),
        };
    }

    // Callers supply the existing route compiler; this harness never selects routes or authoring inputs.
    internal static ImmutableArray<RoutePlanComparison> CompareRoutes(
        IReadOnlyList<string> routeIds,
        Func<string, CompositionPlan> compileExisting,
        Func<string, CompositionPlan> compileCandidate)
    {
        return [.. routeIds.Select(routeId => new RoutePlanComparison(routeId,
            Compare(Project(compileExisting(routeId)), Project(compileCandidate(routeId)))))];
    }

    internal static ImmutableArray<NeutralOperation> Project(CompositionPlan plan)
    {
        return Project(plan.OrderedOperations);
    }

    internal static ImmutableArray<NeutralOperation> Project(IReadOnlyList<CompositionOperation> orderedOperations)
    {
        return [.. orderedOperations.Select(static operation => new NeutralOperation(
            operation.Kind, operation.SourceSpaceId, operation.SourceRange,
            operation.TargetSpaceId, operation.TargetRange, operation.OverlapPolicy,
            [.. operation.DeclaredWriteRanges], operation.ExternalProcessorInvocation?.ProcessorId))];
    }

    internal static ImmutableArray<PlanDifference> Compare(
        IReadOnlyList<NeutralOperation> expected, IReadOnlyList<NeutralOperation> actual)
    {
        List<PlanDifference> differences = [];
        for (int index = 0; index < Math.Max(expected.Count, actual.Count); index++)
        {
            string path = FormattableString.Invariant($"operations[{index}]");
            if (index >= expected.Count)
            {
                differences.Add(new PlanDifference(path + ".operation", null, actual[index].Kind.ToString()));
            }
            else if (index >= actual.Count)
            {
                differences.Add(new PlanDifference(path + ".operation", expected[index].Kind.ToString(), null));
            }
            else
            {
                CompareOperation(expected[index], actual[index], path, differences);
            }
        }

        return [.. differences];
    }

    private static void CompareOperation(
        NeutralOperation expected, NeutralOperation actual, string path, List<PlanDifference> differences)
    {
        AddValue(path + ".kind", expected.Kind, actual.Kind, differences);
        AddValue(path + ".sourceSpaceId", expected.SourceSpaceId, actual.SourceSpaceId, differences);
        CompareRange(path + ".sourceRange", expected.SourceRange, actual.SourceRange, differences);
        AddValue(path + ".targetSpaceId", expected.TargetSpaceId, actual.TargetSpaceId, differences);
        CompareRange(path + ".targetRange", expected.TargetRange, actual.TargetRange, differences);
        AddValue(path + ".overlapPolicy", expected.OverlapPolicy, actual.OverlapPolicy, differences);
        for (int index = 0; index < Math.Max(expected.WriteRanges.Length, actual.WriteRanges.Length); index++)
        {
            CompareRange(FormattableString.Invariant($"{path}.writeRanges[{index}]"),
                index < expected.WriteRanges.Length ? expected.WriteRanges[index] : null,
                index < actual.WriteRanges.Length ? actual.WriteRanges[index] : null,
                differences);
        }

        AddValue(path + ".processorId", expected.ProcessorId, actual.ProcessorId, differences);
    }

    private static void CompareRange(string path, ByteRange? expected, ByteRange? actual, List<PlanDifference> differences)
    {
        if (expected is { } left && actual is { } right)
        {
            AddValue(path + ".start", left.Start, right.Start, differences);
            AddValue(path + ".endExclusive", left.EndExclusive, right.EndExclusive, differences);
        }
        else
        {
            AddValue(path, expected, actual, differences);
        }
    }

    private static void AddValue<T>(string path, T expected, T actual, List<PlanDifference> differences)
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
        {
            differences.Add(new PlanDifference(path, Render(expected), Render(actual)));
        }
    }

    private static string? Render<T>(T value)
    {
        return value is null ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
    }
}
