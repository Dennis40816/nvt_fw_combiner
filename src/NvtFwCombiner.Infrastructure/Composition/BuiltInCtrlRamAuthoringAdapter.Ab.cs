using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Profiles.V2;

namespace NvtFwCombiner.Infrastructure.Composition;

internal sealed partial class BuiltInCtrlRamAuthoringAdapter
{
    public CapabilityRouteResolutionResult ResolveAbReferenceRoute(string icId, string number)
    {
        return !string.IsNullOrWhiteSpace(icId) &&
            IcIdentifier.Normalize(icId) == CanonicalDynamicRouteInventory.BankReplaceIdentity.IcId &&
            number == IcNumberSelectionTokens.SingleChip
                ? _catalog.ResolveDynamicRoute(CanonicalDynamicRouteInventory.BankReplaceIdentity.RouteId)
                : new(null, new CapabilityCatalogIssue(CapabilityCatalogIssueCodes.RouteUnavailable,
                    "AB CtrlRAM Replace is available only for the NT51929 fw200 Single candidate."));
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
            BankReferenceReplaceDefinition definition = CanonicalDynamicRouteInventory.CreateBankReplaceDefinition();
            BankReferenceDefinitionSource source = definition.Layout;
            if (bytes.LongLength != source.CapacityBytes)
            {
                return Failed(CompositionIssueCodes.InputAddressSpaceLengthMismatch,
                    $"AB Reference length is 0x{bytes.LongLength:X}; expected 0x{source.CapacityBytes:X}.");
            }
            BuiltInV2Bundle layoutBundle = BuiltInV2BundleRegistry.All["nt51919-nt51929-nt51932-ab-merge"];
            BuiltInV2Bundle localBundle = BuiltInV2BundleRegistry.All["nt51929-ctrlram-replace-candidate"];
            V2CompositionPlanCompileResult layoutResult = layoutBundle.Compile(source.ProfileId, source.ProfileVersion,
                source.MemberId, ExperienceIds.AbMerge, source.CapacityBytes, null, [], selectedInputSlotIds: ["dp-ab-input"]);
            if (!layoutResult.IsCompiled)
            {
                return new(null, expected, layoutResult.Issues);
            }
            CompiledComposition layout = layoutResult.CompiledComposition!;
            var requests = new List<V2RuntimeReferenceBankReplaceRequest>();
            var plans = new Dictionary<string, LegacyCombinerPostbuildCommandPlan>(StringComparer.Ordinal);
            var advisories = new List<CompositionIssue>();
            foreach (FirmwareRegionInstance bank in layout.V2Details.Provenance.ResolvedMap.ImageMap.RegionSets
                         .SelectMany(static set => set.RegionInstances).Where(instance =>
                             (instance.InstanceId == "a-bank" && draft.Banks != AbCtrlRamBankSelection.B) ||
                             (instance.InstanceId == "b-bank" && draft.Banks != AbCtrlRamBankSelection.A)))
            {
                var range = new ByteRange(bank.BaseOffset, bank.Template.Capacity);
                if (range.EndExclusive > bytes.LongLength || range.Length != definition.Local.CapacityBytes)
                {
                    return Failed(CompositionIssueCodes.InputAddressSpaceLengthMismatch, $"{bank.InstanceId}: invalid canonical bank range.");
                }
                byte[] slice = bytes.AsSpan(checked((int)range.Start), checked((int)range.Length)).ToArray();
                inputs[CompositionSlotIds.ReplaceBase] = slice;
                CtrlRamReplaceRunContext local = CreateCtrlRamReplaceRunContext(_projection, icId, number, slotPaths,
                    bank.InstanceId == "a-bank" ? draft.AVersion : draft.BVersion, inputs);
                if (local.ValidationIssues.Count != 0)
                {
                    return new(null, expected, local.ValidationIssues.Select(issue =>
                        new CompositionIssue(issue.Code, $"{bank.InstanceId}: {issue.Message}", issue.OperationId, issue.Severity)));
                }
                foreach ((string key, string value) in CreateExpectedPaths(local, slotPaths))
                {
                    expected[key] = value;
                }
                LegacyCombinerPostbuildCommandPlan plan = local.CommandPlan!;
                requests.Add(new(bank.InstanceId, CreateCompileRequest(local,
                    new TopologySelection(plan.TopologyCount, plan.Selector.Token, TopologySelectionSource.Requested, "ic-number"),
                    new FirmwareArtifactPayload(CompositionAddressSpaceIds.ReferenceBase, slice))));
                plans.Add(bank.InstanceId, plan);
                advisories.AddRange(local.AdvisoryIssues);
            }
            CompiledComposition compiled = localBundle.CompileBankReplace(layout,
                new FirmwareArtifactPayload(CompositionAddressSpaceIds.ReferenceBase, bytes), requests);
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
