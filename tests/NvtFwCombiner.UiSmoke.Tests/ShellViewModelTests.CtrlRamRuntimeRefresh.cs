using System.Text.Json;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Bootstrap;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.Presentation.Avalonia;
using NvtFwCombiner.Presentation.Avalonia.ViewModels;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class CtrlRamWorkflowTests
{
    /// <summary>Startup tool publication refreshes an already inspected CtrlRAM session without reselecting files.</summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task CtrlRamStartupEnvironmentPublicationRefreshesLoadedInputs(bool toolsAvailable, bool explicitRefresh)
    {
        using var workspace = TempWorkspace.Create("ctrlram-runtime-refresh");
        var loader = new ExternalProcessorEnvironmentLoader(toolsAvailable
            ? RepositoryPaths.FromRepositoryRoot("external-tools") : workspace.Root);
        CompositionHostServices host = CompositionHostServices.Create(loader);
        PresentationHostServices services = PresentationTestHost.CreateServices("runtime-refresh", host, static authoring => authoring);
        var shell = new MainWindowViewModel("test", "runtime-refresh", ShellLanguage.English, services);
        _ = PresentationTestHost.PublishCanonicalCatalog(services, shell);
        JsonElement fixture = CanonicalGoldenTestData.LoadDirectEvidenceCase(
            "ctrlram-replace", "nt51927-3chip-self-20260705");
        var arguments = new List<string> { "--workflow", "ctrlram-replace", "--ic", "NT51927", "--ic-num", "3" };
        foreach (JsonElement artifact in fixture.GetProperty("artifacts").EnumerateArray())
        {
            string slot = artifact.GetProperty("slotId").GetString()!;
            if (slot is not ("replace-base" or "replace-ctrlram-nf" or "replace-ctrlram-vn")) { continue; }
            string path = CanonicalGoldenTestData.ArtifactPath(artifact);
            arguments.Add(slot == "replace-base" ? "--base" : "--ctrlram");
            arguments.Add(slot == "replace-base" ? path : $"{slot}={path}");
        }
        await MainWindow.ApplyCtrlRamLaunchAsync(shell, UiLaunchOptions.Parse([.. arguments]).CtrlRam!, TestContext.Current.CancellationToken);
        Assert.False(shell.Replace.CanBuildReplace);
        Assert.Contains("not currently published", shell.ReplaceBuildBlockerCard.AutomationText);
        FirmwareSlotViewModel[] selected = [.. shell.Replace.ReplaceSlots.Where(slot => slot.HasFile)];
        object?[] projections = [.. selected.Select(slot => slot.CurrentInspectionProjection)];
        Assert.Equal(3, selected.Length);
        var blockerNotifications = new List<bool>();
        shell.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainWindowViewModel.HasReplaceBuildBlocker))
            {
                blockerNotifications.Add(shell.HasReplaceBuildBlocker);
            }
        };

        if (explicitRefresh)
        {
            await shell.MessageCenter.RefreshCommand.ExecuteAsync(null);
            await shell.Replace.Inspection.ActiveTask;
        }
        else
        {
            await shell.MessageCenter.RefreshExternalEnvironmentAfterStartupAsync(static (_, _) => { }, TestContext.Current.CancellationToken);
        }

        Assert.Equal(ExternalProcessorEnvironmentState.Current, loader.Current.State);
        Assert.Equal(toolsAvailable, shell.Replace.CanBuildReplace);
        Assert.Equal(!toolsAvailable, shell.HasReplaceBuildBlocker);
        Assert.NotEmpty(blockerNotifications);
        Assert.Equal(!toolsAvailable, blockerNotifications[^1]);
        Assert.DoesNotContain("not currently published", shell.ReplaceBuildBlockerCard.AutomationText);
        for (int index = 0; !explicitRefresh && index < selected.Length; index++)
        {
            Assert.Same(projections[index], selected[index].CurrentInspectionProjection);
        }
        if (toolsAvailable)
        {
            string outputPath = workspace.PathFor("runtime-refreshed-output.bin");
            await shell.Replace.BuildReplaceAsync(outputPath);
            Assert.True(shell.RunSession.LastRunResult.Succeeded, shell.RunSession.LastRunResult.Detail);
            byte[] output = await File.ReadAllBytesAsync(outputPath, TestContext.Current.CancellationToken);
            Assert.Equal(0x40000, output.Length);
            Assert.Equal(shell.Reports.LoadedReport.OutputSha256,
                Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(output)), ignoreCase: true);
        }
    }
}
