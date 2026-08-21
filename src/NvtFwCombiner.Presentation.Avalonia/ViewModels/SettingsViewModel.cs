using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.VersionManagement;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal enum SettingsSection
{
    Overview,
    Preferences,
    Version,
    SupportMatrix,
}

internal sealed partial class SettingChoiceViewModel(string value, string label) : ObservableObject
{
    public string Value { get; } = value;

    [ObservableProperty]
    public partial string Label { get; internal set; } = label;
}

internal sealed partial class SettingsViewModel : ObservableObject
{
    private readonly string _appVersion;
    private readonly Func<ShellTextResources> _textProvider;
    private readonly IVersionManagementExperience? _versionManagement;

    internal SettingsViewModel(
        string appVersion,
        ICanonicalSupportMatrixQuery supportMatrixQuery,
        Func<ShellTextResources> textProvider,
        IVersionManagementExperience? versionManagement = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);
        _appVersion = appVersion;
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        _versionManagement = versionManagement;
        SupportMatrix = new SupportMatrixPresentationViewModel(supportMatrixQuery);
    }

    public ObservableCollection<SettingSummaryViewModel> OverviewRows { get; } = [];

    public ObservableCollection<SettingSummaryViewModel> CapabilityRows { get; } = [];

    public IReadOnlyList<SettingChoiceViewModel> ThemeChoices { get; } =
    [
        new("System", "System"),
        new("Light", "Light"),
        new("Dark", "Dark"),
    ];

    public IReadOnlyList<SettingChoiceViewModel> LanguageChoices { get; } =
    [
        new("English", "English"),
        new("Traditional Chinese", "Traditional Chinese"),
    ];

    /// <summary>Focused immutable Support Matrix disclosure.</summary>
    public SupportMatrixPresentationViewModel SupportMatrix { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverviewSelected))]
    [NotifyPropertyChangedFor(nameof(IsPreferencesSelected))]
    [NotifyPropertyChangedFor(nameof(IsVersionSelected))]
    [NotifyPropertyChangedFor(nameof(IsSupportMatrixOpen))]
    public partial SettingsSection SelectedSection { get; private set; }

    public bool IsOverviewSelected => SelectedSection == SettingsSection.Overview;

    public bool IsPreferencesSelected => SelectedSection == SettingsSection.Preferences;

    public bool IsVersionSelected => SelectedSection == SettingsSection.Version;

    public bool IsSupportMatrixOpen => SelectedSection == SettingsSection.SupportMatrix;

    internal void Refresh(ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(text);
        ApplyChoiceLabels(text);
        RefreshVersionLabels();
        SupportMatrix.Refresh(text);
        bool chinese = text.Language == ShellLanguage.ChineseTraditional;
        SupportMatrixRowViewModel[] authoringAvailableRows =
        [
            .. SupportMatrix.Rows.Where(static row => row.IsAuthoringAvailable),
        ];
        int catalogIcCount = authoringAvailableRows
            .Select(static row => row.IcId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        bool hasPublication = SupportMatrix.CatalogState is
            CanonicalSupportMatrixCatalogState.Current or
            CanonicalSupportMatrixCatalogState.LastKnownGood;
        string pendingValue = SupportMatrix.CatalogState ==
            CanonicalSupportMatrixCatalogState.Loading
                ? chinese ? "載入中…" : "Loading…"
                : text.NotAvailableLabel;
        string pendingStatus = SupportMatrix.CatalogState ==
            CanonicalSupportMatrixCatalogState.Loading
                ? chinese ? "載入中" : "Loading"
                : chinese ? "無法使用" : "Unavailable";
        string CatalogValue(int value, string unit = "")
        {
            return hasPublication ? $"{value}{unit}" : pendingValue;
        }

        string CatalogStatus(string availableStatus)
        {
            return hasPublication ? availableStatus : pendingStatus;
        }

        string CatalogIcValue(int value)
        {
            return hasPublication
                ? chinese ? $"{value} 個 IC" : $"{value} {(value == 1 ? "IC" : "ICs")}"
                : pendingValue;
        }

        ReplaceRows(
            OverviewRows,
            [
                new SettingSummaryViewModel(
                    chinese ? "應用程式版本" : "App version",
                    _appVersion,
                    chinese ? "目前安裝套件的應用程式版本。" : "Application version in the installed package.",
                    chinese ? "目前版本" : "Current"),
                new SettingSummaryViewModel(
                    chinese ? "IC 目錄" : "IC catalog",
                    CatalogValue(catalogIcCount),
                    chinese
                        ? "至少有一條可編輯路徑的 IC；每個工作流程仍分開判定。"
                        : "ICs with at least one authoring-available route; workflows remain independent.",
                    CatalogStatus(chinese ? "目錄" : "Catalog")),
                new SettingSummaryViewModel(
                    "Standard Merge",
                    CatalogIcValue(CountAvailableIcs(
                        authoringAvailableRows,
                        ExperienceIds.StandardMerge)),
                    chinese
                        ? "至少有一條 Standard Merge 路徑可出現在一般編輯選擇器中的 IC。"
                        : "ICs with at least one Standard Merge route available to ordinary authoring selectors.",
                    CatalogStatus(chinese ? "可編輯" : "Authoring available")),
                new SettingSummaryViewModel(
                    "DP Replace",
                    CatalogIcValue(CountAvailableIcs(
                        authoringAvailableRows,
                        ExperienceIds.DpReplace)),
                    chinese
                        ? "至少有一條 DP Replace 路徑可出現在一般編輯選擇器中的 IC。"
                        : "ICs with at least one DP Replace route available to ordinary authoring selectors.",
                    CatalogStatus(chinese ? "可編輯" : "Authoring available")),
            ]);

        ReplaceRows(
            CapabilityRows,
            [
                new SettingSummaryViewModel(
                    chinese ? "CtrlRAM Replace 可用的 IC" : "CtrlRAM Replace available ICs",
                    CatalogIcValue(CountAvailableIcs(
                        authoringAvailableRows,
                        ExperienceIds.CtrlRamReplace)),
                    chinese
                        ? "可出現在一般編輯選擇器中；執行、發布與證據狀態請見支援矩陣。"
                        : "Available to ordinary authoring selectors; see the matrix for execution, publication, and evidence.",
                    CatalogStatus(chinese ? "可用" : "Available")),
            ]);
    }

    private static int CountAvailableIcs(
        IEnumerable<SupportMatrixRowViewModel> rows,
        string workflowId)
    {
        return rows
            .Where(row => StringComparer.Ordinal.Equals(row.WorkflowId, workflowId))
            .Select(static row => row.IcId)
            .Distinct(StringComparer.Ordinal)
            .Count();
    }

    private void ApplyChoiceLabels(ShellTextResources text)
    {
        ThemeChoices[0].Label = text.SystemThemeChoiceLabel;
        ThemeChoices[1].Label = text.LightThemeChoiceLabel;
        ThemeChoices[2].Label = text.DarkThemeChoiceLabel;
        LanguageChoices[0].Label = text.EnglishLanguageChoiceLabel;
        LanguageChoices[1].Label = text.ChineseTraditionalLanguageChoiceLabel;
    }

    [RelayCommand]
    private void SelectSection(SettingsSection section)
    {
        if (section == SettingsSection.SupportMatrix)
        {
            SupportMatrix.Refresh(_textProvider());
        }

        if (section == SettingsSection.Version)
        {
            _ = RefreshVersionAsync(isAutomatic: false);
        }

        SelectedSection = section;
    }

    private static void ReplaceRows<T>(ObservableCollection<T> target, IEnumerable<T> rows)
    {
        target.Clear();
        foreach (T row in rows)
        {
            target.Add(row);
        }
    }
}
