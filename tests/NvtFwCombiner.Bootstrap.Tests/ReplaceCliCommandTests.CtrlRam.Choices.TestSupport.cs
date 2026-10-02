using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    private static async Task<CtrlRamChoiceRun> RunCtrlRamChoiceAsync(
        TempWorkspace workspace, bool ab, string[] options, string action = "preview")
    {
        CompositionHostServices host = BootstrapTestHost.Services;
        var authoring = new CliChoiceRecordingCtrlRamAuthoring(host.CtrlRamAuthoring);
        var execution = new CliChoiceRecordingExecution(host.CompositionExecution);
        var services = new CliCompositionServices(host.CompositionCapabilityExperience, host.SavedRuleAuthoring,
            host.StandardMergeAuthoring, host.AbMergeAuthoring, authoring,
            host.GeneralAuthoring, host.CompositionOutputNaming, execution);
        (_, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        byte[] reference = bytes["replace-base"];
        string referencePath = workspace.Write("base.bin", ab ? reference : reference[..0x40000]);
        string sourcePath = workspace.Write("nf.bin", bytes["replace-ctrlram-nf"]);
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exit = await ReplaceCliCommandHandler.RunAsync(services, host.LocalFiles, "ctrlram-replace",
            [action, "--profile", "NT51929", "--ic-num", "single", "--base", referencePath,
                "--ctrlram", $"replace-ctrlram-nf={sourcePath}", .. options,
                "--output", workspace.PathFor("output.bin"), "--report", workspace.PathFor("report.json")],
            output, error, TestContext.Current.CancellationToken);
        return new(exit, output.ToString(), error.ToString(), authoring, execution);
    }

    private sealed record CtrlRamChoiceRun(int ExitCode, string Output, string Error,
        CliChoiceRecordingCtrlRamAuthoring Authoring, CliChoiceRecordingExecution Execution);
}
