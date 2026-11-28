// Copyright (c) 2026 Dennis Liu. All rights reserved.
using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Publishes the legacy display and output map together from one memory-layout revision.</summary>
internal sealed class OutputLayoutPresentationViewModel : ObservableObject
{
    // Publication is serialized by MergePresentationViewModel's _memoryProjectionGate.
    private OutputLayoutPublication _current = new(string.Empty, [], [], [], null, null);

    /// <summary>Gets the complete display; consumers observe one replacement after preparation finishes.</summary>
    public OutputLayoutPublication Current => _current;

    internal void Publish(
        PresentationCompositionServices services,
        ActiveSessionSnapshot? authoring,
        ShellTextResources text,
        IEnumerable<FirmwareSlotViewModel> slots,
        MemoryPendingPrerequisite fallbackPrerequisite,
        GeneralAuthoringAdmissionResult? admission = null,
        IReadOnlyList<string>? requiredAddressSpaces = null)
    {
        string rangeLabel;
        IReadOnlyList<MemoryMapRowViewModel> rows;
        IReadOnlyList<MemoryCoverageSegmentViewModel> coverageSegments;
        OutputMapModel? map = null;
        if (authoring?.ExactCapability is { } capability)
        {
            (rangeLabel, rows, coverageSegments) = UiCompositionRunner.GetMemoryDisplay(
                services, authoring, text, out _, out MemoryLayoutSnapshot layout, admission);
            map = OutputMapProjection.Create(layout, authoring, capability.CompiledComposition.Plan.OutputSpaceId, text);
        }
        else
        {
            (rangeLabel, rows, coverageSegments) = UiCompositionRunner.GetPendingMemoryDisplay(
                text, slots, fallbackPrerequisite, authoring, requiredAddressSpaces);
        }

        // Logical items assign the interaction shared by the rail and legend before either can bind it.
        IReadOnlyList<MemoryCoverageLogicalItemViewModel> logicalItems =
            ReplaceRegionGroupBuilder.CreateLogicalItems(coverageSegments, text);
        var publication = new OutputLayoutPublication(
            rangeLabel,
            Array.AsReadOnly([.. rows]),
            Array.AsReadOnly([.. coverageSegments]),
            Array.AsReadOnly([.. logicalItems.SelectMany(static item => item.Ranges)]),
            map,
            authoring?.AuthoringRevision);
        _ = SetProperty(ref _current, publication, nameof(Current));
    }
}

/// <summary>One prepared display, including pending or blocked details when geometry is unavailable.</summary>
internal sealed record OutputLayoutPublication(
    string RangeLabel,
    IReadOnlyList<MemoryMapRowViewModel> Rows,
    IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageSegments,
    IReadOnlyList<MemoryCoverageSegmentViewModel> CoverageRows,
    OutputMapModel? Map,
    AuthoringRevision? Revision)
{
    public string StartAddress => MemoryCoverageBarProjection.OuterAddresses(CoverageSegments).Start;

    public string EndAddress => MemoryCoverageBarProjection.OuterAddresses(CoverageSegments).End;
}
