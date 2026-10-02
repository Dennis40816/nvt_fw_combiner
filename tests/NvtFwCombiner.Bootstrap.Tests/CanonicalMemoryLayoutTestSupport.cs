using System.Text.Json;
using NvtFwCombiner.TestSupport;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class CanonicalMemoryLayoutTestSupport
{
    internal static (MemoryLayoutSnapshot Layout, string[] ReplacementAddressSpaces) PrepareCtrlRamReplace()
    {
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace", "nt51927-3chip-self-20260705");
        Dictionary<string, string> paths = fixture.GetProperty("artifacts").EnumerateArray()
            .ToDictionary(artifact => artifact.GetProperty("slotId").GetString()!, CanonicalGoldenTestData.ArtifactPath);
        Dictionary<string, byte[]> bytes = paths.ToDictionary(static pair => pair.Key, static pair => File.ReadAllBytes(pair.Value));
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.CtrlRamReplace), "NT51927", "3", paths, bytes);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues.Select(static issue => issue.Message)));
        ActiveSessionSnapshot accepted = prepared.AcceptedSession!;
        return (MemoryLayoutProjector.Project(accepted.ExactCapability!, accepted, accepted.ExactCapability!.CompiledComposition),
            accepted.ExactCapability.CompiledComposition.Plan.OrderedOperations.Where(operation =>
                operation.SourceSpaceId is not null &&
                operation.SourceSpaceId != accepted.ExactCapability.CompiledComposition.Plan.OutputInitialization.ReferenceSpaceId)
                .Select(static operation => operation.SourceSpaceId!)
                .Distinct(StringComparer.Ordinal)
                .ToArray());
    }
}
