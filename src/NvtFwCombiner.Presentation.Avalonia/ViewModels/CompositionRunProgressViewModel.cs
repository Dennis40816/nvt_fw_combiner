using CommunityToolkit.Mvvm.ComponentModel;
using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Presentation.Avalonia.ViewModels;

/// <summary>Visual state of one Application-owned composition lifecycle phase.</summary>
public enum CompositionRunProgressStepState
{
    /// <summary>The applicable phase has not started.</summary>
    Pending,

    /// <summary>The applicable phase currently owns the run.</summary>
    Active,

    /// <summary>The applicable phase was entered and completed.</summary>
    Completed,
}

/// <summary>UI delivery boundary between composition, committed output, and complete report projection.</summary>
public enum CompositionRunDeliveryState
{
    /// <summary>No active or retained delivery state exists.</summary>
    Idle,

    /// <summary>Composition is running and no committed Build artifact has been announced.</summary>
    Running,

    /// <summary>The Build artifact is committed while nonessential report work continues.</summary>
    ArtifactCommitted,

    /// <summary>The complete report model is ready for review and history capture.</summary>
    ReportReady,
}

/// <summary>Localized, accessible projection of one applicable composition lifecycle phase.</summary>
public sealed class CompositionRunProgressStepViewModel
{
    internal CompositionRunProgressStepViewModel(
        CompositionRunPhase phase,
        string label,
        CompositionRunProgressStepState state,
        string accessibleLabel)
    {
        Phase = phase;
        Label = label;
        State = state;
        StateMarker = state switch
        {
            CompositionRunProgressStepState.Completed => "✓",
            CompositionRunProgressStepState.Active => "▶",
            CompositionRunProgressStepState.Pending => "○",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown progress step state."),
        };
        AccessibleLabel = accessibleLabel;
    }

    /// <summary>Application-owned lifecycle phase represented by this step.</summary>
    public CompositionRunPhase Phase { get; }

    /// <summary>Localized phase label.</summary>
    public string Label { get; }

    /// <summary>Truthful lifecycle state derived only from the Application snapshot.</summary>
    public CompositionRunProgressStepState State { get; }

    /// <summary>Non-color visual marker that remains distinct when motion or theme color is unavailable.</summary>
    public string StateMarker { get; }

    /// <summary>Localized phase and state description for assistive technology.</summary>
    public string AccessibleLabel { get; }

    /// <summary>True only for the phase that currently owns the run.</summary>
    public bool IsActive => State == CompositionRunProgressStepState.Active;

    /// <summary>True only for a phase explicitly reported as completed.</summary>
    public bool IsCompleted => State == CompositionRunProgressStepState.Completed;
}

/// <summary>
/// Projects bounded Application progress snapshots into localized step state without inferring firmware semantics.
/// </summary>
public sealed class CompositionRunProgressViewModel : ObservableObject
{
    private static readonly IReadOnlyList<CompositionRunProgressStepViewModel> EmptySteps =
        [];

    private ShellTextResources _text;
    private CompositionRunPhase[] _applicablePhases = [];
    private bool _isReducedMotionEnabled;

    /// <summary>Initializes an empty progress projection for the selected language and motion preference.</summary>
    public CompositionRunProgressViewModel(
        ShellLanguage language = ShellLanguage.English,
        bool isReducedMotionEnabled = false)
    {
        _text = ShellTextResources.For(language);
        _isReducedMotionEnabled = isReducedMotionEnabled;
    }

    /// <summary>Run id currently owning this projection, or null before the first accepted snapshot.</summary>
    public string? RunId { get; private set; }

    /// <summary>Applicable steps in the exact order supplied by Application.</summary>
    public IReadOnlyList<CompositionRunProgressStepViewModel> Steps { get; private set; } = EmptySteps;

    /// <summary>True after the first typed snapshot for the owning run is accepted.</summary>
    public bool HasTypedProgress => CurrentPhase.HasValue;

    /// <summary>Application-owned phase that currently owns the run.</summary>
    public CompositionRunPhase? CurrentPhase { get; private set; }

    /// <summary>Gets the current artifact/report delivery boundary.</summary>
    public CompositionRunDeliveryState DeliveryState { get; private set; }

    /// <summary>Gets the committed output identity while its report finishes in the background.</summary>
    public string? CommittedOutputId { get; private set; }

