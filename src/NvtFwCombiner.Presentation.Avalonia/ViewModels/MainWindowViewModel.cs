using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>View model for the production-backed firmware workbench.</summary>
public sealed partial class MainWindowViewModel : ObservableObject
{
    private const string DpReplaceMode = "DP";
    private const string CtrlRamReplaceMode = "CtrlRAM";
    private const string GeneralReplaceMode = "General";
    private const string MergeDpSlotId = "merge-dp";
    private const string MergeTpSlotId = "merge-tp";
    private const string MergeLdSlotId = "merge-ld";

    private static readonly JsonSerializerOptions ReportJsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly FirmwareSlotViewModel _mergeDpSlot = new(
        MergeDpSlotId,
        "DP BIN",
        "Display payload for Standard Merge");
    private readonly FirmwareSlotViewModel _mergeTpSlot = new(
        MergeTpSlotId,
        "TP BIN",
        "Touch payload for Standard Merge");
    private readonly FirmwareSlotViewModel _mergeLdSlot = new(
        MergeLdSlotId,
        "LD BIN",
        "Required only when the selected profile uses LD",
        isOptional: true);
    private readonly FirmwareSlotViewModel _replaceBaseSlot = new(
        "replace-base",
        "Base flash BIN",
        "Reference firmware image before replacement");
    private readonly FirmwareSlotViewModel _replaceDpSlot = new(
        "replace-dp",
        "DP replacement BIN",
        "DP payload; short files are padded by the profile policy");
    private readonly FirmwareSlotViewModel _replaceLdSlot = new(
        "replace-ld",
        "LD replacement BIN",
        "LD payload under DP Replace",
        isOptional: true);
    private readonly FirmwareSlotViewModel _replaceCtrlRamSlot = new(
        "replace-ctrlram",
        "CtrlRAM payload BIN",
        "CtrlRAM payload; oversized content is truncated with warning");
    private readonly FirmwareSlotViewModel _replaceGeneralSlot = new(
        "replace-general",
        "General input BIN",
        "Explicit range input for General Replace");

    /// <summary>Initializes the main workbench view model.</summary>
    public MainWindowViewModel(
        string shellVersion,
        string workspaceTitle,
        string workspaceSummary,
        string previewActionLabel,
        string buildActionLabel,
        string reportModalActionLabel,
        string deviceContextTitle,
        string icLabel,
        string numberLabel,
        string deviceContextStatus,
        PlanningCardViewModel settingsPreview,
        PlanningCardViewModel mergePreview,
        PlanningCardViewModel replacePreview,
        string footerStatus)
    {
        ShellVersion = shellVersion;
        WorkspaceTitle = workspaceTitle;
        WorkspaceSummary = workspaceSummary;
        PreviewActionLabel = previewActionLabel;
        BuildActionLabel = buildActionLabel;
        ReportModalActionLabel = reportModalActionLabel;
        DeviceContextTitle = deviceContextTitle;
        IcLabel = icLabel;
        NumberLabel = numberLabel;
        DeviceContextRefreshSummary = deviceContextStatus;
        SettingsPreview = settingsPreview;
        MergePreview = mergePreview;
        ReplacePreview = replacePreview;
        FooterStatus = footerStatus;
        ShowHomeCommand = new RelayCommand(() => SetSelectedPage(ShellPage.Home));
        ShowSettingsCommand = new RelayCommand(() => SetSelectedPage(ShellPage.Settings));
        ShowMergeCommand = new RelayCommand(() => SetSelectedPage(ShellPage.Merge));
        ShowReplaceCommand = new RelayCommand(() => SetSelectedPage(ShellPage.Replace));
        ShowDpReplaceCommand = new RelayCommand(() => SelectReplaceMode(DpReplaceMode));
        ShowCtrlRamReplaceCommand = new RelayCommand(() => SelectReplaceMode(CtrlRamReplaceMode));
        ShowGeneralReplaceCommand = new RelayCommand(() => SelectReplaceMode(GeneralReplaceMode));
        ShowNormalMergeCommand = new RelayCommand(() => SelectMergeMode("Normal"));
        PreviewMergeCommand = new AsyncRelayCommand(
            () => RunStandardMergeAsync(build: false),
            CanRunStandardMerge);
        BuildMergeCommand = new AsyncRelayCommand(
            () => RunStandardMergeAsync(build: true),
            CanRunStandardMerge);

        MergeSlots.Add(_mergeDpSlot);
        MergeSlots.Add(_mergeTpSlot);
        MergeSlots.Add(_mergeLdSlot);
        RefreshContextState();
    }

