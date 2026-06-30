namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Provides separated synthetic data for the 0.1.1 planning shell.</summary>
public static class DemoShellSampleData
{
    /// <summary>Creates the sample view model used before application-core wiring.</summary>
    /// <returns>A populated planning shell view model.</returns>
    public static MainWindowViewModel Create()
    {
        return new MainWindowViewModel(
            "0.1.1 demo shell",
            "UI planning workspace",
            "Synthetic preview only. No firmware files are read and no external tool is executed.",
            "Preview unavailable until 0.2.0",
            "Build disabled until 0.2.0",
            CreateNavigationItems(),
            CreateMergePreview(),
            CreateReplacePreview(),
            CreateSavedRulesAndReports(),
            CreateDiagnostics(),
            "Profile catalog: demo only | Validation: preview unavailable | Diagnostics: read-only transcript | Firmware mutation: none");
    }

    private static IReadOnlyList<NavigationItemViewModel> CreateNavigationItems()
    {
        return
        [
            new("Settings"),
            new("Merge"),
            new("Replace"),
        ];
    }

    private static PlanningCardViewModel CreateMergePreview()
    {
        return new PlanningCardViewModel(
            "Merge preview",
            "Modes: Standard / AB / General",
            [
                "Profile selector: demo-standard-merge",
                "Inputs: DP demo.bin, TP demo.bin, optional LD placeholder",
                "Preview: shared Memory coverage before/after supplied by application core later",
            ],
            "Status: synthetic data, build blocked");
    }

    private static PlanningCardViewModel CreateReplacePreview()
    {
        return new PlanningCardViewModel(
            "Replace preview",
            "Personas: Display / TP HW / TP FW / General",
            [
                "Display: DP declared partitions and TP whole only; CtrlRAM hidden",
                "TP HW: CtrlRAM only; TP firmware regions denied",
                "TP FW: non-CtrlRAM TP firmware regions only; CtrlRAM denied",
                "General: profile-declared explicit ranges only; protected regions denied",
                "Preview: shared Memory coverage before/after and protected warnings",
            ],
            "Status: access policy display only");
    }

    private static PlanningCardViewModel CreateSavedRulesAndReports()
    {
        return new PlanningCardViewModel(
            "Saved rules and reports",
            "Secondary surfaces in this shell",
            [
                "Saved rules: General Merge/Replace action and status, not top-level navigation",
                "Reports: opened from preview/build results or Settings diagnostics",
                "Promotion requires validation, compatibility review, and evidence.",
            ],
            "Status: report schema wiring arrives after core execution");
    }

    private static PlanningCardViewModel CreateDiagnostics()
    {
        return new PlanningCardViewModel(
            "Diagnostics",
            "Secondary read-only transcript",
            [
                "[info] Loaded candidate profile: demo-standard-merge",
                "[info] Compiled 6 synthetic operations",
                "[warn] Build disabled until Composition Core milestone",
                "[info] Diagnostics open from Settings/status and remain sanitized",
            ],
            "Status: no external process is invoked");
    }
}
