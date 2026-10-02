using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    private const string AbBaseVersionOptionsMessage =
        "this Base has A and B banks: use --a-firmware-version/--a-firmware-sub-version and --b-firmware-version/--b-firmware-sub-version, not --firmware-version";
    private const string StandardBaseBankOptionsMessage =
        "--bank and the per-bank version options apply only to a Base with A and B banks";
    private const string UnselectedBankVersionMessage =
        "Version editing is available only for selected banks. Select the bank before changing its version.";

    /// <summary>Every bank token reaches the same Application transition and accepted execution session.</summary>
    [Theory]
    [InlineData("a", AbCtrlRamBankSelection.A, "preview")]
    [InlineData("b", AbCtrlRamBankSelection.B, "preview")]
    [InlineData("both", AbCtrlRamBankSelection.Both, "preview")]
    [InlineData("b", AbCtrlRamBankSelection.B, "build")]
    [InlineData(" A ", AbCtrlRamBankSelection.A, "preview")]
    [InlineData(" Both ", AbCtrlRamBankSelection.Both, "build")]
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
    [Fact]
    public async Task CtrlRamStandardVersionPairReachesApplicationAsync()
    {
        using var workspace = TempWorkspace.Create("ctrlram-cli-version");
        CtrlRamChoiceRun result = await RunCtrlRamChoiceAsync(workspace, ab: false,
            ["--firmware-version", "a9", "--firmware-sub-version", "F2"], action: "build");

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
        CtrlRamChoiceRun result = await RunCtrlRamChoiceAsync(workspace, ab: true, options, action: "build");

        Assert.True(result.ExitCode == 0, result.Error);
        AbCtrlRamDraftState draft = Assert.IsType<AbCtrlRamDraftState>(result.Authoring.Drafts[^1]);
        Assert.Equal(new AbCtrlRamDraftState(expected, editA ? new(0x21, 0x32) : null,
            editB ? new(0x29, 0x41) : null), draft);
        Assert.Equal(draft, Assert.Single(result.Execution.Requests).AcceptedSession.DraftState);
    }

    /// <summary>CLI rejects options incompatible with the accepted Base; Application rejects edits to unselected banks.</summary>
    [Theory]
    [InlineData(false, "cli.ctrlram.standard-base-bank-options", StandardBaseBankOptionsMessage, "--bank", "a")]
    [InlineData(false, "cli.ctrlram.standard-base-bank-options", StandardBaseBankOptionsMessage, "--bank", "both")]
    [InlineData(false, "cli.ctrlram.standard-base-bank-options", StandardBaseBankOptionsMessage, "--a-firmware-version", "21", "--a-firmware-sub-version", "32")]
    [InlineData(false, "cli.ctrlram.standard-base-bank-options", StandardBaseBankOptionsMessage, "--b-firmware-version", "29", "--b-firmware-sub-version", "41")]
    [InlineData(true, "cli.ctrlram.ab-base-version-options", AbBaseVersionOptionsMessage, "--firmware-version", "21", "--firmware-sub-version", "32")]
    [InlineData(true, "authoring.ctrlram.unselected-bank-version", UnselectedBankVersionMessage, "--bank", "a", "--b-firmware-version", "29", "--b-firmware-sub-version", "41")]
    [InlineData(true, "authoring.ctrlram.unselected-bank-version", UnselectedBankVersionMessage, "--bank", "b", "--a-firmware-version", "21", "--a-firmware-sub-version", "32")]
    [InlineData(true, "authoring.ctrlram.unselected-bank-version", UnselectedBankVersionMessage, "--bank", "a", "--a-firmware-version", "21", "--a-firmware-sub-version", "32",
        "--b-firmware-version", "29", "--b-firmware-sub-version", "41")]
    [InlineData(true, "authoring.ctrlram.unselected-bank-version", UnselectedBankVersionMessage, "--bank", "b", "--a-firmware-version", "21", "--a-firmware-sub-version", "32",
        "--b-firmware-version", "29", "--b-firmware-sub-version", "41")]
    public async Task CtrlRamChoiceRefusalWritesNoArtifactsAsync(bool ab, string issueCode, string message, params string[] options)
    {
        ArgumentNullException.ThrowIfNull(issueCode);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);
        await AssertCtrlRamChoiceRefusalAsync(ab, issueCode, message, options, existingArtifacts: false);
        await AssertCtrlRamChoiceRefusalAsync(ab, issueCode, message, options, existingArtifacts: true);
    }

    private static async Task AssertCtrlRamChoiceRefusalAsync(
        bool ab,
        string issueCode,
        string message,
        string[] options,
        bool existingArtifacts)
    {
        using var workspace = TempWorkspace.Create("ctrlram-cli-choice-refusal");
        byte[] originalOutput = [0x12, 0x34];
        byte[] originalReport = [0x56, 0x78];
        if (existingArtifacts)
        {
            _ = workspace.Write("output.bin", originalOutput);
            _ = workspace.Write("report.json", originalReport);
        }

        CtrlRamChoiceRun result = await RunCtrlRamChoiceAsync(workspace, ab, options, action: "build");

        Assert.Equal(1, result.ExitCode);
        Assert.Empty(result.Execution.Requests);
        Assert.Contains($"{issueCode}: {message}", result.Error, StringComparison.Ordinal);
        if (issueCode.StartsWith("cli.", StringComparison.Ordinal))
        {
            Assert.Empty(result.Authoring.Drafts);
        }
        else
        {
            Assert.NotEmpty(result.Authoring.Drafts);
        }

        Assert.Empty(result.Output);
        if (existingArtifacts)
        {
            Assert.Equal(originalOutput, File.ReadAllBytes(workspace.PathFor("output.bin")));
            Assert.Equal(originalReport, File.ReadAllBytes(workspace.PathFor("report.json")));
        }
        else
        {
            Assert.False(File.Exists(workspace.PathFor("output.bin")));
            Assert.False(File.Exists(workspace.PathFor("report.json")));
        }
    }

    /// <summary>Invalid typed values and incomplete version pairs are usage errors before any input read.</summary>
    [Theory]
    [InlineData("--bank", "unknown")]
    [InlineData("--bank", "c")]
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

        Assert.Equal(64, result.ExitCode);
        Assert.StartsWith("error:", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("input.artifact.read-failed", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(workspace.PathFor("output.bin")));
        Assert.False(File.Exists(workspace.PathFor("report.json")));
    }

    /// <summary>Malformed CtrlRAM options precede the unsupported-IC refusal and never read or write firmware artifacts.</summary>
    [Theory]
    [InlineData("build", "--bank must be a, b or both", "--bank", "unknown")]
    [InlineData("build", "must be supplied together", "--firmware-version", "21")]
    [InlineData("build", "require two hexadecimal digits", "--firmware-version", "GG", "--firmware-sub-version", "00")]
    [InlineData("build", "expects --ctrlram <slot-id=path>", "--ctrlram", "malformed")]
    [InlineData("preview", "firmware-version options are available only for ctrlram-replace build", "--a-firmware-version", "21")]
    public async Task CtrlRamMalformedOptionsPrecedeUnavailableWorkflowAsync(string action, string message, params string[] options)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);
        using var workspace = TempWorkspace.Create("ctrlram-cli-unavailable-parse");
        CtrlRamChoiceRun result = await RunUnavailableCtrlRamWorkflowAsync(
            [action, "--profile", UnavailableCtrlRamTestIc, "--ic-num", "single",
                "--base", workspace.PathFor("unread-base.bin"),
                "--ctrlram", $"nf={workspace.PathFor("unread-nf.bin")}", .. options,
                "--output", workspace.PathFor("output.bin"), "--report", workspace.PathFor("report.json")],
            expectedAvailabilityQueries: 0);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains(message, result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("Replace is Not available", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("input.artifact.read-failed", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(workspace.PathFor("output.bin")));
        Assert.False(File.Exists(workspace.PathFor("report.json")));
    }

    /// <summary>Well-formed CtrlRAM options on an unsupported IC still return the Application-backed refusal before reading Base.</summary>
    [Fact]
    public async Task CtrlRamUnavailableWorkflowRemainsCompositionRefusalAsync()
    {
        using var workspace = TempWorkspace.Create("ctrlram-cli-unavailable");
        CtrlRamChoiceRun result = await RunUnavailableCtrlRamWorkflowAsync(
            ["build", "--profile", UnavailableCtrlRamTestIc, "--ic-num", "single",
                "--base", workspace.PathFor("unread-base.bin"), "--bank", "both",
                "--ctrlram", $"nf={workspace.PathFor("unread-nf.bin")}",
                "--output", workspace.PathFor("output.bin"), "--report", workspace.PathFor("report.json")],
            expectedAvailabilityQueries: 1);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains($"{UnavailableCtrlRamTestIc} ctrlram-replace Replace is Not available", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("input.artifact.read-failed", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Output);
        Assert.False(File.Exists(workspace.PathFor("output.bin")));
        Assert.False(File.Exists(workspace.PathFor("report.json")));
    }

    /// <summary>Preview rejects every version option before reading inputs and preserves existing destinations.</summary>
    [Theory]
    [InlineData("--firmware-version", "21", "--firmware-sub-version", "32")]
    [InlineData("--a-firmware-version", "21", "--a-firmware-sub-version", "32")]
    [InlineData("--b-firmware-version", "21", "--b-firmware-sub-version", "32")]
    [InlineData("--firmware-version", "21")]
    [InlineData("--firmware-sub-version", "32")]
    [InlineData("--a-firmware-version", "21")]
    [InlineData("--a-firmware-sub-version", "32")]
    [InlineData("--b-firmware-version", "21")]
    [InlineData("--b-firmware-sub-version", "32")]
    [InlineData("--firmware-version", "GG", "--firmware-sub-version", "00")]
    public async Task CtrlRamPreviewVersionOptionsAreUsageErrorsAsync(params string[] options)
    {
        using var workspace = TempWorkspace.Create("ctrlram-cli-preview-version");
        byte[] original = [0x12, 0x34];
        string outputPath = workspace.Write("output.bin", original);
        string reportPath = workspace.Write("report.json", original);
        CliRunResult result = await RunCliAsync(
            ["ctrlram-replace", "preview", "--profile", "NT51929", "--ic-num", "single",
                "--base", workspace.PathFor("unread-base.bin"), .. options,
                "--ctrlram", $"nf={workspace.PathFor("unread-nf.bin")}", "--output", outputPath, "--report", reportPath]);

        Assert.Equal(64, result.ExitCode);
        Assert.Contains("firmware-version options are available only for ctrlram-replace build", result.Error, StringComparison.Ordinal);
        Assert.DoesNotContain("input.artifact.read-failed", result.Error, StringComparison.Ordinal);
        Assert.Empty(result.Output);
        Assert.Equal(original, File.ReadAllBytes(outputPath));
        Assert.Equal(original, File.ReadAllBytes(reportPath));
    }
}
