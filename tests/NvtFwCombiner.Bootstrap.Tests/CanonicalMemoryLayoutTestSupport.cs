using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class CanonicalMemoryLayoutTestSupport
{
    internal static MemoryLayoutSnapshot PrepareDpReplace(
        string icId,
        int capacity)
    {
        CompiledAuthoringSelectionSnapshot discovery =
            BootstrapTestHost.Services.DpReplaceAuthoring.GetAuthoringSnapshot(
                icId,
                [],
                new Dictionary<string, FileStamp>(StringComparer.Ordinal),
                new AuthoringRevision(1));
        CompiledAuthoringInputBinding replacement = discovery.InputBindings.Single(static binding =>
            !StringComparer.Ordinal.Equals(
                binding.AddressSpaceId,
                CompositionAddressSpaceIds.ReferenceBase) &&
            !StringComparer.Ordinal.Equals(
                binding.AddressSpaceId,
                CompositionAddressSpaceIds.LdcReplacement));
        var session = new AuthoringSessionState(ExperienceIds.DpReplace);
        CompiledAuthoringSessionPreparation prepared =
            BootstrapTestHost.Services.DpReplaceAuthoring.PrepareSession(
                session,
                icId,
                [
                    new CompiledAuthoringSelectedInput(
                        CompositionAddressSpaceIds.ReferenceBase,
                        "reference.bin",
                        new byte[capacity]),
                    new CompiledAuthoringSelectedInput(
                        replacement.AddressSpaceId,
                        "replacement.bin",
                        new byte[capacity]),
                ]);
        Assert.True(
            prepared.Succeeded,
            string.Join(" | ", prepared.Issues.Select(static issue => issue.Message)));
        ActiveSessionSnapshot accepted = prepared.Snapshot!;
        return MemoryLayoutProjector.Project(
            accepted.ExactCapability!,
            accepted,
            accepted.ExactCapability!.CompiledComposition);
    }
}
