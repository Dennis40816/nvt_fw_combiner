using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.InputInspection;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Confirmation labels distinguish input roles from delivered files and canonical selector tokens.</summary>
[Collection(UiAvaloniaRuntimeCollection.Name)]
public sealed class OutputConfirmationLabelTests
{
    /// <summary>The accepted DP length advisory is readable on the Output Settings confirmation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void NonstandardDpWarningExplainsSuspectedOsdWithoutBlocking(bool chinese)
    {
        ShellTextResources text = ShellTextResources.For(
            chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        AuthoringInputSlotStatus status = StandardMergeFeedbackTests.Status(
            "DP_NONSTANDARD_SIZE_WARNING", AuthoringSlotLifecycle.Warning,
            actualLength: 0x40001);
        var input = new CompositionOutputInputSummary("dp-input", "dp-input", "dp.bin", 0x40001,
            [], null, status.InspectionLifecycle, status.InspectionIssueCode)
        { Inspection = status.Inspection };

        string warning = text.FormatOutputInputWarning(input);
        Assert.Contains(chinese ? "OSD 客製化" : "customized OSD", warning, StringComparison.Ordinal);
        Assert.DoesNotContain("DP_NONSTANDARD_SIZE_WARNING", warning, StringComparison.Ordinal);
    }

    /// <summary>Length and metadata warnings survive together, without repeated advisory codes.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void ConfirmationRetainsLengthAndMetadataWarnings(bool chinese, bool advisoryIsPrimary = false)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        AuthoringInputSlotStatus status = StandardMergeFeedbackTests.Status("input.outer-length.warning",
            AuthoringSlotLifecycle.Warning, actualLength: 4, ignoredTrailingBytes: true);
        var advisory = new CompiledInputArtifactInspectionAdvisory(
            InputArtifactInspectionIssueCodes.AbVersionMetadataUnknown,
            CompiledInputArtifactInspectionNextAction.ReviewUnknownVersion);
        var input = new CompositionOutputInputSummary("tp-a-input", "tp-a-input", "input.bin", 4, [], null,
            status.InspectionLifecycle, advisoryIsPrimary ? advisory.IssueCode : status.InspectionIssueCode)
        {
            Inspection = status.Inspection,
            InspectionAdvisories = [advisory, advisory],
        };

