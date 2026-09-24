using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Capabilities;

/// <summary>Focused compilation seam required by host-backed General authoring defaults.</summary>
public interface IStandardMergeCompilationPort
{
    /// <summary>Compiles the exact Standard Merge composition for one optional DP length.</summary>
    bool TryCompileStandardMerge(
        string icId,
        long? dpInputLength,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues);
}

/// <summary>Read-only Standard source-envelope metadata lookup; never grants execution authority.</summary>
public interface IStandardMergeMetadataPlanQuery
{
    /// <summary>Returns null only when the IC has no Standard source-envelope declaration.</summary>
    MetadataPlanResolutionResult? ResolveSourceEnvelopeMetadataPlan(string icId, long dpInputLength);
}

internal sealed partial class CanonicalCapabilityCompilerAdapter : IStandardMergeMetadataPlanQuery
{
    MetadataPlanResolutionResult? IStandardMergeMetadataPlanQuery.ResolveSourceEnvelopeMetadataPlan(
        string icId,
        long dpInputLength)
    {
        return ResolveSourceEnvelopeMetadataPlan(icId, dpInputLength);
    }

    bool IStandardMergeCompilationPort.TryCompileStandardMerge(
        string icId,
        long? dpInputLength,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompileStandardMerge(
            icId,
            dpInputLength,
            out composition,
            out issues);
    }

    internal bool TryCompileStandardMerge(
        string icId,
        long? dpInputLength,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        composition = null;
        issues = [];
        return TryGetBuiltInV2StandardMergeCompilation(icId, dpInputLength, out composition, out issues) &&
            composition is not null;
    }

    internal bool TryCompileStandardMerge(
        string icId,
        long? dpInputLength,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        composition = null;
        resolvedCapability = null;
        issues = [];
        return TryGetBuiltInV2StandardMergeCompilation(
                icId, dpInputLength, out composition, out resolvedCapability, out issues) &&
            composition is not null && resolvedCapability is not null;
    }

    internal bool TryCompileStandardMerge(
        string icId,
        long? dpInputLength,
        IReadOnlyCollection<string> selectedInputSlotIds,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out IReadOnlyList<CompositionIssue> issues)
    {
        return TryCompileStandardMerge(
            icId,
            dpInputLength,
            selectedInputSlotIds,
            out composition,
            out _,
            out issues);
    }

    internal bool TryCompileStandardMerge(
        string icId,
        long? dpInputLength,
        IReadOnlyCollection<string> selectedInputSlotIds,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(selectedInputSlotIds);
        composition = null;
        resolvedCapability = null;
        issues = [];
        return TryGetBuiltInV2StandardMergeCompilation(
                icId,
                dpInputLength,
                selectedInputSlotIds,
                out composition,
                out resolvedCapability,
                out issues) &&
            composition is not null;
    }

    internal bool TryCompileStandardMerge(
        string icId,
        ReadOnlyMemory<byte> capturedDp,
        IReadOnlyCollection<string> selectedInputSlotIds,
        [NotNullWhen(true)] out CompiledComposition? composition,
        out ResolvedCapability? resolvedCapability,
        out IReadOnlyList<CompositionIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentNullException.ThrowIfNull(selectedInputSlotIds);
        composition = null;
        resolvedCapability = null;
        issues = [];
        return TryGetBuiltInV2StandardMergeCompilation(
                icId, capturedDp, selectedInputSlotIds,
                out composition, out resolvedCapability, out issues) &&
            composition is not null && resolvedCapability is not null;
    }
}
