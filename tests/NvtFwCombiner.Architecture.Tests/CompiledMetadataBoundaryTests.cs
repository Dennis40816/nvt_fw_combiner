#pragma warning disable CA1707 // Owner requires Method_Scenario_Expected names.
using System.Collections.Immutable;
using NvtFwCombiner.Architecture.Tests.Metadata;

namespace NvtFwCombiner.Architecture.Tests;

internal sealed record BoundaryFinding(string Rule, string Type, string Member, string ReferencedType)
{
    public override string ToString() => $"{Rule}: {Type} | {Member} -> {ReferencedType}";
}

/// <summary>A2 rules operate on compiled references, including generated method bodies.</summary>
public sealed class CompiledMetadataBoundaryTests
{
    private const string Presentation = MetadataFixtureBuilder.Presentation;
    // UiCompositionRunner is a ViewModel projection helper (accepted layering audit, memory-layout row).
    // Namespace roles are explicit; these reusable primitives historically reside under Views.
    private static readonly ImmutableDictionary<string, string> ExactRoles = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Views.HexViewportControl"] = "Control", ["Views.MemoryCoverageBar"] = "Control",
        ["Views.ProportionalStackPanel"] = "Control", ["Views.ReportTableText"] = "Control",
        ["Views.SpaciousPanel"] = "Control", ["Views.VersionCheckingIndicator"] = "Control",
        ["PresentationCompositionServices"] = "Service", ["PresentationHostServices"] = "Service",
        ["LocalJsonDocument"] = "Service", ["ReportHistoryFileStore"] = "Service", ["ShellPreferenceFileStore"] = "Service",
        ["UiCompositionRunner"] = "ViewModel", ["LatestSnapshotPersistenceCoordinator`1"] = "Service",
        ["StartupTraceFileSink"] = "Service", ["StartupTraceSession"] = "Service",
        ["App"] = "View", ["MainWindow"] = "View", ["ShellViewModelLocator"] = "View",
        ["DesktopLaunchCoordinator"] = "Lifecycle", ["DesktopLaunchContext"] = "Lifecycle",
        ["DesktopCaptureSession"] = "Lifecycle", ["ShellPreloadSession"] = "Lifecycle",
    }.ToImmutableDictionary(StringComparer.Ordinal);
    internal static string Role(string type)
    {
        string root = type.Split('+')[0];
        if (!root.StartsWith(Presentation + ".", StringComparison.Ordinal)) { return "Other"; }
        string relative = root[(Presentation.Length + 1)..];
        if (ExactRoles.TryGetValue(relative, out string? role)) { return role; }
        return relative switch
        {
            var name when name.StartsWith("ViewModels.", StringComparison.Ordinal) => "ViewModel",
            var name when name.StartsWith("Controls.", StringComparison.Ordinal) => "Control",
            var name when name.StartsWith("Services.", StringComparison.Ordinal) => "Service",
            var name when name.StartsWith("Views.", StringComparison.Ordinal) => "View",
            _ => "Other",
        };
    }
    internal static ImmutableArray<BoundaryFinding> Find(IEnumerable<MetadataReference> references)
    {
        return [.. references.Select(reference =>
        {
            string role = Role(reference.Owner);
            string rule = role == "ViewModel" && reference.Target.StartsWith("Avalonia.Controls.", StringComparison.Ordinal) ? "A2.ViewModelControls"
                : role is "Control" or "Service" && Role(reference.Target) == "ViewModel" ? $"A2.{role}ViewModels"
                : reference.Owner.StartsWith(Presentation + ".", StringComparison.Ordinal)
                    && ((reference.Target == "System.Text.Json.JsonDocument" && reference.TargetMember == "Parse")
                    || (reference.Target == "System.Text.Json.JsonSerializer" && reference.TargetMember.StartsWith("Deserialize", StringComparison.Ordinal))) ? "A2.PresentationRawJson" : "";
            return new BoundaryFinding(rule, reference.Owner, reference.Member, reference.Target + (rule == "A2.PresentationRawJson" ? "." + reference.TargetMember : ""));
        }).Where(item => item.Rule.Length != 0).Distinct().OrderBy(item => item.ToString(), StringComparer.Ordinal)];
    }
    internal static void RequireClean(IEnumerable<MetadataReference> references)
    {
        ImmutableArray<BoundaryFinding> findings = Find(references);
        Assert.True(findings.IsEmpty, string.Join(Environment.NewLine, findings));
    }
    // Reviewed N0a legacy findings only. Remove resolved entries; never add new allowances.
    // N0b moves these exact identities and reasons into its ratcheted baseline.
    private static readonly ImmutableArray<(BoundaryFinding Finding, string Reason)> KnownViolations =
    [
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", ".ctor [closure]", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", ".ctor(System.Func`3<System.Action,System.TimeSpan,System.IDisposable>)", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "<>9__122_0", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "<>9__132_0", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "<>9__132_2", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "<>9__72_0", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusPositionViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "<>9__72_4", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusPositionViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "<>9__83_1", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageInteractionState", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "BuildLegend [closure]", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageFillRole", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "BuildLegend()", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "BuildPositions [closure]", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageInteractionState", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusLaneViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusPositionViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellTextResources"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "BuildPositions()", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusPositionViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "CloseAll()", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "CollisionEntry(NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel)", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "ContainingStates [closure]", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageInteractionState", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusLaneViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "ContainingStates(NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusLaneViewModel)", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageInteractionState", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusLaneViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "GroupSummary(NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem)", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellTextResources"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "OpenCard(Avalonia.Controls.Control,NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel,System.Nullable`1<System.Boolean>)", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "OpenLocal(NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem,Avalonia.Controls.Control,NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusLaneViewModel,System.Boolean)", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageInteractionState", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusLaneViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellTextResources"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "Rebuild [closure]", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusLaneViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellTextResources"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "Rebuild()", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarProjection", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellTextResources"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "SegmentContent [closure]", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageFillRole", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "SegmentContent(NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel)", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageFillRole", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellTextResources"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "Text", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellTextResources"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "UpdateTinyMarkers [closure]", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusLaneViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "UpdateTinyMarkers(System.Double)", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellTextResources"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "WireSlice [closure]", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "WireSlice(Avalonia.Controls.Control,NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel,System.Boolean)", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "_activeGroup", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "_displaySegments", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "get_Text()", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellLanguage", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellTextResources"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "item", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageBarItem"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "lane", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusLaneViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "position", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusPositionViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "positions", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryFocusPositionViewModel"),
        .. Known("A2.ControlViewModels", "NvtFwCombiner.Presentation.Avalonia.Views.MemoryCoverageBar", "slice", "Legacy shared coverage primitive consumes ViewModels display models; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.MemoryCoverageSegmentViewModel"),
        .. Known("A2.PresentationRawJson", "NvtFwCombiner.Presentation.Avalonia.LocalJsonDocument", "DeserializeAsync(System.IO.Stream,System.Threading.CancellationToken)", "Legacy report/local-state JSON decoding call; N0b owns its exact debt.", "System.Text.Json.JsonSerializer.DeserializeAsync"),
        .. Known("A2.PresentationRawJson", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryEntryViewModel", ".ctor(System.Int32,NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot,System.Nullable`1<System.Int64>)", "Legacy report/local-state JSON decoding call; N0b owns its exact debt.", "System.Text.Json.JsonDocument.Parse"),
        .. Known("A2.PresentationRawJson", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportPresentationViewModel", "TryPrepareReportHistoryEntry(NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot,NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellLanguage,System.Boolean,System.Threading.CancellationToken,NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot&,NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportReviewViewModel&)", "Legacy report/local-state JSON decoding call; N0b owns its exact debt.", "System.Text.Json.JsonDocument.Parse"),
        .. Known("A2.PresentationRawJson", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportReviewViewModel", "FromJsonCore(System.String,System.String,System.String,NvtFwCombiner.Application.Composition.CompositionRunInspectionSnapshot,NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellLanguage,System.Threading.CancellationToken)", "Legacy report/local-state JSON decoding call; N0b owns its exact debt.", "System.Text.Json.JsonDocument.Parse"),
        .. Known("A2.PresentationRawJson", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportReviewViewModel", "ParseHexDiffRange(System.String,NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportReviewViewModel+JsonValueSlice,NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHexDiffRangeDescriptor,System.String,System.Int64,NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellLanguage)", "Legacy report/local-state JSON decoding call; N0b owns its exact debt.", "System.Text.Json.JsonDocument.Parse"),
        .. Known("A2.PresentationRawJson", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportReviewViewModel", "ParseOutputDifference(System.String,NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportReviewViewModel+JsonValueSlice,NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellLanguage)", "Legacy report/local-state JSON decoding call; N0b owns its exact debt.", "System.Text.Json.JsonDocument.Parse"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore", "<1>__ToSnapshot", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore", "<2>__OmitDerivableReportHistoryMetadata", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore", "<>9__6_0", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore", "<>9__7_0", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore", "LoadAsync(NvtFwCombiner.Application.Ports.ILocalFileStore,System.String,System.Threading.CancellationToken)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore", "RetainPayloadBudget [closure]", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore", "RetainPayloadBudget(System.Collections.Generic.IEnumerable`1<NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot>)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore", "SaveAsync(NvtFwCombiner.Application.Ports.ILocalFileStore,System.String,System.Collections.Generic.IEnumerable`1<NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot>,System.Threading.CancellationToken)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportPresentationViewModel"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore", "Serialize [closure]", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore", "Serialize(System.Collections.Generic.IReadOnlyList`1<NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot>)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore", "ToSnapshot(NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistorySnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry", ".ctor(NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry", ".ctor(System.String,System.String,System.String,NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry", "<Metadata>k__BackingField", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry", "Deconstruct(System.String&,System.String&,System.String&,NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot&)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry", "Equals(NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry", "GetHashCode()", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry", "Metadata", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry", "PrintMembers(System.Text.StringBuilder)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry", "get_Metadata()", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ReportHistoryFileStore+ReportHistoryFileEntry", "set_Metadata(NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ReportHistoryMetadataSnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ShellPreferenceFileStore", "LoadAsync(NvtFwCombiner.Application.Ports.ILocalFileStore,System.String)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellPreferenceSnapshot"),
        .. Known("A2.ServiceViewModels", "NvtFwCombiner.Presentation.Avalonia.ShellPreferenceFileStore", "SaveAsync(NvtFwCombiner.Application.Ports.ILocalFileStore,System.String,NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellPreferenceSnapshot,System.Threading.CancellationToken)", "Legacy bounded UI-history adapter consumes ViewModels snapshots; N0b owns its exact debt.", "NvtFwCombiner.Presentation.Avalonia.ViewModels.ShellPreferenceSnapshot"),
    ];
    private static IEnumerable<(BoundaryFinding Finding, string Reason)> Known(string rule, string type, string member, string reason, params string[] targets)
        => targets.Select(target => (new BoundaryFinding(rule, type, member, target), reason));

    /// <summary>Every acquired assembly is scanned and new or stale A2 allowances fail.</summary>
    [Fact]
    public void Boundaries_CurrentAssemblies_MatchReviewedKnownViolations()
    {
        ImmutableArray<BoundaryFinding> actual = Find(AssemblyInventory.Load().SelectMany(input => MetadataReferenceWalker.Read(input.Image, Path.GetDirectoryName(input.Path))));
        Assert.True(KnownViolations.Select(item => item.Finding.ToString()).Order(StringComparer.Ordinal).SequenceEqual(actual.Select(item => item.ToString())), string.Join(Environment.NewLine, actual));
    }
    /// <summary>Every metadata traversal surface can expose a forbidden Control.</summary>
    [Theory]
    [InlineData("Base")]
    [InlineData("Interface")]
    [InlineData("TypeConstraint")]
    [InlineData("MethodConstraint")]
    [InlineData("Parameter")]
    [InlineData("Return")]
    [InlineData("NestedGeneric")]
    [InlineData("TypeSpecification")]
    [InlineData("Field")]
    [InlineData("Property")]
    [InlineData("Event")]
    [InlineData("Attribute")]
    [InlineData("NamedAttribute")]
    [InlineData("AttributeArray")]
    [InlineData("AttributeEnum")]
    [InlineData("Local")]
    [InlineData("Catch")]
    [InlineData("TypeToken")]
    [InlineData("FieldToken")]
    [InlineData("MemberReference")]
    [InlineData("MethodSpecification")]
    [InlineData("Calli")]
    public void Boundaries_ForbiddenControl_NamesRuleAndLogicalOwner(string scenario)
    {
        ImmutableArray<MetadataReference> references = MetadataReferenceWalker.Read(MetadataFixtureBuilder.Create(scenario));
        Assert.Contains($"A2.ViewModelControls: {MetadataFixtureBuilder.ViewModel}", Assert.ThrowsAny<Exception>(() => RequireClean(references)).Message, StringComparison.Ordinal);
    }
    /// <summary>Other A2 rules have independent forbidden examples.</summary>
    [Theory]
    [InlineData("Control", ".Controls.ForbiddenControl", "A2.ControlViewModels")]
    [InlineData("Service", ".Services.ForbiddenService", "A2.ServiceViewModels")]
    [InlineData("JsonDeserialize", ".Services.JsonCodec", "A2.PresentationRawJson")]
    [InlineData("JsonDeserializeAsync", ".Services.JsonCodec", "A2.PresentationRawJson")]
    [InlineData("JsonParse", ".Services.JsonCodec", "A2.PresentationRawJson")]
    public void Boundaries_ForbiddenReference_NamesRuleAndOffender(string scenario, string owner, string rule)
    {
        ImmutableArray<MetadataReference> references = MetadataReferenceWalker.Read(MetadataFixtureBuilder.Create(scenario, Presentation + owner));
        Assert.Contains($"{rule}: {Presentation + owner}", Assert.ThrowsAny<Exception>(() => RequireClean(references)).Message, StringComparison.Ordinal);
    }
    /// <summary>Unused reference rows and legitimate View consumption do not fail A2.</summary>
    [Theory]
    [InlineData("Positive", ".ViewModels.CleanViewModel")]
    [InlineData("Positive", ".Controls.CleanControl")]
    [InlineData("Positive", ".Services.CleanService")]
    [InlineData("View", ".Views.LegitimateView")]
    public void Boundaries_AllowedReference_Passes(string scenario, string owner)
    {
        RequireClean(MetadataReferenceWalker.Read(MetadataFixtureBuilder.Create(scenario, Presentation + owner)));
    }
    /// <summary>Real MoveNext, closure and async-closure bodies retain their logical owner.</summary>
    [Theory]
    [InlineData("AfterAwait(System.Threading.Tasks.Task)")]
    [InlineData("WithClosure [closure]")]
    [InlineData("AsyncClosure [closure]")]
    public void Boundaries_CompiledGeneratedBody_NamesLogicalOwner(string member)
    {
        ImmutableArray<MetadataReference> references = MetadataReferenceWalker.Read([.. File.ReadAllBytes(typeof(CompiledMetadataBoundaryTests).Assembly.Location)]);
        Assert.Contains("A2.ViewModelControls: " + Presentation + ".ViewModels.AsyncBoundaryFixture | " + member,
            Assert.ThrowsAny<Exception>(() => RequireClean(references)).Message, StringComparison.Ordinal);
    }
    /// <summary>View inheritance and Coordinator suffixes do not invent reusable-control/service roles.</summary>
    [Theory]
    [InlineData(".MainWindow", "View")]
    [InlineData(".Views.SettingsModal", "View")]
    [InlineData(".Views.MemoryCoverageBar", "Control")]
    [InlineData(".DesktopLaunchCoordinator", "Lifecycle")]
    [InlineData(".UnlistedCoordinator", "Other")]
    public void Role_ExplicitCategory_MatchesPolicy(string suffix, string expected) { Assert.Equal(expected, Role(Presentation + suffix)); }
}
