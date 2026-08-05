using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Diagnostics;
using NvtFwCombiner.Infrastructure.Shell;
using NvtFwCombiner.Infrastructure.Time;

namespace NvtFwCombiner.Bootstrap;

/// <summary>Creates host adapters used by the desktop composition root.</summary>
public static class WorkbenchHostServices
{
    internal static CanonicalCapabilityCatalogHost CanonicalCapabilities { get; } =
        new(new CanonicalCapabilityCatalogMigrationSource());

    /// <summary>Gets the focused query over the host's single canonical publication.</summary>
    public static ICanonicalCapabilityQuery CanonicalCapabilityQuery => CanonicalCapabilities;

    /// <summary>Gets the focused query over the host's single canonical catalog publication.</summary>
    public static ICanonicalSupportMatrixQuery CanonicalSupportMatrixQuery { get; } =
        new CanonicalSupportMatrixQuery(static () => CanonicalCapabilities.LatestReload);

    /// <summary>Gets the focused capability experience port.</summary>
    public static ICompositionCapabilityExperience CompositionCapabilityExperience { get; } = new CompositionCapabilityExperienceAdapter();

    /// <summary>Gets the focused authoring experience port.</summary>
    public static ICompositionAuthoringExperience CompositionAuthoringExperience { get; } = new CompositionAuthoringExperienceAdapter();

    /// <summary>Gets the focused authoring-session port.</summary>
    public static ICompositionAuthoringSession CompositionAuthoringSession { get; } = new CompositionAuthoringSessionPort();

    /// <summary>Gets the focused memory-presentation port.</summary>
    public static ICompositionMemoryPresentation CompositionMemoryPresentation { get; } = new CompositionMemoryPresentationAdapter();

    /// <summary>Gets the focused immutable firmware-inspection port.</summary>
    public static IFirmwareInspection FirmwareInspectionExperience { get; } = new FirmwareInspectionPort();

    /// <summary>Gets the focused output-naming port.</summary>
    public static ICompositionOutputNaming CompositionOutputNaming { get; } = new CompositionOutputNamingAdapter();

    /// <summary>Gets the focused optional AB delivery-planning port.</summary>
    public static IAbMergeDeliveryPlanning AbMergeDeliveryPlanning { get; } = new AbMergeDeliveryPlanningPort();

    /// <summary>Gets the focused accepted-session execution port.</summary>
    public static ICompositionExecution CompositionExecution { get; } = new CompositionExecutionPort();

    /// <summary>Warms the canonical catalog on the caller-owned background worker.</summary>
    public static void WarmCanonicalCapabilities(CancellationToken cancellationToken)
    {
        CanonicalCapabilities.Warm(cancellationToken);
    }

    /// <summary>Creates a focused current-session System Information lifecycle.</summary>
    public static ISystemInformationService CreateSystemInformationService(
        string applicationVersion)
    {
        return new SystemInformationService(
            applicationVersion,
            CanonicalSupportMatrixQuery,
            CanonicalCapabilities,
            new SystemRuntimeProbe(),
            new SystemClock());
    }

    /// <summary>Creates the privacy-filtered local diagnostic JSON exporter.</summary>
    public static ISystemDiagnosticsExporter CreateSystemDiagnosticsExporter()
    {
        return new JsonSystemDiagnosticsExporter();
    }

    /// <summary>Creates the constrained Windows file-reveal adapter.</summary>
    public static IFileRevealService CreateFileRevealService()
    {
        return new WindowsExplorerFileRevealService();
    }
}
