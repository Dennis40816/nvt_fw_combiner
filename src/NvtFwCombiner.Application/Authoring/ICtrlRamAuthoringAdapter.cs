using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

#pragma warning disable CS1591 // Infrastructure adapter contracts are not end-user API.

namespace NvtFwCombiner.Application.Authoring;

/// <summary>Host adapter for profile-owned CtrlRAM compilation and display facts.</summary>
public interface ICtrlRamAuthoringAdapter
{
    CtrlRamInspectionDisplay GetDiscoveryDisplay(
        string icId,
        string number,
        string? basePath);

    CtrlRamInspectionDisplay GetDiscoveryDisplayFromAcceptedBase(
        string icId,
        string number,
        ReadOnlyMemory<byte> acceptedBaseBytes);

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

public sealed class CtrlRamAuthoringCompilation
{
    public CtrlRamAuthoringCompilation(
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

    public ResolvedCapability? Capability { get; }

    public IReadOnlyDictionary<string, string> ExpectedPaths { get; }

    public IReadOnlyList<CompositionIssue> Issues { get; }
}