    /// <summary>Gets the shell milestone label.</summary>
    public string ShellVersion { get; }

    /// <summary>Gets the workspace title.</summary>
    public string WorkspaceTitle { get; }

    /// <summary>Gets the workspace summary.</summary>
    public string WorkspaceSummary { get; }

    /// <summary>Gets the preview action label.</summary>
    public string PreviewActionLabel { get; }

    /// <summary>Gets the build action label.</summary>
    public string BuildActionLabel { get; }

    /// <summary>Gets the report modal action label.</summary>
    public string ReportModalActionLabel { get; }

    /// <summary>Gets the shared device context heading.</summary>
    public string DeviceContextTitle { get; }

    /// <summary>Gets the IC field label.</summary>
    public string IcLabel { get; }

    /// <summary>Gets the IC count/variant field label.</summary>
    public string NumberLabel { get; }

    /// <summary>Gets the shared device context status text.</summary>
    public string DeviceContextStatus => $"{SelectedIc} / {SelectedNumber}: {DeviceContextRefreshSummary}";

    /// <summary>Gets supported production IC choices.</summary>
    public IReadOnlyList<string> IcChoices { get; } = UiCompositionRunner.GetSupportedIcIds();

    /// <summary>Gets replace mode choices.</summary>
    public IReadOnlyList<string> ReplaceModeChoices { get; } =
    [
        DpReplaceMode,
        CtrlRamReplaceMode,
        GeneralReplaceMode,
    ];

    /// <summary>Gets settings sample content.</summary>
    public PlanningCardViewModel SettingsPreview { get; }

    /// <summary>Gets merge preview sample content.</summary>
    public PlanningCardViewModel MergePreview { get; }

    /// <summary>Gets replace preview sample content.</summary>
    public PlanningCardViewModel ReplacePreview { get; }

    /// <summary>Gets footer status content.</summary>
    public string FooterStatus { get; }

    /// <summary>Gets merge input slots.</summary>
    public ObservableCollection<FirmwareSlotViewModel> MergeSlots { get; } = [];

    /// <summary>Gets replace input slots for the selected replace mode.</summary>
    public ObservableCollection<FirmwareSlotViewModel> ReplaceSlots { get; } = [];

    /// <summary>Gets replace inspector rows for the selected replace mode.</summary>
    public ObservableCollection<string> ActiveReplaceRows { get; } = [];

    /// <summary>Gets merge inspector rows for the selected IC and Number.</summary>
    public ObservableCollection<string> ActiveMergeRows { get; } = [];

    /// <summary>Gets CtrlRAM region rows for the selected IC and Number.</summary>
    public ObservableCollection<CtrlRamRegionViewModel> CtrlRamRegions { get; } = [];

    /// <summary>Gets the latest UI-triggered run summary.</summary>
    public UiRunResultViewModel LastRunResult { get; private set; } = new(
        "No run yet",
        "Drop required BIN files, then run Preview.",
        "No output",
        succeeded: true);

    /// <summary>Gets the loaded run report summary.</summary>
    public ReportReviewViewModel LoadedReport { get; private set; } = ReportReviewViewModel.Empty;

    /// <summary>True when a run report is loaded into the shell.</summary>
    public bool HasLoadedReport => !LoadedReport.IsEmpty;

