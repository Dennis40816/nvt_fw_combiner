using System.Globalization;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

internal sealed record OutputConfirmationInputRow(string FileName, string Size, string Role, string Warning, string Sha256)
{
    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);
}

internal sealed record OutputConfirmationCheck(string Label, string Value);

internal sealed partial class OutputDeliveryConfirmationViewModel
{
    private CompositionOutputConfirmationSummary? Confirmation => _request?.Proposal.Confirmation;

    public bool HasConfirmation => Confirmation is not null;
    public string InputSourcesSummary => string.Format(CultureInfo.CurrentCulture, Text.OutputDeliverySourcesSummaryFormat, Confirmation?.Inputs.Count ?? 0);
    public string TargetSummary
    {
        get
        {
            if (Confirmation is not { } summary) { return string.Empty; }
            string topology = summary.IcNumber is { } number ? Text.FormatOutputNumber(number) :
                Text.FormatOutputTopology(summary.TopologyRequirement);
            return string.IsNullOrWhiteSpace(topology) ? summary.IcId : $"{summary.IcId} · {topology}";
        }
    }
    public string ModeSummary => Confirmation is not { } summary ? string.Empty :
        WorkflowModeDisplayConverters.GetDisplayName(summary.WorkflowId);
    public string FlashMapSummary => Confirmation?.FlashMap is { } map
        ? map.DisplayName ?? map.MapId : Text.FirmwareSlotNotApplicableLabel;
    public string FlashOutputSize => Confirmation is { } summary ? FormatOutputBytes(summary.OutputLengthBytes) : string.Empty;
    public string AdditionalOutputSize => _request?.AdditionalDelivery is { } delivery ? FormatOutputBytes(delivery.SourceRange.Length) : string.Empty;
    public bool HasGeneratedInputs => Confirmation?.HasGeneratedInputs == true;
    public bool HasInputNotice => HasGeneratedInputs || HasInputWarnings;
    public bool HasValidationMessage => !string.IsNullOrWhiteSpace(ValidationMessage);
    public bool ShowsBundleContents => BundleEnabled && AdditionalDeliveryEnabled;
    public string DeliveryDescription => BundleEnabled && !AdditionalDeliveryEnabled
        ? Text.FormatOutputBundleContents(Sources.Count) : Text.OutputDeliveryBundleLabel;
    public bool HasInputWarnings => Confirmation?.Inputs.Any(input =>
        input.InspectionLifecycle == AuthoringSlotLifecycle.Warning) == true;

    public IReadOnlyList<OutputConfirmationInputRow> InputRows => Confirmation?.Inputs.Select(input =>
        new OutputConfirmationInputRow(input.SourceFileName, FormatInputBytes(input.SizeBytes),
            ShellTextResources.GetOutputInputLabel(input.BindingId), Text.FormatOutputInputWarning(input), input.Sha256)).ToArray() ?? [];

    public IReadOnlyList<OutputConfirmationCheck> ExpectedInputChecks => Confirmation?.Inputs
        .Where(input => input.ExpectedLengths.Count > 0)
        .Select(input => new OutputConfirmationCheck(Text.FormatExpectedInputSizeLabel(input.BindingId),
            string.Join(" / ", input.ExpectedLengths.Select(FormatInputBytes)))).ToArray() ?? [];

    public IReadOnlyList<OutputConfirmationCheck> EventBufferChecks => Confirmation?.Inputs
        .Where(input => input.EventBufferFormat is not null)
        .Select(input => new OutputConfirmationCheck(ShellTextResources.GetOutputInputLabel(input.BindingId),
            $"0x{input.EventBufferFormat!.RawByte:X2} - {input.EventBufferFormat.DetectedDisplayName ?? input.EventBufferFormat.DisplayName}")).ToArray() ?? [];

    public bool HasEventBufferChecks => EventBufferChecks.Count > 0;
    public bool HasSourceChecks => ExpectedInputChecks.Count > 0 || HasEventBufferChecks;

    private static string FormatOutputBytes(long bytes)
    {
        return bytes >= 1048576
            ? string.Format(CultureInfo.CurrentCulture, "{0:0.###} MiB ({1:N0} bytes)", bytes / 1048576d, bytes)
            : string.Format(CultureInfo.CurrentCulture, "{0:0.###} KiB ({1:N0} bytes)", bytes / 1024d, bytes);
    }

    private static string FormatInputBytes(long bytes)
    {
        return string.Format(CultureInfo.CurrentCulture, "{0:N0} bytes", bytes);
    }

    private void NotifySummary()
    {
        OnPropertyChanged(nameof(HasConfirmation));
        OnPropertyChanged(nameof(TargetSummary));
        OnPropertyChanged(nameof(ModeSummary));
        OnPropertyChanged(nameof(FlashMapSummary));
        OnPropertyChanged(nameof(FlashOutputSize));
        OnPropertyChanged(nameof(AdditionalOutputSize));
        OnPropertyChanged(nameof(HasGeneratedInputs));
        OnPropertyChanged(nameof(HasInputNotice));
        OnPropertyChanged(nameof(HasInputWarnings));
        OnPropertyChanged(nameof(InputRows));
        OnPropertyChanged(nameof(InputSourcesSummary));
        OnPropertyChanged(nameof(ExpectedInputChecks));
        OnPropertyChanged(nameof(EventBufferChecks));
        OnPropertyChanged(nameof(HasEventBufferChecks));
        OnPropertyChanged(nameof(HasSourceChecks));
        OnPropertyChanged(nameof(ShowsBundleContents));
        OnPropertyChanged(nameof(DeliveryDescription));
    }
}
