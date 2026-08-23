using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class XamlControlStyleContractTests
{
    /// <summary>Language changes retain the same typed fact-publication gate as initial inspection.</summary>
    [Fact]
    public void FirmwareFactRelocalizationPreservesInspectionPublicationGate()
    {
        string localization = ReadPresentationFile("ViewModels/WorkflowSessionPresentationViewModel.cs");
        string inspection = ReadPresentationFile(
            "ViewModels/WorkflowSessionPresentationViewModel.FirmwareInspection.cs");

        Assert.Contains("!FirmwareInspectionProjection.SupportsFacts(slot, inspection)", localization, StringComparison.Ordinal);
        Assert.Contains(
            "else if (item.PublishFacts && FirmwareInspectionProjection.SupportsFacts(slot, inspection))",
            inspection,
            StringComparison.Ordinal);
    }

    /// <summary>Selected files replace requirement chrome with one keyboard-equivalent pinnable state surface.</summary>
    [Fact]
    public void FirmwareSlotUsesOneFocusablePinnableSemanticStateSurface()
    {
        string app = ReadPresentationFile("App.axaml");
        string slotCard = ReadPresentationFile("Views/FirmwareSlotCard.axaml");

        Assert.Contains("Styles/FirmwareSlotExperienceStyles.axaml", app, StringComparison.Ordinal);
        Assert.Contains(
            "xmlns:behaviors=\"using:NvtFwCombiner.Presentation.Avalonia.Behaviors\"",
            slotCard,
            StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding IsRequirementLabelVisible}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes=\"slotStateAction\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("IsVisible=\"{Binding HasSemanticState}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsSemanticStateDetailExpanded}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Focusable=\"True\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("behaviors:FocusToolTipBehavior.IsEnabled=\"True\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding SemanticStateAutomationText}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains(
            "AutomationProperties.Name=\"{Binding SemanticStateAutomationText}\"",
            slotCard,
            StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SemanticStateLabel}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Text=\"{Binding SemanticStateAutomationText}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes.pendingInput=\"{Binding IsSemanticStatePendingInput}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("IsEnabled=\"{Binding CanSelectFile}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("DragDrop.AllowDrop=\"{Binding CanSelectFile}\"", slotCard, StringComparison.Ordinal);
        string buttonStyles = ReadPresentationFile("Styles/MainWindowButtonStyles.axaml");
        string browse = ExtractStyle(buttonStyles, "Button.browseAction");
        string browsePresenter = ExtractStyle(
            buttonStyles,
            "Button.browseAction /template/ ContentPresenter#PART_ContentPresenter");
        Assert.Contains("Property=\"MinWidth\" Value=\"88\"", browse, StringComparison.Ordinal);
        Assert.Contains("Property=\"MaxHeight\" Value=\"36\"", browse, StringComparison.Ordinal);
        Assert.Contains("Property=\"HorizontalContentAlignment\" Value=\"Center\"", browse, StringComparison.Ordinal);
        Assert.Contains("Property=\"VerticalContentAlignment\" Value=\"Center\"", browse, StringComparison.Ordinal);
        Assert.Contains("VerticalAlignment=\"Center\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Classes=\"semanticAction action browseAction\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("CornerRadius=\"{DynamicResource NfcCompactCornerRadius}\"", buttonStyles, StringComparison.Ordinal);
        Assert.Contains(
            "CornerRadius\" Value=\"{DynamicResource NfcPillCornerRadius}",
            browsePresenter,
            StringComparison.Ordinal);
        Assert.DoesNotContain("UsesSharedSlotPresentation", slotCard, StringComparison.Ordinal);
        Assert.DoesNotContain("UsesLegacySlotPresentation", slotCard, StringComparison.Ordinal);
    }

    /// <summary>Firmware facts use the approved borderless four-column grid and disclose only overflow.</summary>
    [Fact]
    public void FirmwareSlotFactsUseApprovedBorderlessFourColumnGrid()
    {
        string slotCard = ReadPresentationFile("Views/FirmwareSlotCard.axaml");
        string templates = ReadPresentationFile("Resources/MainWindowSharedTemplates.axaml");
        string factTemplate = ExtractDataTemplate(templates, "FirmwareSlotInformationFactTemplate");
        string controlStyles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string factLabelStyle = ExtractStyle(controlStyles, "TextBlock.firmwareSlotFactLabel");

        Assert.Contains("ItemsSource=\"{Binding PrimaryFirmwareFacts}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("ItemsSource=\"{Binding AdditionalFirmwareFacts}\"", slotCard, StringComparison.Ordinal);
        Assert.DoesNotContain("IsChecked=\"{Binding IsFirmwareFactsExpanded}\"", slotCard, StringComparison.Ordinal);
        Assert.DoesNotContain("Content=\"{Binding FirmwareFactsDisclosureLabel}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding IsAdditionalFirmwareFactsExpanded}\"", slotCard, StringComparison.Ordinal);
        Assert.Contains("Content=\"{Binding AdditionalFirmwareFactsLabel}\"", slotCard, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(slotCard, "<UniformGrid Columns=\"4\" Rows=\"1\" />"));
        Assert.Contains("Classes=\"firmwareSlotFact\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Padding=\"0,0,16,0\"", factTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("BorderThickness=", factTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("CornerRadius=", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Orientation=\"Vertical\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Property=\"FontWeight\" Value=\"SemiBold\"", factLabelStyle, StringComparison.Ordinal);
        Assert.Contains("TextTrimming=\"CharacterEllipsis\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Classes=\"firmwareSlotFactStateIcon\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("AutomationProperties.Name=\"{Binding StateAutomationText}\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("ToolTip.Tip=\"{Binding StateAutomationText}\"", factTemplate, StringComparison.Ordinal);
        Assert.Contains("Focusable=\"True\"", factTemplate, StringComparison.Ordinal);
        Assert.DoesNotContain("MaxWidth=\"430\"", slotCard, StringComparison.Ordinal);

        string styles = ReadPresentationFile("Styles/FirmwareSlotExperienceStyles.axaml");
        string quietDisclosure = ExtractStyle(styles, "ToggleButton.quietDisclosure");
        string quietDisclosurePresenter = ExtractStyle(
            styles,
            "ToggleButton.quietDisclosure /template/ ContentPresenter#PART_ContentPresenter");
        Assert.Contains("NfcAccentStrongBrush", quietDisclosure, StringComparison.Ordinal);
        Assert.Contains("TextElement.Foreground", quietDisclosurePresenter, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", quietDisclosurePresenter, StringComparison.Ordinal);
        Assert.Contains("ToggleButton.quietDisclosure:pointerover", styles, StringComparison.Ordinal);
        Assert.Contains("ToggleButton.quietDisclosure:pressed", styles, StringComparison.Ordinal);
        Assert.Contains("ToggleButton.quietDisclosure:focus-visible /template/ ContentPresenter#PART_ContentPresenter", styles, StringComparison.Ordinal);
        Assert.Contains("ToggleButton.quietDisclosure:disabled", styles, StringComparison.Ordinal);
        string factFocus = ExtractStyle(styles, "Path.firmwareSlotFactStateIcon:focus-visible");
        Assert.Contains("NfcAccentStrongBrush", factFocus, StringComparison.Ordinal);
        Assert.Contains("2.8", factFocus, StringComparison.Ordinal);
    }

    /// <summary>Shared interaction styles preserve semantic meaning across hover, focus, and pinned states.</summary>
    [Fact]
    public void FirmwareSlotSemanticActionOwnsEveryInteractiveVisualState()
    {
        string styles = ReadPresentationFile("Styles/FirmwareSlotExperienceStyles.axaml");
        string controlStyles = ReadPresentationFile("Styles/MainWindowControlStyles.axaml");
        string selected = ExtractStyle(
            styles,
            "Border.fileDropZone.firmwareSlot.compactExperience.hasFile");
        string required = ExtractStyle(
            controlStyles,
            "Label.slotBadge.firmwareSlotRequirement");
        string pending = ExtractStyle(
            styles,
            "ToggleButton.slotStateAction.pendingInput /template/ ContentPresenter#PART_ContentPresenter");
        Assert.Contains("NfcAccentSurfaceSubtleBrush", selected, StringComparison.Ordinal);
        Assert.Contains("NfcAccentBorderStrongBrush", selected, StringComparison.Ordinal);
        Assert.Contains("NfcRequiredMissingBadgeSurfaceBrush", required, StringComparison.Ordinal);
        Assert.Contains("NfcDangerBorderBrush", required, StringComparison.Ordinal);
        Assert.Contains("NfcDangerTextBrush", required, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "<Style Selector=\"Label.slotBadge.firmwareSlotRequirement.availableInput\">",
            styles,
            StringComparison.Ordinal);
        Assert.Contains("NfcWarningSurfaceBrush", pending, StringComparison.Ordinal);
        Assert.Contains("NfcWarningTextBrush", pending, StringComparison.Ordinal);
        string pinned = ExtractStyle(styles, "ToggleButton.slotStateAction:checked /template/ ContentPresenter#PART_ContentPresenter");
        string hover = ExtractStyle(styles, "ToggleButton.slotStateAction:pointerover");
        string pressed = ExtractStyle(styles, "ToggleButton.slotStateAction:pressed");
        string focus = ExtractStyle(styles, "ToggleButton.slotStateAction:focus-visible /template/ ContentPresenter#PART_ContentPresenter");
        string disabled = ExtractStyle(styles, "ToggleButton.slotStateAction:disabled");

        Assert.Contains("BorderThickness", pinned, StringComparison.Ordinal);
        Assert.DoesNotContain("Background", pinned, StringComparison.Ordinal);
        Assert.Contains("Property=\"Opacity\"", hover, StringComparison.Ordinal);
        Assert.Contains("0.72", pressed, StringComparison.Ordinal);
        Assert.Contains("NfcAccentStrongBrush", focus, StringComparison.Ordinal);
        Assert.Contains("Value=\"2\"", focus, StringComparison.Ordinal);
        Assert.Contains("0.45", disabled, StringComparison.Ordinal);

        foreach ((string state, string surface, string border, string text) in new[]
                 {
                     ("checking", "NfcAccentSurfaceBrush", "NfcAccentBorderLightBrush", "NfcAccentStrongBrush"),
                     ("verified", "NfcSuccessSurfaceBrush", "NfcSuccessBorderBrush", "NfcSuccessEmphasisBrush"),
                     ("warning", "NfcWarningSurfaceBrush", "NfcWarningBorderStrongBrush", "NfcWarningTextBrush"),
                     ("error", "NfcCriticalSurfaceBrush", "NfcDangerBorderBrush", "NfcDangerTextBrush"),
                     ("notApplicable", "NfcSurfaceSubtleBrush", "NfcBorderMutedBrush", "NfcTextSecondaryBrush"),
                 })
        {
            string semantic = ExtractStyle(
                styles,
                $"ToggleButton.slotStateAction.{state} /template/ ContentPresenter#PART_ContentPresenter");
            Assert.Contains(surface, semantic, StringComparison.Ordinal);
            Assert.Contains(border, semantic, StringComparison.Ordinal);
            Assert.Contains(text, semantic, StringComparison.Ordinal);
            Assert.True(
                styles.IndexOf(hover, StringComparison.Ordinal) > styles.IndexOf(semantic, StringComparison.Ordinal) &&
                styles.IndexOf(focus, StringComparison.Ordinal) > styles.IndexOf(semantic, StringComparison.Ordinal),
                "Hover and focus feedback must follow every semantic state without replacing its surface or text color.");
        }
    }

    /// <summary>Current slot and workflow copy uses canonical LDC terminology rather than the legacy LD alias.</summary>
    [Fact]
    public void FirmwareSlotCopyUsesCanonicalLdcTerminology()
    {
        string localized = ReadPresentationFile("ViewModels/ShellTextResources.Localized.cs");
        string dynamicText = ReadPresentationFile("ViewModels/ShellTextResources.DynamicText.cs");

        Assert.DoesNotContain("optional LD\"", localized, StringComparison.Ordinal);
        Assert.DoesNotContain("選用 LD\"", localized, StringComparison.Ordinal);
        Assert.DoesNotContain("optional LD payload", dynamicText, StringComparison.Ordinal);
        Assert.DoesNotContain("選用 LD payload", dynamicText, StringComparison.Ordinal);
        Assert.DoesNotContain("separate DP and LD payloads", localized, StringComparison.Ordinal);
        Assert.DoesNotContain("DP 與 LD payload", localized, StringComparison.Ordinal);
        Assert.Contains("optional LDC", localized, StringComparison.Ordinal);
        Assert.Contains("選用 LDC", localized, StringComparison.Ordinal);
        Assert.Contains("CompositionAddressSpaceIds.LdcReplacement => (", dynamicText, StringComparison.Ordinal);
        Assert.Contains("\"LDC 取代 BIN 檔案\"", dynamicText, StringComparison.Ordinal);
        Assert.DoesNotContain("InputSelectionNextActionKind.LoadPrerequisite", dynamicText, StringComparison.Ordinal);
        Assert.Contains("InputSelectionNextActionKind.LoadArtifactFirst", dynamicText, StringComparison.Ordinal);
    }

    /// <summary>Traditional Chinese readiness and layout guidance uses Chinese UI prose around stable product terms.</summary>
    [Fact]
    public void FirmwareReadinessGuidanceIsFullyLocalizedInTraditionalChinese()
    {
        var text = ShellTextResources.For(ShellLanguage.ChineseTraditional);
        var english = ShellTextResources.For(ShellLanguage.English);
        string standardReadiness = text.GetMergeReadinessStatus(
            ExperienceIds.StandardMerge,
            "NT51950",
            "DP、TP",
            isStandardMergeSupported: true,
            generalMappingFileCount: 0);
        string mappedReadiness = text.GetMergeReadinessStatus(
            ExperienceIds.GeneralMerge,
            "NT51950",
            "DP、TP",
            isStandardMergeSupported: true,
            generalMappingFileCount: 2);
        string emptyMappingReadiness = text.GetMergeReadinessStatus(
            ExperienceIds.GeneralMerge,
            "NT51950",
            "DP、TP",
            isStandardMergeSupported: true,
            generalMappingFileCount: 0);
        string[] localizedGuidance =
        [
            text.GetReplaceMemorySummary(ExperienceIds.DpReplace),
            text.GetReplaceMemorySummary(ExperienceIds.CtrlRamReplace),
            text.GetReplaceMemorySummary(ExperienceIds.GeneralReplace),
            text.GetReplaceReadinessStatus(ExperienceIds.DpReplace, canRun: true),
            text.GetReplaceReadinessStatus(ExperienceIds.DpReplace, canRun: false),
            text.GetReplaceReadinessStatus(ExperienceIds.CtrlRamReplace, canRun: true),
            text.GetReplaceReadinessStatus(ExperienceIds.CtrlRamReplace, canRun: false),
            text.GetReplaceReadinessStatus(ExperienceIds.GeneralReplace, canRun: true),
            text.GetReplaceReadinessStatus(ExperienceIds.GeneralReplace, canRun: false),
            text.GetMergeMemorySummary(
                ExperienceIds.StandardMerge,
                isStandardMergeSupported: true,
                hasGeneralMapping: false),
            text.GetMergeMemorySummary(
                ExperienceIds.GeneralMerge,
                isStandardMergeSupported: true,
                hasGeneralMapping: true),
            text.GetStandardMergeSupportSummary("NT51950", supported: true, "DP、TP"),
            standardReadiness,
            mappedReadiness,
            emptyMappingReadiness,
        ];
        string[] untranslatedUiPhrases =
        [
            " bytes",
            "base firmware",
            "Base flash",
            "input file",
            "input",
            "output",
            "report",
            "region",
            "mapping",
            "range",
            "source",
            "blank",
            "BIN files",
            "slots",
            "Ready：",
            "Build blocked",
        ];

        Assert.Contains("BIN 檔案", standardReadiness, StringComparison.Ordinal);
        Assert.Equal(
            "Solid colors identify selected CtrlRAM regions; diagonal hatching means bytes remain from the base firmware.",
            english.GetReplaceMemorySummary(ExperienceIds.CtrlRamReplace));
        Assert.Equal(
            "實色標示已選取的 CtrlRAM 區域；斜線表示資料仍保留自基礎韌體。",
            text.GetReplaceMemorySummary(ExperienceIds.CtrlRamReplace));
        Assert.Contains("來源 BIN 檔案對應", mappedReadiness, StringComparison.Ordinal);
        Assert.Contains("空白輸出映像", mappedReadiness, StringComparison.Ordinal);
        Assert.Contains("來源 BIN 檔案對應", emptyMappingReadiness, StringComparison.Ordinal);
        Assert.All(localizedGuidance, guidance =>
            Assert.All(untranslatedUiPhrases, phrase =>
                Assert.DoesNotContain(phrase, guidance, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>The shared semantic state gives blocking selection readiness precedence over file inspection.</summary>
    [Fact]
    public void FirmwareSlotSemanticStateComposesReadinessAndInspection()
    {
        var slot = new FirmwareSlotViewModel(
            "dp",
            "DP BIN",
            "Select DP firmware",
            FirmwareSlotKind.Dp);

        Assert.Equal(FirmwareSlotSemanticState.Empty, slot.SemanticState);
        Assert.True(slot.IsRequirementLabelVisible);
        Assert.False(slot.HasSemanticState);

        slot.FilePath = "C:\\firmware\\dp.bin";

        Assert.Equal(FirmwareSlotSemanticState.Checking, slot.SemanticState);
        Assert.Equal("Checking", slot.SemanticStateLabel);
        Assert.False(slot.IsRequirementLabelVisible);
        Assert.True(slot.HasSemanticState);

        slot.SetInputInspection(FirmwareInputInspectionSeverity.Valid, "Inspection passed");

        Assert.Equal(FirmwareSlotSemanticState.Verified, slot.SemanticState);
        Assert.Equal("Verified", slot.SemanticStateLabel);
        Assert.Equal("Inspection passed", slot.SemanticStateDetail);

        slot.SetSelectionReadiness(
            ResolvedChildReadiness.Blocked,
            "Blocked",
            "Reference length rejects this child.",
            "Blocked. Reference length rejects this child.");

        Assert.Equal(FirmwareSlotSemanticState.Error, slot.SemanticState);
        Assert.Equal("Blocked", slot.SemanticStateLabel);
        Assert.Equal("Reference length rejects this child.", slot.SemanticStateDetail);
        Assert.Equal("Blocked. Reference length rejects this child.", slot.SemanticStateAutomationText);
    }

    /// <summary>Not-applicable empty slots replace misleading requirement chrome with the compiled state.</summary>
    [Fact]
    public void FirmwareSlotNotApplicableStateHidesRequirementLabel()
    {
        var slot = new FirmwareSlotViewModel(
            "ldc",
            "LDC BIN",
            "Select LDC firmware",
            FirmwareSlotKind.Dp);

        slot.SetSelectionReadiness(
            ResolvedChildReadiness.NotApplicable,
            "Not applicable",
            "Reference length does not include LDC.",
            "Not applicable. Reference length does not include LDC.");

        Assert.Equal(FirmwareSlotSemanticState.NotApplicable, slot.SemanticState);
        Assert.False(slot.IsRequirementLabelVisible);
        Assert.True(slot.HasSemanticState);
    }

    /// <summary>An available empty optional slot keeps one requirement signal and no iconless Applicable surface.</summary>
    [Fact]
    public void FirmwareSlotReadyWithoutFileShowsOnlyRequirementSignal()
    {
        var slot = new FirmwareSlotViewModel(
            "ldc",
            "LDC BIN",
            "Select optional LDC firmware",
            FirmwareSlotKind.Dp,
            isOptional: true);

        slot.SetSelectionReadiness(
            ResolvedChildReadiness.Ready,
            "Applicable",
            "Optional LDC input is available.",
            "Applicable. Optional LDC input is available.");

        Assert.Equal(FirmwareSlotSemanticState.Empty, slot.SemanticState);
        Assert.False(slot.HasSemanticState);
        Assert.True(slot.IsRequirementLabelVisible);
        Assert.Equal("Optional", slot.RequirementLabel);
    }

    /// <summary>Typed selection admission disables only the independent picker transition.</summary>
    [Fact]
    public void FirmwareSlotSelectionAdmissionKeepsRetainedFileInspectable()
    {
        var slot = new FirmwareSlotViewModel(
            "ldc",
            "LDC BIN",
            "Select LDC firmware",
            FirmwareSlotKind.Dp)
        {
            FilePath = "C:\\firmware\\ldc.bin",
        };

        slot.SetSelectionReadiness(
            ResolvedChildReadiness.PendingInput,
            "Waiting for Reference FlashCode",
            "Load Reference FlashCode first.",
            "Waiting for Reference FlashCode. Load Reference FlashCode first.",
            canSelect: false);

        Assert.False(slot.CanSelectFile);
        Assert.True(slot.HasFile);
        Assert.Equal("ldc.bin", slot.DisplayName);
        Assert.True(slot.IsSemanticStatePendingInput);
    }

    /// <summary>A structural admission error remains blocking while another prerequisite is still pending.</summary>
    [Fact]
    public void FirmwareSlotBlockingInspectionOutranksPendingSelection()
    {
        var slot = new FirmwareSlotViewModel(
            "ldc",
            "LDC BIN",
            "Select LDC firmware",
            FirmwareSlotKind.Dp)
        {
            FilePath = "C:\\firmware\\ldc.bin",
        };
        slot.SetSelectionReadiness(
            ResolvedChildReadiness.PendingInput,
            "Waiting for Reference FlashCode",
            "Load Reference FlashCode first.",
            "Waiting for Reference FlashCode. Load Reference FlashCode first.");
        slot.SetInputInspection(
            FirmwareInputInspectionSeverity.Blocking,
            "The selected BIN failed structural admission.");

        Assert.Equal(FirmwareSlotSemanticState.Error, slot.SemanticState);
        Assert.Equal("Error", slot.SemanticStateLabel);
        Assert.Equal("The selected BIN failed structural admission.", slot.SemanticStateDetail);
        Assert.Equal(
            "Error: The selected BIN failed structural admission.",
            slot.SemanticStateAutomationText);
    }

    /// <summary>A structural admission error also outranks an independently blocking readiness result.</summary>
    [Fact]
    public void FirmwareSlotBlockingInspectionOutranksBlockedSelectionText()
    {
        var slot = new FirmwareSlotViewModel(
            "ldc",
            "LDC BIN",
            "Select LDC firmware",
            FirmwareSlotKind.Dp)
        {
            FilePath = "C:\\firmware\\ldc.bin",
        };
        slot.SetSelectionReadiness(
            ResolvedChildReadiness.Blocked,
            "Blocked",
            "Reference policy blocks this child.",
            "Blocked. Reference policy blocks this child.");
        slot.SetInputInspection(
            FirmwareInputInspectionSeverity.Blocking,
            "The selected BIN failed structural admission.");

        Assert.Equal(FirmwareSlotSemanticState.Error, slot.SemanticState);
        Assert.Equal("Error", slot.SemanticStateLabel);
        Assert.Equal("The selected BIN failed structural admission.", slot.SemanticStateDetail);
    }

    /// <summary>Firmware facts are visible by default while retaining localized collapse and overflow controls.</summary>
    [Fact]
    public void FirmwareSlotFactsLimitPrimaryCardsAndPreserveAdditionalFacts()
    {
        var slot = new FirmwareSlotViewModel(
            "base",
            "Reference BIN",
            "Select reference firmware",
            FirmwareSlotKind.Base);
        FirmwareSlotFactViewModel[] facts =
        [
            new("DP Version", "D01-01"),
            new("Jira Index", "NVT-1"),
            new("Common FW Version", "2.0.0"),
            new("TP Version", "T01-01"),
            new("PID", "0x5195"),
        ];

        slot.SetFirmwareFacts(facts);

        Assert.Equal(4, slot.PrimaryFirmwareFacts.Count);
        _ = Assert.Single(slot.AdditionalFirmwareFacts);
        Assert.Equal("PID", slot.AdditionalFirmwareFacts[0].Label);
        Assert.True(slot.HasAdditionalFirmwareFacts);
        Assert.Equal("Show 1 more details", slot.AdditionalFirmwareFactsLabel);

        slot.IsAdditionalFirmwareFactsExpanded = true;

        slot.RelocalizeFirmwareFacts(facts);

        Assert.True(slot.IsAdditionalFirmwareFactsExpanded);
        Assert.Equal("Show fewer details", slot.AdditionalFirmwareFactsLabel);
        Assert.Equal(facts, slot.FirmwareFacts);
    }

    /// <summary>Fact state remains typed and NotApplicable facts never enter the active card.</summary>
    [Fact]
    public void FirmwareSlotFactsRetainTypedStateAndOmitNotApplicable()
    {
        var slot = new FirmwareSlotViewModel(
            "base",
            "Reference BIN",
            "Select reference firmware",
            FirmwareSlotKind.Base);
        var unknown = new FirmwareSlotFactViewModel(
            "AB",
            "Unknown",
            FirmwareSlotFactState.Unknown,
            "Unknown",
            "Review the selected file and report details.");
        var pending = new FirmwareSlotFactViewModel(
            "DP",
            "Pending",
            FirmwareSlotFactState.PendingInput,
            "Waiting for DP BIN",
            "Load the prerequisite input, then inspect again.");
        var omitted = new FirmwareSlotFactViewModel("LDC", "Not applicable", FirmwareSlotFactState.NotApplicable);

        slot.SetFirmwareFacts([unknown, pending, omitted]);

        Assert.Equal([unknown, pending], slot.FirmwareFacts);
        Assert.True(unknown.IsUnknown);
        Assert.True(pending.IsPendingInput);
        Assert.DoesNotContain(slot.FirmwareFacts, static fact => fact.IsNotApplicable);
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            new FirmwareSlotFactViewModel("TP", "T01-01", FirmwareSlotFactState.Warning));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            new FirmwareSlotFactViewModel("TP", "T01-01", (FirmwareSlotFactState)999, "Invalid", "Invalid state."));

        var emptyInspection = new FirmwareInspectionSnapshot(null, null, null, null, null, null);
        FirmwareSlotFactViewModel localizedUnknown = Assert.Single(
            UiCompositionRunner.GetDpFirmwareSlotFacts(
                emptyInspection,
                ShellTextResources.For(ShellLanguage.ChineseTraditional)));
        Assert.Equal("未知", localizedUnknown.Value);
        Assert.Contains("DP", localizedUnknown.StateAutomationText, StringComparison.Ordinal);
        Assert.Contains("未知", localizedUnknown.StateAutomationText, StringComparison.Ordinal);
        Assert.Contains("無法解碼 metadata", localizedUnknown.StateAutomationText, StringComparison.Ordinal);
    }
}
