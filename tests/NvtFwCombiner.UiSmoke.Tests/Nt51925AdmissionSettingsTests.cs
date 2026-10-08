using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Settings uses the exact selector admission rather than counting disclosed candidate rows.</summary>
public sealed class Nt51925AdmissionSettingsTests
{
    /// <summary>Adding or removing blocked declarations cannot inflate either available-IC count.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SettingsCountsMatchSelectorsWithAndWithoutNt51925Declarations(bool includeCandidate)
    {
        var catalog = new CanonicalCapabilityCatalog(CompositionHostServices.CreateCanonicalCapabilityCatalogSource());
        Assert.True(catalog.Reload(TestContext.Current.CancellationToken).Succeeded);
        CapabilitySelectorPublication selector = catalog.GetCurrentSnapshot().SelectorPublication;
        CanonicalSupportMatrixSnapshot current = Assert.IsType<CanonicalSupportMatrixSnapshot>(catalog.Query().Matrix);
        CanonicalSupportMatrixRow[] rows = [.. current.Rows.Where(row => includeCandidate || row.Identity.IcId != "NT51925")];
        var matrix = new CanonicalSupportMatrixSnapshot(current.CatalogId, current.CatalogVersion,
            current.SourceSha256, current.ResolutionToken, rows, current.AuthoringSummary);
        var settings = new SettingsViewModel("test", new MatrixQuery(matrix),
            static () => ShellTextResources.For(ShellLanguage.English));

        settings.Refresh(ShellTextResources.For(ShellLanguage.English));

        Assert.Equal(includeCandidate ? 9 : 0, rows.Count(static row => row.Identity.IcId == "NT51925"));
        Assert.DoesNotContain("NT51925", selector.IcIds);
        Assert.Equal(new CapabilityCatalogSummary(10, 10, 10), selector.GetCatalogSummary());
        Assert.Equal(selector.GetCatalogSummary(), matrix.AuthoringSummary);
        Assert.Equal("10", settings.OverviewRows.Single(static row => row.Title == "IC catalog").Value);
        Assert.Equal("10 ICs", settings.OverviewRows.Single(static row => row.Title == "Standard Merge").Value);
        Assert.Equal("10 ICs", settings.CapabilityRows.Single().Value);
        Assert.Equal(10, selector.IcIds.Count(ic => selector.IsWorkflowAuthorable(ic, ExperienceIds.StandardMerge)));
        Assert.Equal(10, selector.IcIds.Count(ic => selector.IsWorkflowAuthorable(ic, ExperienceIds.CtrlRamReplace)));
    }

    private sealed class MatrixQuery(CanonicalSupportMatrixSnapshot matrix) : ICanonicalSupportMatrixQuery
    {
        public CanonicalSupportMatrixQueryResult Query()
        {
            return new(CanonicalSupportMatrixCatalogState.Current, matrix);
        }
    }
}

public sealed partial class ReplaceSurfaceCompatibilityTests
{
    /// <summary>Every declared FW/topology route stays visible as blocked and cannot enable Build.</summary>
    [Theory]
    [InlineData("141", IcNumberSelectionTokens.SingleChip, "1-ic")]
    [InlineData("141", IcNumberSelectionTokens.Cascade, "2-plus-ic")]
    [InlineData("200", IcNumberSelectionTokens.SingleChip, "1-ic")]
    [InlineData("200", IcNumberSelectionTokens.Cascade, "2-plus-ic")]
    public void Nt51925CtrlRamReplaceSelectionAndEveryFwTopologyRemainBlocked(string fw, string number, string countVariant)
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateProductViewModel();
        viewModel.ShowReplaceCommand.Execute(null);
        viewModel.WorkflowSession.SelectedIc = "NT51925";
        viewModel.WorkflowSession.SelectedNumber = number;

        Assert.DoesNotContain("NT51925", viewModel.WorkflowSession.IcChoices);
        Assert.False(viewModel.Replace.CanBuildReplace);
        Assert.False(viewModel.Replace.BuildReplaceCommand.CanExecute(null));
        Assert.False(viewModel.Replace.BuildAvailability.IsAvailable);
        Assert.NotNull(viewModel.Replace.PrimaryBuildBlocker);
        Assert.True(viewModel.HasReplaceBuildBlocker);
        Assert.NotEmpty(viewModel.ReplaceBuildBlockerText);
        Assert.NotNull(viewModel.ReplaceBuildBlockerCard);
        viewModel.OpenSettingsCommand.Execute(null);
        SupportMatrixIcRowViewModel candidate = Assert.Single(viewModel.Settings.SupportMatrix.IcRows,
            static row => row.IcId == "NT51925");
        Assert.Equal(SupportMatrixCellStatus.Blocked,
            candidate.Cells.Single(static cell => cell.WorkflowLabel == "CtrlRAM Replace").Status);
        SupportMatrixRowViewModel[] routes = [.. viewModel.Settings.SupportMatrix.Rows.Where(row =>
            row.IcId == "NT51925" && row.WorkflowId == ExperienceIds.CtrlRamReplace &&
            row.IcCountLabel == ShellTextResources.SupportMatrixIcCountValue(countVariant) && row.MapVariant.Contains($"fw{fw}", StringComparison.Ordinal))];
        Assert.Equal(2, routes.Length);
        Assert.All(routes, static row =>
        {
            Assert.False(row.IsAuthoringAvailable);
            Assert.True(row.HasBlocker);
            Assert.Equal("Candidate", row.PublicationLabel);
            Assert.Equal(CapabilityEvidenceStatus.ContractOnly, row.EvidenceStatus);
        });
    }
}
