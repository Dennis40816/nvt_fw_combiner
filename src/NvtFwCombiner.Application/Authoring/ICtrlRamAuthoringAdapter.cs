using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Host adapter for profile-owned CtrlRAM compilation and display facts.</summary>
internal interface ICtrlRamAuthoringAdapter
{
    CtrlRamInspectionDisplay GetDiscoveryDisplay(
        string icId,
        string number,
        string? basePath);

    CtrlRamAuthoringCompilation Resolve(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        CtrlRamFirmwareVersionDraftState? firmwareVersionEdit,
        IReadOnlyDictionary<string, byte[]>? selectedInputBytes = null);

    bool IsAcceptedCapability(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        CtrlRamFirmwareVersionDraftState? firmwareVersionEdit,
        IReadOnlyDictionary<string, byte[]>? selectedInputBytes,
        ResolvedCapability capability,
        out IReadOnlyDictionary<string, string> expectedPaths,
        out IReadOnlyList<CompositionIssue> issues);
}

internal sealed class CtrlRamAuthoringCompilation
{
    internal CtrlRamAuthoringCompilation(
        ResolvedCapability? capability,
        IReadOnlyDictionary<string, string> expectedPaths,
        IEnumerable<CompositionIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(expectedPaths);
        ArgumentNullException.ThrowIfNull(issues);
        Capability = capability;
        ExpectedPaths = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(expectedPaths, StringComparer.Ordinal));
        Issues = Array.AsReadOnly([.. issues]);
    }

    internal ResolvedCapability? Capability { get; }

    internal IReadOnlyDictionary<string, string> ExpectedPaths { get; }

    internal IReadOnlyList<CompositionIssue> Issues { get; }
}
