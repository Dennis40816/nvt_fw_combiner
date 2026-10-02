using System.Globalization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Cli;

internal static partial class ReplaceCliCommandHandler
{
    private const string AbBaseVersionOptionsIssueCode = "cli.ctrlram.ab-base-version-options";
    private const string StandardBaseBankOptionsIssueCode = "cli.ctrlram.standard-base-bank-options";

    private static readonly string[] CtrlRamChoiceOptions =
    [
        "--bank", "--firmware-version", "--firmware-sub-version",
        "--a-firmware-version", "--a-firmware-sub-version",
        "--b-firmware-version", "--b-firmware-sub-version",
    ];

    private static bool TryParseCtrlRamChoices(
        ParsedCliOptions options, TextWriter error, out CtrlRamCliChoices choices)
    {
        choices = new(null, null, null, null);
        AbCtrlRamBankSelection? banks = null;
        if (options.Values.TryGetValue("--bank", out string? token))
        {
            banks = token.Trim().ToLowerInvariant() switch
            {
                "a" => AbCtrlRamBankSelection.A,
                "b" => AbCtrlRamBankSelection.B,
                "both" => AbCtrlRamBankSelection.Both,
                _ => null,
            };
            if (banks is null)
            {
                error.WriteLine("error: --bank must be a, b or both");
                return false;
            }
        }

        if (!TryParseCtrlRamVersion(options, "--firmware-version", "--firmware-sub-version", error, out CtrlRamFirmwareVersionDraftState? version) ||
            !TryParseCtrlRamVersion(options, "--a-firmware-version", "--a-firmware-sub-version", error, out CtrlRamFirmwareVersionDraftState? aVersion) ||
            !TryParseCtrlRamVersion(options, "--b-firmware-version", "--b-firmware-sub-version", error, out CtrlRamFirmwareVersionDraftState? bVersion))
        {
            return false;
        }

        if (version is not null && (banks is not null || aVersion is not null || bVersion is not null))
        {
            error.WriteLine("error: Standard firmware-version options cannot be combined with AB bank options");
            return false;
        }

        choices = new(banks, version, aVersion, bVersion);
        return true;
    }

    private static bool TryParseCtrlRamVersion(
        ParsedCliOptions options, string versionOption, string subVersionOption, TextWriter error,
        out CtrlRamFirmwareVersionDraftState? draft)
    {
        draft = null;
        bool hasVersion = options.Values.TryGetValue(versionOption, out string? versionText);
        bool hasSubVersion = options.Values.TryGetValue(subVersionOption, out string? subVersionText);
        if (!hasVersion && !hasSubVersion)
        {
            return true;
        }

        if (!hasVersion || !hasSubVersion)
        {
            error.WriteLine($"error: {versionOption} and {subVersionOption} must be supplied together");
            return false;
        }

        if (!TryParseFirmwareVersionByte(versionText, out byte version) ||
            !TryParseFirmwareVersionByte(subVersionText, out byte subVersion))
        {
            error.WriteLine($"error: {versionOption} and {subVersionOption} require two hexadecimal digits (00..FF)");
            return false;
        }

        draft = new(version, subVersion);
        return true;
    }

    private static bool TryParseFirmwareVersionByte(string? text, out byte value)
    {
        value = 0;
        return text is { Length: 2 } &&
            byte.TryParse(text, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out value);
    }

    private static CtrlRamAuthoringTransitionResult ApplyCtrlRamChoices(
        ICtrlRamAuthoring authoring, AuthoringSessionState session, string icId, string number,
        IReadOnlyDictionary<string, string> slotPaths, ActiveSessionSnapshot accepted,
        CtrlRamCliChoices choices)
    {
        if (accepted.DraftState is AbCtrlRamDraftState && choices.Version is not null)
        {
            return new(null, [new CompositionIssue(
                AbBaseVersionOptionsIssueCode,
                "this Base has A and B banks: use --a-firmware-version/--a-firmware-sub-version and --b-firmware-version/--b-firmware-sub-version, not --firmware-version")]);
        }

        if (accepted.DraftState is not AbCtrlRamDraftState &&
            (choices.Banks is not null || choices.AVersion is not null || choices.BVersion is not null))
        {
            return new(null, [new CompositionIssue(
                StandardBaseBankOptionsIssueCode,
                "--bank and the per-bank version options apply only to a Base with A and B banks")]);
        }

        ActiveSessionSnapshot current = accepted;
        if (choices.Banks is { } banks)
        {
            CtrlRamAuthoringTransitionResult bankSelection = authoring.TransitionFirmwareVersionCompilation(
                session, icId, number, slotPaths, new AbCtrlRamDraftState(banks));
            if (!bankSelection.Succeeded)
            {
                return bankSelection;
            }

            current = bankSelection.Session!;
        }

        if (choices.Version is not null)
        {
            return authoring.TransitionFirmwareVersionCompilation(session, icId, number, slotPaths, choices.Version);
        }

        (bool IsA, CtrlRamFirmwareVersionDraftState? Version)[] edits = [(true, choices.AVersion), (false, choices.BVersion)];
        foreach ((bool isA, CtrlRamFirmwareVersionDraftState? version) in edits)
        {
            if (version is null)
            {
                continue;
            }

            // Forward each explicit bank edit separately so the existing transition's
            // unselected-bank refusal also applies when the caller edits both banks.
            AbCtrlRamDraftState? ab = current.DraftState as AbCtrlRamDraftState;
            var draft = new AbCtrlRamDraftState(ab?.Banks ?? AbCtrlRamBankSelection.Both,
                isA ? version : ab?.AVersion, isA ? ab?.BVersion : version);
            CtrlRamAuthoringTransitionResult edited = authoring.TransitionFirmwareVersionCompilation(
                session, icId, number, slotPaths, draft);
            if (!edited.Succeeded)
            {
                return edited;
            }

            current = edited.Session!;
        }

        return new(current, []);
    }

    private sealed record CtrlRamCliChoices(
        AbCtrlRamBankSelection? Banks, CtrlRamFirmwareVersionDraftState? Version,
        CtrlRamFirmwareVersionDraftState? AVersion, CtrlRamFirmwareVersionDraftState? BVersion);
}
