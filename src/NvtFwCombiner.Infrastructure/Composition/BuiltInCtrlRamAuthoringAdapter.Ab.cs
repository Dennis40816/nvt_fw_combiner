using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Infrastructure.Composition;

internal sealed partial class BuiltInCtrlRamAuthoringAdapter
{
    public IReadOnlyList<CompositionIssue> ValidateAbReference(CompiledComposition layout, ReadOnlyMemory<byte> reference)
    {
        try
        {
            var payload = new FirmwareArtifactPayload(CompositionAddressSpaceIds.ReferenceBase, reference.Span);
            string memberId = layout.V2Details.Provenance.Context.MemberId;
            BankReplaceRouteBinding? countSource = CanonicalDynamicRouteInventory.FindBankReplaceBinding(memberId, "1-ic");
            int count = countSource?.Definition.FinalizationKind == BankReferenceFinalizationKind.RunAbHeaderProcessor
                ? V2CompositionPlanCompiler.ReadPartialAbNativeCount(layout, payload,
                    countSource.Definition.Local, BuiltInV2BundleRegistry.All[countSource.Local.Route.BundleId]
                        .GetFirmwareFamily(countSource.Definition.Local.ProfileId,
                            countSource.Definition.Local.ProfileVersion))
                : V2CompositionPlanCompiler.ReadAbNativeCount(layout, payload);
            BankReplaceRouteBinding definitionRoute = CanonicalDynamicRouteInventory.FindBankReplaceBinding(
                memberId, count == 1 ? "1-ic" : countSource?.Definition.FinalizationKind ==
                    BankReferenceFinalizationKind.RunAbHeaderProcessor ? "2-ic" : "2-8-ic") ??
                throw new ArgumentException("AB Reference has no trusted bank shape for its native IC count.");
            BankReferenceReplaceDefinition definition = definitionRoute.Definition;
            BankReferenceDefinitionSource source = definition.Layout;
            V2CompiledCompositionDetails details = layout.V2Details;
            if (details.ProfileId != source.ProfileId || details.ProfileVersion != source.ProfileVersion ||
                details.Provenance.Bundle.ContentHash != source.Bundle.ContentHash ||
                details.Provenance.ProfileEntry.ContentHash != source.Entry.ContentHash ||
                details.Provenance.Context.FamilyContentHash != source.FamilyHash ||
                details.Provenance.ResolvedMap.ImageMap.MapId != source.MapId)
            {
                return [new("input.bank-reference.invalid", "AB detection requires the exact trusted layout definition.")];
            }
            BankReferenceDefinitionSource local = definition.Local;
            IReadOnlyList<FirmwareImageMap> maps = BuiltInV2BundleRegistry.All[definitionRoute.Local.Route.BundleId]
                .GetMapVariants(local.ProfileId, local.ProfileVersion, local.MemberId, ExperienceIds.CtrlRamReplace,
                    out IReadOnlyList<CompositionIssue> issues);
            if (issues.Count != 0)
            {
                return issues;
            }
            FirmwareImageMap map = maps.Single(candidate => candidate.MapId == local.MapId);
            V2CompositionPlanCompiler.ValidateAbReference(layout, payload, definition, map);
            return [];
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            return [new("input.bank-reference.invalid", exception.Message, CompositionSlotIds.ReplaceBase)];
        }
    }

