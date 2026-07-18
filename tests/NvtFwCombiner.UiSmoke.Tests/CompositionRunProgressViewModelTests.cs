using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Locks typed composition progress projection, localization, accessibility, and motion policy.</summary>
public sealed class CompositionRunProgressViewModelTests
{
    private static readonly CompositionRunPhase[] FullBuildPhases =
    [
        CompositionRunPhase.Preparing,
        CompositionRunPhase.ReadingInputs,
        CompositionRunPhase.ExecutingComposition,
        CompositionRunPhase.RunningExternalProcessor,
        CompositionRunPhase.ValidatingOutput,
        CompositionRunPhase.CommittingOutput,
        CompositionRunPhase.PreparingReport,
    ];

    /// <summary>Applicable, completed, and active state comes only from the typed Application snapshot.</summary>
    [Fact]
    public void SnapshotProjectsOnlyApplicationOwnedState()
    {
        var progress = new CompositionRunProgressViewModel();

        bool accepted = progress.TryApply(
            "run-1",
            CompositionRunPhase.ExecutingComposition,
            FullBuildPhases,
            [CompositionRunPhase.Preparing, CompositionRunPhase.ReadingInputs]);

        Assert.True(accepted);
        Assert.Equal("run-1", progress.RunId);
        Assert.True(progress.HasTypedProgress);
        Assert.Equal(CompositionRunPhase.ExecutingComposition, progress.CurrentPhase);
        Assert.Equal(3, progress.CurrentStep);
        Assert.Equal(7, progress.StepCount);
        Assert.Equal("Executing composition", progress.CurrentStepLabel);
        Assert.Equal("Step 3 of 7", progress.StepOrdinalLabel);
        Assert.Equal("Step 3 of 7: Executing composition", progress.AccessibleStatus);
        Assert.True(progress.ShouldAnimateActiveStep);
        Assert.Equal(
            [
                CompositionRunProgressStepState.Completed,
                CompositionRunProgressStepState.Completed,
                CompositionRunProgressStepState.Active,
                CompositionRunProgressStepState.Pending,
                CompositionRunProgressStepState.Pending,
                CompositionRunProgressStepState.Pending,
                CompositionRunProgressStepState.Pending,
            ],
            progress.Steps.Select(static step => step.State));
        Assert.Equal("Executing composition: in progress", progress.Steps[2].AccessibleLabel);
    }

    /// <summary>Preview renders only phases Application declared applicable to that run.</summary>
    [Fact]
    public void PreviewOmitsInapplicableProcessorAndCommitSteps()
    {
        CompositionRunPhase[] applicablePhases =
        [
            CompositionRunPhase.Preparing,
            CompositionRunPhase.ReadingInputs,
            CompositionRunPhase.ExecutingComposition,
            CompositionRunPhase.ValidatingOutput,
            CompositionRunPhase.PreparingReport,
        ];
        var progress = new CompositionRunProgressViewModel();

        _ = progress.TryApply(
            "preview-1",
            CompositionRunPhase.PreparingReport,
            applicablePhases,
            applicablePhases[..^1]);

        Assert.Equal(5, progress.StepCount);
        Assert.Equal(5, progress.CurrentStep);
        Assert.DoesNotContain(
            progress.Steps,
            step => step.Phase is CompositionRunPhase.RunningExternalProcessor or CompositionRunPhase.CommittingOutput);
        Assert.All(progress.Steps.Take(progress.Steps.Count - 1), static step => Assert.True(step.IsCompleted));
        Assert.True(progress.Steps[^1].IsActive);
    }

    /// <summary>A terminal report phase retains skipped work as pending after an earlier run failure.</summary>
    [Fact]
    public void TerminalReportDoesNotFabricateSkippedPhaseCompletion()
    {
        var progress = new CompositionRunProgressViewModel();

        bool accepted = progress.TryApply(
            "input-failure",
            CompositionRunPhase.PreparingReport,
            FullBuildPhases,
            [CompositionRunPhase.Preparing, CompositionRunPhase.ReadingInputs]);

        Assert.True(accepted);
        Assert.True(progress.Steps[0].IsCompleted);
        Assert.True(progress.Steps[1].IsCompleted);
        Assert.All(progress.Steps.Skip(2).Take(progress.Steps.Count - 3), static step => Assert.Equal(
            CompositionRunProgressStepState.Pending,
            step.State));
        Assert.True(progress.Steps[^1].IsActive);
        Assert.Equal("Step 7 of 7: Preparing report", progress.AccessibleStatus);
    }

