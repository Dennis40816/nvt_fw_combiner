using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed partial class SettingsViewModel : ObservableObject
{
    private readonly string _appVersion;
    private readonly Func<ShellTextResources> _textProvider;

    internal SettingsViewModel(
        string appVersion,
        ICanonicalSupportMatrixQuery supportMatrixQuery,
        Func<ShellTextResources> textProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(appVersion);
        _appVersion = appVersion;
        _textProvider = textProvider ?? throw new ArgumentNullException(nameof(textProvider));
        SupportMatrix = new SupportMatrixPresentationViewModel(supportMatrixQuery);
        OpenSupportMatrixCommand = new RelayCommand(OpenSupportMatrix);
        CloseSupportMatrixCommand = new RelayCommand(CloseSupportMatrix);
    }

    public ObservableCollection<SettingSummaryViewModel> OverviewRows { get; } = [];

    public ObservableCollection<SettingSummaryViewModel> CapabilityRows { get; } = [];

    public IReadOnlyList<string> ThemeChoices { get; } = ["System", "Light", "Dark"];

    public IReadOnlyList<string> LanguageChoices { get; } = ["English", "Traditional Chinese"];

    /// <summary>Focused immutable Support Matrix disclosure.</summary>
    public SupportMatrixPresentationViewModel SupportMatrix { get; }

    public IRelayCommand OpenSupportMatrixCommand { get; }

    public IRelayCommand CloseSupportMatrixCommand { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOverviewVisible))]
    public partial bool IsSupportMatrixOpen { get; private set; }

    public bool IsOverviewVisible => !IsSupportMatrixOpen;

    internal void Refresh(ShellTextResources text)
    {
        ArgumentNullException.ThrowIfNull(text);
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
            return CatalogValue(value, value == 1 ? " IC" : " ICs");
        }

        ReplaceRows(
            OverviewRows,
            [
                new SettingSummaryViewModel(
                    chinese ? "App 版本" : "App version",
                    _appVersion,
                    chinese ? "目前安裝套件的應用程式版本。" : "Application version in the installed package.",
                    chinese ? "目前版本" : "Current"),
                new SettingSummaryViewModel(
                    chinese ? "IC 目錄" : "IC catalog",
                    CatalogValue(catalogIcCount),
                    chinese
                        ? "至少有一條 authoring-available 路徑的 IC；各 workflow 仍分開判定。"
                        : "ICs with at least one authoring-available route; workflows remain independent.",
                    CatalogStatus("Catalog")),
                new SettingSummaryViewModel(
                    "Standard Merge",
                    CatalogIcValue(CountAvailableIcs(
                        authoringAvailableRows,
                        ExperienceIds.StandardMerge)),
                    chinese
                        ? "至少有一條 Standard Merge 路徑可出現在一般 authoring 選擇器的 IC。"
                        : "ICs with at least one Standard Merge route available to ordinary authoring selectors.",
                    CatalogStatus(chinese ? "可編輯" : "Authoring available")),
                new SettingSummaryViewModel(
                    "DP Replace",
                    CatalogIcValue(CountAvailableIcs(
                        authoringAvailableRows,
                        ExperienceIds.DpReplace)),
                    chinese
                        ? "至少有一條 DP Replace 路徑可出現在一般 authoring 選擇器的 IC。"
                        : "ICs with at least one DP Replace route available to ordinary authoring selectors.",
                    CatalogStatus(chinese ? "可編輯" : "Authoring available")),
            ]);

        ReplaceRows(
            CapabilityRows,
            [
                new SettingSummaryViewModel(
                    chinese ? "CtrlRAM Replace 可用 IC" : "CtrlRAM Replace available ICs",
                    CatalogIcValue(CountAvailableIcs(
                        authoringAvailableRows,
                        ExperienceIds.CtrlRamReplace)),
                    chinese
                        ? "可出現在一般 authoring 選擇器；execution、publication 與 evidence 狀態請見支援矩陣。"
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

    private void OpenSupportMatrix()
    {
        SupportMatrix.Refresh(_textProvider());
        IsSupportMatrixOpen = true;
    }

    private void CloseSupportMatrix()
    {
        IsSupportMatrixOpen = false;
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
