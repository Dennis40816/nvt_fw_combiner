using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    /// <summary>The Settings header opens the complete current canonical route disclosure.</summary>
    [Fact]
    public void SettingsOpensCanonicalSupportMatrixFromFocusedChild()
    {
        MainWindowViewModel viewModel = PresentationTestHost.CreateViewModel();
        viewModel.OpenSettingsCommand.Execute(null);

        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.SupportMatrix);

        Assert.True(viewModel.Settings.IsSupportMatrixOpen);
        Assert.False(viewModel.Settings.IsOverviewSelected);
        Assert.Equal(78, viewModel.Settings.SupportMatrix.Rows.Count);
        Assert.Equal("78 routes", viewModel.Settings.SupportMatrix.RouteCountLabel);
        Assert.Equal("Current", viewModel.Settings.SupportMatrix.CatalogStateLabel);
        Assert.False(viewModel.Settings.SupportMatrix.HasStatusNotice);
        Assert.Equal(6, viewModel.Settings.SupportMatrix.WorkflowColumns.Count);
        Assert.Equal(10, viewModel.Settings.SupportMatrix.IcRows.Count);
        SupportMatrixIcRowViewModel nt51929 = Assert.Single(
            viewModel.Settings.SupportMatrix.IcRows,
            static row => row.IcId == "NT51929");
        Assert.Equal(
            SupportMatrixCellStatus.ReviewedEvidence,
            nt51929.Cells.Single(static cell => cell.WorkflowLabel == "Standard Merge").Status);
        Assert.Equal(
            "Verified",
            nt51929.Cells.Single(static cell => cell.WorkflowLabel == "Standard Merge").StatusLabel);
        Assert.Equal(
            SupportMatrixCellStatus.ContractOnly,
            nt51929.Cells.Single(static cell => cell.WorkflowLabel == "CtrlRAM Replace").Status);
        Assert.Equal(
            "Defined only",
            nt51929.Cells.Single(static cell => cell.WorkflowLabel == "CtrlRAM Replace").StatusLabel);
        Assert.All(
            viewModel.Settings.SupportMatrix.Rows,
            static row => Assert.False(string.IsNullOrWhiteSpace(row.AccessibleLabel)));

        viewModel.Settings.SelectSectionCommand.Execute(SettingsSection.Overview);

        Assert.False(viewModel.Settings.IsSupportMatrixOpen);
        Assert.True(viewModel.Settings.IsOverviewSelected);
    }

    /// <summary>Cell icons conservatively aggregate routes without turning partial evidence into a green claim.</summary>
    [Fact]
    public void SupportMatrixPivotsIcWorkflowCellsIntoTypedEvidenceStates()
    {
        var query = new StubSupportMatrixQuery(CurrentMatrix(
            Row(
                "NT51929",
                CapabilityAuthoringAvailability.Available,
                "standard-merge",
                CapabilityEvidenceStatus.DirectGolden),
            Row(
                "NT51929",
                CapabilityAuthoringAvailability.Available,
                "dp-replace",
                CapabilityEvidenceStatus.ContractOnly),
            Row(
                "NT51929",
                CapabilityAuthoringAvailability.Available,
                "general-merge",
                CapabilityEvidenceStatus.Missing),
            Row(
                "NT51929",
                CapabilityAuthoringAvailability.Unavailable,
                "ctrlram-replace",
                CapabilityEvidenceStatus.ContractOnly),
            Row(
                "NT51950",
                CapabilityAuthoringAvailability.Available,
                "standard-merge",
                CapabilityEvidenceStatus.DirectGolden)));
        var settings = new SettingsViewModel(
            "0.10.2",
            query,
            static () => ShellTextResources.For(ShellLanguage.English));

        settings.Refresh(ShellTextResources.For(ShellLanguage.English));

        SupportMatrixIcRowViewModel nt51929 = settings.SupportMatrix.IcRows[0];
        Assert.Equal(
            [
                SupportMatrixCellStatus.ReviewedEvidence,
                SupportMatrixCellStatus.ContractOnly,
                SupportMatrixCellStatus.Blocked,
                SupportMatrixCellStatus.ReviewRequired,
            ],
            nt51929.Cells.Select(static cell => cell.Status));
        SupportMatrixIcRowViewModel nt51950 = settings.SupportMatrix.IcRows[1];
        Assert.Equal(SupportMatrixCellStatus.ReviewedEvidence, nt51950.Cells[0].Status);
        Assert.All(
            nt51950.Cells.Skip(1),
            static cell => Assert.Equal(SupportMatrixCellStatus.NotDeclared, cell.Status));
        Assert.All(
            nt51929.Cells,
            static cell => Assert.False(string.IsNullOrWhiteSpace(cell.AccessibleLabel)));

        settings.Refresh(ShellTextResources.For(ShellLanguage.ChineseTraditional));

        Assert.Equal("僅有定義", settings.SupportMatrix.IcRows[0].Cells[1].StatusLabel);
    }

    /// <summary>The matrix displays Available and Unavailable as independent typed policy facts.</summary>
    [Fact]
    public void SupportMatrixProjectsBothAuthoringValuesAndBlockerProvenance()
    {
        var query = new StubSupportMatrixQuery(CurrentMatrix(
            Row("NT51929", CapabilityAuthoringAvailability.Available),
            Row("NT51950", CapabilityAuthoringAvailability.Unavailable)));
        var settings = new SettingsViewModel(
            "0.10.2",
            query,
            static () => ShellTextResources.For(ShellLanguage.English));

        settings.Refresh(ShellTextResources.For(ShellLanguage.English));
        settings.SelectSectionCommand.Execute(SettingsSection.SupportMatrix);

        Assert.Equal(["Available", "Unavailable"],
            settings.SupportMatrix.Rows.Select(static row => row.AuthoringLabel));
        SupportMatrixRowViewModel unavailable = settings.SupportMatrix.Rows[1];
        Assert.True(unavailable.HasBlocker);
        Assert.Equal("Authoring unavailable", unavailable.BlockerLabel);
        Assert.Contains("owner-policy:test", unavailable.ProvenanceDetail, StringComparison.Ordinal);
        Assert.Contains("capability.authoring.unavailable", unavailable.AccessibleLabel, StringComparison.Ordinal);
    }

    /// <summary>Execution, every blocker, and immutable decision provenance remain visible.</summary>
    [Fact]
    public void SupportMatrixRetainsExecutionAndCompleteDecisionDisclosure()
    {
        var query = new StubSupportMatrixQuery(CurrentMatrix(
            Row(
                "NT51929",
                CapabilityAuthoringAvailability.Available,
                executionState:
                    CanonicalSupportMatrixExecutionState.RequiresAuthoringCompilation),
            Row(
                "NT51950",
                CapabilityAuthoringAvailability.Unavailable,
                evidence: CapabilityEvidenceStatus.Missing,
                executionState: CanonicalSupportMatrixExecutionState.Unavailable,
                includeCertificationBlocker: true)));
        var settings = new SettingsViewModel(
            "0.10.2",
            query,
            static () => ShellTextResources.For(ShellLanguage.English));

        settings.Refresh(ShellTextResources.For(ShellLanguage.English));

        Assert.Contains(
            settings.OverviewRows,
            static row => row.Title == "IC catalog" && row.Value == "1");
        Assert.Contains(
            settings.OverviewRows,
            static row =>
                row.Title == "Standard Merge" &&
                row.Value == "1 IC" &&
                row.Status == "Authoring available");
        SupportMatrixRowViewModel dynamic = settings.SupportMatrix.Rows[0];
        Assert.Equal("Requires authoring compilation", dynamic.ExecutionLabel);
        Assert.Contains(
            "Execution Requires authoring compilation",
            dynamic.AccessibleLabel,
            StringComparison.Ordinal);
        SupportMatrixRowViewModel blocked = settings.SupportMatrix.Rows[1];
        Assert.Equal(
            "Authoring unavailable + Execution unavailable + Certification inconsistency",
            blocked.BlockerLabel);
        Assert.Contains(blocked.BlockerLabel, blocked.DecisionSummary, StringComparison.Ordinal);
        Assert.Contains("authoring (owner-policy:authoring)", blocked.ProvenanceDetail, StringComparison.Ordinal);
        Assert.Contains("publication (owner-policy:publication)", blocked.ProvenanceDetail, StringComparison.Ordinal);
        Assert.Contains("evidence (owner-policy:evidence)", blocked.ProvenanceDetail, StringComparison.Ordinal);
        Assert.Contains(CapabilityCatalogIssueCodes.AuthoringUnavailable, blocked.ProvenanceDetail, StringComparison.Ordinal);
        Assert.Contains(CapabilityCatalogIssueCodes.ExecutionUnavailable, blocked.ProvenanceDetail, StringComparison.Ordinal);
        Assert.Contains(CapabilityCatalogIssueCodes.SupportedWithoutEvidence, blocked.ProvenanceDetail, StringComparison.Ordinal);
    }

    /// <summary>Loading, empty, cold-start, and last-known-good states remain explicit and accessible.</summary>
    [Theory]
    [InlineData(CanonicalSupportMatrixCatalogState.Loading, false, "Loading Support Matrix", false)]
    [InlineData(CanonicalSupportMatrixCatalogState.Current, false, "No capability routes", false)]
    [InlineData(CanonicalSupportMatrixCatalogState.ColdStartBlocked, false, "Support Matrix unavailable", false)]
    [InlineData(CanonicalSupportMatrixCatalogState.LastKnownGood, true, "Showing last known good catalog", true)]
    public void SupportMatrixExposesAccessibleCatalogLifecycleStates(
        CanonicalSupportMatrixCatalogState state,
        bool includeRows,
        string expectedTitle,
        bool expectedStale)
    {
        CanonicalSupportMatrixQueryResult result = state switch
        {
            CanonicalSupportMatrixCatalogState.Loading =>
                CanonicalSupportMatrixQueryResult.Loading(),
            CanonicalSupportMatrixCatalogState.ColdStartBlocked => new(
                state,
                matrix: null,
                [Issue()]),
            CanonicalSupportMatrixCatalogState.LastKnownGood => new(
                state,
                Matrix(includeRows ? [Row("NT51929", CapabilityAuthoringAvailability.Available)] : []),
                [Issue()]),
            CanonicalSupportMatrixCatalogState.Current => new(state, Matrix([])),
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
        var query = new StubSupportMatrixQuery(result);
        var settings = new SettingsViewModel(
            "0.10.2",
            query,
            static () => ShellTextResources.For(ShellLanguage.English));

        settings.Refresh(ShellTextResources.For(ShellLanguage.English));
        settings.SelectSectionCommand.Execute(SettingsSection.SupportMatrix);

        Assert.True(settings.SupportMatrix.HasStatusNotice);
        Assert.Equal(expectedTitle, settings.SupportMatrix.StatusNotice!.Title);
        Assert.False(string.IsNullOrWhiteSpace(
            settings.SupportMatrix.StatusNotice.AccessibleLabel));
        Assert.Equal(expectedStale, settings.SupportMatrix.IsStale);
        Assert.Equal(includeRows, settings.SupportMatrix.HasRows);
        if (state == CanonicalSupportMatrixCatalogState.Loading)
        {
            Assert.All(
                settings.OverviewRows.Skip(1),
                static row => Assert.Equal("Loading…", row.Value));
            Assert.All(
                settings.CapabilityRows,
                static row => Assert.Equal("Loading…", row.Value));
        }
        else if (state == CanonicalSupportMatrixCatalogState.ColdStartBlocked)
        {
            Assert.All(
                settings.OverviewRows.Skip(1),
                static row => Assert.Equal("Not available", row.Value));
            Assert.All(
                settings.CapabilityRows,
                static row => Assert.Equal("Not available", row.Value));
        }
    }

    /// <summary>Support Matrix labels relocalize without rebuilding canonical policy.</summary>
    [Fact]
    public void SupportMatrixRelocalizesTypedFacts()
    {
        var query = new StubSupportMatrixQuery(CurrentMatrix(
            Row("NT51950", CapabilityAuthoringAvailability.Unavailable)));
        var settings = new SettingsViewModel(
            "0.10.2",
            query,
            static () => ShellTextResources.For(ShellLanguage.ChineseTraditional));

        settings.Refresh(ShellTextResources.For(ShellLanguage.ChineseTraditional));
        settings.SelectSectionCommand.Execute(SettingsSection.SupportMatrix);

        SupportMatrixRowViewModel row = Assert.Single(settings.SupportMatrix.Rows);
        Assert.Equal("不可用", row.AuthoringLabel);
        Assert.Equal("編輯不可用", row.BlockerLabel);
        Assert.Equal("僅有定義", row.EvidenceLabel);
        Assert.Equal("1 條路徑", settings.SupportMatrix.RouteCountLabel);
        Assert.Equal("目前版本", settings.SupportMatrix.CatalogStateLabel);
        Assert.Equal("已阻擋", settings.SupportMatrix.IcRows[0].Cells[0].StatusLabel);
        Assert.Equal("執行", ShellTextResources.For(ShellLanguage.ChineseTraditional).SupportMatrixExecutionLabel);
        Assert.Equal("證據", ShellTextResources.For(ShellLanguage.ChineseTraditional).SupportMatrixEvidenceLabel);
        Assert.Equal("待審查", ShellTextResources.For(ShellLanguage.ChineseTraditional).SupportMatrixReviewRequiredLabel);
        Assert.Equal(
            "狀態彙整驗證證據與路徑阻擋原因；聚焦任一格可查看明細。",
            ShellTextResources.For(ShellLanguage.ChineseTraditional).SupportMatrixHoverHint);
    }

    private static CanonicalSupportMatrixQueryResult CurrentMatrix(
        params CanonicalSupportMatrixRow[] rows)
    {
        return new CanonicalSupportMatrixQueryResult(
            CanonicalSupportMatrixCatalogState.Current,
            Matrix(rows));
    }

    private static CanonicalSupportMatrixSnapshot Matrix(
        IEnumerable<CanonicalSupportMatrixRow> rows)
    {
        return new CanonicalSupportMatrixSnapshot(
            "test-catalog",
            "1.0.0",
            new string('f', 64),
            new ResolutionToken("test-catalog:1"),
            rows);
    }

    private static CanonicalSupportMatrixRow Row(
        string icId,
        CapabilityAuthoringAvailability availability,
        string workflowId = "standard-merge",
        CapabilityEvidenceStatus evidence = CapabilityEvidenceStatus.ContractOnly,
        CanonicalSupportMatrixExecutionState executionState =
            CanonicalSupportMatrixExecutionState.Admitted,
        bool includeCertificationBlocker = false)
    {
        var identity = new CapabilityRouteIdentity(
            icId,
            workflowId,
            "selector-free",
            $"{icId.ToLowerInvariant()}-{workflowId}-256k");
        string fingerprint = new('a', 64);
        var blockers = new List<CanonicalSupportMatrixBlocker>();
        if (availability == CapabilityAuthoringAvailability.Unavailable)
        {
            blockers.Add(new CanonicalSupportMatrixBlocker(
                CanonicalSupportMatrixBlockerKind.AuthoringUnavailable,
                CapabilityCatalogIssueCodes.AuthoringUnavailable,
                "owner-policy:test"));
        }

        if (executionState == CanonicalSupportMatrixExecutionState.Unavailable)
        {
            blockers.Add(new CanonicalSupportMatrixBlocker(
                CanonicalSupportMatrixBlockerKind.ExecutionUnavailable,
                CapabilityCatalogIssueCodes.ExecutionUnavailable,
                "compiler:test"));
        }

        if (includeCertificationBlocker)
        {
            blockers.Add(new CanonicalSupportMatrixBlocker(
                CanonicalSupportMatrixBlockerKind.CertificationInconsistency,
                CapabilityCatalogIssueCodes.SupportedWithoutEvidence,
                "evidence:test"));
        }
        return new CanonicalSupportMatrixRow(
            identity,
            fingerprint,
            Decision(identity, fingerprint, availability, "authoring"),
            Decision(
                identity,
                fingerprint,
                CapabilityPublicationStatus.Candidate,
                "publication"),
            Decision(
                identity,
                fingerprint,
                evidence,
                "evidence"),
            executionState,
            blockers);
    }

    private static PinnedCapabilityDecision<TValue> Decision<TValue>(
        CapabilityRouteIdentity identity,
        string fingerprint,
        TValue value,
        string decisionId)
        where TValue : struct, Enum
    {
        return new PinnedCapabilityDecision<TValue>(
            decisionId,
            identity.RouteId,
            fingerprint,
            value,
            $"owner-policy:{decisionId}");
    }

    private static CapabilityCatalogIssue Issue()
    {
        return new CapabilityCatalogIssue(
            CapabilityCatalogIssueCodes.SourceUnavailable,
            "The test catalog reload failed.");
    }

    private sealed class StubSupportMatrixQuery(
        CanonicalSupportMatrixQueryResult result) : ICanonicalSupportMatrixQuery
    {
        public CanonicalSupportMatrixQueryResult Query()
        {
            return result;
        }
    }
}