    /// <summary>True when the selected CtrlRAM catalog has visible rows.</summary>
    public bool HasCtrlRamRegions => CtrlRamRegions.Count > 0;

    /// <summary>Gets selected CtrlRAM row summary text.</summary>
    public string CtrlRamRegionSummary => string.Equals(SelectedNumber, "single", StringComparison.OrdinalIgnoreCase)
        ? $"{SelectedIc} single: TP Overview regions that require multi-chip context are hidden."
        : $"{SelectedIc} {SelectedNumber}: TP Overview CtrlRAM regions are loaded from the production flash-map catalog.";

    /// <summary>Gets the standard merge support summary for the selected IC.</summary>
    public string StandardMergeSupportSummary => IsStandardMergeSupported
        ? $"{SelectedIc}: Standard Merge profile found. Required slots: {GetRequiredStandardMergeSlotLabels()}."
        : $"{SelectedIc}: no Standard Merge profile yet.";

    /// <summary>Gets the Replace card status for the home screen.</summary>
    public string HomeReplaceStatus => $"{SelectedIc} / {SelectedNumber}: {CtrlRamRegions.Count} CtrlRAM regions";

    /// <summary>Gets the Merge card status for the home screen.</summary>
    public string HomeMergeStatus => IsStandardMergeSupported
        ? $"{SelectedIc}: standard merge ready"
        : $"{SelectedIc}: no merge profile";

    /// <summary>Gets the selected shell page.</summary>
    public ShellPage SelectedPage { get; private set; } = ShellPage.Home;

    /// <summary>Gets the selected Merge quick-jump mode.</summary>
    public string SelectedMergeMode { get; private set; } = "Normal";

    /// <summary>True when the clean home view is visible.</summary>
    public bool IsHomeVisible => SelectedPage == ShellPage.Home;

    /// <summary>True when the Settings page is visible.</summary>
    public bool IsSettingsVisible => SelectedPage == ShellPage.Settings;

    /// <summary>True when the Merge page is visible.</summary>
    public bool IsMergeVisible => SelectedPage == ShellPage.Merge;

    /// <summary>True when the Replace page is visible.</summary>
    public bool IsReplaceVisible => SelectedPage == ShellPage.Replace;

    /// <summary>True when DP Replace is selected.</summary>
    public bool IsDpReplaceModeSelected => string.Equals(SelectedReplaceMode, DpReplaceMode, StringComparison.Ordinal);

    /// <summary>True when CtrlRAM Replace is selected.</summary>
    public bool IsCtrlRamReplaceModeSelected => string.Equals(SelectedReplaceMode, CtrlRamReplaceMode, StringComparison.Ordinal);

    /// <summary>True when General Replace is selected.</summary>
    public bool IsGeneralReplaceModeSelected => string.Equals(SelectedReplaceMode, GeneralReplaceMode, StringComparison.Ordinal);

    /// <summary>True when Normal Merge is selected.</summary>
    public bool IsNormalMergeModeSelected => string.Equals(SelectedMergeMode, "Normal", StringComparison.Ordinal);

    /// <summary>True when selected IC has a built-in standard merge profile.</summary>
    public bool IsStandardMergeSupported => UiCompositionRunner.IsStandardMergeSupported(SelectedIc);

    /// <summary>Description shown under the selected replace mode.</summary>
    public string SelectedReplaceModeDescription => SelectedReplaceMode switch
    {
        DpReplaceMode => "Replace DP and optional LD payloads without CRC postbuild.",
        CtrlRamReplaceMode => "Replace CtrlRAM payloads, then run combiner.exe postbuild for CRC/header refresh.",
        GeneralReplaceMode => "Replace an explicit profile-approved range with a selected input BIN.",
        _ => "Select a replace mode.",
    };

    /// <summary>Status shown in the replace inspector.</summary>
    public string ReplaceReadinessStatus =>
        $"{SelectedReplaceMode} / {SelectedIc} / {SelectedNumber}: input collection only.";

