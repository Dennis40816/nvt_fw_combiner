namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Explicit immutable Application-port dependencies for one desktop presentation graph.</summary>
public sealed class PresentationCompositionServices
{
    /// <summary>Creates one dependency bundle supplied by the desktop composition root.</summary>
    public PresentationCompositionServices(
        ICompositionCapabilityExperience capabilities,
        IStandardMergeAuthoring standardMergeAuthoring,
        IAbMergeAuthoring abMergeAuthoring,
        IDpReplaceAuthoring dpReplaceAuthoring,
        IGeneralAuthoring generalAuthoring,
        ICtrlRamAuthoring ctrlRamAuthoring,
        IFirmwareInspection firmwareInspection,
        ICompositionOutputNaming outputNaming,
        ICompositionExecution execution)
    {
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        StandardMergeAuthoring = standardMergeAuthoring ??
            throw new ArgumentNullException(nameof(standardMergeAuthoring));
        AbMergeAuthoring = abMergeAuthoring ??
            throw new ArgumentNullException(nameof(abMergeAuthoring));
        DpReplaceAuthoring = dpReplaceAuthoring ??
            throw new ArgumentNullException(nameof(dpReplaceAuthoring));
        GeneralAuthoring = generalAuthoring ??
            throw new ArgumentNullException(nameof(generalAuthoring));
        CtrlRamAuthoring = ctrlRamAuthoring ??
            throw new ArgumentNullException(nameof(ctrlRamAuthoring));
        FirmwareInspection = firmwareInspection ?? throw new ArgumentNullException(nameof(firmwareInspection));
        OutputNaming = outputNaming ?? throw new ArgumentNullException(nameof(outputNaming));
        Execution = execution ?? throw new ArgumentNullException(nameof(execution));
    }

    /// <summary>Canonical capability and workflow disclosure.</summary>
    public ICompositionCapabilityExperience Capabilities { get; }

    /// <summary>Canonical Standard Merge authoring.</summary>
    public IStandardMergeAuthoring StandardMergeAuthoring { get; }

    /// <summary>Canonical AB Merge authoring.</summary>
    public IAbMergeAuthoring AbMergeAuthoring { get; }

    /// <summary>Canonical DP Replace authoring.</summary>
    public IDpReplaceAuthoring DpReplaceAuthoring { get; }

    /// <summary>Canonical General Merge and General Replace authoring.</summary>
    public IGeneralAuthoring GeneralAuthoring { get; }

    /// <summary>Canonical CtrlRAM Replace authoring.</summary>
    public ICtrlRamAuthoring CtrlRamAuthoring { get; }

    /// <summary>Firmware input inspection.</summary>
    public IFirmwareInspection FirmwareInspection { get; }

    /// <summary>Output-name resolution.</summary>
    public ICompositionOutputNaming OutputNaming { get; }

    /// <summary>Preview and build execution.</summary>
    public ICompositionExecution Execution { get; }

    internal PresentationCompositionServices WithFirmwareInspection(
        IFirmwareInspection firmwareInspection)
    {
        ArgumentNullException.ThrowIfNull(firmwareInspection);
        return ReferenceEquals(FirmwareInspection, firmwareInspection)
            ? this
            : new PresentationCompositionServices(
                Capabilities,
                StandardMergeAuthoring,
                AbMergeAuthoring,
                DpReplaceAuthoring,
                GeneralAuthoring,
                CtrlRamAuthoring,
                firmwareInspection,
                OutputNaming,
                Execution);
    }
}