    public CapabilityRouteResolutionResult ResolveAbReferenceRoute(string icId, string number)
    {
        BankReplaceRouteBinding? single = string.IsNullOrWhiteSpace(icId) ? null :
            CanonicalDynamicRouteInventory.FindBankReplaceBinding(IcIdentifier.Normalize(icId), "1-ic");
        string? variant = number switch
        {
            IcNumberSelectionTokens.SingleChip => "1-ic",
            IcNumberSelectionTokens.Cascade
                when single?.Definition.FinalizationKind == BankReferenceFinalizationKind.RunAbHeaderProcessor => "2-ic",
            IcNumberSelectionTokens.CascadeTwoToEight
                when single?.Definition.FinalizationKind != BankReferenceFinalizationKind.RunAbHeaderProcessor => "2-8-ic",
            _ => null,
        };
        BankReplaceRouteBinding? binding = variant is null || string.IsNullOrWhiteSpace(icId)
            ? null
            : CanonicalDynamicRouteInventory.FindBankReplaceBinding(IcIdentifier.Normalize(icId), variant);
        return binding is not null
            ? _catalog.ResolveDynamicRoute(binding.Identity.RouteId)
            : new(null, new CapabilityCatalogIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
                "AB CtrlRAM Replace has no trusted Single or Cascade candidate for this selection."));
    }

    private CtrlRamAuthoringCompilation ResolveAb(string icId, string number,
        IReadOnlyDictionary<string, string> slotPaths, AbCtrlRamDraftState draft,
        IReadOnlyDictionary<string, byte[]>? captured)
    {
        var expected = new Dictionary<string, string>(StringComparer.Ordinal);
        CapabilityRouteResolutionResult available = ResolveAbReferenceRoute(icId, number);
        if (!available.Succeeded)
        {
            return Failed(CompositionPlanningIssueCodes.ReplaceWorkflowNotSupported, available.Issue!.Message);
        }

        // Capture once at this host boundary. Every local adapter call receives this dictionary.
        Dictionary<string, byte[]> inputs = captured is null
            ? slotPaths.Select(pair => (pair.Key, Bytes: BuiltInFirmwareInspection.TryReadFirmwareImage(pair.Value)))
                .Where(static pair => pair.Bytes is not null)
                .ToDictionary(static pair => pair.Key, static pair => pair.Bytes!, StringComparer.Ordinal)
            : captured.ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        if (!inputs.TryGetValue(CompositionSlotIds.ReplaceBase, out byte[]? bytes) ||
            !slotPaths.TryGetValue(CompositionSlotIds.ReplaceBase, out string? path))
        {
            return Failed(CompositionPlanningIssueCodes.InputMissing, "AB Reference BIN is required.");
        }
        expected.Add(CompositionAddressSpaceIds.ReferenceBase, Path.GetFullPath(path));
        try
        {
            BankReplaceRouteBinding definitionRoute = CanonicalDynamicRouteInventory.FindBankReplaceBinding(
                available.Route!.Identity.RouteId) ??
                throw new ArgumentException("AB Replace route lost its trusted parent binding.");
            BankReferenceReplaceDefinition definition = definitionRoute.Definition;
            BankReferenceDefinitionSource source = definition.Layout;
            BuiltInV2Bundle layoutBundle = BuiltInV2BundleRegistry.All.Values.Single(bundle =>
                bundle.ContentHash == definition.Layout.Bundle.ContentHash);
            BuiltInV2Bundle localBundle = BuiltInV2BundleRegistry.All[definitionRoute.Local.Route.BundleId];
            FirmwareImageMap layoutMap = definitionRoute.Layout.GetMapVariants(out _, out _).Single();
            TopologySelection? layoutTopology = layoutMap.Applicability.TopologyRequirement.Kind ==
                    TopologyRequirementKind.SingleChip
                ? new TopologySelection(1, "single", TopologySelectionSource.Requested, "number-selector")
                : layoutMap.Applicability.TopologyRequirement.Kind == TopologyRequirementKind.Cascade
                    ? new TopologySelection(2, "cascade_2to8", TopologySelectionSource.Requested, "number-selector")
                    : null;
            V2CompositionPlanCompileResult layoutResult = layoutBundle.Compile(source.ProfileId, source.ProfileVersion,
                source.MemberId, ExperienceIds.AbMerge, bytes.LongLength, layoutTopology,
                [new FirmwareArtifactPayload("dp-ab-input", bytes)], selectedInputSlotIds: ["dp-ab-input"]);
            if (!layoutResult.IsCompiled)
            {
                return new(null, expected, layoutResult.Issues);
            }
            CompiledComposition layout = layoutResult.CompiledComposition!;
            var requests = new List<V2RuntimeReferenceBankReplaceRequest>();
            var plans = new Dictionary<string, LegacyCombinerPostbuildCommandPlan>(StringComparer.Ordinal);
            var advisories = new List<CompositionIssue>();
            FirmwareImageMap resolvedMap = layout.V2Details.Provenance.ResolvedMap.ImageMap;
            (string Id, long Start, long Capacity)[] bankViews = definition.FinalizationKind ==
                BankReferenceFinalizationKind.RunAbHeaderProcessor
                ? [.. resolvedMap.Regions.Where(static region => region.RegionId is "a-bank" or "b-bank")
                    .Select(static region => (region.RegionId, region.Range.Start, region.Range.Length))]
                : [.. resolvedMap.RegionSets.SelectMany(static set => set.RegionInstances)
                    .Where(static instance => instance.InstanceId is "a-bank" or "b-bank")
                    .Select(static instance => (instance.InstanceId, instance.BaseOffset, instance.Template.Capacity))];
            foreach ((string bankId, long bankStart, long bankCapacity) in bankViews.Where(bank =>
                         (bank.Id == "a-bank" && draft.Banks != AbCtrlRamBankSelection.B) ||
                         (bank.Id == "b-bank" && draft.Banks != AbCtrlRamBankSelection.A)))
            {
                var range = new ByteRange(checked(bankStart + definition.LocalBankRange.Start),
                    definition.LocalBankRange.Length);
                if (range.EndExclusive > bytes.LongLength ||
                    definition.LocalBankRange.EndExclusive > bankCapacity)
                {
                    return Failed(CompositionIssueCodes.InputAddressSpaceLengthMismatch, $"{bankId}: invalid canonical bank range.");
                }
                byte[] slice = bytes.AsSpan(checked((int)range.Start), checked((int)range.Length)).ToArray();
                inputs[CompositionSlotIds.ReplaceBase] = slice;
                CtrlRamReplaceRunContext local = CreateCtrlRamReplaceRunContext(_projection, icId, number, slotPaths,
                    bankId == "a-bank" ? draft.AVersion : draft.BVersion, inputs);
                if (local.ValidationIssues.Count != 0)
                {
                    return new(null, expected, local.ValidationIssues.Select(issue =>
                        new CompositionIssue(issue.Code, $"{bankId}: {issue.Message}", issue.OperationId, issue.Severity)));
                }
                foreach ((string key, string value) in CreateExpectedPaths(local, slotPaths))
                {
                    expected[key] = value;
                }
                LegacyCombinerPostbuildCommandPlan plan = local.CommandPlan!;
                requests.Add(new(bankId, CreateCompileRequest(local,
                    new TopologySelection(plan.TopologyCount, plan.Selector.Token, TopologySelectionSource.Requested, "ic-number"),
                    new FirmwareArtifactPayload(CompositionAddressSpaceIds.ReferenceBase, slice))));
                plans.Add(bankId, plan);
                advisories.AddRange(local.AdvisoryIssues);
            }
            var referencePayload = new FirmwareArtifactPayload(CompositionAddressSpaceIds.ReferenceBase, bytes);
            int nativeCount = definition.FinalizationKind == BankReferenceFinalizationKind.RunAbHeaderProcessor
                ? V2CompositionPlanCompiler.ReadPartialAbNativeCount(layout, referencePayload,
                    definition.Local, localBundle.GetFirmwareFamily(definition.Local.ProfileId,
                        definition.Local.ProfileVersion))
                : V2CompositionPlanCompiler.ReadAbNativeCount(layout, referencePayload);
            if (plans.Values.Any(plan => plan.TopologyCount != nativeCount))
            {
                return Failed("input.bank-reference.count",
                    $"AB native IC count Read {nativeCount} does not match the selected CtrlRAM topology.");
            }
            CompiledComposition compiled = localBundle.CompileBankReplace(layout,
                referencePayload, definition, nativeCount, requests);
            ResolvedCapability capability = available.Route!.BindCompilation(compiled,
                runtimeReferenceProof: RuntimeReferenceCompilationProof.CreateBankReplace(compiled, plans))
                .BindCtrlRamExecutionPlan(new AcceptedCtrlRamExecutionPlan(IcNumberSelection.FromToken(number), draft, advisories));
            return new(capability, expected, []);
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidDataException or InvalidOperationException or OverflowException)
        {
            return Failed("input.bank-reference.invalid", exception.Message);
        }

        CtrlRamAuthoringCompilation Failed(string code, string message)
        {
            return new(null, expected, [new CompositionIssue(code, message, CompositionSlotIds.ReplaceBase)]);
        }
    }
}