    /// <summary>One-based lifecycle ordinal within the applicable phase sequence.</summary>
    public int CurrentStep => CurrentPhase is { } phase
        ? Array.IndexOf(_applicablePhases, phase) + 1
        : 0;

    /// <summary>Number of lifecycle phases applicable to the current run shape.</summary>
    public int StepCount => _applicablePhases.Length;

    /// <summary>Localized label for the current Application phase.</summary>
    public string CurrentStepLabel => CurrentPhase is { } phase
        ? DeliveryState switch
        {
            CompositionRunDeliveryState.ArtifactCommitted => _text.GetCompositionArtifactCommittedLabel(),
            CompositionRunDeliveryState.ReportReady => _text.GetCompositionReportReadyLabel(),
            CompositionRunDeliveryState.Idle or
            CompositionRunDeliveryState.Running => _text.GetCompositionRunPhaseLabel(phase),
            _ => throw new ArgumentOutOfRangeException(nameof(DeliveryState), DeliveryState, null),
        }
        : string.Empty;

    /// <summary>Localized lifecycle ordinal; this is not a byte percentage.</summary>
    public string StepOrdinalLabel => HasTypedProgress
        ? _text.FormatCompositionRunStepOrdinal(CurrentStep, StepCount)
        : string.Empty;

    /// <summary>Localized live status announced once per accepted Application phase transition.</summary>
    public string AccessibleStatus => HasTypedProgress
        ? _text.FormatCompositionRunProgressStatus(CurrentStep, StepCount, CurrentStepLabel)
        : string.Empty;

    /// <summary>True when platform or user preference requests static progress emphasis.</summary>
    public bool IsReducedMotionEnabled => _isReducedMotionEnabled;

    /// <summary>True only when the active step may use a restrained indeterminate animation.</summary>
    public bool ShouldAnimateActiveStep => HasTypedProgress && !IsReducedMotionEnabled;

    /// <summary>Accepts a snapshot for the owning run and ignores stale snapshots from other runs.</summary>
    public bool TryApply(CompositionRunProgressSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return TryApply(
            snapshot.RunId,
            snapshot.CurrentPhase,
            snapshot.ApplicablePhases,
            snapshot.CompletedPhases,
            snapshot.CommittedOutputId);
    }

    /// <summary>Updates localized labels without changing lifecycle state.</summary>
    public void ApplyLanguage(ShellLanguage language)
    {
        if (_text.Language == language)
        {
            return;
        }

        _text = ShellTextResources.For(language);
        RebuildSteps(GetCompletedPhases());
        NotifyProjectionChanged();
    }

    /// <summary>Updates whether the active phase uses motion without changing progress state.</summary>
    public void SetReducedMotion(bool enabled)
    {
        if (SetProperty(ref _isReducedMotionEnabled, enabled, nameof(IsReducedMotionEnabled)))
        {
            OnPropertyChanged(nameof(ShouldAnimateActiveStep));
        }
    }

    /// <summary>Releases run ownership after the normal result/report surface takes over.</summary>
    public void Reset()
    {
        if (RunId is null && !HasTypedProgress && DeliveryState == CompositionRunDeliveryState.Idle)
        {
            return;
        }

        RunId = null;
        _applicablePhases = [];
        CurrentPhase = null;
        DeliveryState = CompositionRunDeliveryState.Idle;
        CommittedOutputId = null;
        Steps = EmptySteps;
        OnPropertyChanged(nameof(RunId));
        NotifyProjectionChanged();
    }

    internal bool TryApply(
        string runId,
        CompositionRunPhase currentPhase,
        IReadOnlyList<CompositionRunPhase> applicablePhases,
        IReadOnlyList<CompositionRunPhase> completedPhases,
        string? committedOutputId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runId);
        ArgumentNullException.ThrowIfNull(applicablePhases);
        ArgumentNullException.ThrowIfNull(completedPhases);
        ValidateSnapshot(currentPhase, applicablePhases, completedPhases);

        if (RunId is not null && !string.Equals(RunId, runId, StringComparison.Ordinal))
        {
            return false;
        }