    /// <summary>Status shown in the merge inspector.</summary>
    public string MergeReadinessStatus => IsStandardMergeSupported
        ? $"{SelectedIc} / {SelectedNumber}: drop {GetRequiredStandardMergeSlotLabels()} BIN files."
        : $"{SelectedIc}: Standard Merge is not available yet.";

    /// <summary>Command that returns to the clean home view.</summary>
    public IRelayCommand ShowHomeCommand { get; }

    /// <summary>Command that opens Settings.</summary>
    public IRelayCommand ShowSettingsCommand { get; }

    /// <summary>Command that opens Merge.</summary>
    public IRelayCommand ShowMergeCommand { get; }

    /// <summary>Command that opens Replace.</summary>
    public IRelayCommand ShowReplaceCommand { get; }

    /// <summary>Command that opens DP Replace.</summary>
    public IRelayCommand ShowDpReplaceCommand { get; }

    /// <summary>Command that opens CtrlRAM Replace.</summary>
    public IRelayCommand ShowCtrlRamReplaceCommand { get; }

    /// <summary>Command that opens General Replace.</summary>
    public IRelayCommand ShowGeneralReplaceCommand { get; }

    /// <summary>Command that opens Normal Merge.</summary>
    public IRelayCommand ShowNormalMergeCommand { get; }

    /// <summary>Command that previews Standard Merge through the application core.</summary>
    public IAsyncRelayCommand PreviewMergeCommand { get; }

    /// <summary>Command that builds Standard Merge output through the application core.</summary>
    public IAsyncRelayCommand BuildMergeCommand { get; }

    /// <summary>Sets a local file path for a UI input slot.</summary>
    public void SetSlotFile(string slotId, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slotId);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        FirmwareSlotViewModel? slot = FindSlot(slotId);
        if (slot is null)
        {
            return;
        }

