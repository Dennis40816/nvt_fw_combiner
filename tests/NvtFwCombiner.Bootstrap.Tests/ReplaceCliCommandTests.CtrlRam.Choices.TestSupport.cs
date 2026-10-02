using System.Reflection;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    private const string UnavailableCtrlRamTestIc = "NT00000";

    private static async Task<CtrlRamChoiceRun> RunUnavailableCtrlRamWorkflowAsync(
        string[] arguments, int expectedAvailabilityQueries)
    {
        // package-trust-index.json and canonical-capability-policy-v1.json expose CtrlRAM for every selectable IC.
        // Inject a known but unavailable test IC to exercise the refusal independently of that catalog.
        CompositionHostServices host = BootstrapTestHost.Services;
        ICompositionCapabilityExperience capabilities =
            DispatchProxy.Create<ICompositionCapabilityExperience, StandardMergeCliCommandTests.CapturedCliProxy>();
        var proxy = (StandardMergeCliCommandTests.CapturedCliProxy)capabilities;
        proxy.Target = host.CompositionCapabilityExperience;
        int availabilityQueries = 0;
        proxy.Intercept = (method, result, args) =>
        {
            if (method.Name == nameof(ICompositionCapabilityExperience.GetIcIds))
            {
                return (IReadOnlyList<string>)[UnavailableCtrlRamTestIc];
            }

            if (method.Name == nameof(ICompositionCapabilityExperience.IsReplaceWorkflowAvailable))
            {
                Assert.Equal(UnavailableCtrlRamTestIc, Assert.IsType<string>(args![0]));
                Assert.Equal("ctrlram-replace", Assert.IsType<string>(args[1]));
                availabilityQueries++;
                return false;
            }

            return result;
        };
        var authoring = new CliChoiceRecordingCtrlRamAuthoring(host.CtrlRamAuthoring);
        var execution = new CliChoiceRecordingExecution(host.CompositionExecution);
        var services = new CliCompositionServices(capabilities, host.SavedRuleAuthoring,
            host.StandardMergeAuthoring, host.AbMergeAuthoring, authoring,
            host.GeneralAuthoring, host.CompositionOutputNaming, execution);
        using var output = new StringWriter();
        using var error = new StringWriter();
        int exit = await ReplaceCliCommandHandler.RunAsync(services, host.LocalFiles, "ctrlram-replace",
            arguments, output, error, TestContext.Current.CancellationToken);

        Assert.Equal(expectedAvailabilityQueries, availabilityQueries);
        Assert.Empty(authoring.Drafts);
        Assert.Empty(execution.Requests);
        return new(exit, output.ToString(), error.ToString(), authoring, execution);
    }

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