        if (RunId is null)
        {
            RunId = runId;
            _applicablePhases = [.. applicablePhases];
            OnPropertyChanged(nameof(RunId));
        }
        else if (!_applicablePhases.SequenceEqual(applicablePhases))
        {
            throw new InvalidOperationException("Applicable composition progress phases changed within one run.");
        }

        if (DeliveryState == CompositionRunDeliveryState.ReportReady)
        {
            return true;
        }

        CurrentPhase = currentPhase;
        CommittedOutputId = committedOutputId;
        DeliveryState = committedOutputId is null
            ? CompositionRunDeliveryState.Running
            : CompositionRunDeliveryState.ArtifactCommitted;

        RebuildSteps(completedPhases);
        NotifyProjectionChanged();
        return true;
    }

    internal void MarkReportReady(bool reportPublished)
    {
        if (!reportPublished || RunId is null || DeliveryState == CompositionRunDeliveryState.ReportReady)
        {
            return;
        }

        DeliveryState = CompositionRunDeliveryState.ReportReady;
        OnPropertyChanged(nameof(DeliveryState));
        OnPropertyChanged(nameof(CurrentStepLabel));
        OnPropertyChanged(nameof(AccessibleStatus));
    }

    private static void ValidateSnapshot(
        CompositionRunPhase currentPhase,
        IReadOnlyList<CompositionRunPhase> applicablePhases,
        IReadOnlyList<CompositionRunPhase> completedPhases)
    {
        if (applicablePhases.Count == 0 ||
            applicablePhases.Count > Enum.GetValues<CompositionRunPhase>().Length ||
            applicablePhases.Distinct().Count() != applicablePhases.Count ||
            !applicablePhases.Contains(currentPhase))
        {
            throw new InvalidOperationException("Composition progress contains an invalid applicable phase sequence.");
        }

        int currentIndex = applicablePhases.TakeWhile(phase => phase != currentPhase).Count();
        bool completedSequenceIsValid = currentPhase == CompositionRunPhase.PreparingReport
            ? completedPhases.Count <= currentIndex &&
              completedPhases.SequenceEqual(applicablePhases.Take(completedPhases.Count))
            : completedPhases.SequenceEqual(applicablePhases.Take(currentIndex));
        if (!completedSequenceIsValid)
        {
            throw new InvalidOperationException("Composition progress contains invalid completed phases.");
        }
    }

    private IReadOnlyList<CompositionRunPhase> GetCompletedPhases()
    {
        return [.. Steps
            .Where(static step => step.IsCompleted)
            .Select(static step => step.Phase)];
    }

    private void RebuildSteps(IReadOnlyList<CompositionRunPhase> completedPhases)
    {
        if (CurrentPhase is not { } currentPhase)
        {
            Steps = EmptySteps;
            return;
        }

        Steps = Array.AsReadOnly(
            _applicablePhases
                .Select(phase => CreateStep(phase, currentPhase, completedPhases))
                .ToArray());
    }

    private CompositionRunProgressStepViewModel CreateStep(
        CompositionRunPhase phase,
        CompositionRunPhase currentPhase,
        IReadOnlyList<CompositionRunPhase> completedPhases)
    {
        CompositionRunProgressStepState state = completedPhases.Contains(phase)
            ? CompositionRunProgressStepState.Completed
            : phase == currentPhase
                ? CompositionRunProgressStepState.Active
                : CompositionRunProgressStepState.Pending;
        string label = _text.GetCompositionRunPhaseLabel(phase);
        return new CompositionRunProgressStepViewModel(
            phase,
            label,
            state,
            _text.FormatCompositionRunStepAccessibleLabel(label, state));
    }

    private void NotifyProjectionChanged()
    {
        OnPropertyChanged(nameof(Steps));
        OnPropertyChanged(nameof(HasTypedProgress));
        OnPropertyChanged(nameof(CurrentPhase));
        OnPropertyChanged(nameof(DeliveryState));
        OnPropertyChanged(nameof(CommittedOutputId));
        OnPropertyChanged(nameof(CurrentStep));
        OnPropertyChanged(nameof(StepCount));
        OnPropertyChanged(nameof(CurrentStepLabel));
        OnPropertyChanged(nameof(StepOrdinalLabel));
        OnPropertyChanged(nameof(AccessibleStatus));
        OnPropertyChanged(nameof(ShouldAnimateActiveStep));
    }
}
