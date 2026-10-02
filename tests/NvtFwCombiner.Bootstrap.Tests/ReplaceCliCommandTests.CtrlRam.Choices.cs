using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    /// <summary>Every bank token reaches the same Application transition and accepted execution session.</summary>
    [Theory]
    [InlineData("a", AbCtrlRamBankSelection.A, "preview")]
    [InlineData("b", AbCtrlRamBankSelection.B, "preview")]
    [InlineData("both", AbCtrlRamBankSelection.Both, "preview")]
    [InlineData("b", AbCtrlRamBankSelection.B, "build")]
    public async Task CtrlRamBankChoiceReachesApplicationAsync(string token, AbCtrlRamBankSelection expected, string action)
    {
        using var workspace = TempWorkspace.Create("ctrlram-cli-bank");
        CtrlRamChoiceRun result = await RunCtrlRamChoiceAsync(workspace, ab: true, ["--bank", token], action);

        Assert.True(result.ExitCode == 0, result.Error);
        AbCtrlRamDraftState forwarded = Assert.IsType<AbCtrlRamDraftState>(Assert.Single(result.Authoring.Drafts));
        Assert.Equal(new AbCtrlRamDraftState(expected), forwarded);
        Assert.Equal(forwarded, Assert.Single(result.Execution.Requests).AcceptedSession.DraftState);
        Assert.Equal(action == "build", File.Exists(workspace.PathFor("output.bin")));
    }

    /// <summary>Standard version and sub-version text are typed bytes at the existing Application owner.</summary>
    [Theory]
    [InlineData("preview")]
    [InlineData("build")]
    public async Task CtrlRamStandardVersionPairReachesApplicationAsync(string action)
    {
        using var workspace = TempWorkspace.Create("ctrlram-cli-version");
        CtrlRamChoiceRun result = await RunCtrlRamChoiceAsync(workspace, ab: false,
            ["--firmware-version", "a9", "--firmware-sub-version", "F2"], action);

        Assert.True(result.ExitCode == 0, result.Error);
        CtrlRamFirmwareVersionDraftState draft = Assert.IsType<CtrlRamFirmwareVersionDraftState>(Assert.Single(result.Authoring.Drafts));
        Assert.Equal(new CtrlRamFirmwareVersionDraftState(0xA9, 0xF2), draft);
        Assert.Equal(draft, Assert.Single(result.Execution.Requests).AcceptedSession.DraftState);
    }

    /// <summary>Independent bank version pairs retain selected banks in the shared compiler.</summary>
    [Theory]
    [InlineData("a", true, false, AbCtrlRamBankSelection.A)]
    [InlineData("b", false, true, AbCtrlRamBankSelection.B)]
    [InlineData("both", true, true, AbCtrlRamBankSelection.Both)]
    [InlineData(null, true, true, AbCtrlRamBankSelection.Both)]
    public async Task CtrlRamBankVersionPairsReachApplicationAsync(
        string? bank, bool editA, bool editB, AbCtrlRamBankSelection expected)
    {
        using var workspace = TempWorkspace.Create("ctrlram-cli-bank-versions");
        string[] options =
        [
            .. bank is null ? Array.Empty<string>() : ["--bank", bank],
            .. editA ? new[] { "--a-firmware-version", "21", "--a-firmware-sub-version", "32" } : [],
            .. editB ? new[] { "--b-firmware-version", "29", "--b-firmware-sub-version", "41" } : [],
        ];
        CtrlRamChoiceRun result = await RunCtrlRamChoiceAsync(workspace, ab: true, options);

        Assert.True(result.ExitCode == 0, result.Error);
        AbCtrlRamDraftState draft = Assert.IsType<AbCtrlRamDraftState>(result.Authoring.Drafts[^1]);
        Assert.Equal(new AbCtrlRamDraftState(expected, editA ? new(0x21, 0x32) : null,
            editB ? new(0x29, 0x41) : null), draft);
        Assert.Equal(draft, Assert.Single(result.Execution.Requests).AcceptedSession.DraftState);
    }

    /// <summary>Application rejects bank/profile mismatch and version edits to unselected banks before execution.</summary>
    [Theory]
    [InlineData(false, "--bank", "a")]
    [InlineData(false, "--bank", "both")]
    [InlineData(false, "--a-firmware-version", "21", "--a-firmware-sub-version", "32")]
    [InlineData(false, "--b-firmware-version", "29", "--b-firmware-sub-version", "41")]
    [InlineData(true, "--firmware-version", "21", "--firmware-sub-version", "32")]
    [InlineData(true, "--bank", "a", "--b-firmware-version", "29", "--b-firmware-sub-version", "41")]
    [InlineData(true, "--bank", "b", "--a-firmware-version", "21", "--a-firmware-sub-version", "32")]
    [InlineData(true, "--bank", "a", "--a-firmware-version", "21", "--a-firmware-sub-version", "32",
        "--b-firmware-version", "29", "--b-firmware-sub-version", "41")]
    [InlineData(true, "--bank", "b", "--a-firmware-version", "21", "--a-firmware-sub-version", "32",
        "--b-firmware-version", "29", "--b-firmware-sub-version", "41")]
    public async Task CtrlRamApplicationRefusalWritesNoArtifactsAsync(bool ab, params string[] options)
    {
        using var workspace = TempWorkspace.Create("ctrlram-cli-choice-refusal");
        CtrlRamChoiceRun result = await RunCtrlRamChoiceAsync(workspace, ab, options, action: "build");

        Assert.Equal(1, result.ExitCode);
        Assert.NotEmpty(result.Authoring.Drafts);
        Assert.Empty(result.Execution.Requests);
        Assert.NotEmpty(result.Error);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(workspace.PathFor("output.bin")));
        Assert.False(File.Exists(workspace.PathFor("report.json")));
    }

    /// <summary>Invalid typed values and incomplete version pairs preserve the command's early-refusal exit.</summary>
    [Theory]
    [InlineData("--bank", "unknown")]
    [InlineData("--firmware-version", "GG", "--firmware-sub-version", "00")]
    [InlineData("--firmware-version", "100", "--firmware-sub-version", "00")]
    [InlineData("--firmware-version", "21")]
    [InlineData("--firmware-sub-version", "32")]
    [InlineData("--a-firmware-version", "21")]
    [InlineData("--a-firmware-sub-version", "32")]
    [InlineData("--b-firmware-version", "29")]
    [InlineData("--b-firmware-sub-version", "41")]
    [InlineData("--b-firmware-version", "00", "--b-firmware-sub-version", "-1")]
    [InlineData("--a-firmware-version", "00", "--a-firmware-sub-version", "ZZ")]
    [InlineData("--firmware-version", "21", "--firmware-sub-version", "32", "--bank", "both")]
    public async Task CtrlRamInvalidChoiceRefusesBeforeReadingBaseAsync(params string[] options)
    {
        using var workspace = TempWorkspace.Create("ctrlram-cli-choice-parse");
        CliRunResult result = await RunCliAsync(
            ["ctrlram-replace", "build", "--profile", "NT51929", .. options,
                "--base", workspace.PathFor("unread-base.bin"), "--ic-num", "single",
                "--ctrlram", $"nf={workspace.PathFor("unread-nf.bin")}",
                "--output", workspace.PathFor("output.bin"), "--report", workspace.PathFor("report.json")]);

        Assert.Equal(1, result.ExitCode);
        Assert.StartsWith("error:", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("input.artifact.read-failed", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(workspace.PathFor("output.bin")));
        Assert.False(File.Exists(workspace.PathFor("report.json")));
    }
}
