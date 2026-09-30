using System.Text.Json;
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>A published slice keeps the interaction state every observer bound when it was published.</summary>
public sealed class MemoryCoverageStatePublicationTests
{
    /// <summary>Building logical items again over the same slices keeps each slice's state.</summary>
    [Fact]
    public void LogicalItemConstructionKeepsExistingStates()
    {
        ShellTextResources text = ShellTextResources.For(ShellLanguage.English);
        MemoryCoverageSegmentViewModel first = Slice(0x100, 0x200, text);
        MemoryCoverageSegmentViewModel second = Slice(0x200, 0x300, text);
        MemoryCoverageSegmentViewModel apart = Slice(0x400, 0x500, text);
        MemoryCoverageInteractionState head = first.Interaction;
        MemoryCoverageInteractionState lone = apart.Interaction;
        _ = new MemoryCoverageLogicalItemViewModel("item", [first, second, apart], text);
        Assert.Same(head, first.Interaction);
        Assert.Same(head, second.Interaction);
        Assert.Same(lone, apart.Interaction);
        _ = new MemoryCoverageLogicalItemViewModel("item", [first, second], text);
        Assert.Same(head, first.Interaction);
        Assert.Same(head, second.Interaction);
    }

    /// <summary>Replace (CtrlRAM) builds its logical items before a bar can bind the published slices.</summary>
    [AvaloniaFact]
    public async Task ReplacePublishesSlicesWithTheirFinalStates()
    {
        using var workspace = TempWorkspace.Create("state-publication-replace");
        MainWindowViewModel shell = CreateShell(workspace);
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectCase("ctrlram-replace", "nt51950-fw200-single-auto-prj-676-20260717");
        List<(MemoryCoverageSegmentViewModel Slice, MemoryCoverageInteractionState State)> published = Watch(shell.Replace.ReplaceCoverageSegments);
        UiLaunchOptions options = UiLaunchOptions.Parse(["--workflow", "ctrlram-replace", "--ic", "NT51950", "--ic-num", "single",
            "--base", PathFor(fixture, "expected-output"), "--ctrlram", "replace-ctrlram-nf=" + PathFor(fixture, "postbuild-nf-ctrlram")]);
        await MainWindow.ApplyCtrlRamLaunchAsync(shell, options.CtrlRam!, TestContext.Current.CancellationToken);
        Assert.Contains(shell.Replace.ReplaceCoverageSegments, static slice => slice.ContentRole == MemoryContentRole.CtrlRam);
        Assert.NotEmpty(shell.Replace.ReplaceCoverageGroups);
        AssertStatesKept(published);
    }

    /// <summary>Merge builds its logical items before a bar can bind the published slices.</summary>
    [AvaloniaFact]
    public async Task MergePublishesSlicesWithTheirFinalStates()
    {
        using var workspace = TempWorkspace.Create("state-publication-merge");
        MainWindowViewModel shell = CreateShell(workspace);
        JsonElement golden = CanonicalGoldenTestData.LoadDirectCase("ab-merge", "nt51929-ab-t05-d06");
        List<(MemoryCoverageSegmentViewModel Slice, MemoryCoverageInteractionState State)> published = Watch(shell.Merge.MergeCoverageSegments);
        await MainWindow.ApplyAbMergeLaunchAsync(shell, new AbMergeLaunchRequest("NT51929", "single",
            PathFor(golden, "dp-ab-input"), PathFor(golden, "tp-a-input"), PathFor(golden, "tp-b-input")),
            TestContext.Current.CancellationToken);
        Assert.NotEmpty(shell.Merge.MergeCoverageRows);
        AssertStatesKept(published);
    }

    private static List<(MemoryCoverageSegmentViewModel Slice, MemoryCoverageInteractionState State)> Watch(
        ResettableObservableCollection<MemoryCoverageSegmentViewModel> collection)
    {
        var published = new List<(MemoryCoverageSegmentViewModel Slice, MemoryCoverageInteractionState State)>();
        collection.CollectionChanged += (_, _) =>
        {
            published.Clear();
            published.AddRange(collection.Select(static slice => (slice, slice.Interaction)));
        };
        return published;
    }

    private static void AssertStatesKept(List<(MemoryCoverageSegmentViewModel Slice, MemoryCoverageInteractionState State)> published)
    {
        Assert.NotEmpty(published);
        Assert.All(published, entry => Assert.Same(entry.State, entry.Slice.Interaction));
    }

    private static MemoryCoverageSegmentViewModel Slice(long start, long end, ShellTextResources text)
    {
        return new MemoryCoverageSegmentViewModel($"flash [0x{start:X},0x{end:X})", "Normal", "detail",
            MemoryCoverageFillRole.CtrlRamNormal, end - start, text: text, regionGroup: ReplaceRegionGroup.Master,
            rangeStart: start, rangeEndExclusive: end, contentRole: MemoryContentRole.CtrlRam, addressSpaceId: "flash");
    }

    private static string PathFor(JsonElement fixture, string id)
    {
        return CanonicalGoldenTestData.ArtifactPath(fixture.GetProperty("artifacts").EnumerateArray()
            .Single(artifact => artifact.GetProperty("artifactId").GetString() == id));
    }

    private static MainWindowViewModel CreateShell(TempWorkspace workspace)
    {
        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(),
            loadPolicy: null, localStateDirectory: IsolatedLocalState.CreateDirectory(), configurationPath: workspace.PathFor("format.json"));
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke", host, static authoring => authoring);
        return PresentationTestHost.PublishCanonicalCatalog(services, ShellViewModelFactory.Create(services, ShellLanguage.English));
    }
}
