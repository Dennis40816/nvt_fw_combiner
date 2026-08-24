namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Shared output confirmation owns modal, accessibility, responsive scroll, and typed bindings.</summary>
    [Fact]
    public void OutputDeliveryConfirmationUsesOneAccessibleSharedModal()
    {
        string shell = ReadPresentationFile("MainWindow.axaml");
        string modal = ReadPresentationFile("Views/OutputDeliveryConfirmationModal.axaml");
        string codeBehind = ReadPresentationFile("Views/OutputDeliveryConfirmationModal.axaml.cs");

        Assert.Contains("x:Name=\"OutputDeliveryConfirmationModalHost\"", shell, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding OutputDelivery.IsOpen}\"", shell, StringComparison.Ordinal);
        Assert.Contains("KeyboardNavigation.TabNavigation=\"Cycle\"", modal, StringComparison.Ordinal);
        Assert.Contains("KeyDown=\"OutputDeliveryConfirmationModal_OnKeyDown\"", modal, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.LiveSetting=\"Assertive\"", modal, StringComparison.Ordinal);
        Assert.Contains("<ScrollViewer", modal, StringComparison.Ordinal);
        Assert.Contains("Width=\"760\"", modal, StringComparison.Ordinal);
        Assert.Contains("MaxHeight=\"720\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BuildSettingsSurface\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CloseButton\"", modal, StringComparison.Ordinal);
        Assert.Contains("Classes=\"semanticAction closeButton\"", modal, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding CancelCommand}\"", modal, StringComparison.Ordinal);
        Assert.DoesNotContain("MinWidth=\"980\"", modal, StringComparison.Ordinal);
        Assert.Contains("NfcModalScrimBrush", modal, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding Sources}\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SourcesDisclosureToggle\"", modal, StringComparison.Ordinal);
        Assert.Contains("Classes=\"quietDisclosure buildSettingsDisclosure\"", modal, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding AreSourcesExpanded}\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"SourcesListPanel\"", modal, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding AreSourcesExpanded}\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OutputFileNameDisplay\"", modal, StringComparison.Ordinal);
        Assert.Contains("<SelectableTextBlock x:Name=\"OutputFileNameDisplay\"", modal, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding !IsOutputFileNameEditing}\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"OutputFileNameInput\"", modal, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsOutputFileNameEditing}\"", modal, StringComparison.Ordinal);
        Assert.DoesNotContain("IsReadOnly=\"{Binding !IsOutputFileNameEditing}\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"EditOutputFileNameButton\"", modal, StringComparison.Ordinal);
        Assert.Contains("Click=\"EditOutputFileNameButton_OnClick\"", modal, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding Text.OutputDeliveryEditOutputNameLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding Text.OutputDeliveryEditOutputNameLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BundleReviewPanel\"", modal, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding !IsBundleDestinationEditing}\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"BundleEditPanel\"", modal, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsBundleDestinationEditing}\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"EditBundleDestinationButton\"", modal, StringComparison.Ordinal);
        Assert.Contains("Click=\"EditBundleDestinationButton_OnClick\"", modal, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding Text.OutputDeliveryEditBundleDestinationLabel}\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CompleteBundleDestinationEditButton\"", modal, StringComparison.Ordinal);
        Assert.Contains("Click=\"CompleteBundleDestinationEditButton_OnClick\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CtrlRamVersionModeRow\"", modal, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"CtrlRamVersionFieldRow\"", modal, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding CtrlRamOptions.IsCtrlRamFirmwareVersionEditSelected}\"", modal, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"versionSummaryCard\"", modal, StringComparison.Ordinal);
        Assert.Contains("<ToolTip.Tip>", modal, StringComparison.Ordinal);
        Assert.Contains("{Binding Size, StringFormat='{}{0:N0} bytes'}", modal, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding Sha256}\"", modal, StringComparison.Ordinal);
        Assert.DoesNotContain("Classes=\"insetSurface\" Margin=\"0,0,0,6\"", modal, StringComparison.Ordinal);
        Assert.Contains("Classes=\"semanticAction primary action\"", modal, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding HasCtrlRamOptions}\"", modal, StringComparison.Ordinal);
        Assert.Contains("CtrlRamOptions.SelectCtrlRamFirmwareVersionEditCommand", modal, StringComparison.Ordinal);
        Assert.Contains("ValidateBundleDestination", ReadPresentationFile(
            "ViewModels/OutputDeliveryConfirmationViewModel.cs"), StringComparison.Ordinal);
        Assert.Contains("PickBundleParentDirectoryAsync", codeBehind, StringComparison.Ordinal);
        Assert.Contains("prepareModeSpecific: false", codeBehind, StringComparison.Ordinal);
        Assert.Contains("Path.GetFileName(outputPath)", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("viewModel.OffersAdditionalDelivery &&", codeBehind, StringComparison.Ordinal);
        Assert.DoesNotContain("GetFileNameWithoutExtension", codeBehind, StringComparison.Ordinal);
        Assert.Contains("OutputFileNameUsesAutomaticName", codeBehind, StringComparison.Ordinal);
    }

    /// <summary>Confirmation actions reuse the exact Open normal, hover, and pressed palette.</summary>
    [Fact]
    public void OutputDeliveryContinueUsesTheSharedOpenInteractionPalette()
    {
        string styles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        string primaryHover = ExtractStyle(
            styles,
            "Button.primary:pointerover /template/ ContentPresenter#PART_ContentPresenter");
        string primaryPressed = ExtractStyle(
            styles,
            "Button.primary:pressed /template/ ContentPresenter#PART_ContentPresenter");

        Assert.Contains("NfcAccentBrush", primaryHover, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceBrush", primaryHover, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", primaryPressed, StringComparison.Ordinal);
        Assert.Contains("NfcSurfaceBrush", primaryPressed, StringComparison.Ordinal);
    }

    /// <summary>Every GUI Build entry point routes to the shared output confirmation.</summary>
    [Fact]
    public void GuiBuildEntryPointsCannotBypassOutputConfirmation()
    {
        string mainBuild = ReadPresentationFile("MainWindow.Build.cs");
        string replaceSelection = ReadPresentationFile("Views/ReplaceSelectionModal.axaml.cs");
        string ctrlRam = ReadPresentationFile("ViewModels/ReplacePresentationViewModel.Execution.cs");
        string merge = ReadPresentationFile("ViewModels/MergePresentationViewModel.cs");
        string replace = ReadPresentationFile("ViewModels/ReplacePresentationViewModel.cs");

        Assert.Contains("RequestBuildOutputDeliveryAsync", mainBuild, StringComparison.Ordinal);
        Assert.Contains("OpenReplaceBuildSettingsAsync", replaceSelection, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshSelected", mainBuild, StringComparison.Ordinal);
        Assert.DoesNotContain("RefreshSelected", replaceSelection, StringComparison.Ordinal);
        Assert.Contains("RequestCtrlRamBuildSettingsAsync", ctrlRam, StringComparison.Ordinal);
        Assert.Contains("RequestBuildOutputDeliveryAsync", merge, StringComparison.Ordinal);
        Assert.Contains("RequestBuildFromCommandAsync", replace, StringComparison.Ordinal);
        Assert.DoesNotContain("CtrlRamFirmwareVersionModalHost", ReadPresentationFile("MainWindow.axaml"), StringComparison.Ordinal);
    }

    /// <summary>One typed coordinator covers all six route modes and forwards bundle intent to execution.</summary>
    [Fact]
    public void OutputDeliveryCoordinatorCoversCompleteCompositionRouteMatrix()
    {
        string merge = ReadPresentationFile("ViewModels/MergePresentationViewModel.Execution.cs");
        string replace = ReadPresentationFile("ViewModels/ReplacePresentationViewModel.Execution.cs");

        Assert.Contains("NormalMergeMode => _standardMergeSession.CurrentSnapshot", merge, StringComparison.Ordinal);
        Assert.Contains("AbCodeMergeMode => _abMergeSession.CurrentSnapshot", merge, StringComparison.Ordinal);
        Assert.Contains("GeneralMergeMode => _generalMergeSession.CurrentSnapshot", merge, StringComparison.Ordinal);
        Assert.Contains("DpReplaceMode => _dpReplaceSession.CurrentSnapshot", replace, StringComparison.Ordinal);
        Assert.Contains("CtrlRamReplaceMode => _ctrlRamReplaceSession.CurrentSnapshot", replace, StringComparison.Ordinal);
        Assert.Contains("GeneralReplaceMode => _generalReplaceSession.CurrentSnapshot", replace, StringComparison.Ordinal);
        Assert.Contains("outputBundle: outputBundle", merge, StringComparison.Ordinal);
        Assert.Contains("outputBundle: outputBundle", replace, StringComparison.Ordinal);
        Assert.Contains("decision.OutputPathUsesAutomaticName", replace, StringComparison.Ordinal);
        Assert.Contains("outputPathUsesAutomaticName: outputPathUsesAutomaticName", replace, StringComparison.Ordinal);
    }
}
