using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Canonical AB route facts survive projection into the actual DP card template.</summary>
public sealed class MemoryCoverageDpFieldTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
    /// <summary>Both banks fold the DP-owned field and preserve its exact output address.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbDpCardIncludesDeclaredFieldInBothBanks(bool chinese)
    {
        MainWindowViewModel shell = await CreateAbCtrlRamReadyAsync(AbCtrlRamBankSelection.Both);
        if (chinese) { shell.SelectedLanguage = "Traditional Chinese"; }
        foreach (bool bankB in new[] { false, true })
        {
            shell.Replace.IsViewingCtrlRamBankB = bankB;
            long start = bankB ? 0x40000 : 0;
            MemoryCoverageSegmentViewModel dp = Assert.Single(shell.Replace.CtrlRamOverview,
                item => item.ContentRole == MemoryContentRole.Dp);
            Assert.Equal(start, dp.RangeStart);
            Assert.Equal(start + 0x7000, dp.RangeEndExclusive);
            string expected = bankB ? "flash [0x4401A,0x4401D)" : "flash [0x401A,0x401D)";
            MemoryRegionFact field = Assert.Single(dp.ProcessingFacts, fact => fact.Value == expected);
            Assert.Contains(bankB ? "b-cmi-dp-version" : "a-cmi-dp-version", field.Label, StringComparison.Ordinal);
            Assert.DoesNotContain(shell.Replace.CtrlRamOverview, item =>
                item.RangeStart == start + 0x401A && item.RangeEndExclusive == start + 0x401D);
            var uri = new Uri("avares://NvtFwCombiner.Presentation.Avalonia/Resources/MainWindowSharedTemplates.axaml");
            var resources = new ResourceInclude(uri) { Source = uri };
            Assert.True(resources.TryGetResource("MemoryCoverageRegionCardTemplate", ThemeVariant.Light, out object? template));
            var card = new ContentControl { Content = dp, ContentTemplate = Assert.IsType<IDataTemplate>(template, exactMatch: false) };
            var window = new Window
            {
                Width = 420,
                Height = 650,
                DataContext = shell,
                Content = new Border { Padding = new Thickness(16), Child = card },
            };
            window.Resources.MergedDictionaries.Add(resources);
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                Expander details = Assert.Single(card.GetVisualDescendants().OfType<Expander>());
                Assert.False(details.IsExpanded);
                details.IsExpanded = true;
                Dispatcher.UIThread.RunJobs();
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), block =>
                    block.IsEffectivelyVisible && block.Text == expected && block.Bounds.Width > 0);
                Assert.Contains(card.GetVisualDescendants().OfType<TextBlock>(), block => block.Text == field.Label);
            }
            finally { window.Close(); }
        }
    }
}
