using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
namespace NvtFwCombiner.Cli;

internal static partial class ReplaceCliCommandHandler
{
    private static async Task<int> RunGeneralReplaceAsync(
        CompositionHostServices host,
        string action,
        string icId,
        ParsedCliOptions options,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (!RequireOption(options, "--ic-num", error, out string? icNumber) ||
            !RequireOption(options, "--base", error, out string? basePath))
        {
            return UsageError;
        }

        bool usesSavedRule = options.Values.TryGetValue(
            "--rule",
            out string? rulePath);
        GeneralSavedRuleResourcePolicy? savedRulePolicy = null;
        if (usesSavedRule &&
            (options.GetValues("--mapping").Count != 0 ||
             options.GetValues("--patch").Count != 0 ||
             options.GetValues("--fill").Count != 0))
        {
            error.WriteLine(
                "error: --rule cannot be combined with --mapping, --patch, or --fill");
            return UsageError;
        }

        if (usesSavedRule
                ? !TryCreateGeneralReplaceDraftFromSavedRule(
                    host.SavedRuleAuthoring,
                    rulePath!,
                    options.GetValues("--slot"),
                    basePath,
                    icId,
                    error,
                    out GeneralMappingDraftState? mappingDraft,
                    out savedRulePolicy)
                : !TryCreateGeneralAuthoringInputs(
                    options,
                    error,
                    out mappingDraft))
        {
            return UsageError;
        }

        if (!usesSavedRule && options.GetValues("--slot").Count != 0)
        {
            error.WriteLine("error: --slot requires --rule");
            return UsageError;
        }
        if (savedRulePolicy is not null)
        {
            mappingDraft = mappingDraft.WithSavedRuleResourcePolicy(savedRulePolicy);
        }

        Dictionary<string, string> slotPaths = new(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = Path.GetFullPath(basePath),
        };
        Dictionary<string, string> protectedInputPaths = new(slotPaths, StringComparer.Ordinal);
        foreach (GeneralMappingDraftRow mapping in mappingDraft.Rows.Where(
                     static row => row.Source.Kind == GeneralMappingSourceKind.FileArtifact))
        {
            protectedInputPaths[mapping.MappingId] = Path.GetFullPath(mapping.Source.Reference);
        }

        if (usesSavedRule)
        {
            protectedInputPaths["saved-rule"] = Path.GetFullPath(rulePath!);
        }

        var session = new AuthoringSessionState(ExperienceIds.GeneralReplace);
        GeneralAuthoringSessionPreparation prepared =
            await host.GeneralAuthoring.PrepareReplaceSessionAsync(
                    session,
                    icId,
                    icNumber,
                    slotPaths[CompositionSlotIds.ReplaceBase],
                    mappingDraft,
                    cancellationToken)
                .ConfigureAwait(false);

        if (!prepared.Succeeded)
        {
            await CliCompositionRunSupport.PrintIssuesAsync(error, prepared.Issues)
                .ConfigureAwait(false);
            return CompositionFailed;
        }

        ActiveSessionSnapshot acceptedSession = prepared.AcceptedSession!;
        if (prepared.Readiness is { } readiness)
        {
            CapabilityActionAvailability availability = action == "build"
                ? readiness.Build
                : readiness.Preview;
            if (!availability.IsAvailable)
            {
                CapabilityActionBlocker blocker = availability.PrimaryBlocker!;
                await CliCompositionRunSupport.PrintIssuesAsync(
                        error,
                        [new CompositionIssue(blocker.Code, blocker.Message, blocker.SubjectId)])
                    .ConfigureAwait(false);
                return CompositionFailed;
            }

            if (action != "build" && prepared.DiagnosticPreviewReport is { } diagnosticReport)
            {
                await CompleteGeneralReplaceDiagnosticPreviewAsync(
                        diagnosticReport,
                        options.Values.GetValueOrDefault("--report"),
                        CreateBindings(protectedInputPaths),
                        output,
                        error,
                        cancellationToken)
                    .ConfigureAwait(false);
                return CompositionFailed;
            }
        }

        string defaultOutputFileName = host.CompositionOutputNaming
            .ResolveAcceptedOutput(acceptedSession)
            .OutputName.FileName;

        return await CompleteReplaceRunAsync(
                action,
                icId,
                ExperienceIds.GeneralReplace,
                options,
                protectedInputPaths,
                defaultOutputFileName,
                (outputPath, build, token) =>
                    ExecuteAcceptedAsync(host.CompositionExecution.ExecuteAsync(
                        new AcceptedCompositionExecutionRequest(
                            acceptedSession,
                            slotPaths,
                            build,
                            outputPath: outputPath,
                            actionReadiness: prepared.Readiness),
                        new CompositionRunProgressFeed(),
                        token)),
                output,
                error,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task CompleteGeneralReplaceDiagnosticPreviewAsync(
        CompositionRunReport report,
        string? reportPath,
        IReadOnlyList<InputArtifactBinding> bindings,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        GeneralReplaceDiagnosticPreviewSummary diagnostic = report.DiagnosticPreview ??
            throw new ArgumentException(
                "A General Replace diagnostic Preview requires its plan-only marker.",
                nameof(report));
        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            ProtectedPathGuard.EnsureReportDoesNotAliasProtectedPaths(
                reportPath,
                bindings,
                outputPath: null,
                "--report");
            await CliCompositionRunSupport.WriteReportJsonAsync(
                    reportPath,
                    CompositionRunReportJson.SerializeDiagnosticPreview(report),
                    output,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await output.WriteLineAsync("Status: DiagnosticPlanOnly").ConfigureAwait(false);
        await output.WriteLineAsync($"Profile: {report.ProfileId} ({report.IcId})")
            .ConfigureAwait(false);
        await output.WriteLineAsync($"Experience: {report.ExperienceId}").ConfigureAwait(false);
        await output.WriteLineAsync("Output: not produced").ConfigureAwait(false);
        await output.WriteLineAsync(diagnostic.Message).ConfigureAwait(false);
        await CliCompositionRunSupport.PrintIssuesAsync(error, report.Issues)
            .ConfigureAwait(false);
    }

    private static bool TryCreateGeneralReplaceDraftFromSavedRule(
        ISavedRuleAuthoring authoring,
        string rulePath,
        IReadOnlyList<string> slotValues,
        string basePath,
        string icId,
        TextWriter error,
        [NotNullWhen(true)] out GeneralMappingDraftState? mappingDraft,
        [NotNullWhen(true)] out GeneralSavedRuleResourcePolicy? savedRulePolicy)
    {
        mappingDraft = null;
        savedRulePolicy = null;
        string? parentReferenceSlotId =
            authoring.GetGeneralReplaceSavedRuleReferenceSlotId(icId);
        if (parentReferenceSlotId is null)
        {
            error.WriteLine(
                $"error: no exact trusted {icId} / General Replace Saved Rule parent is registered");
            return false;
        }

        if (!SavedRuleCliSupport.TryCreateSlotBindings(
                slotValues,
                error,
                out Dictionary<string, string>? slotsById))
        {
            return false;
        }

        if (slotsById.ContainsKey(parentReferenceSlotId))
        {
            error.WriteLine(
                $"error: --slot {parentReferenceSlotId} is reserved for --base");
            return false;
        }

        slotsById.Add(parentReferenceSlotId, Path.GetFullPath(basePath));

        SavedRuleV2DraftLoadResult<GeneralMappingDraftState> load =
            authoring.LoadGeneralReplaceSavedRule(
                icId,
                rulePath,
                slotsById);
        if (!load.IsValid)
        {
            SavedRuleCliSupport.PrintIssues(load.Issues, error);
            return false;
        }

        mappingDraft = load.Draft!;
        savedRulePolicy = load.ResourcePolicy!;
        return true;
    }

    private static bool TryCreateGeneralAuthoringInputs(
        ParsedCliOptions options,
        TextWriter error,
        [NotNullWhen(true)] out GeneralMappingDraftState? mappingDraft)
    {
        List<string> mappingValues = options.GetValues("--mapping");
        List<string> patchValues = options.GetValues("--patch");
        List<string> fillValues = options.GetValues("--fill");
        if (mappingValues.Count == 0 && patchValues.Count == 0 && fillValues.Count == 0)
        {
            mappingDraft = null;
            error.WriteLine(
                "error: at least one --mapping <target-start+length=path>, --patch <target-start+length=hex>, or --fill <target-start+length=byte> value is required for real IC General Replace");
            return false;
        }

        List<GeneralMappingDraftRow> items = [];
        for (int index = 0; index < mappingValues.Count; index++)
        {
            if (!TryParseGeneralMappingValue(
                    mappingValues[index],
                    index + 1,
                    error,
                    out GeneralMappingDraftRow? mapping))
            {
                mappingDraft = null;
                return false;
            }

            items.Add(mapping);
        }

        if (!TryAppendGeneralPatches(
                "--patch",
                patchValues,
                GeneralMappingSourceKind.HexOverwrite,
                "general-patch",
                error,
                items) ||
            !TryAppendGeneralPatches(
                "--fill",
                fillValues,
                GeneralMappingSourceKind.HexFill,
                "general-fill",
                error,
                items))
        {
            mappingDraft = null;
            return false;
        }

        if (!GeneralAuthoringMappingUseCase.TryCreateGeneralReplaceDraft(
                items,
                out mappingDraft,
                out IReadOnlyList<CompositionIssue> issues))
        {
            foreach (CompositionIssue issue in issues)
            {
                error.WriteLine($"error: {issue.Message}");
            }

            return false;
        }

        return true;
    }

    private static bool TryAppendGeneralPatches(
        string optionName,
        List<string> values,
        GeneralMappingSourceKind kind,
        string idPrefix,
        TextWriter error,
        List<GeneralMappingDraftRow> rows)
    {
        for (int index = 0; index < values.Count; index++)
        {
            if (!TryParseGeneralRangeValue(
                    optionName,
                    values[index],
                    error,
                    out string? payload,
                    out ByteRange targetRange))
            {
                return false;
            }

            string mappingId = string.Create(
                CultureInfo.InvariantCulture,
                $"{idPrefix}-{index + 1}");
            GeneralMappingSource source = kind switch
            {
                GeneralMappingSourceKind.HexOverwrite =>
                    GeneralMappingSource.HexOverwrite(payload, mappingId),
                GeneralMappingSourceKind.HexFill =>
                    GeneralMappingSource.HexFill(payload, mappingId),
                GeneralMappingSourceKind.FileArtifact => throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "A file source is not an inline General Replace patch."),
                _ => throw new ArgumentOutOfRangeException(
                    nameof(kind),
                    kind,
                    "Unknown General Replace patch kind."),
            };
            rows.Add(new GeneralMappingDraftRow(
                mappingId,
                ExplicitMappingOperationKind.ReplaceRange,
                source,
                new ByteRange(0, targetRange.Length),
                CompositionAddressSpaceIds.OutputImage,
                targetRange,
                OverlapPolicy.Reject,
                alignment: 1,
                kind == GeneralMappingSourceKind.HexFill
                    ? "Fill hexadecimal General range."
                    : "Overwrite hexadecimal General range."));
        }

        return true;
    }

    private static bool TryParseGeneralMappingValue(
        string value,
        int index,
        TextWriter error,
        [NotNullWhen(true)] out GeneralMappingDraftRow? mapping)
    {
        mapping = null;
        if (!TryParseGeneralRangeValue(
                "--mapping",
                value,
                error,
                out string? path,
                out ByteRange targetRange))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            error.WriteLine("error: --mapping path must not be empty");
            return false;
        }

        string mappingId = string.Create(
            CultureInfo.InvariantCulture,
            $"general-map-{index}");
        mapping = new GeneralMappingDraftRow(
            mappingId,
            ExplicitMappingOperationKind.ReplaceRange,
            GeneralMappingSource.File(Path.GetFullPath(path)),
            new ByteRange(0, targetRange.Length),
            CompositionAddressSpaceIds.OutputImage,
            targetRange,
            OverlapPolicy.Reject,
            alignment: 1,
            "Replace explicit General range.",
            fileRangePreset: GeneralMappingFileRangePreset.FromFileStart);
        return true;
    }

    private static bool TryParseGeneralRangeValue(
        string optionName,
        string value,
        TextWriter error,
        [NotNullWhen(true)] out string? payload,
        out ByteRange targetRange)
    {
        payload = null;
        targetRange = default;
        int separatorIndex = value.IndexOf('=', StringComparison.Ordinal);
        if (separatorIndex <= 0)
        {
            error.WriteLine(
                $"error: {optionName} expects <target-start+length=value>; example: {optionName} 0x100+0x20=value");
            return false;
        }

        string rangeText = value[..separatorIndex].Trim();
        payload = value[(separatorIndex + 1)..].Trim();
        int plusIndex = rangeText.IndexOf('+', StringComparison.Ordinal);
        if (plusIndex <= 0 || plusIndex == rangeText.Length - 1)
        {
            error.WriteLine($"error: {optionName} range must use <target-start+length>");
            return false;
        }

        if (!AuthoringByteRangeCodec.TryParseStartAndLength(
                rangeText[..plusIndex],
                rangeText[(plusIndex + 1)..],
                out targetRange,
                out AuthoringRangeTextIssue? issue))
        {
            error.WriteLine($"error: {optionName} {issue!.Message}");
            return false;
        }

        return true;
    }
}
