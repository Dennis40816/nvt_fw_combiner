using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles;
using V2CompositionPlanCompileResult = NvtFwCombiner.Profiles.V2.V2CompositionPlanCompileResult;

namespace NvtFwCombiner.Bootstrap;

public static partial class WorkbenchCompositionService
{
    internal static async ValueTask<WorkbenchRunResult> RunCtrlRamReplaceWithProcessorAsync(
        string icId,
        string number,
        IReadOnlyDictionary<string, string> slotPaths,
        bool build,
        string? outputPath,
        WorkbenchCtrlRamFirmwareVersionEdit? firmwareVersionEdit,
        IExternalProcessor? externalProcessor,
        CancellationToken cancellationToken)
    {
        CtrlRamReplaceRunContext context = CreateCtrlRamReplaceRunContext(
            icId,
            number,
            slotPaths,
            firmwareVersionEdit);
        WorkbenchRunResult Blocked(
            IReadOnlyList<CompositionIssue> issues,
            string? outputFileName = null)
        {
            return CreateReplaceReportRunResult(
                icId,
                WorkbenchReplaceModes.CtrlRam,
                slotPaths,
                build,
                CreateCtrlRamPlanningOperations(
                    icId,
                    context.Selection,
                    context.Sources,
                    slotPaths,
                    runnablePreview: false,
                    context.PostbuildProfile),
                issues,
                outputFileName ?? GetReplaceDefaultOutputFileName(icId, WorkbenchReplaceModes.CtrlRam));
        }

        if (context.ValidationIssues.Count != 0 ||
            context.BasePath is null ||
            context.PostbuildProfile is null ||
            context.CommandPlan is null)
        {
            return Blocked(context.ValidationIssues);
        }

        WorkbenchFirmwareContextSuggestion? firmware = firmwareVersionEdit is null
            ? TryReadFirmwareContextSuggestion(icId, context.BasePath!)
            : null;
        string? v2ProfileId = (
            context.PostbuildProfile!.IcId,
            context.PostbuildProfile!.ProcessorId,
            context.CommandPlan!.Branch,
            context.Selection.Mode,
            firmware?.CommonFwVersion,
            firmware?.ChipNumber,
            firmware?.ProjectId) switch
        {
            ("NT51920", "nfc.nt51920.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "1.2.0", 1, 0xF401)
                when StringComparer.Ordinal.Equals(Sha256File(context.BasePath!), Nt51920Fw120SingleBaseSha256) =>
                "nt51920-ctrlram-replace-fw120-single",
            ("NT51920", "nfc.nt51920.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "1.2.0", 2, 0x1403)
                when StringComparer.Ordinal.Equals(Sha256File(context.BasePath!), Nt51920Fw120Cascade2BaseSha256) =>
                "nt51920-ctrlram-replace-fw120-cascade2",
            ("NT51923", "nfc.nt51923.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "1.4.1", 1, 0x6005)
                when StringComparer.Ordinal.Equals(Sha256File(context.BasePath!), Nt51923Fw141SingleBaseSha256) =>
                "nt51923-ctrlram-replace-fw141-single",
            ("NT51923", "nfc.nt51923.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "1.4.1", 3, 0x4C03)
                when StringComparer.Ordinal.Equals(Sha256File(context.BasePath!), Nt51923Fw141Cascade3BaseSha256) =>
                "nt51923-ctrlram-replace-fw141-cascade3",
            ("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "1.4.1", 1, 0x5709)
                when StringComparer.Ordinal.Equals(Sha256File(context.BasePath!), Nt51927Fw141SingleBaseSha256) =>
                "nt51927-ctrlram-replace-fw141-single",
            ("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.TwoChip, IcNumberInputMode.NumericSelector, "1.3.2", 2, 0x1615)
                when StringComparer.Ordinal.Equals(Sha256File(context.BasePath!), Nt51927Fw132TwoChipBaseSha256) =>
                "nt51927-ctrlram-replace-fw132-twochip",
            ("NT51926", "nfc.nt51926.ctrlram-postbuild-fw1.4.1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, _, > 1, _) =>
                "nt51926-ctrlram-replace-fw141-runtime-cascade",
            ("NT51926", Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "2.0.0", 1, 0x1309) =>
                "nt51926-ctrlram-replace-fw200-runtime-single",
            ("NT51926", Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "2.0.0", 3, 0x1309) =>
                "nt51926-ctrlram-replace-fw200-runtime-cascade",
            ("NT51930", "nfc.nt51930.ctrlram-postbuild-fw1.x", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "1.3.0", 3, 0x110D) =>
                "nt51930-ctrlram-replace-fw130-cascade3",
            ("NT51931", "nfc.nt51931.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "1.3.0", 6, 0x131B)
                when StringComparer.Ordinal.Equals(Sha256File(context.BasePath!), Nt51931Fw130ExactBaseSha256) =>
                "nt51931-ctrlram-replace-fw130-cascade6",
            ("NT51932", "nfc.nt51932.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "2.0.0", 3, 0x5601)
                when StringComparer.Ordinal.Equals(Sha256File(context.BasePath!), Nt51932Fw200ExactBaseSha256) =>
                "nt51932-ctrlram-replace-fw200-cascade3",
            ("NT51951", "nfc.nt51951.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "2.0.0", 1, 0x5901)
                when StringComparer.Ordinal.Equals(Sha256File(context.BasePath!), Nt51951Fw200ExactBaseSha256) =>
                "nt51951-ctrlram-replace-fw200-single",
            _ => null,
        };
        if (v2ProfileId is not null)
        {
            V2CompositionPlanCompileResult v2Compile = CompileCtrlRamV2(
                context,
                v2ProfileId,
                new(firmware!.ChipNumber, firmware.NumberToken, TopologySelectionSource.Requested, "ic-number"));
            return !v2Compile.IsCompiled
                ? Blocked(v2Compile.Issues, $"{icId.ToLowerInvariant()}-ctrlram-replace.bin")
                : await RunCompiledCompositionAsync(
                    CtrlRamReplaceRunIdPrefix,
                    v2Compile.CompiledComposition!,
                    CreateCtrlRamReplaceBindings(
                        v2Compile.CompiledComposition!,
                        context,
                        slotPaths),
                    context.BasePath!,
                    build,
                    outputPath,
                    externalProcessor,
                    icNumberSelection: context.Selection,
                    overwrite: true,
                    cancellationToken).ConfigureAwait(false);
        }

        ExternalProcessorStagedSourceBinding[] stagedSourceBindings =
        [
            .. context.SelectedSources.SelectMany(source =>
            {
                string slotId = CtrlRamSlotId(source.SourceId);
                long sourceLength = context.SelectedSourceLengths[source.SourceId];
                return source.Blocks.Select(block =>
                {
                    long effectiveLength = Math.Min(
                        block.FirmwareRange.Length,
                        sourceLength - block.SourceOffset);
                    return new ExternalProcessorStagedSourceBinding(
                        slotId,
                        new ByteRange(block.SourceOffset, effectiveLength),
                        new ByteRange(block.FirmwareRange.Start, effectiveLength));
                });
            }),
        ];
        IReadOnlyList<LegacyCombinerPostbuildWriteRange> postbuildWriteRangeSections =
            LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForStagedSources(
            context.CommandPlan!,
            context.BaseLength,
            stagedSourceBindings.Select(binding => binding.FirmwareRange),
            context.Regions.Select(region => region.Range));
        if (postbuildWriteRangeSections.Count == 0)
        {
            return Blocked(
                [
                    new CompositionIssue(
                        WorkbenchIssueCodes.ReplaceCtrlRamPostbuildWriteRangeMissing,
                        "No approved postbuild write range could be derived from the legacy Combiner command plan.",
                        "postbuild"),
                ]);
        }

        CompositionProfileDefinition profile = CreateCtrlRamReplaceProfile(
            icId,
            context.Selection,
            context.BaseLength,
            context.Regions,
            context.SelectedSources,
            context.SelectedSourceLengths,
            stagedSourceBindings,
            context.PostbuildProfile!,
            context.CommandPlan!,
            postbuildWriteRangeSections,
            context.FirmwareVersionWritePlan);
        ProfileCompileResult compile = CompositionProfileCompiler.Compile(profile, []);
        if (!compile.IsSuccess)
        {
            return Blocked(compile.Issues, profile.DefaultOutputFileName);
        }

        InputArtifactBinding[] bindings = CreateCtrlRamReplaceBindings(
            compile.CompiledComposition!,
            context,
            slotPaths);

        return await RunCompiledCompositionAsync(
            CtrlRamReplaceRunIdPrefix,
            compile.CompiledComposition!,
            bindings,
            context.BasePath!,
            build,
            outputPath,
            externalProcessor,
            icNumberSelection: context.Selection,
            overwrite: true,
            cancellationToken).ConfigureAwait(false);
    }
}