        string warning = text.FormatOutputInputWarning(input);
        Assert.Equal(text.CreateInputIssueCard(status, "TP A").Summary + Environment.NewLine +
            text.AbUnknownVersionWarning, warning);
    }

    /// <summary>A logical General Merge output does not claim a physical Flash map in either language.</summary>
    [AvaloniaFact]
    public async Task LogicalOutputShowsLocalizedNotApplicableMap()
    {
        using TempWorkspace workspace = TempWorkspace.Create("confirmation-logical-map");
        CompositionHostServices host = CompositionHostServices.Create();
        string source = workspace.Write("source.bin", [0xA5, 0x5A]);
        var mappings = new GeneralMappingDraftState([
            new GeneralMappingDraftRow("copy", ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(source), new ByteRange(0, 2), CompositionAddressSpaceIds.OutputImage,
                new ByteRange(0, 2), OverlapPolicy.Reject, 1, "confirmation")]);
        GeneralAuthoringSessionPreparation prepared = await host.GeneralAuthoring.PrepareMergeSessionAsync(
            new AuthoringSessionState(ExperienceIds.GeneralMerge), "NT51926",
            new GeneralMergeDraftState(new GeneralMergeOutputInitializer(16), mappings), TestContext.Current.CancellationToken);
        Assert.True(prepared.Succeeded);
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(
            prepared.AcceptedSession!, TestContext.Current.CancellationToken);
        Assert.Null(proposal.Confirmation!.FlashMap);
        foreach (ShellLanguage language in new[] { ShellLanguage.English, ShellLanguage.ChineseTraditional })
        {
            var vm = new OutputDeliveryConfirmationViewModel(host.CompositionOutputNaming, () => ShellTextResources.For(language));
            vm.Open(new OutputDeliveryRequest(proposal, false, null, () => true, null, null, null, _ => Task.CompletedTask));
            Assert.Equal(language == ShellLanguage.English ? "Not applicable" : "不適用", vm.FlashMapSummary);
        }
    }

    /// <summary>The confirmation reuses the input card's localized non-blocking version warning.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void UnknownVersionWarningMatchesAcceptedInputHelp(bool chinese, bool trailing = false)
    {
        ShellTextResources text = ShellTextResources.For(chinese ? ShellLanguage.ChineseTraditional : ShellLanguage.English);
        AuthoringInputSlotStatus status = StandardMergeFeedbackTests.Status("ab.input.version-unknown", AuthoringSlotLifecycle.Warning,
            actualLength: trailing ? 4 : 2, ignoredTrailingBytes: trailing);
        var input = new CompositionOutputInputSummary("tp-a-input", "tp-a-input", "input.bin", 2, [], null,
            status.InspectionLifecycle, status.InspectionIssueCode)
        { Inspection = status.Inspection };
        string warning = text.FormatOutputInputWarning(input);
        Assert.Equal(trailing ? text.CreateInputIssueCard(status, "TP A").Summary : text.GetInputSlotInspectionStatus(status), warning);
        Assert.DoesNotContain("ab.input.version-unknown", warning, StringComparison.Ordinal);
    }

    /// <summary>Using the same file in two roles shows two input rows but copies just one bundle source.</summary>
    [AvaloniaFact]
    public async Task SharedInputFileCountsRolesSeparatelyFromBundleCopies()
    {
        using TempWorkspace workspace = TempWorkspace.Create("confirmation-shared-source");
        CompositionHostServices host = CompositionHostServices.Create();
        byte[] input = new byte[0x40000];
        input[0x36001] = 0xFF;
        input[0x36017] = 1;
        new byte[] { 0, 0x4E, 0x56, 0x54 }.CopyTo(input, 0x36FFC);
        string path = workspace.PathFor("shared.bin");
        CompiledAuthoringSessionPreparation prepared = host.AbMergeAuthoring.PrepareSession(
            new AuthoringSessionState(ExperienceIds.AbMerge), "NT51932", null,
            [new("tp-a-input", path, input), new("tp-b-input", path, input)], AbMergeDpMode.Dummy);
        Assert.True(prepared.Succeeded);
        CompositionOutputBundleProposal proposal = await host.CompositionOutputNaming.PrepareBundleProposalAsync(prepared.Snapshot!, TestContext.Current.CancellationToken);
        foreach (ShellLanguage language in new[] { ShellLanguage.English, ShellLanguage.ChineseTraditional })
        {
            ShellTextResources text = ShellTextResources.For(language);
            Assert.All(proposal.Confirmation!.Inputs, input =>
            {
                Assert.Equal(AuthoringSlotLifecycle.Verified, input.InspectionLifecycle);
                Assert.Empty(text.FormatOutputInputWarning(input));
            });
        }
        var vm = new OutputDeliveryConfirmationViewModel(host.CompositionOutputNaming, () => ShellTextResources.For(ShellLanguage.English));
        vm.Open(new OutputDeliveryRequest(proposal, false, null, () => true, null, null, null, _ => Task.CompletedTask));
        vm.SetBundleEnabled(true);
        var modal = new OutputDeliveryConfirmationModal { DataContext = vm, IsOpen = true };
        var window = new Window { Content = modal };
        try
        {
            window.Show();
            Assert.Equal(2, vm.InputRows.Count);
            _ = Assert.Single(vm.Sources);
            Assert.Equal("2 input sources", modal.FindControl<TextBlock>("SourcesCountLabel")!.Text);
            Assert.Equal("1 input sources", vm.SourcesSummary);
            Assert.Equal("Flash BIN + 1 source files", vm.DeliveryDescription);
            Assert.Equal("AB Code", vm.ModeSummary);
            Assert.Equal("Common", vm.FlashMapSummary);
            Assert.True(vm.CanConfirm);
        }
        finally { window.Close(); }
    }

    /// <summary>Display labels never expose internal cascade tokens and retain numeric units.</summary>
    [Theory]
    [InlineData("single", "Single", "單 IC")]
    [InlineData("2", "2 IC", "2 IC")]
    [InlineData("cascade", "Cascade", "串接")]
    [InlineData("cascade_2to8", "Cascade (2–8 IC)", "串接（2–8 IC）")]
    public void ConfirmationNumberUsesReadableLabels(string token, string english, string chinese)
    {
        IcNumberSelection selection = IcNumberSelection.FromToken(token);
        Assert.Equal(english, ShellTextResources.For(ShellLanguage.English).FormatOutputNumber(selection));
        Assert.Equal(chinese, ShellTextResources.For(ShellLanguage.ChineseTraditional).FormatOutputNumber(selection));
    }
}
