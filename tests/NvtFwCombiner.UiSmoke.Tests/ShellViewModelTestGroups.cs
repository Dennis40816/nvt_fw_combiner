namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Concurrent report projection smoke coverage.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
public sealed partial class ReportProjectionConcurrencyTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
}

/// <summary>Report review, history, triage, warning, and Hex Diff coverage.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
public sealed partial class ReportReviewHistoryTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
}

/// <summary>Firmware inspection and slot readiness coverage.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
public sealed partial class FirmwareInspectionSlotTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
}

/// <summary>Merge workflow smoke coverage.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
public sealed partial class MergeWorkflowTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
}

/// <summary>DP Replace workflow smoke coverage.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
public sealed partial class DpReplaceWorkflowTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
}

/// <summary>General Merge and Replace workflow smoke coverage.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
public sealed partial class GeneralWorkflowTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
}

/// <summary>CtrlRAM workflow smoke coverage.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
public sealed partial class CtrlRamWorkflowTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
}

/// <summary>Build outcome and artifact delivery coverage.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
public sealed partial class BuildOutcomeTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
}

/// <summary>Shell navigation, settings, and system-surface coverage.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
public sealed partial class ShellNavigationSystemTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
}

/// <summary>Run progress and Hex Editor coverage.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
public sealed partial class RunAndHexEditorTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
}

/// <summary>Serialized external CtrlRAM golden coverage.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
[Collection(UiExternalGoldenCollection.Name)]
public sealed partial class CtrlRamExternalGoldenTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
}

/// <summary>Process-wide performance observation coverage.</summary>
/// <param name="fixture">The group-local Bootstrap graph.</param>
[Collection(UiProcessWideObservationCollection.Name)]
public sealed partial class UiPerformanceObservationTests(ShellViewModelTestHostFixture fixture)
    : ShellViewModelTestBase(fixture), IClassFixture<ShellViewModelTestHostFixture>
{
}

internal static class UiExternalGoldenCollection
{
    internal const string Name = "UiExternalGolden";
}

/// <summary>Serializes tests that consume external CtrlRAM golden fixtures.</summary>
[CollectionDefinition(UiExternalGoldenCollection.Name)]
public sealed class UiExternalGoldenCollectionDefinition;

internal static class UiAvaloniaRuntimeCollection
{
    internal const string Name = "UiAvaloniaRuntime";
}

/// <summary>Serializes Avalonia control construction while leaving pure ViewModel tests parallel.</summary>
[CollectionDefinition(UiAvaloniaRuntimeCollection.Name)]
public sealed class UiAvaloniaRuntimeCollectionDefinition;

internal static class UiProcessWideObservationCollection
{
    internal const string Name = "UiProcessWideObservation";
}

/// <summary>Isolates process-wide performance and application-state observations.</summary>
[CollectionDefinition(UiProcessWideObservationCollection.Name, DisableParallelization = true)]
public sealed class UiProcessWideObservationCollectionDefinition;
