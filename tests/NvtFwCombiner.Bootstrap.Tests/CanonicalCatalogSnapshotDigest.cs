using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>One named part of the canonical catalog text with its own digest.</summary>
internal sealed record CanonicalCatalogDigestSection(string Name, int Length, string Sha256);

/// <summary>
/// Deterministic canonical text of one published catalog snapshot. Every
/// published sequence keeps the order the snapshot exposes; only key sets
/// without a published order (IC and workflow lookups) are sorted ordinally.
/// The text uses invariant formatting and contains no machine path, time,
/// publication generation or object identity.
/// </summary>
internal sealed class CanonicalCatalogSnapshotDigest
{
    private static readonly string[] KnownWorkflowIds =
    [
        ExperienceIds.StandardMerge,
        ExperienceIds.AbMerge,
        ExperienceIds.GeneralMerge,
        ExperienceIds.DpReplace,
        ExperienceIds.CtrlRamReplace,
        ExperienceIds.GeneralReplace,
    ];

    private CanonicalCatalogSnapshotDigest(
        string text,
        IReadOnlyList<CanonicalCatalogDigestSection> sections)
    {
        Text = text;
        Sections = sections;
        Sha256 = Hash(text);
    }

    /// <summary>Complete canonical text of the snapshot.</summary>
    internal string Text { get; }

    /// <summary>Lowercase SHA-256 of the UTF-8 canonical text.</summary>
    internal string Sha256 { get; }

    /// <summary>Per-section lengths and digests in text order.</summary>
    internal IReadOnlyList<CanonicalCatalogDigestSection> Sections { get; }

    internal static CanonicalCatalogSnapshotDigest Create(
        CanonicalCapabilityCatalogSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        string[] workflowIds = CreateWorkflowUniverse(snapshot);
        string[] icIds = CreateIcUniverse(snapshot, workflowIds);
        (string Name, Action<CanonicalTextWriter> Write)[] sections =
        [
            ("catalog", writer => WriteCatalog(writer, snapshot)),
            ("static-routes", writer => WriteStaticRoutes(writer, snapshot)),
            ("dynamic-routes", writer => WriteDynamicRoutes(writer, snapshot)),
            ("full-image-plans", writer => WriteFullImagePlans(writer, snapshot)),
            ("disclosure", writer => WriteDisclosure(writer, snapshot, workflowIds, icIds)),
            ("selector", writer => WriteSelector(writer, snapshot, workflowIds, icIds)),
            ("certification", writer => WriteCertification(writer, snapshot)),
        ];
        var text = new StringBuilder();
        List<CanonicalCatalogDigestSection> digests = [];
        foreach ((string name, Action<CanonicalTextWriter> write) in sections)
        {
            var writer = new CanonicalTextWriter();
            writer.Line("section", name);
            write(writer);
            string sectionText = writer.ToString();
            _ = text.Append(sectionText);
            digests.Add(new CanonicalCatalogDigestSection(
                name,
                sectionText.Length,
                Hash(sectionText)));
        }

        return new CanonicalCatalogSnapshotDigest(text.ToString(), digests.AsReadOnly());
    }