        slot.FilePath = path;
        RefreshCommandState();
    }

    /// <summary>Loads a CLI/application run report JSON into the readable report panel.</summary>
    public void LoadReportJson(string json, string sourceName)
    {
        try
        {
            LoadedReport = ReportReviewViewModel.FromJson(json, sourceName);
        }
        catch (JsonException exception)
        {
            LoadedReport = ReportReviewViewModel.Error(sourceName, exception.Message);
        }

        OnPropertyChanged(nameof(LoadedReport));
        OnPropertyChanged(nameof(HasLoadedReport));
    }

    private string DeviceContextRefreshSummary { get; }

    private void RefreshNumberChoicesForSelectedIc()
    {
        IReadOnlyList<string> nextChoices = UiCompositionRunner.GetNumberChoices(SelectedIc);
        NumberChoices = nextChoices;
        if (!nextChoices.Contains(SelectedNumber, StringComparer.Ordinal))
        {
            SelectedNumber = nextChoices[0];
        }
    }

    private void RefreshContextState(bool resetRunResult = false)
    {
        RefreshCtrlRamRegions();
        RefreshMergeSlotRequirements();
        RefreshMergeModeState();
        RefreshReplaceModeState();
        RefreshCommandState();
        NotifyContextTextChanged();
        if (resetRunResult)
        {
            ResetRunResultForContextChange();
        }
    }

    private void RefreshCtrlRamRegions()
    {
        CtrlRamRegions.Clear();
        foreach (TpFlashMapRegion region in UiCompositionRunner.GetCtrlRamRegions(SelectedIc, SelectedNumber))
        {
            CtrlRamRegions.Add(new CtrlRamRegionViewModel(
                region.DisplayName,
                ToHex(region.Range.Start),
                ToHex(region.Range.Length),
                region.Tags.Any(tag =>
                    string.Equals(tag, "diff", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tag, "dlm", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(tag, "slave", StringComparison.OrdinalIgnoreCase))));
        }

        OnPropertyChanged(nameof(HasCtrlRamRegions));
        OnPropertyChanged(nameof(CtrlRamRegionSummary));
    }

    private void RefreshMergeSlotRequirements()
    {
        IReadOnlyList<string> required = UiCompositionRunner.GetStandardMergeRequiredAddressSpaces(SelectedIc);
        _mergeDpSlot.IsOptional = !required.Contains("dp-input", StringComparer.Ordinal);
        _mergeTpSlot.IsOptional = !required.Contains("tp-input", StringComparer.Ordinal);
        _mergeLdSlot.IsOptional = !required.Contains("ld-input", StringComparer.Ordinal);
    }

    private void RefreshMergeModeState()
    {
        ActiveMergeRows.Clear();
        string? profileId = UiCompositionRunner.GetStandardMergeProfileId(SelectedIc);
        if (profileId is null)
        {
            AddMergeRows(
                $"Profile: not available for {SelectedIc}",
                "Preview and Build stay disabled until a profile is added.",
                $"{SelectedIc} / {SelectedNumber} still refreshes Replace region policy.");
            return;
        }

        AddMergeRows(
            $"Profile: {profileId}",
            $"Required slots: {GetRequiredStandardMergeSlotLabels()}",
            GetStandardMergeRangeSummary());
    }

    private void RefreshReplaceModeState()
    {
        ReplaceSlots.Clear();
        ActiveReplaceRows.Clear();
        switch (SelectedReplaceMode)
        {
            case DpReplaceMode:
                ReplaceSlots.Add(_replaceBaseSlot);
                ReplaceSlots.Add(_replaceDpSlot);
                ReplaceSlots.Add(_replaceLdSlot);
                AddRows(
                    $"{SelectedIc} / {SelectedNumber}: DP Replace input policy is active.",
                    "DP replacement can pad short DP/LD inputs when the profile permits it.",
                    "CRC/header postbuild is not required for DP/LD replacement.",
                    "LD is grouped under DP Replace, but remains a separate BIN slot.");
                break;
            case CtrlRamReplaceMode:
                ReplaceSlots.Add(_replaceBaseSlot);
                ReplaceSlots.Add(_replaceCtrlRamSlot);
                AddRows(
                    $"{SelectedIc} / {SelectedNumber}: {CtrlRamRegions.Count} visible CtrlRAM regions.",
                    "Visible CtrlRAM regions come from TP Overview for the selected IC.",
                    "Single hides DIFF/DLM and slave-only regions unless an IC override is added.",
                    "combiner.exe postbuild will be required before final TDDI FW output.");
                break;
            case GeneralReplaceMode:
                ReplaceSlots.Add(_replaceBaseSlot);
                ReplaceSlots.Add(_replaceGeneralSlot);
                AddRows(
                    $"{SelectedIc} / {SelectedNumber}: General Replace input policy is active.",
                    "General Replace uses explicit source and target ranges.",
                    "The compiler must approve the selected region before build.",
                    "Use this only when DP/CtrlRAM profiles do not fit the edit.");
                break;
            default:
                AddRows("Select a replace mode.");
                break;
        }

        OnPropertyChanged(nameof(SelectedReplaceModeDescription));
        OnPropertyChanged(nameof(IsDpReplaceModeSelected));
        OnPropertyChanged(nameof(IsCtrlRamReplaceModeSelected));
        OnPropertyChanged(nameof(IsGeneralReplaceModeSelected));
    }

    private void AddRows(params string[] rows)
    {
        foreach (string row in rows)
        {
            ActiveReplaceRows.Add(row);
        }
    }

    private void AddMergeRows(params string[] rows)
    {
        foreach (string row in rows)
        {
            ActiveMergeRows.Add(row);
        }
    }

    private void NotifyContextTextChanged()
    {
        OnPropertyChanged(nameof(IsStandardMergeSupported));
        OnPropertyChanged(nameof(StandardMergeSupportSummary));
        OnPropertyChanged(nameof(HomeReplaceStatus));
        OnPropertyChanged(nameof(HomeMergeStatus));
        OnPropertyChanged(nameof(MergeReadinessStatus));
        OnPropertyChanged(nameof(ReplaceReadinessStatus));
    }

    private void ResetRunResultForContextChange()
    {
        LastRunResult = new UiRunResultViewModel(
            "Context changed",
            $"{SelectedIc} / {SelectedNumber}: rerun Preview before Build.",
            "No output",
            succeeded: true);
        OnPropertyChanged(nameof(LastRunResult));
    }

    private string GetRequiredStandardMergeSlotLabels()
    {
        IReadOnlyList<string> required = UiCompositionRunner.GetStandardMergeRequiredAddressSpaces(SelectedIc);
        return required.Count == 0
            ? "none"
            : string.Join(", ", required.Select(AddressSpaceLabel));
    }

    private string GetStandardMergeRangeSummary()
    {
        return SelectedIc is "NT51950" or "NT51951"
            ? "TP paste range: 0xA000-0x36FFF; 0x37000-0x37FFF is preserved customer information."
            : "Address ranges come from the built-in Standard Merge profile.";
    }

    private static string AddressSpaceLabel(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            "dp-input" => "DP",
            "tp-input" => "TP",
            "ld-input" => "LD",
            _ => addressSpaceId,
        };
    }

    private static string ToHex(long value)
    {
        return $"0x{value:X5}";
    }

    private FirmwareSlotViewModel? FindSlot(string slotId)
    {
        return MergeSlots.Concat(ReplaceSlots)
            .Concat([
                _replaceBaseSlot,
                _replaceDpSlot,
                _replaceLdSlot,
                _replaceCtrlRamSlot,
                _replaceGeneralSlot,
            ])
            .FirstOrDefault(slot => string.Equals(slot.SlotId, slotId, StringComparison.Ordinal));
    }

    private void SelectReplaceMode(string mode)
    {
        if (ReplaceModeChoices.Contains(mode, StringComparer.Ordinal))
        {
            SelectedReplaceMode = mode;
        }

        SetSelectedPage(ShellPage.Replace);
    }

    private void SelectMergeMode(string mode)
    {
        if (!string.Equals(SelectedMergeMode, mode, StringComparison.Ordinal))
        {
            SelectedMergeMode = mode;
            OnPropertyChanged(nameof(SelectedMergeMode));
            OnPropertyChanged(nameof(IsNormalMergeModeSelected));
        }

        SetSelectedPage(ShellPage.Merge);
    }

    private void SetSelectedPage(ShellPage page)
    {
        if (SelectedPage == page)
        {
            return;
        }

        SelectedPage = page;
        OnPropertyChanged(nameof(SelectedPage));
        OnPropertyChanged(nameof(IsHomeVisible));
        OnPropertyChanged(nameof(IsSettingsVisible));
        OnPropertyChanged(nameof(IsMergeVisible));
        OnPropertyChanged(nameof(IsReplaceVisible));
    }

    private bool CanRunStandardMerge()
    {
        IReadOnlyList<string> requiredAddressSpaces = UiCompositionRunner.GetStandardMergeRequiredAddressSpaces(SelectedIc);
        return requiredAddressSpaces.Count > 0 && requiredAddressSpaces.All(addressSpace =>
            MergeSlotForAddressSpace(addressSpace) is { HasFile: true });
    }

    private async Task RunStandardMergeAsync(bool build)
    {
        try
        {
            CompositionRunResult result = await UiCompositionRunner
                .RunStandardMergeAsync(SelectedIc, CreateStandardMergeSlotPaths(), build, CancellationToken.None)
                .ConfigureAwait(false);
            ApplyRunResult(result, build);
        }
        catch (Exception exception) when (exception is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            LastRunResult = new UiRunResultViewModel(
                build ? "Build failed" : "Preview failed",
                exception.Message,
                "No output",
                succeeded: false);
            OnPropertyChanged(nameof(LastRunResult));
        }
    }

    private Dictionary<string, string> CreateStandardMergeSlotPaths()
    {
        Dictionary<string, string> paths = new(StringComparer.Ordinal);
        AddPath(paths, "dp-input", _mergeDpSlot);
        AddPath(paths, "tp-input", _mergeTpSlot);
        AddPath(paths, "ld-input", _mergeLdSlot);
        return paths;
    }

    private static void AddPath(
        Dictionary<string, string> paths,
        string addressSpaceId,
        FirmwareSlotViewModel slot)
    {
        if (!string.IsNullOrWhiteSpace(slot.FilePath))
        {
            paths[addressSpaceId] = slot.FilePath;
        }
    }

    private void ApplyRunResult(CompositionRunResult result, bool build)
    {
        bool succeeded = result.Status == CompositionExecutionStatus.Succeeded;
        string action = build ? "Build" : "Preview";
        LastRunResult = new UiRunResultViewModel(
            succeeded ? $"{action} succeeded" : $"{action} blocked",
            $"{result.Report.ProfileId} / {result.Report.Output.Size} bytes / {result.Report.Output.Sha256[..Math.Min(12, result.Report.Output.Sha256.Length)]}",
            result.CommittedOutputId ?? result.Report.Output.FileName,
            succeeded);
        OnPropertyChanged(nameof(LastRunResult));

        string json = JsonSerializer.Serialize(result.Report, ReportJsonOptions);
        LoadedReport = ReportReviewViewModel.FromJson(json, $"{action.ToLowerInvariant()} report");
        OnPropertyChanged(nameof(LoadedReport));
        OnPropertyChanged(nameof(HasLoadedReport));
    }

    private FirmwareSlotViewModel? MergeSlotForAddressSpace(string addressSpaceId)
    {
        return addressSpaceId switch
        {
            "dp-input" => _mergeDpSlot,
            "tp-input" => _mergeTpSlot,
            "ld-input" => _mergeLdSlot,
            _ => null,
        };
    }

    private void RefreshCommandState()
    {
        PreviewMergeCommand.NotifyCanExecuteChanged();
        BuildMergeCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedReplaceModeChanged(string value)
    {
        RefreshReplaceModeState();
    }

    partial void OnSelectedIcChanged(string value)
    {
        RefreshNumberChoicesForSelectedIc();
        RefreshContextState(resetRunResult: true);
    }

    partial void OnSelectedNumberChanged(string value)
    {
        RefreshContextState(resetRunResult: true);
    }

    /// <summary>Gets selected replace mode.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedReplaceModeDescription))]
    [NotifyPropertyChangedFor(nameof(ReplaceReadinessStatus))]
    [NotifyPropertyChangedFor(nameof(IsDpReplaceModeSelected))]
    [NotifyPropertyChangedFor(nameof(IsCtrlRamReplaceModeSelected))]
    [NotifyPropertyChangedFor(nameof(IsGeneralReplaceModeSelected))]
    public partial string SelectedReplaceMode { get; set; } = DpReplaceMode;

    /// <summary>Gets supported IC count/variant choices for the selected IC.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial IReadOnlyList<string> NumberChoices { get; set; } = UiCompositionRunner.GetNumberChoices("NT51950");

    /// <summary>Gets or sets the selected IC id in the shared context row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial string SelectedIc { get; set; } = "NT51950";

    /// <summary>Gets or sets the selected IC count/variant in the shared context row.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeviceContextStatus))]
    public partial string SelectedNumber { get; set; } = "single";
}

/// <summary>Top-level shell page state.</summary>
public enum ShellPage
{
    /// <summary>Clean home view with three entry cards.</summary>
    Home,

    /// <summary>Settings planning page.</summary>
    Settings,

    /// <summary>Merge planning page.</summary>
    Merge,

    /// <summary>Replace planning page.</summary>
    Replace,
}
