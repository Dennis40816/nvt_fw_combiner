using System.Text.Json;
using System.Reflection;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
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
        var host = CompositionHostServices.Create(loader, IsolatedLocalState.CreateDirectory());
        ICtrlRamAuthoring proxy = DispatchProxy.Create<ICtrlRamAuthoring, GatedCtrlRamReadinessProxy>();
        var readinessGate = (GatedCtrlRamReadinessProxy)proxy;
        readinessGate.Inner = host.CtrlRamAuthoring;
        PresentationHostServices services = PresentationTestHost.CreateServices(
            "runtime-refresh", host, static authoring => authoring, ctrlRamAuthoring: proxy);
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
        Assert.Equal(1, loader.Current.RequestGeneration);
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
        var finalLease = new WindowPublicationLease();
        shell.Replace.WindowPublication = finalLease;
        int postCloseReadinessNotifications = 0;
        shell.Replace.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(ReplacePresentationViewModel.CanBuildReplace))
            {
                postCloseReadinessNotifications++;
            }
        };
        readinessGate.Arm();
        Task refresh = shell.Replace.RefreshCtrlRamActionReadinessAsync(TestContext.Current.CancellationToken);
        await readinessGate.Started.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        finalLease.Revoke();
        readinessGate.Release();
        await refresh;
        Assert.Equal(0, postCloseReadinessNotifications);
    }

    /// <summary>Delays one CtrlRAM readiness response until the final close decision.</summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "CA1852:Seal internal types",
        Justification = "DispatchProxy creates a runtime subclass.")]
    public class GatedCtrlRamReadinessProxy : DispatchProxy
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ICtrlRamAuthoring Inner { get; set; } = null!;
        internal Task Started => _started.Task;
        private bool _armed;

        internal void Arm()
        {
            _armed = true;
        }

        internal void Release()
        {
            _ = _release.TrySetResult();
        }

        /// <inheritdoc />
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2012:Use ValueTasks correctly",
            Justification = "DispatchProxy must forward the boxed ValueTask without consuming it.")]
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            ArgumentNullException.ThrowIfNull(args);
            return _armed && targetMethod.Name == nameof(ICtrlRamAuthoring.GetActionReadinessAsync)
                ? ReadAfterGateAsync((string)args[0]!, (string)args[1]!,
                    (IReadOnlyDictionary<string, string>)args[2]!, (ActiveSessionSnapshot)args[3]!,
                    (CancellationToken)args[4]!)
                : targetMethod.Invoke(Inner, args);
        }

        private async ValueTask<CapabilityActionReadinessSnapshot?> ReadAfterGateAsync(
            string icId, string number, IReadOnlyDictionary<string, string> slotPaths,
            ActiveSessionSnapshot session, CancellationToken cancellationToken)
        {
            _ = _started.TrySetResult();
            await _release.Task;
            return await Inner.GetActionReadinessAsync(icId, number, slotPaths, session, cancellationToken);
        }
    }
}
