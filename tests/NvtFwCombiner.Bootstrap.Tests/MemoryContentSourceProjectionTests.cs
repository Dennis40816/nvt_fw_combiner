using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Content attribution consumes real compiled AB and CtrlRAM contracts without changing execution.</summary>
public sealed class MemoryContentSourceProjectionTests
{
    /// <summary>The canonical 950 inputs produce five contiguous BIN runs while three processor imports retain their writers.</summary>
    [Fact]
    public async Task Nt51950AbPostbuildImportsRetainTpContentAndExactOperationsAsync()
    {
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ab-merge", "nt51950-ab-boe-d82t80");
        JsonElement[] artifacts = [.. fixture.GetProperty("artifacts").EnumerateArray()];
        string[] inputIds = [CompositionAddressSpaceIds.DpAbInput, CompositionAddressSpaceIds.TpAInput, CompositionAddressSpaceIds.TpBInput];
        Dictionary<string, string> paths = inputIds.ToDictionary(id => id,
            id => CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact => artifact.GetProperty("artifactId").GetString() == id)));
        using TempWorkspace workspace = TempWorkspace.Create("memory-content-ab");
        var environment = new ExternalProcessorEnvironmentLoader(RepositoryPaths.FromRepositoryRoot("external-tools"));
        CompositionHostServices host = CompositionHostServices.Create(environment, loadPolicy: null,
            configurationPath: workspace.PathFor("missing-format.json"));
        CompiledAuthoringSessionPreparation prepared = await host.AbMergeAuthoring.PrepareSessionAsync(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51950", "single",
            [.. paths.Select(pair => new CompiledAuthoringSelectedInput(pair.Key, pair.Value, File.ReadAllBytes(pair.Value)))],
            AbMergeDpMode.Normal, TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues.Select(issue => issue.Message)));
        ActiveSessionSnapshot session = prepared.Snapshot!;
        ResolvedCapability capability = session.ExactCapability!;
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(capability, session, capability.CompiledComposition);
        Assert.All(layout.AfterSegments, segment => Assert.NotNull(segment.ContentSource));
        MemoryLayoutSegment[] imports = [.. layout.AfterSegments.Where(segment => segment.SourceSpaceId == "ab-combiner-work")];
        Assert.Equal(3, imports.Length);
        Assert.All(imports, segment =>
        {
            Assert.Equal(4, segment.Range.Length);
            Assert.Equal(CompositionAddressSpaceIds.TpBInput, segment.ContentSource!.SourceSpaceId);
            Assert.Null(segment.SourceSlotId);
            Assert.Equal("ab-combiner-work", segment.ContributingOperations[^1].SourceSpaceId);
            Assert.All(segment.ContributingOperations, operation =>
                Assert.Contains(capability.CompiledComposition.Plan.OrderedOperations, candidate => ReferenceEquals(candidate, operation)));
        });
        Assert.Equal(
            [new ByteRange(0, 0xA000), new ByteRange(0xA000, 0x2D000), new ByteRange(0x37000, 0x13000),
             new ByteRange(0x4A000, 0x2D000), new ByteRange(0x77000, 0x9000)],
            ContentRuns(layout));
        MemoryLayoutContentSource a = layout.AfterSegments.First(segment => segment.SourceSpaceId == CompositionAddressSpaceIds.TpAInput).ContentSource!;
        MemoryLayoutContentSource b = imports[0].ContentSource!;
        Assert.Equal(paths[CompositionAddressSpaceIds.TpAInput], paths[CompositionAddressSpaceIds.TpBInput]);
        Assert.Equal(a.ArtifactIdentity, b.ArtifactIdentity);
        Assert.NotEqual(a.SourceSlotId, b.SourceSlotId);
    }

    /// <summary>Real CtrlRAM routes retain distinct replacement/reference identities and every original writer/range/group.</summary>
    [Theory]
    [InlineData("NT51950", "nt51950-fw200-single-auto-prj-676-20260717", "postbuild-nf-ctrlram")]
    [InlineData("NT51951", "nt51951-fw200-single-auto-prj-695-20260718", "nf-ctrlram-input")]
    public void CtrlRamReplaceSeparatesReplacementBinFromPreservedReference(string ic, string caseId, string nfArtifact)
    {
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", caseId);
        JsonElement[] artifacts = [.. fixture.GetProperty("artifacts").EnumerateArray()];
        string basePath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact => artifact.GetProperty("artifactId").GetString() == "expected-output"));
        string nfPath = CanonicalGoldenTestData.ArtifactPath(artifacts.Single(artifact => artifact.GetProperty("artifactId").GetString() == nfArtifact));
        var paths = new Dictionary<string, string> { [CompositionSlotIds.ReplaceBase] = basePath, ["replace-ctrlram-nf"] = nfPath };
        Dictionary<string, byte[]> bytes = paths.ToDictionary(pair => pair.Key, pair => File.ReadAllBytes(pair.Value));
        CtrlRamAuthoringSessionPreparation prepared = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.CtrlRamReplace), ic, "single", paths, bytes);
        ActiveSessionSnapshot session = Assert.IsType<ActiveSessionSnapshot>(prepared.AcceptedSession);
        ResolvedCapability capability = session.ExactCapability!;
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(capability, session, capability.CompiledComposition);
        MemoryLayoutContentSource reference = Assert.IsType<MemoryLayoutContentSource>(layout.BeforeSegments[0].ContentSource);
        MemoryLayoutSegment[] kept = [.. layout.AfterSegments.Where(segment => segment.Disposition == MemoryWorkflowDisposition.Kept)];
        MemoryLayoutSegment[] written = [.. layout.AfterSegments.Where(segment => segment.Disposition == MemoryWorkflowDisposition.WillReplace)];
        Assert.NotEmpty(kept);
        Assert.NotEmpty(written);
        Assert.All(kept, segment => Assert.Equal(reference.ArtifactIdentity, segment.ContentSource?.ArtifactIdentity));
        Assert.All(written.Where(segment => segment.SourceSlotId is not null), segment =>
        {
            MemoryLayoutContentSource source = Assert.IsType<MemoryLayoutContentSource>(segment.ContentSource);
            Assert.NotEqual(reference.ArtifactIdentity, source.ArtifactIdentity);
            Assert.Equal(segment.SourceSlotId, source.SourceSlotId);
            Assert.Equal(segment.SourceSpaceId, source.SourceSpaceId);
            Assert.NotEmpty(segment.LogicalCoverageGroupId);
        });
        Assert.Equal(layout.Capacity, layout.AfterSegments.Sum(segment => segment.Range.Length));
        Assert.All(layout.AfterSegments, segment => Assert.All(segment.ContributingOperations, operation =>
            Assert.Contains(capability.CompiledComposition.Plan.OrderedOperations, candidate => ReferenceEquals(candidate, operation))));
    }

    private static IEnumerable<ByteRange> ContentRuns(MemoryLayoutSnapshot layout)
    {
        MemoryLayoutSegment first = layout.AfterSegments[0];
        long start = first.Range.Start;
        long end = first.Range.EndExclusive;
        string? identity = first.ContentSource?.ArtifactIdentity;
        foreach (MemoryLayoutSegment segment in layout.AfterSegments.Skip(1))
        {
            if (identity is not null && identity == segment.ContentSource?.ArtifactIdentity && end == segment.Range.Start)
            {
                end = segment.Range.EndExclusive;
                continue;
            }

            yield return ByteRange.FromStartEndExclusive(start, end);
            start = segment.Range.Start;
            end = segment.Range.EndExclusive;
            identity = segment.ContentSource?.ArtifactIdentity;
        }

        yield return ByteRange.FromStartEndExclusive(start, end);
    }
}
