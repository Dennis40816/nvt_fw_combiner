// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text.Json;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>The output map receives only facts the accepted memory layout publishes.</summary>
public sealed class OutputMapProjectionTests
{
    /// <summary>A region is cut only where its declared access changes.</summary>
    [Fact]
    public void AccessSplitsARegionOnlyWhereTheDeclarationChanges()
    {
        var region = new ByteRange(0x100, 0x100);
        Assert.Equal([(region, false)], OutputMapProjection.SplitByAccess(region, []));
        Assert.Equal([(region, false)], OutputMapProjection.SplitByAccess(region, [new ByteRange(0, 0x100)]));
        Assert.Equal([(region, true)], OutputMapProjection.SplitByAccess(region, [new ByteRange(0, 0x400)]));
        Assert.Equal(
            [(region, true)],
            OutputMapProjection.SplitByAccess(region, [new ByteRange(0x100, 0x80), new ByteRange(0x180, 0x80)]));
        Assert.Equal(
            [(new ByteRange(0x100, 0x40), false), (new ByteRange(0x140, 0x40), true), (new ByteRange(0x180, 0x80), false)],
            OutputMapProjection.SplitByAccess(region, [new ByteRange(0x140, 0x40)]));
    }

    /// <summary>An accepted Standard merge publishes its writes, its three files and a contiguous region list.</summary>
    [Fact]
    public void AcceptedStandardMergePublishesItsWritesAndFiles()
    {
        using var golden = StandardMergeGoldenManifest.Load();
        JsonElement inputs = golden.CaseByIc("51928").GetProperty("inputs");
        Dictionary<string, string> files = new[]
        {
            CompositionAddressSpaceIds.DpInput, CompositionAddressSpaceIds.TpInput, CompositionAddressSpaceIds.LdcInput,
        }.ToDictionary(id => id, id => golden.ManifestPath(inputs.GetProperty(id)), StringComparer.Ordinal);
        CompositionHostServices host = CompositionHostServices.Create(IsolatedLocalState.CreateDirectory());
        CompiledAuthoringSessionPreparation prepared = host.StandardMergeAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.StandardMerge),
            "NT51928",
            [.. files.Select(file => new CompiledAuthoringSelectedInput(file.Key, file.Value, File.ReadAllBytes(file.Value)))]);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues.Select(issue => issue.Message)));
        ResolvedCapability capability = prepared.Snapshot!.ExactCapability!;
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(capability, prepared.Snapshot, capability.CompiledComposition);

        OutputMapModel map = Assert.IsType<OutputMapModel>(OutputMapProjection.Create(layout, prepared.Snapshot,
            capability.CompiledComposition.Plan.OutputSpaceId, ShellTextResources.For(ShellLanguage.English)));

        AssertRegionsPartitionTheOutput(map);
        Assert.Empty(map.CrcAnnotations);
        Assert.Null(map.Highlight);
        Assert.Equal(
            files.Values.Select(Path.GetFileName).Order(StringComparer.Ordinal),
            map.Sources.Select(source => source.FileName).Order(StringComparer.Ordinal));
        Assert.All(map.Blocks, block => Assert.True(block.TargetRange.EndExclusive <= map.OutputLength));
        Assert.Equal(map.Blocks.Count, map.Blocks.Select(block => block.Key).Distinct(StringComparer.Ordinal).Count());

        // Every range the layout attributes to a selected file is drawn from that same file.
        MemoryLayoutSegment[] written =
            [.. layout.AfterSegments.Where(segment => segment.SourceSlotId is not null && segment.IsPlannedWrite)];
        Assert.NotEmpty(written);
        Assert.Contains(map.Blocks, block => block.SourceRange is not null);
        foreach (MemoryLayoutSegment segment in written)
        {
            OutputMapBlock last = map.Blocks.Last(block => block.TargetRange.Overlaps(segment.Range));
            OutputMapSource source = Assert.Single(map.Sources, candidate => candidate.Key == last.SourceKey);
            Assert.Equal(Path.GetFileName(files[segment.SourceSlotId!]), source.FileName);
            if (last.SourceRange is { } read)
            {
                Assert.Equal(last.TargetRange.Length, read.Length);
                Assert.True(read.EndExclusive <= source.Length);
            }
        }
    }

    /// <summary>A merge that composes in a scratch space gets no partial map.</summary>
    [Fact]
    public void ScratchSpaceCompositionPublishesNoMap()
    {
        CompositionHostServices host = CompositionHostServices.Create(IsolatedLocalState.CreateDirectory());
        byte[] tp = new byte[0x40000];
        tp[0x36001] = 0xFF;
        tp[0x36017] = 1;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(tp, 0x36FFC);
        CompiledAuthoringSessionPreparation prepared = host.AbMergeAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51932", null,
            [new(CompositionAddressSpaceIds.TpAInput, "a.bin", tp), new(CompositionAddressSpaceIds.TpBInput, "b.bin", tp)],
            AbMergeDpMode.Dummy);
        Assert.True(prepared.Succeeded, string.Join(" | ", prepared.Issues.Select(issue => issue.Message)));
        ResolvedCapability capability = prepared.Snapshot!.ExactCapability!;
        string output = capability.CompiledComposition.Plan.OutputSpaceId;
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(capability, prepared.Snapshot, capability.CompiledComposition);
        Assert.Contains(layout.Steps.Values, step => step.TargetSpaceId != output);

        Assert.Null(OutputMapProjection.Create(layout, prepared.Snapshot, output,
            ShellTextResources.For(ShellLanguage.English)));
    }

    /// <summary>Unknown access and an undecoded version are not described as writable or as a value.</summary>
    [AvaloniaFact]
    public void UnknownAccessAndValueAreNotClaimed()
    {
        var model = new OutputMapModel("output", 0x1000,
            regions: [new OutputMapRegion("whole", "Data", new ByteRange(0, 0x1000), IsReadOnly: null)],
            version: new OutputMapVersion("DP version", new ByteRange(0x10, 4), ValueText: null));
        using var host = new OutputMapTestHost(model);

        string name = AutomationProperties.GetName(host.Region("whole"))!;
        Assert.StartsWith("Data · output [", name, StringComparison.Ordinal);
        Assert.EndsWith(")", name, StringComparison.Ordinal);
        Assert.DoesNotContain("read", name, StringComparison.Ordinal);

        Border marker = Assert.Single(host.Bar.Children.OfType<Border>(), b => b.Classes.Contains("outputMapVersion"));
        string[] version = AutomationProperties.GetName(marker)!.Split(" · ");
        Assert.Equal(2, version.Length);
        Assert.Equal("DP version", version[0]);
    }

    private static void AssertRegionsPartitionTheOutput(OutputMapModel map)
    {
        long next = 0;
        foreach (OutputMapRegion region in map.Regions)
        {
            Assert.Equal(next, region.Range.Start);
            Assert.False(string.IsNullOrWhiteSpace(region.Name));
            next = region.Range.EndExclusive;
        }

        Assert.Equal(map.OutputLength, next);
    }
}
