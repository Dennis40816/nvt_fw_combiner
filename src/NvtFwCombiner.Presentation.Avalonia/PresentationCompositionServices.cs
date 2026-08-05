namespace NvtFwCombiner.Presentation.Avalonia;

/// <summary>Explicit immutable Application-port dependencies for one desktop presentation graph.</summary>
public sealed class PresentationCompositionServices
{
    /// <summary>Creates one dependency bundle supplied by the desktop composition root.</summary>
    public PresentationCompositionServices(
        ICompositionCapabilityExperience capabilities,
        ICompositionAuthoringExperience authoring,
        ICompositionAuthoringSession authoringSession,
        ICompositionMemoryPresentation memory,
        IFirmwareInspection firmwareInspection,
        ICompositionOutputNaming outputNaming,
        IAbMergeDeliveryPlanning abMergeDeliveryPlanning,
        ICompositionExecution execution)
    {
        Capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        Authoring = authoring ?? throw new ArgumentNullException(nameof(authoring));
        AuthoringSession = authoringSession ?? throw new ArgumentNullException(nameof(authoringSession));
        Memory = memory ?? throw new ArgumentNullException(nameof(memory));
        FirmwareInspection = firmwareInspection ?? throw new ArgumentNullException(nameof(firmwareInspection));
        OutputNaming = outputNaming ?? throw new ArgumentNullException(nameof(outputNaming));
        AbMergeDeliveryPlanning = abMergeDeliveryPlanning ??
            throw new ArgumentNullException(nameof(abMergeDeliveryPlanning));
        Execution = execution ?? throw new ArgumentNullException(nameof(execution));
    }

    /// <summary>Canonical capability and workflow disclosure.</summary>
    public ICompositionCapabilityExperience Capabilities { get; }

    /// <summary>Canonical authoring policy and planning.</summary>
    public ICompositionAuthoringExperience Authoring { get; }

    /// <summary>Revision-bound authoring sessions and inspection.</summary>
    public ICompositionAuthoringSession AuthoringSession { get; }

    /// <summary>Semantic memory-layout snapshots.</summary>
    public ICompositionMemoryPresentation Memory { get; }

    /// <summary>Firmware input inspection.</summary>
    public IFirmwareInspection FirmwareInspection { get; }

    /// <summary>Output-name resolution.</summary>
    public ICompositionOutputNaming OutputNaming { get; }

    /// <summary>Optional AB artifact delivery planning.</summary>
    public IAbMergeDeliveryPlanning AbMergeDeliveryPlanning { get; }

    /// <summary>Preview and build execution.</summary>
    public ICompositionExecution Execution { get; }
}
