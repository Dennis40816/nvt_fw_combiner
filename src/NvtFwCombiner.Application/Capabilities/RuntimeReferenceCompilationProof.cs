using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>
/// Application-owned proof that one reviewed postbuild plan is bound to the
/// exact runtime-reference compilation which selected it.
/// </summary>
public sealed class RuntimeReferenceCompilationProof
{
    private readonly string _compilationFingerprint;
    private readonly string _processorId;
    private readonly string _toolBindingId;

    private RuntimeReferenceCompilationProof(
        string compilationFingerprint,
        string processorId,
        string toolBindingId,
        string selectorToken,
        string planFingerprint)
    {
        _compilationFingerprint = compilationFingerprint;
        _processorId = processorId;
        _toolBindingId = toolBindingId;
        SelectorToken = selectorToken;
        PlanFingerprint = planFingerprint;
    }

    /// <summary>
    /// Creates a proof from one exact compiled composition and the typed
    /// postbuild plan selected for that same run.
    /// </summary>
    public static RuntimeReferenceCompilationProof CreateLegacyPostbuild(
        CompiledComposition composition,
        LegacyCombinerPostbuildCommandPlan plan)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ArgumentNullException.ThrowIfNull(plan);
        RuntimeReferenceReplaceV2CompilationContext context =
            composition.V2Details?.Provenance.Context as
                RuntimeReferenceReplaceV2CompilationContext ??
            throw new ArgumentException(
                "A runtime-reference proof requires an exact runtime-reference compilation.",
                nameof(composition));
        ExternalProcessorInvocation invocation = GetSingleProcessor(composition);
        if (!StringComparer.Ordinal.Equals(
                composition.IcId,
                plan.Profile.IcId) ||
            !StringComparer.Ordinal.Equals(
                invocation.ProcessorId,
                plan.Profile.ProcessorId) ||
            !StringComparer.Ordinal.Equals(
                invocation.ToolBindingId,
                plan.Profile.ToolBindingId))
        {
            throw new ArgumentException(
                "The selected postbuild plan does not match the compiled processor and tool binding.",
                nameof(plan));
        }

        if (context.ResolvedMap.TopologySelection is { } topology &&
            topology.ChipCount != plan.TopologyCount)
        {
            throw new ArgumentException(
                "The selected postbuild plan topology does not match the compiled resolved map.",
                nameof(plan));
        }

        long capacity = composition.Plan.OutputInitialization.Capacity;
        LegacyCombinerPostbuildCommandPlan resolvedPlan =
            LegacyCombinerPostbuildPlanner.CreatePlan(
                plan.Profile,
                plan.Selector,
                plan.TopologyCount);
        if (!StringComparer.Ordinal.Equals(
                LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                    plan,
                    capacity),
                LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                    resolvedPlan,
                    capacity)))
        {
            throw new ArgumentException(
                "The supplied postbuild command plan is not the exact planner-owned topology expansion.",
                nameof(plan));
        }

        LegacyCombinerPostbuildCommandPlan reviewedPlan =
            LegacyCombinerPostbuildPlanner.CreatePlan(
                plan.Profile,
                plan.Selector);
        return new RuntimeReferenceCompilationProof(
            composition.CompilationFingerprint,
            invocation.ProcessorId,
            invocation.ToolBindingId,
            plan.Selector.Token,
            LegacyCombinerPostbuildPlanner.CalculateIntegrityFingerprint(
                reviewedPlan,
                capacity));
    }

    internal IReadOnlyList<string> ValidateAndGetSemanticBindings(
        CompiledComposition composition)
    {
        ArgumentNullException.ThrowIfNull(composition);
        ExternalProcessorInvocation invocation = GetSingleProcessor(composition);
        _ = StringComparer.Ordinal.Equals(
                _compilationFingerprint,
                composition.CompilationFingerprint) &&
            StringComparer.Ordinal.Equals(
                _processorId,
                invocation.ProcessorId) &&
            StringComparer.Ordinal.Equals(
                _toolBindingId,
                invocation.ToolBindingId)
            ? true
            : throw new ArgumentException(
                "The runtime-reference proof is not bound to this exact compiled processor plan.",
                nameof(composition));

        return
        [
            $"postbuild-selector:{SelectorToken}",
            $"postbuild-plan:{PlanFingerprint}",
        ];
    }

    internal RuntimeReferenceCompilationProof BindCapabilityCompilation(
        CompiledComposition source,
        CompiledComposition bound)
    {
        _ = ValidateAndGetSemanticBindings(source);
        ExternalProcessorInvocation invocation = GetSingleProcessor(bound);
        _ = StringComparer.Ordinal.Equals(
                _processorId,
                invocation.ProcessorId) &&
            StringComparer.Ordinal.Equals(
                _toolBindingId,
                invocation.ToolBindingId)
            ? true
            : throw new ArgumentException(
                "Capability binding changed the compiled processor plan.",
                nameof(bound));

        return new RuntimeReferenceCompilationProof(
            bound.CompilationFingerprint,
            _processorId,
            _toolBindingId,
            SelectorToken,
            PlanFingerprint);
    }

    internal string SelectorToken { get; }

    internal string PlanFingerprint { get; }

    private static ExternalProcessorInvocation GetSingleProcessor(
        CompiledComposition composition)
    {
        if (composition.V2Details?.Provenance.Context is not
                RuntimeReferenceReplaceV2CompilationContext)
        {
            throw new ArgumentException(
                "A runtime-reference proof cannot bind another compiler context.",
                nameof(composition));
        }

        CompositionOperation[] processorOperations =
        [
            .. composition.Plan.OrderedOperations.Where(static operation =>
                operation.Kind ==
                    CompositionOperationKind.RunExternalProcessor),
        ];
        return processorOperations.Length == 1
            ? processorOperations[0].ExternalProcessorInvocation!
            : throw new ArgumentException(
                "A postbuild runtime-reference proof requires exactly one compiled external processor.",
                nameof(composition));
    }
}
