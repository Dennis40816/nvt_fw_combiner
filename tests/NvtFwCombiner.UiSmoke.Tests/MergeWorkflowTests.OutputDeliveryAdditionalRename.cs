using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.Presentation.Avalonia.Views;
using NvtFwCombiner.TestSupport;
using System.Security.Cryptography;
using System.Text.Json;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class MergeWorkflowTests
{
    /// <summary>A late secondary picker cannot close or execute a newly opened delivery proposal.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdditionalOutputPickerRejectsCancelAndReopen(bool reopen)
    {
        using var inputs = TempWorkspace.Create("ab-picker-context-inputs");
        using var destination = TempWorkspace.Create("ab-picker-context-output");
        MainWindowViewModel model = await CreateAbOutputNamingModelAsync(inputs);
        await model.Merge.RequestBuildOutputDeliveryAsync();
        model.OutputDelivery.SetAdditionalDeliveryEnabled(true);
        var pending = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        Task confirmation = OutputDeliveryConfirmationModal.ConfirmPreparedLooseWithPickersAsync(model.OutputDelivery,
            () => Task.FromResult<string?>(destination.PathFor("ab.bin")), () => pending.Task);
        Assert.False(confirmation.IsCompleted);
        model.OutputDelivery.CancelCommand.Execute(null);
        if (reopen) { await model.Merge.RequestBuildOutputDeliveryAsync(); }
        pending.SetResult(destination.PathFor("a.bin"));
        await confirmation;

        Assert.Equal(reopen, model.OutputDelivery.IsOpen);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
    }

    /// <summary>Each enabled output has an independent inline editor in both delivery modes.</summary>
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbAdditionalOutputNameEditorIsIndependent(bool bundle)
    {
        using var inputs = TempWorkspace.Create("ab-output-name-inputs");
        MainWindowViewModel model = await Task.Run(() => CreateAbOutputNamingModelAsync(inputs));
        if (bundle) { model.SelectedLanguage = "Traditional Chinese"; }
        PresentationHostServices services = PresentationTestHost.CreateServices("ui-smoke");
        using var window = new MainWindow(UiLaunchOptions.Empty, StartupTraceSession.Disabled, services, ShellPreferenceSnapshot.Default)
        {
            DataContext = model,
            Width = 1280,
            Height = 900,
            RequestedThemeVariant = bundle ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        try
        {
            window.Show();
            window.FindControl<ContentControl>("OutputDeliveryConfirmationModalHost")!.Content = model.OutputDelivery;
            await model.Merge.RequestBuildOutputDeliveryAsync();
            model.OutputDelivery.SetAdditionalDeliveryEnabled(true);
            model.OutputDelivery.SetBundleEnabled(bundle);
            Dispatcher.UIThread.RunJobs();
            OutputDeliveryConfirmationModal modal = window.GetVisualDescendants().OfType<OutputDeliveryConfirmationModal>().Single();
            Button? edit = modal.FindControl<Button>("EditAdditionalOutputFileNameButton");
            Assert.NotNull(edit);
            Assert.True(edit.IsVisible);
            string primary = model.OutputDelivery.OutputFileName;
            edit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            TextBox input = modal.FindControl<TextBox>("AdditionalOutputFileNameInput")!;
            Assert.True(input.IsVisible);
            Assert.True(input.IsFocused);
            input.Text = "customer-a.bin";
            Dispatcher.UIThread.RunJobs();
            Assert.Equal("customer-a.bin", model.OutputDelivery.AdditionalOutputFileName);
            modal.FindControl<Button>("CompleteAdditionalOutputFileNameEditButton")!
                .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(primary, model.OutputDelivery.OutputFileName);
            Assert.Equal("customer-a.bin", modal.FindControl<SelectableTextBlock>("AdditionalOutputFileNameDisplay")!.Text);
            Assert.True(edit.IsFocused);
            string? visualDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
            if (!string.IsNullOrWhiteSpace(visualDirectory))
            {
                _ = Directory.CreateDirectory(visualDirectory);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
                Assert.NotNull(frame);
                frame.Save(Path.Combine(visualDirectory, bundle ? "additional-name-dark.png" : "additional-name-light.png"));
            }
        }
        finally
        {
            model.OutputDelivery.CancelCommand.Execute(null);
            window.Close();
        }
    }

    /// <summary>Renamed artifacts keep the original complete bytes and existing collision/source allocation.</summary>
    [Theory]
    [InlineData("customer-ab.bin", "customer-a.bin", "customer-a.bin")]
    [InlineData("same.bin", "same.bin", "same (2).bin")]
    [InlineData("customer-ab.bin", "tp-a.bin", "tp-a.bin")]
    public async Task AbOutputNameOverridesReachAtomicBundleAndReport(string primaryName, string additionalName, string deliveredAdditional)
    {
        using var inputs = TempWorkspace.Create("ab-output-name-inputs");
        using var destination = TempWorkspace.Create("ab-output-name-destination");
        MainWindowViewModel model = await CreateAbOutputNamingModelAsync(inputs);
        await model.Merge.RequestBuildOutputDeliveryAsync();
        model.OutputDelivery.SetAdditionalDeliveryEnabled(true);
        model.OutputDelivery.SetBundleEnabled(true);
        model.OutputDelivery.SetParentDirectory(destination.Root);
        model.OutputDelivery.SetBundleFolderName("baseline");
        string canonical = model.OutputDelivery.OutputFileName;
        await model.OutputDelivery.ConfirmBundleAsync();
        Assert.True(model.RunSession.LastRunResult.Succeeded, model.RunSession.LastRunResult.Detail);
        byte[] baseline = File.ReadAllBytes(Path.Combine(destination.Root, "baseline", canonical));

        await model.Merge.RequestBuildOutputDeliveryAsync();
        model.OutputDelivery.SetAdditionalDeliveryEnabled(true);
        model.OutputDelivery.SetBundleFolderName("renamed");
        model.OutputDelivery.BeginOutputFileNameEdit();
        model.OutputDelivery.SetOutputFileName(primaryName);
        model.OutputDelivery.CompleteOutputFileNameEdit();
        model.OutputDelivery.BeginAdditionalOutputFileNameEdit();
        model.OutputDelivery.SetAdditionalOutputFileName(additionalName);
        model.OutputDelivery.CompleteAdditionalOutputFileNameEdit();
        Assert.True(model.OutputDelivery.CanConfirm, model.OutputDelivery.ValidationMessage);
        await model.OutputDelivery.ConfirmBundleAsync();
        Assert.True(model.RunSession.LastRunResult.Succeeded, model.RunSession.LastRunResult.Detail);
        string bundle = Path.Combine(destination.Root, "renamed");
        Assert.Equal(baseline, File.ReadAllBytes(Path.Combine(bundle, primaryName)));
        Assert.Equal(baseline[..0x40000], File.ReadAllBytes(Path.Combine(bundle, deliveredAdditional)));
        using var report = JsonDocument.Parse(model.Reports.LoadedReportJson);
        JsonElement delivery = Assert.Single(report.RootElement.GetProperty("DeliveryArtifacts").EnumerateArray());
        Assert.Contains(deliveredAdditional, delivery.GetRawText(), StringComparison.Ordinal);
        JsonElement[] artifacts = [.. report.RootElement.GetProperty("BundleDelivery").GetProperty("Artifacts").EnumerateArray()];
        Assert.Equal(deliveredAdditional, Assert.Single(artifacts, a => a.GetProperty("Role").GetString() == "additional-delivery")
            .GetProperty("DeliveredFileName").GetString());
        foreach (JsonElement source in artifacts.Where(a => a.GetProperty("Role").GetString() == "source"))
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(bundle, source.GetProperty("DeliveredFileName").GetString()!));
            Assert.Equal(source.GetProperty("Sha256").GetString(), Convert.ToHexStringLower(SHA256.HashData(bytes)));
        }
    }

    /// <summary>Each name recovers independently, unselected drafts do not block, and same-session reopen retains edits.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbAdditionalOutputNameValidationAndDraftsAreIndependent(bool bundle)
    {
        using var inputs = TempWorkspace.Create("ab-output-name-inputs");
        using var destination = TempWorkspace.Create("ab-output-name-destination");
        MainWindowViewModel model = await CreateAbOutputNamingModelAsync(inputs);
        await model.Merge.RequestBuildOutputDeliveryAsync();
        OutputDeliveryConfirmationViewModel vm = model.OutputDelivery;
        vm.SetBundleEnabled(bundle);
        vm.SetParentDirectory(destination.Root);
        vm.SetAdditionalDeliveryEnabled(true);
        string canonical = vm.AdditionalSuggestedFileName;
        foreach (string invalid in new[] { "", "../escape.bin", "CON.bin", "bad?.bin", new string('a', 256) + ".bin" })
        {
            vm.BeginAdditionalOutputFileNameEdit();
            vm.SetAdditionalOutputFileName(invalid);
            Assert.False(vm.CanConfirm);
            vm.SetAdditionalDeliveryEnabled(false);
            Assert.True(vm.CanConfirm, vm.ValidationMessage);
            vm.SetAdditionalDeliveryEnabled(true);
            Assert.False(vm.CanConfirm);
            vm.CompleteAdditionalOutputFileNameEdit();
            Assert.Equal(canonical, vm.AdditionalOutputFileName);
            Assert.True(vm.CanConfirm, vm.ValidationMessage);
        }
        vm.BeginOutputFileNameEdit();
        vm.SetOutputFileName("bad?.bin");
        vm.BeginAdditionalOutputFileNameEdit();
        vm.SetAdditionalOutputFileName("retained-a.bin");
        vm.CompleteAdditionalOutputFileNameEdit();
        Assert.Equal("retained-a.bin", vm.AdditionalOutputFileName);
        Assert.Equal("bad?.bin", vm.OutputFileName);
        vm.CompleteOutputFileNameEdit();
        Assert.True(vm.CanConfirm, vm.ValidationMessage);
        vm.SetBundleEnabled(!bundle);
        vm.SetBundleEnabled(bundle);
        vm.CancelCommand.Execute(null);
        await model.Merge.RequestBuildOutputDeliveryAsync();
        Assert.Equal("retained-a.bin", vm.AdditionalOutputFileName);
        Assert.False(vm.AdditionalOutputFileNameUsesAutomaticName);
        vm.BeginAdditionalOutputFileNameEdit();
        vm.SetAdditionalOutputFileName(canonical);
        vm.CompleteAdditionalOutputFileNameEdit();
        Assert.True(vm.AdditionalOutputFileNameUsesAutomaticName);
        vm.BeginAdditionalOutputFileNameEdit();
        vm.SetAdditionalOutputFileName("old-preparation.bin");
        vm.CompleteAdditionalOutputFileNameEdit();
        vm.CancelCommand.Execute(null);
        await model.WorkflowSession.SetSlotFileAsync(CompositionAddressSpaceIds.TpAInput,
            inputs.Write("new-tp-a.bin", CreateUiAbTpImage(0x81, 0x00, 1, 4, 1, 0x5102)),
            TestContext.Current.CancellationToken);
        await model.Merge.RequestBuildOutputDeliveryAsync();
        Assert.Equal(vm.AdditionalSuggestedFileName, vm.AdditionalOutputFileName);
        Assert.True(vm.AdditionalOutputFileNameUsesAutomaticName);
        Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
    }

    /// <summary>Loose additional custom names retain explicit overwrite behavior and reject primary aliases.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AbAdditionalOutputNameLooseSelectionPreservesExplicitIdentity(bool aliasPrimary)
    {
        using var inputs = TempWorkspace.Create("ab-output-name-inputs");
        using var destination = TempWorkspace.Create("ab-output-name-destination");
        MainWindowViewModel model = await CreateAbOutputNamingModelAsync(inputs);
        await model.Merge.RequestBuildOutputDeliveryAsync();
        model.OutputDelivery.SetAdditionalDeliveryEnabled(true);
        model.OutputDelivery.SetBundleEnabled(false);
        model.OutputDelivery.BeginAdditionalOutputFileNameEdit();
        model.OutputDelivery.SetAdditionalOutputFileName("customer-a.bin");
        model.OutputDelivery.CompleteAdditionalOutputFileNameEdit();
        string primary = destination.PathFor("customer-ab.bin");
        string additional = aliasPrimary ? primary : destination.Write("customer-a.bin", [0x5A]);
        await OutputDeliveryConfirmationModal.ConfirmPreparedLooseWithPickersAsync(model.OutputDelivery,
            () => Task.FromResult<string?>(primary), () => Task.FromResult<string?>(additional));
        if (aliasPrimary)
        {
            Assert.False(model.RunSession.LastRunResult.Succeeded);
            Assert.True(model.Reports.HasLoadedReport);
            Assert.Contains("must not overwrite Primary", model.RunSession.LastRunResult.Detail, StringComparison.Ordinal);
            using var report = JsonDocument.Parse(model.Reports.LoadedReportJson);
            JsonElement issue = Assert.Single(report.RootElement.GetProperty("Issues").EnumerateArray());
            Assert.Equal("NT51929", issue.GetProperty("OperationId").GetString());
            Assert.Equal(model.RunSession.LastRunResult.Detail, issue.GetProperty("Message").GetString());
            Assert.Empty(Directory.EnumerateFileSystemEntries(destination.Root));
        }
        else
        {
            Assert.True(model.RunSession.LastRunResult.Succeeded, model.RunSession.LastRunResult.Detail);
            Assert.Equal(File.ReadAllBytes(primary)[..0x40000], File.ReadAllBytes(additional));
            Assert.Equal(2, Directory.EnumerateFiles(destination.Root).Count());
        }
    }

    /// <summary>Error reports preserve counted labels and accept selector-free contexts without inventing counts.</summary>
    [Theory]
    [InlineData("", "NT51929")]
    [InlineData("exact-2", "NT51929 / exact-2")]
    [InlineData(null, null)]
    public void AbAdditionalOutputNameErrorReportPreservesSelection(string? number, string? expected)
    {
        MainWindowViewModel model = PresentationTestHost.CreateViewModel();
        void Load()
        {
            model.Reports.LoadRunErrorReport("build", "test-profile", "NT51929", number!, "original failure",
                new Dictionary<string, string>(), modeId: ExperienceIds.AbMerge, experienceId: ExperienceIds.AbMerge);
        }
        if (number is null)
        {
            _ = Assert.Throws<ArgumentNullException>(Load);
            return;
        }
        Load();
        using var report = JsonDocument.Parse(model.Reports.LoadedReportJson);
        JsonElement issue = Assert.Single(report.RootElement.GetProperty("Issues").EnumerateArray());
        Assert.Equal(expected, issue.GetProperty("OperationId").GetString());
        Assert.Equal("original failure", issue.GetProperty("Message").GetString());
    }

    private static async Task<MainWindowViewModel> CreateAbOutputNamingModelAsync(TempWorkspace inputs)
    {
        byte[] dp = new byte[0x80000];
        WriteUiAbCmi(dp, 0, major: 0x06, minor: 0x05, jira: 0x123);
        WriteUiAbCmi(dp, 0x40000, major: 0x07, minor: 0x08, jira: 0x456);
        MainWindowViewModel model = PresentationTestHost.CreateViewModel();
        model.ShowMergeCommand.Execute(null);
        model.WorkflowSession.SelectedIc = "NT51929";
        model.Merge.SelectedMergeMode = ExperienceIds.AbMerge;
        foreach ((string slot, string name, byte[] bytes) in new[]
        {
            (CompositionAddressSpaceIds.DpAbInput, "dp-ab.bin", dp),
            (CompositionAddressSpaceIds.TpAInput, "tp-a.bin", CreateUiAbTpImage(0x81, 0x00, 1, 4, 1, 0x5102)),
            (CompositionAddressSpaceIds.TpBInput, "tp-b.bin", CreateUiAbTpImage(0x82, 0x03, 2, 0, 0, 0x6A5C)),
        })
        {
            await model.WorkflowSession.SetSlotFileAsync(slot, inputs.Write(name, bytes), TestContext.Current.CancellationToken);
        }
        Assert.True(model.Merge.CanBuildMerge);
        return model;
    }
}
