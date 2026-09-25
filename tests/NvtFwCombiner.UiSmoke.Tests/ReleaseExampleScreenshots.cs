using System.Globalization;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.UiSmoke.Tests.ReportControlTestHost;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Real canonical input selections rendered by the product window for owner visual review.</summary>
public sealed class ReleaseExampleScreenshots
{
    /// <summary>Captures both Details states for the representative release workflow matrix.</summary>
    [AvaloniaTheory]
    [InlineData("nt51926-gen-flash")]
    [InlineData("nt51927-gen-flash")]
    [InlineData("nt51929-gen-flash")]
    [InlineData("51950-dp-256k")]
    [InlineData("nt51929-ab-t05-d06")]
    [InlineData("nt51950-ab-boe-d82t80")]
    [InlineData("nt51950-ab-hiway-d82t80")]
    [InlineData("nt51926-fw200-single-auto-prj-597-20260718")]
    [InlineData("nt51927-fw141-single-auto-prj-529-20260717")]
    [InlineData("nt51927-3chip-self-20260705")]
    [InlineData("nt51929-fw200-single-auto-prj-594-20260717")]
    [InlineData("nt51950-fw200-single-auto-prj-676-20260717")]
    [InlineData("nt51929-ab-ctrlram-candidate")]
    [InlineData("nt51950-ab-ctrlram-unsupported")]
    public async Task LoadedInputsHaveClosedAndOpenDetailsEvidence(string exampleId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exampleId);
        string? outputDirectory = Environment.GetEnvironmentVariable("NFC_VISUAL_OUTPUT_DIR");
        string[] args = Arguments(exampleId);
        UiLaunchOptions options = UiLaunchOptions.Parse(args);
        Assert.Empty(options.Issues);
        Assert.True(options.HasStartupInputs, $"No startup request for {exampleId}");
        using var workspace = TempWorkspace.Create("release-example-visual");
        PresentationHostServices services = await CreateServicesAsync(workspace);
        using var window = new MainWindow(options, StartupTraceSession.Disabled,
            services, ShellPreferenceSnapshot.Default)
        {
            Width = 1440,
            Height = exampleId.Contains("3chip", StringComparison.Ordinal) ? 2600 :
            options.CtrlRam is not null || options.AbMerge is not null ? 1600 : 1100
        };
        window.Show();
        try
        {
            ShellPreloadSession preload = Assert.IsType<ShellPreloadSession>(
                window.FindControl<Border>("OptionalPreloadStatusHost")!.DataContext);
            using (var wait = new CancellationTokenSource(TimeSpan.FromSeconds(45)))
            {
                while (preload.Stage(ShellPreloadSession.HistoryStageId).State is not
                    (ShellPreloadStageState.Succeeded or ShellPreloadStageState.Failed or ShellPreloadStageState.Cancelled) ||
                    preload.HasOptionalStatus)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(50, wait.Token);
                }
            }
            Assert.Equal(ShellPreloadStageState.Succeeded, preload.Stage(ShellPreloadSession.HistoryStageId).State);
            Assert.False(preload.HasOptionalStatus);
            MainWindowViewModel shell = Assert.IsType<MainWindowViewModel>(window.DataContext);
            Grid interaction = window.FindControl<Grid>("ShellInteractionHost")!;
            using (var wait = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                while (!interaction.IsEnabled)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(50, wait.Token);
                }
            }
            FirmwareSlotViewModel[] selected = [.. shell.Merge.MergeSlots.Concat(shell.Merge.AbMergeSlots)
                .Concat(shell.Replace.ReplaceSlots).Where(slot => slot.HasFile).Distinct()];
            Assert.True(selected.Length > 0,
                $"No selected inputs for {exampleId}: page={shell.SelectedPage}, IC={shell.WorkflowSession.SelectedIc}, " +
                $"number={shell.WorkflowSession.SelectedNumber}, merge={shell.Merge.SelectedMergeMode}, " +
                $"replace={shell.Replace.SelectedReplaceMode}, modal={shell.WorkflowSession.IsWorkflowContextModalOpen}, " +
                $"report={shell.Reports.HasLoadedReport}:" +
                string.Join(" | ", shell.Reports.LoadedReport.Issues.Select(issue => issue.Detail)));
            Assert.All(selected, slot => Assert.NotNull(slot.CurrentInspectionProjection));
            if (options.CtrlRam is { } ctrlRam)
            {
                foreach (CtrlRamLaunchInput input in ctrlRam.Inputs)
                {
                    FirmwareSlotViewModel slot = Assert.Single(shell.Replace.ReplaceSlots,
                        slot => slot.SlotId == input.SlotId);
                    Assert.True(slot.HasFile, $"Requested input {input.SlotId} was not loaded for {exampleId}.");
                    Assert.NotNull(slot.CurrentInspectionProjection);
                    Assert.False(slot.IsSemanticStateError);
                }
            }
            Assert.False(shell.Reports.HasLoadedReport);
            if (exampleId.Contains("3chip", StringComparison.Ordinal))
            {
                foreach (FirmwareSlotGroupViewModel group in shell.Replace.ReplaceSlotGroups)
                {
                    group.IsExpanded = true;
                }
            }
            foreach (FirmwareSlotViewModel slot in selected)
            {
                slot.IsAdditionalFirmwareFactsExpanded = false;
            }
            Save("details-closed");
            foreach (FirmwareSlotViewModel slot in selected.Where(slot => slot.HasAdditionalFirmwareFacts))
            {
                slot.IsAdditionalFirmwareFactsExpanded = true;
            }
            Save("details-open");
        }
        finally { await CloseAndFlushAsync(window); }

        void Save(string state)
        {
            if (string.IsNullOrWhiteSpace(outputDirectory)) { return; }
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            _ = Directory.CreateDirectory(outputDirectory);
            using Avalonia.Media.Imaging.Bitmap? frame = window.GetLastRenderedFrame();
            Assert.NotNull(frame);
            frame.Save(Path.Combine(outputDirectory, $"{exampleId}-{state}.png"));
        }
    }

    private static string[] Arguments(string exampleId)
    {
        if (exampleId is "nt51929-ab-ctrlram-candidate" or "nt51950-ab-ctrlram-unsupported")
        {
            bool is929 = exampleId.StartsWith("nt51929", StringComparison.Ordinal);
            JsonElement ab = Case(is929 ? "nt51929-ab-t05-d06" : "nt51950-ab-boe-d82t80");
            JsonElement ctrl = Case(is929 ? "nt51929-fw200-single-auto-prj-594-20260717" :
                "nt51950-fw200-single-auto-prj-676-20260717");
            return ["--workflow", "ctrlram-replace", "--ic", is929 ? "NT51929" : "NT51950",
                "--ic-num", "single", "--base", Artifact(ab, "expected-output"),
                "--ctrlram", "replace-ctrlram-nf=" + Artifact(ctrl, "postbuild-nf-ctrlram")];
        }

        JsonElement golden = Case(exampleId);
        string workflow = golden.GetProperty("workflow").GetString()!;
        string topology = golden.GetProperty("topology").GetString()!;
        string number = golden.TryGetProperty("icCount", out JsonElement count)
            ? count.GetInt32() == 1 ? "single" : count.GetInt32().ToString(CultureInfo.InvariantCulture)
            : topology.StartsWith("cascade-", StringComparison.Ordinal) ? topology[8..] : "single";
        List<string> args = ["--workflow", workflow, "--ic", golden.GetProperty("ic").GetString()!,
            "--ic-num", number];
        if (workflow == "standard-merge")
        {
            args.AddRange(["--dp", Artifact(golden, "dp-input"), "--tp", Artifact(golden, "tp-input")]);
        }
        else if (workflow == "ab-merge")
        {
            args.AddRange(["--dp", Artifact(golden, "dp-ab-input"), "--tp-a", Artifact(golden, "tp-a-input"),
                "--tp-b", Artifact(golden, "tp-b-input")]);
        }
        else if (workflow == "ctrlram-replace")
        {
            JsonElement[] artifacts = [.. golden.GetProperty("artifacts").EnumerateArray()];
            string baseId = artifacts.Any(artifact => artifact.GetProperty("artifactId").GetString() == "reference-base")
                ? "reference-base" : "expected-output";
            args.AddRange(["--base", Artifact(golden, baseId)]);
            foreach (JsonElement artifact in artifacts)
            {
                string artifactId = artifact.GetProperty("artifactId").GetString()!;
                string? slot = artifact.TryGetProperty("slotId", out JsonElement slotProperty)
                    ? slotProperty.GetString() : null;
                if (slot?.StartsWith("replace-ctrlram-", StringComparison.Ordinal) != true)
                {
                    string? role = artifactId switch
                    {
                        "postbuild-nf-ctrlram" or "nf-ctrlram-input" => "nf",
                        "postbuild-normal-ctrlram" or "normal-ctrlram-input" => "normal",
                        "postbuild-mp-ctrlram" or "mp-ctrlram-input" => "mp",
                        "postbuild-vn-ctrlram" or "vn-ctrlram-input" => "vn",
                        _ => null,
                    };
                    slot = role is null ? null : "replace-ctrlram-" + role;
                }
                if (slot is not null)
                {
                    args.AddRange(["--ctrlram", slot + "=" + CanonicalGoldenTestData.ArtifactPath(artifact)]);
                }
            }
        }
        return [.. args];
    }

    private static string Artifact(JsonElement golden, string artifactId)
    {
        JsonElement entry = Assert.Single(golden.GetProperty("artifacts").EnumerateArray(),
            artifact => artifact.GetProperty("artifactId").GetString() == artifactId);
        return CanonicalGoldenTestData.ArtifactPath(entry);
    }

    private static JsonElement Case(string caseId)
    {
        string file = Assert.Single(Directory.EnumerateFiles(CanonicalGoldenTestData.Root, "case.json",
            SearchOption.AllDirectories), path => Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path))) == caseId);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
        return document.RootElement.Clone();
    }
}