    private static string Hash(string text)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    }

    private static void WriteCatalog(
        CanonicalTextWriter writer,
        CanonicalCapabilityCatalogSnapshot snapshot)
    {
        writer.Line("catalog-id", snapshot.CatalogId);
        writer.Line("catalog-version", snapshot.CatalogVersion);
        writer.Line("source-sha256", snapshot.SourceSha256);
        writer.Line("resolution-token", DescribeSnapshotToken(snapshot));
        writer.Line("static-route.count", snapshot.Capabilities.Count);
        writer.Line("dynamic-route.count", snapshot.DynamicRoutes.Count);
        writer.Line("full-image-plan.count", snapshot.FullImageMetadataPlans.Count);
        writer.Line("certification-issue.count", snapshot.CertificationIssues.Count);
    }

    private static string DescribeSnapshotToken(CanonicalCapabilityCatalogSnapshot snapshot)
    {
        string token = snapshot.ResolutionToken.Value;
        string prefix = $"{snapshot.CatalogId}:{snapshot.CatalogVersion}:";
        string suffix = $":{snapshot.SourceSha256[..12]}";
        return token.Length > prefix.Length + suffix.Length &&
            token.StartsWith(prefix, StringComparison.Ordinal) &&
            token.EndsWith(suffix, StringComparison.Ordinal) &&
            long.TryParse(
                token[prefix.Length..^suffix.Length],
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long generation) &&
            generation > 0
                ? "catalog-id:catalog-version:<generation>:source-sha256[..12]"
                : token;
    }

    private static string DescribeToken(
        CanonicalCapabilityCatalogSnapshot snapshot,
        ResolutionToken token)
    {
        return token == snapshot.ResolutionToken ? "snapshot" : token.Value;
    }

    private static void WriteStaticRoutes(
        CanonicalTextWriter writer,
        CanonicalCapabilityCatalogSnapshot snapshot)
    {
        foreach (ResolvedCapability capability in snapshot.Capabilities)
        {
            writer.Line("route", capability.Identity.RouteId);
            writer.Line("kind", "static");
            WriteIdentity(writer, capability.Identity);
            writer.Line("capability-fingerprint", capability.CapabilityFingerprint);
            WriteDecision(writer, "authoring", capability.Authoring);
            WriteDecision(writer, "publication", capability.Publication);
            WriteDecision(writer, "evidence", capability.Evidence);
            writer.Line("execution-admitted", capability.ExecutionAdmitted);
            writer.Line("resolution-token", DescribeToken(snapshot, capability.ResolutionToken));
            WriteComposition(writer, capability.CompiledComposition);
            WriteContract(writer, "compilation-contract", capability.CompilationContract);
            WriteResolvedPlan(writer, "metadata-plan", snapshot, capability.MetadataPlan);
            writer.Line("runtime-reference-proof", capability.RuntimeReferenceProof is not null);
            WriteMemoryLayout(writer, "memory-layout", capability.MemoryLayoutContext);
            writer.Line("general-execution-plan", capability.GeneralExecutionPlan is not null);
            writer.Line("ctrlram-execution-plan", capability.CtrlRamExecutionPlan is not null);
        }
    }

    private static void WriteDynamicRoutes(
        CanonicalTextWriter writer,
        CanonicalCapabilityCatalogSnapshot snapshot)
    {
        foreach (ResolvedCapabilityRoute route in snapshot.DynamicRoutes)
        {
            writer.Line("route", route.Identity.RouteId);
            writer.Line("kind", "dynamic");
            WriteIdentity(writer, route.Identity);
            writer.Line("capability-fingerprint", route.CapabilityFingerprint);
            WriteDecision(writer, "authoring", route.Authoring);
            WriteDecision(writer, "publication", route.Publication);
            WriteDecision(writer, "evidence", route.Evidence);
            writer.Line("resolution-token", DescribeToken(snapshot, route.ResolutionToken));
            WriteContract(writer, "compilation-contract", route.CompilationContract);
            WriteNumberChoice(writer, "number-choice", route.NumberChoice);
            WriteTopologyChoice(writer, "ab-merge-topology-choice", route.AbMergeTopologyChoice);
            WriteMemoryLayout(writer, "memory-layout", route.MemoryLayoutContext);
        }
    }

    private static void WriteFullImagePlans(
        CanonicalTextWriter writer,
        CanonicalCapabilityCatalogSnapshot snapshot)
    {
        for (int index = 0; index < snapshot.FullImageMetadataPlans.Count; index++)
        {
            WriteResolvedPlan(
                writer,
                CanonicalTextWriter.Indexed("full-image-plan", index),
                snapshot,
                snapshot.FullImageMetadataPlans[index]);
        }
    }

    private static void WriteDisclosure(
        CanonicalTextWriter writer,
        CanonicalCapabilityCatalogSnapshot snapshot,
        string[] workflowIds,
        string[] icIds)
    {
        CanonicalCapabilityDisclosure disclosure = snapshot.Disclosure;
        foreach (string workflowId in workflowIds)
        {
            string prefix = $"profile-summaries[{workflowId}]";
            IReadOnlyList<CapabilityProfileSummary> summaries =
                disclosure.GetProfileSummaries(workflowId);
            writer.Line(prefix + ".count", summaries.Count);
            for (int index = 0; index < summaries.Count; index++)
            {
                CapabilityProfileSummary summary = summaries[index];
                string item = CanonicalTextWriter.Indexed(prefix, index);
                writer.Line(item + ".profile-id", summary.ProfileId);
                writer.Line(item + ".ic-id", summary.IcId);
                writer.Line(item + ".composition-kind", summary.CompositionKind);
                writer.Lines(item + ".required-input-spaces", summary.RequiredInputAddressSpaceIds);
                writer.Line(item + ".default-output-file-name", summary.DefaultOutputFileName);
                writer.Line(item + ".ic-number-input-mode", summary.IcNumberInputMode?.ToString());
                writer.Line(item + ".compile-succeeded", summary.CompileSucceeded);
                writer.Lines(item + ".issue-codes", summary.IssueCodes);
                writer.Line(item + ".declaration-ready", summary.DeclarationReady);
            }
        }

        foreach (string icId in icIds)
        {
            string prefix = $"ic[{icId}]";
            WriteNumberChoices(writer, prefix + ".number-choices", disclosure.GetNumberChoices(icId));
            CapabilityFamilySummary family = disclosure.GetFamilySummary(icId);
            writer.Line(prefix + ".family-id", family.FamilyId);
            writer.Line(prefix + ".family-relationship", family.Relationship);
            writer.Line(prefix + ".family-scope", family.Scope);
            writer.Line(prefix + ".dp-perspective", disclosure.IsDpPerspectiveIc(icId));
        }
    }

    private static void WriteSelector(
        CanonicalTextWriter writer,
        CanonicalCapabilityCatalogSnapshot snapshot,
        string[] workflowIds,
        string[] icIds)
    {
        CapabilitySelectorPublication selector = snapshot.SelectorPublication;
        writer.Line("resolution-token", DescribeToken(snapshot, selector.ResolutionToken));
        writer.Line("default-ic", selector.DefaultIcId);
        writer.Lines("ic-ids", selector.IcIds);
        writer.Lines("ab-merge-ic-ids", selector.AbMergeIcIds);
        foreach (string icId in icIds)
        {
            string prefix = $"ic[{icId}]";
            writer.Lines(prefix + ".authorable-workflows", selector.GetAuthorableWorkflowIds(icId));
            WriteNumberChoices(writer, prefix + ".number-choices", selector.GetNumberSelectionChoices(icId));
            foreach (string workflowId in workflowIds)
            {
                IReadOnlyList<CapabilityNumberChoice> workflowChoices =
                    selector.GetNumberSelectionChoices(icId, workflowId);
                if (workflowChoices.Count != 0)
                {
                    WriteNumberChoices(
                        writer,
                        $"{prefix}.workflow[{workflowId}].number-choices",
                        workflowChoices);
                }
            }

            IReadOnlyList<CapabilityTopologyChoice> topologyChoices =
                selector.GetAbMergeTopologyChoices(icId);
            writer.Line(prefix + ".ab-merge-topology-choices.count", topologyChoices.Count);
            for (int index = 0; index < topologyChoices.Count; index++)
            {
                WriteTopologyChoice(
                    writer,
                    CanonicalTextWriter.Indexed(prefix + ".ab-merge-topology-choices", index),
                    topologyChoices[index]);
            }
        }
    }

    private static void WriteCertification(
        CanonicalTextWriter writer,
        CanonicalCapabilityCatalogSnapshot snapshot)
    {
        for (int index = 0; index < snapshot.CertificationIssues.Count; index++)
        {
            CapabilityCatalogIssue issue = snapshot.CertificationIssues[index];
            string prefix = CanonicalTextWriter.Indexed("issue", index);
            writer.Line(prefix + ".code", issue.Code);
            writer.Line(prefix + ".message", issue.Message);
            writer.Line(prefix + ".subject", issue.Subject);
        }
    }

    private static void WriteIdentity(CanonicalTextWriter writer, CapabilityRouteIdentity identity)
    {
        writer.Line("identity.ic-id", identity.IcId);
        writer.Line("identity.workflow-id", identity.WorkflowId);
        writer.Line("identity.ic-count-variant", identity.IcCountVariant);
        writer.Line("identity.map-variant", identity.MapVariant);
    }

    private static void WriteDecision<TValue>(
        CanonicalTextWriter writer,
        string prefix,
        PinnedCapabilityDecision<TValue> decision)
        where TValue : struct, Enum
    {
        writer.Line(prefix + ".decision-id", decision.DecisionId);
        writer.Line(prefix + ".route-id", decision.RouteId);
        writer.Line(prefix + ".capability-fingerprint", decision.CapabilityFingerprint);
        writer.Line(prefix + ".value", decision.Value);
        writer.Line(prefix + ".source-reference", decision.SourceReference);
    }

    private static void WriteComposition(CanonicalTextWriter writer, CompiledComposition composition)
    {
        const string prefix = "composition";
        writer.Line(prefix + ".compilation-fingerprint", composition.CompilationFingerprint);
        writer.Line(prefix + ".capability-fingerprint", composition.CapabilityFingerprint);
        writer.Line(prefix + ".eligibility", composition.Eligibility);
        V2CompiledCompositionDetails details = composition.V2Details;
        writer.Line(prefix + ".profile-id", details.ProfileId);
        writer.Line(prefix + ".profile-version", details.ProfileVersion);
        writer.Line(prefix + ".experience-id", details.ExperienceId);
        writer.Line(prefix + ".composition-kind", details.CompositionKind);
        writer.Line(prefix + ".ic-number-input-mode", details.IcNumberInputMode?.ToString());
        writer.Line(prefix + ".output-file-name-template", details.OutputNamingRequirement.FileNameTemplate);
        writer.Line(prefix + ".additional-delivery.count", details.AdditionalDeliveries.Count);
        writer.Line(prefix + ".ab-definition", details.Ab is not null);
        V2CompilationProvenance provenance = details.Provenance;
        writer.Line(prefix + ".bundle.id", provenance.Bundle.BundleId);
        writer.Line(prefix + ".bundle.version", provenance.Bundle.BundleVersion);
        writer.Line(prefix + ".bundle.content-hash", provenance.Bundle.ContentHash);
        writer.Line(prefix + ".bundle.trust-anchor-binding-id", provenance.Bundle.TrustAnchorBindingId);
        writer.Line(prefix + ".profile-entry.id", provenance.ProfileEntry.EntryId);
        writer.Line(prefix + ".profile-entry.content-hash", provenance.ProfileEntry.ContentHash);
        writer.Line(prefix + ".context.kind", provenance.Context.GetType().Name);
        writer.Line(prefix + ".context.family-id", provenance.Context.FamilyId);
        writer.Line(prefix + ".context.family-version", provenance.Context.FamilyVersion);
        writer.Line(prefix + ".context.family-content-hash", provenance.Context.FamilyContentHash);
        writer.Line(prefix + ".context.member-id", provenance.Context.MemberId);
        writer.Line(prefix + ".context.mode-id", provenance.Context.ModeId);
        if (provenance.Context is MapBoundV2CompilationContext mapBound)
        {
            writer.Line(prefix + ".resolved-map", "present");
            writer.Line(prefix + ".resolved-map.fingerprint", mapBound.ResolvedMap.ResolutionFingerprint);
            WriteImageMap(writer, prefix + ".resolved-map.image-map", mapBound.ResolvedMap.ImageMap);
        }
        else
        {
            writer.Line(prefix + ".resolved-map", "absent");
        }

        writer.Line(prefix + ".promotion.stage", provenance.Promotion.Stage);
        writer.Lines(
            prefix + ".promotion.blockers",
            [.. provenance.Promotion.Blockers.Select(static blocker => blocker.BlockerId)]);
        writer.Lines(prefix + ".profile-evidence", provenance.ProfileEvidenceRefs);
        writer.Line(prefix + ".validation-requirement.count", provenance.ValidationRequirements.Count);
        CompositionPlan plan = composition.Plan;
        writer.Line(prefix + ".plan.output-space", plan.OutputSpaceId);
        writer.Line(prefix + ".plan.output-capacity", plan.OutputInitialization.Capacity);
        writer.Lines(prefix + ".plan.required-input-spaces", plan.RequiredInputAddressSpaceIds);
        writer.Line(prefix + ".plan.operation.count", plan.OrderedOperations.Count);
        writer.Lines(
            prefix + ".plan.address-spaces",
            [
                .. plan.AddressSpaces.Select(static space => FormattableString.Invariant(
                    $"{space.AddressSpaceId}:{space.Length}:{space.Mutability}")),
            ]);
    }

    private static void WriteContract(
        CanonicalTextWriter writer,
        string prefix,
        CanonicalCapabilityCompilationContract contract)
    {
        writer.Line(prefix + ".profile-id", contract.ProfileId);
        writer.Line(prefix + ".profile-version", contract.ProfileVersion);
        writer.Line(prefix + ".trusted-definition-sha256", contract.TrustedDefinitionSha256);
        writer.Lines(prefix + ".allowed-map-variants", contract.AllowedMapVariantIds);
        writer.Line(prefix + ".compiler-semantic-id", contract.CompilerSemanticId);
        writer.Lines(prefix + ".semantic-bindings", contract.SemanticBindingIds);
        writer.Line(prefix + ".allows-logical-output", contract.AllowsLogicalOutput);
    }

    private static void WriteNumberChoice(
        CanonicalTextWriter writer,
        string prefix,
        CapabilityNumberChoice? choice)
    {
        writer.Line(prefix + ".token", choice?.Token);
        writer.Line(prefix + ".display-label", choice?.DisplayLabel);
    }

    private static void WriteNumberChoices(
        CanonicalTextWriter writer,
        string prefix,
        IReadOnlyList<CapabilityNumberChoice> choices)
    {
        writer.Line(prefix + ".count", choices.Count);
        for (int index = 0; index < choices.Count; index++)
        {
            WriteNumberChoice(writer, CanonicalTextWriter.Indexed(prefix, index), choices[index]);
        }
    }

    private static void WriteTopologyChoice(
        CanonicalTextWriter writer,
        string prefix,
        CapabilityTopologyChoice? choice)
    {
        writer.Line(prefix + ".token", choice?.Token);
        if (choice is not null)
        {
            writer.Line(prefix + ".display-label", choice.DisplayLabel);
            writer.Line(prefix + ".selection.chip-count", choice.Selection.ChipCount);
            writer.Line(prefix + ".selection.label", choice.Selection.Label);
            writer.Line(prefix + ".selection.source", choice.Selection.Source);
            writer.Line(prefix + ".selection.source-id", choice.Selection.SourceId);
        }
    }

    private static void WriteMemoryLayout(
        CanonicalTextWriter writer,
        string prefix,
        MemoryLayoutContextMap? context)
    {
        writer.Line(prefix, context is null ? "absent" : "present");
        if (context is not null)
        {
            writer.Line(prefix + ".ic-id", context.IcId);
            writer.Line(prefix + ".profile-id", context.ProfileId);
            writer.Line(prefix + ".profile-version", context.ProfileVersion);
            writer.Line(prefix + ".trusted-definition-sha256", context.TrustedDefinitionSha256);
            writer.Lines(prefix + ".semantic-bindings", context.SemanticBindingIds);
            WriteImageMap(writer, prefix + ".map", context.Map);
        }
    }

    private static void WriteResolvedPlan(
        CanonicalTextWriter writer,
        string prefix,
        CanonicalCapabilityCatalogSnapshot snapshot,
        ResolvedMetadataPlan plan)
    {
        writer.Line(prefix + ".resolution-token", DescribeToken(snapshot, plan.ResolutionToken));
        writer.Lines(
            prefix + ".entry-states",
            [.. plan.Entries.Select(static entry => $"{entry.Definition.BindingId}:{entry.State}")]);
        MetadataPlanDefinition definition = plan.Definition;
        WriteSourceIdentity(writer, prefix + ".source", definition.SourceIdentity);
        WriteFullImageContext(writer, prefix + ".full-image-context", definition.FullImageContext);
        writer.Lines(
            prefix + ".report-projections",
            [.. definition.ReportProjections.Select(static projection => $"{projection.SpaceId}:{projection.SlotId}")]);
        writer.Line(prefix + ".entry.count", definition.Entries.Count);
        for (int index = 0; index < definition.Entries.Count; index++)
        {
            WritePlanEntry(writer, CanonicalTextWriter.Indexed(prefix + ".entry", index), definition.Entries[index]);
        }
    }

    private static void WriteSourceIdentity(
        CanonicalTextWriter writer,
        string prefix,
        MetadataPlanSourceIdentity? identity)
    {
        writer.Line(prefix, identity is null ? "absent" : "present");
        if (identity is not null)
        {
            writer.Line(prefix + ".profile-id", identity.ProfileId);
            writer.Line(prefix + ".profile-version", identity.ProfileVersion);
            writer.Line(prefix + ".trusted-definition-sha256", identity.TrustedDefinitionSha256);
            writer.Line(prefix + ".family-id", identity.FamilyId);
            writer.Line(prefix + ".family-version", identity.FamilyVersion);
            writer.Line(prefix + ".family-content-hash", identity.FamilyContentHash);
            writer.Line(prefix + ".view-id", identity.ViewId);
        }
    }

    private static void WriteFullImageContext(
        CanonicalTextWriter writer,
        string prefix,
        CanonicalFullImageMetadataContext? context)
    {
        writer.Line(prefix, context is null ? "absent" : "present");
        if (context is not null)
        {
            writer.Line(prefix + ".family-id", context.Family.FamilyId);
            writer.Line(prefix + ".family-version", context.Family.FamilyVersion);
            writer.Line(prefix + ".family-content-hash", context.Family.FamilyContentHash);
            writer.Line(prefix + ".member-id", context.MemberId);
            writer.Line(prefix + ".view.id", context.View.ViewId);
            writer.Lines(prefix + ".view.members", context.View.MemberIds);
            writer.Lines(
                prefix + ".view.bindings",
                [.. context.View.MetadataBindings.Select(static binding => binding.BindingId)]);
            writer.Lines(prefix + ".view.evidence", context.View.EvidenceRefs);
            WriteImageMap(writer, prefix + ".view.image-map", context.View.ImageMap);
            WriteSourceIdentity(writer, prefix + ".source", context.SourceIdentity);
        }
    }

    private static void WritePlanEntry(CanonicalTextWriter writer, string prefix, MetadataPlanEntry entry)
    {
        writer.Line(prefix + ".binding-id", entry.BindingId);
        writer.Line(prefix + ".space-id", entry.SpaceId);
        writer.Line(prefix + ".slot-id", entry.SlotId);
        writer.Line(prefix + ".family-id", entry.FamilyDefinition.FamilyId);
        writer.Line(prefix + ".family-version", entry.FamilyDefinition.FamilyVersion);
        writer.Line(prefix + ".family-content-hash", entry.FamilyDefinition.FamilyContentHash);
        writer.Line(prefix + ".image-map-id", entry.ImageMap.MapId);
        writer.Line(prefix + ".member-id", entry.MemberId);
        writer.Line(
            prefix + ".profile-map-fingerprint",
            entry.FullImageContext is null ? entry.ResolvedMap.ResolutionFingerprint : null);
        writer.Line(prefix + ".full-image-binding-id", entry.FullImageBinding?.BindingId);
        WriteFactKey(writer, prefix + ".metadata-set.effective", entry.MetadataSetBinding.EffectiveKey);
        WriteFactKey(writer, prefix + ".metadata-set.direct", entry.MetadataSetBinding.DirectSourceKey);
        writer.Line(prefix + ".metadata-set.canonical-fact-id", entry.MetadataSetBinding.CanonicalFactId);
        writer.Line(prefix + ".structure.id", entry.StructureDefinition.StructureId);
        writer.Line(prefix + ".structure.artifact-binding-id", entry.StructureDefinition.ArtifactBindingId);
        writer.Line(prefix + ".structure.length-bytes", entry.StructureDefinition.LengthBytes);
        writer.Lines(
            prefix + ".targets",
            [.. entry.TargetReferences.Select(static target => $"{target.Kind}:{target.TargetId}")]);
        writer.Lines(prefix + ".purposes", [.. entry.Purposes.Select(static purpose => purpose.ToString())]);
        writer.Lines(prefix + ".evidence", entry.EvidenceRefs);
    }

    private static void WriteFactKey(CanonicalTextWriter writer, string prefix, FirmwareMapFactKey key)
    {
        writer.Line(prefix, $"{key.MemberId}|{key.MapId}|{key.FactKind}|{key.FactId}");
    }

    private static void WriteImageMap(CanonicalTextWriter writer, string prefix, FirmwareImageMap map)
    {
        writer.Line(prefix + ".map-id", map.MapId);
        writer.Line(prefix + ".address-space", map.AddressSpaceId);
        writer.Line(prefix + ".capacity-bytes", map.CapacityBytes);
        writer.Line(prefix + ".coverage-policy", map.CoveragePolicy);
        writer.Lines(prefix + ".evidence", map.EvidenceRefs);
        writer.Lines(
            prefix + ".regions",
            [
                .. map.Regions.Select(static region => string.Create(
                    CultureInfo.InvariantCulture,
                    $"{region.RegionId}|{region.ParentRegionId}|{region.Owner}|{region.Kind}|[0x{region.Range.Start:X},0x{region.Range.EndExclusive:X})|{region.WriteConstraint}|{region.Alignment}")),
            ]);
    }

    private static string[] CreateWorkflowUniverse(CanonicalCapabilityCatalogSnapshot snapshot)
    {
        return
        [
            .. KnownWorkflowIds
                .Concat(snapshot.Capabilities.Select(static capability => capability.Identity.WorkflowId))
                .Concat(snapshot.DynamicRoutes.Select(static route => route.Identity.WorkflowId))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    private static string[] CreateIcUniverse(
        CanonicalCapabilityCatalogSnapshot snapshot,
        string[] workflowIds)
    {
        CapabilitySelectorPublication selector = snapshot.SelectorPublication;
        List<string> declared =
        [
            .. snapshot.Capabilities.Select(static capability => capability.Identity.IcId),
            .. snapshot.DynamicRoutes.Select(static route => route.Identity.IcId),
            .. selector.IcIds,
            .. selector.AbMergeIcIds,
            .. workflowIds.SelectMany(workflowId => snapshot.Disclosure
                .GetProfileSummaries(workflowId)
                .Select(static summary => summary.IcId)),
            .. snapshot.FullImageMetadataPlans
                .Select(static plan => plan.Definition.FullImageContext)
                .Where(static context => context is not null)
                .SelectMany(static context => context!.View.MemberIds),
        ];
        if (selector.DefaultIcId is not null)
        {
            declared.Add(selector.DefaultIcId);
        }

        return
        [
            .. declared
                .Select(IcIdentifier.Normalize)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    /// <summary>Line-oriented key/value writer with quoted, escaped string values.</summary>
    private sealed class CanonicalTextWriter
    {
        private readonly StringBuilder _text = new();

        internal static string Indexed(string key, int index)
        {
            return FormattableString.Invariant($"{key}[{index}]");
        }

        internal void Line(string key, string? value)
        {
            _ = _text.Append(key).Append('=').Append(Quote(value)).Append('\n');
        }

        internal void Line(string key, long value)
        {
            _ = _text.Append(key)
                .Append('=')
                .Append(value.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }

        internal void Line(string key, bool value)
        {
            _ = _text.Append(key).Append('=').Append(value ? "true" : "false").Append('\n');
        }

        internal void Line<TEnum>(string key, TEnum value)
            where TEnum : struct, Enum
        {
            _ = _text.Append(key).Append('=').Append(value.ToString()).Append('\n');
        }

        internal void Lines(string key, IReadOnlyList<string> values)
        {
            Line(key + ".count", values.Count);
            for (int index = 0; index < values.Count; index++)
            {
                Line(Indexed(key, index), values[index]);
            }
        }

        public override string ToString()
        {
            return _text.ToString();
        }

        private static string Quote(string? value)
        {
            if (value is null)
            {
                return "null";
            }

            var quoted = new StringBuilder(value.Length + 2);
            _ = quoted.Append('"');
            foreach (char character in value)
            {
                _ = character switch
                {
                    '"' => quoted.Append("\\\""),
                    '\\' => quoted.Append("\\\\"),
                    _ when char.IsControl(character) => quoted.Append(
                        FormattableString.Invariant($"\\u{(int)character:x4}")),
                    _ => quoted.Append(character),
                };
            }

            return quoted.Append('"').ToString();
        }
    }
}