    /// <summary>Reduced motion changes animation policy without changing truthful or accessible state.</summary>
    [Fact]
    public void ReducedMotionKeepsStepAndAccessibleStateStatic()
    {
        var progress = new CompositionRunProgressViewModel(isReducedMotionEnabled: true);
        _ = progress.TryApply(
            "run-reduced-motion",
            CompositionRunPhase.RunningExternalProcessor,
            FullBuildPhases,
            FullBuildPhases[..3]);
        string accessibleStatus = progress.AccessibleStatus;
        IReadOnlyList<CompositionRunProgressStepState> states =
            [.. progress.Steps.Select(static step => step.State)];

        Assert.False(progress.ShouldAnimateActiveStep);

        progress.SetReducedMotion(enabled: false);

        Assert.True(progress.ShouldAnimateActiveStep);
        Assert.Equal(accessibleStatus, progress.AccessibleStatus);
        Assert.Equal(states, progress.Steps.Select(static step => step.State));
    }

    /// <summary>Changing language relocalizes the retained phase state without altering its lifecycle position.</summary>
    [Fact]
    public void LanguageChangeRelocalizesExistingState()
    {
        var progress = new CompositionRunProgressViewModel();
        _ = progress.TryApply(
            "run-zh",
            CompositionRunPhase.ReadingInputs,
            FullBuildPhases,
            [CompositionRunPhase.Preparing]);

        progress.ApplyLanguage(ShellLanguage.ChineseTraditional);

        Assert.Equal("讀取輸入檔案", progress.CurrentStepLabel);
        Assert.Equal("步驟 2/7", progress.StepOrdinalLabel);
        Assert.Equal("步驟 2/7: 讀取輸入檔案", progress.AccessibleStatus);
        Assert.Equal("準備執行: 已完成", progress.Steps[0].AccessibleLabel);
        Assert.Equal("讀取輸入檔案: 執行中", progress.Steps[1].AccessibleLabel);
    }

    /// <summary>A snapshot from an older run cannot replace progress owned by the active run.</summary>
    [Fact]
    public void StaleRunCannotReplaceCurrentProgress()
    {
        var progress = new CompositionRunProgressViewModel();
        _ = progress.TryApply(
            "current-run",
            CompositionRunPhase.ReadingInputs,
            FullBuildPhases,
            [CompositionRunPhase.Preparing]);

        bool accepted = progress.TryApply(
            "stale-run",
            CompositionRunPhase.PreparingReport,
            FullBuildPhases,
            FullBuildPhases[..^1]);

        Assert.False(accepted);
        Assert.Equal("current-run", progress.RunId);
        Assert.Equal(CompositionRunPhase.ReadingInputs, progress.CurrentPhase);

        progress.Reset();
        Assert.True(progress.TryApply(
            "next-run",
            CompositionRunPhase.Preparing,
            FullBuildPhases,
            []));
        Assert.Equal("next-run", progress.RunId);
    }

    /// <summary>Every stable Application phase has localized sighted and assistive labels.</summary>
    [Theory]
    [InlineData(ShellLanguage.English)]
    [InlineData(ShellLanguage.ChineseTraditional)]
    public void EveryApplicationPhaseHasLocalizedAccessibleText(ShellLanguage language)
    {
        foreach (CompositionRunPhase phase in Enum.GetValues<CompositionRunPhase>())
        {
            var progress = new CompositionRunProgressViewModel(language);
            _ = progress.TryApply("localized-run", phase, [phase], []);

            Assert.False(string.IsNullOrWhiteSpace(progress.CurrentStepLabel));
            Assert.False(string.IsNullOrWhiteSpace(progress.AccessibleStatus));
            Assert.False(string.IsNullOrWhiteSpace(progress.Steps[0].AccessibleLabel));
        }
    }

    /// <summary>The applicable lifecycle shape remains immutable after a run owns the projection.</summary>
    [Fact]
    public void ApplicablePhaseContractCannotDriftWithinOneRun()
    {
        var progress = new CompositionRunProgressViewModel();
        _ = progress.TryApply(
            "stable-run",
            CompositionRunPhase.Preparing,
            FullBuildPhases,
            []);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => progress.TryApply(
            "stable-run",
            CompositionRunPhase.ReadingInputs,
            [.. FullBuildPhases.Where(static phase => phase != CompositionRunPhase.CommittingOutput)],
            [CompositionRunPhase.Preparing]));

        Assert.Equal("Applicable composition progress phases changed within one run.", exception.Message);
    }

    /// <summary>Presentation rejects a snapshot that claims a non-prefix phase completion.</summary>
    [Fact]
    public void InvalidCompletedPhaseSequenceIsRejected()
    {
        var progress = new CompositionRunProgressViewModel();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => progress.TryApply(
            "invalid-run",
            CompositionRunPhase.ExecutingComposition,
            FullBuildPhases,
            [CompositionRunPhase.Preparing]));

        Assert.Equal("Composition progress contains invalid completed phases.", exception.Message);
        Assert.Null(progress.RunId);
        Assert.False(progress.HasTypedProgress);
    }
}
