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
        (string? v2ProfileId, string? requiredBaseSha256) = (
            context.PostbuildProfile!.IcId,
            context.PostbuildProfile!.ProcessorId,
            context.CommandPlan!.Branch,
            context.Selection.Mode,
            firmware?.CommonFwVersion,
            firmware?.ChipNumber,
            firmware?.ProjectId) switch
        {
            ("NT51919", "nfc.nt51919.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "2.0.0", 1, 0x4703) =>
                ("nt51919-ctrlram-replace-fw200-single", "d3c958d2aac1e29bd1f88b8ac62dc74c36810ab11e707770199d4b34f5ce3910"),
            ("NT51917", "nfc.nt51917.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "1.4.1", 1, 0x5709) =>
                ("nt51917-ctrlram-replace-fw141-single", "fc4d2f9701c626b1c7cddd2b448970611d332295c64f86415af2855f1569c55a"),
            ("NT51917", "nfc.nt51917.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.TwoChip, IcNumberInputMode.NumericSelector, "1.3.2", 2, 0x1615) =>
                ("nt51917-ctrlram-replace-fw132-twochip", "11700ec5580f2e07195c7aec3788f929609eef5355d773287d3f88aa1f984dae"),
            ("NT51917", "nfc.nt51917.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.ThreeChip, IcNumberInputMode.NumericSelector, "1.4.0", 3, 0x570A) =>
                ("nt51917-ctrlram-replace-fw140-threechip", "bc44561cc1cb338b9a49bbe701e5d7cbfe78ea40deda0926197fb22002b3061c"),
            ("NT51920", "nfc.nt51920.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "1.2.0", 1, 0xF401) =>
                ("nt51920-ctrlram-replace-fw120-single", "b9965def2946fd6e28165af5929ede885e1d0e3c0ab29266a737ac458225920d"),
            ("NT51920", "nfc.nt51920.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "1.2.0", 2, 0x1403) =>
                ("nt51920-ctrlram-replace-fw120-cascade2", "681f904ecdf5785ca26f94eabb8191ddaa8976e0e6f750145475568c6cde4d43"),
            ("NT51923", "nfc.nt51923.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "1.4.1", 1, 0x6005) =>
                ("nt51923-ctrlram-replace-fw141-single", "a65ae33c9c11091f69d8935422ffc57db32262eb922590364d4bdd9c3af9916f"),
            ("NT51923", "nfc.nt51923.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "1.4.1", 3, 0x4C03) =>
                ("nt51923-ctrlram-replace-fw141-cascade3", "06dda13a592c151a767d47fff60da993f33d7bda37666794dd9ea5cf92094d18"),
            ("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "1.4.1", 1, 0x5709) =>
                ("nt51927-ctrlram-replace-fw141-single", "fc4d2f9701c626b1c7cddd2b448970611d332295c64f86415af2855f1569c55a"),
            ("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.TwoChip, IcNumberInputMode.NumericSelector, "1.3.2", 2, 0x1615) =>
                ("nt51927-ctrlram-replace-fw132-twochip", "11700ec5580f2e07195c7aec3788f929609eef5355d773287d3f88aa1f984dae"),
            ("NT51927", "nfc.nt51927.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.ThreeChip, IcNumberInputMode.NumericSelector, "1.4.0", 3, 0x570A) =>
                ("nt51927-ctrlram-replace-fw140-threechip", "bc44561cc1cb338b9a49bbe701e5d7cbfe78ea40deda0926197fb22002b3061c"),
            ("NT51926", "nfc.nt51926.ctrlram-postbuild-fw1.4.1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, _, > 1, _) =>
                ("nt51926-ctrlram-replace-fw141-runtime-cascade", null),
            ("NT51926", Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "2.0.0", 1, 0x1309) =>
                ("nt51926-ctrlram-replace-fw200-runtime-single", null),
            ("NT51926", Nt51926Fw200ProcessorId, LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "2.0.0", 3, 0x1309) =>
                ("nt51926-ctrlram-replace-fw200-runtime-cascade", null),
            ("NT51929", "nfc.nt51929.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "2.0.0", 1, 0x4703) =>
                ("nt51929-ctrlram-replace-fw200-single", "d3c958d2aac1e29bd1f88b8ac62dc74c36810ab11e707770199d4b34f5ce3910"),
            ("NT51930", "nfc.nt51930.ctrlram-postbuild-fw1.x", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "1.3.0", 3, 0x110D) =>
                ("nt51930-ctrlram-replace-fw130-cascade3", null),
            ("NT51931", "nfc.nt51931.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "1.3.0", 6, 0x131B) =>
                ("nt51931-ctrlram-replace-fw130-cascade6", "2268ac5b49df546a03e177b97858805f0f83fa58b3e55a3b1590899ce9fd07c3"),
            ("NT51932", "nfc.nt51932.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.Cascade, IcNumberInputMode.CascadeSelector, "2.0.0", 3, 0x5601) =>
                ("nt51932-ctrlram-replace-fw200-cascade3", "3eb556e0a9323dd4fbe4c703be1eb33679df2b1ba839e79ddd7bbffa235008fd"),
            ("NT51950", "nfc.nt51950.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "2.0.0", 1, 0x4A06) =>
                ("nt51950-ctrlram-replace-fw200-single", "ccda75d0aa08540e293f9ab4a8058c43c4e39d2dd0238238848a2f13df68e38e"),
            ("NT51951", "nfc.nt51951.ctrlram-postbuild-v1", LegacyCombinerPostbuildBranch.SingleChip, IcNumberInputMode.SingleSelector, "2.0.0", 1, 0x5901) =>
                ("nt51951-ctrlram-replace-fw200-single", "c1cd54d93af431727220adc37fec2488765909dc09cb917d1ff69f6087bb6b69"),
            _ => (null, null),
        };
        if (v2ProfileId is not null &&
            (requiredBaseSha256 is null || StringComparer.Ordinal.Equals(
                Sha256File(context.BasePath!), requiredBaseSha256)))
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
