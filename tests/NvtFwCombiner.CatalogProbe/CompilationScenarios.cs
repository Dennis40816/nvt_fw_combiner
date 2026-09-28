using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.Bootstrap.Tests;

namespace NvtFwCombiner.CatalogProbe;

// Inputs reuse the existing Standard/AB, CtrlRamV2PlanClosure and General candidate contracts.
// Every subject comes from the selected registry; no test-only catalog is constructed.
internal static class CompilationScenarios
{
    internal static JsonArray Capture()
    {
        var results = new JsonArray();
        TrustedProfileBundleCatalog standard = Selected("nt51950-nt51951-standard-merge");
        foreach (long capacity in new long[] { 0x40000, 0x80000, 123 })
        {
            Add(results, "standard", capacity != 123, standard.Compile(
                "nt51950-standard-merge-dp-perspective", "0.8.0", "NT51950", ExperienceIds.StandardMerge,
                capacity, null, []));
        }
        TrustedProfileBundleCatalog ab = Selected("nt51950-ab-merge");
        foreach (int count in new[] { 2, 1 })
        {
            Add(results, "ab", count == 2, ab.Compile("nt51950-ab-merge-cascade", "0.4.0", "NT51950",
                ExperienceIds.AbMerge, 0x100000, new TopologySelection(count, "cascade", TopologySelectionSource.Requested, "test"),
                [], ["dp-ab-input"]));
        }
        TrustedProfileBundleCatalog ctrlram = Selected("nt51923-ctrlram-replace-candidate");
        foreach (int count in new[] { 1, 3, 9 })
        {
            byte[] reference = new byte[0x40000];
            reference[23] = (byte)count;
            reference[0xFFD] = 0x4E;
            reference[0xFFE] = 0x56;
            reference[0xFFF] = 0x54;
            Add(results, "ctrlram", count != 9, ctrlram.CompileRuntimeReferenceReplace(
                count == 3 ? "nt51923-ctrlram-replace-fw141-cascade3" : "nt51923-ctrlram-replace-fw141-single",
                "0.4.0", "NT51923", ExperienceIds.CtrlRamReplace,
                new TopologySelection(count, count == 1 ? "single" : "cascade", TopologySelectionSource.Requested, "number-selector"),
                [new FirmwareArtifactPayload("reference-base", reference)],
                new V2RuntimeReferenceReplaceCompileRequest(
                    [new("reference-base", "reference-base", 0x40000), new("ctrlram-source-1", "ctrlram-source", 1)],
                    [Mapping(ExplicitMappingOperationKind.ReplaceRange, "ctrlram-source-1", 0x22800, 1)])));
        }
        TrustedProfileBundleCatalog merge = Selected("nt51923-nt51926-general-merge-logical-candidate");
        foreach (long offset in new long[] { 0, 16 })
        {
            Add(results, "general-merge", offset == 0, merge.CompileLogicalOutput(
                "nt51923-general-merge-logical-candidate", "0.1.0", "NT51923",
                new V2LogicalOutputCompileRequest(new GeneralMergeOutputInitializer(16),
                    [new("source-a", "source", 4)], [Mapping(ExplicitMappingOperationKind.CopyRange, "source-a", offset, 3)])));
        }
        TrustedProfileBundleCatalog replace = Selected("nt51926-ctrlram-replace-candidate");
        foreach (long offset in new long[] { 0x3E000, 0x40000 })
        {
            Add(results, "general-replace", offset == 0x3E000, replace.CompileRuntimeReferenceReplace(
                "nt51926-general-replace-dp-single-candidate", "0.1.0", "NT51926", ExperienceIds.GeneralReplace, null, [],
                new V2RuntimeReferenceReplaceCompileRequest(
                    [new("base", "reference", 0x40000), new("source-a", "source", 4)],
                    [Mapping(ExplicitMappingOperationKind.ReplaceRange, "source-a", offset, 3)])));
        }
        return results;
    }

    private static TrustedProfileBundleCatalog Selected(string directory)
    {
        SelectedBuiltInBundleEvidence selected = BuiltInV2BundleRegistry.GetSelectedBundleEvidence(directory);
        return ReferenceEquals(selected.Catalog, BuiltInV2BundleRegistry.All[directory].GetLoadedEvidence().Catalog)
            ? selected.Catalog : throw new InvalidOperationException("Evidence must use the product catalog instance.");
    }

    private static ExplicitMapping Mapping(ExplicitMappingOperationKind kind, string source, long offset, long length)
    {
        return new("copy-source", 1, kind, source, new ByteRange(0, length), CompositionAddressSpaceIds.OutputImage,
            new ByteRange(offset, length), OverlapPolicy.Reject, 1, "Cross-source contract");
    }

    private static void Add(JsonArray results, string group, bool expectedSuccess, V2CompositionPlanCompileResult result)
    {
        CompiledComposition? composition = result.CompiledComposition;
        results.Add(new JsonObject
        {
            ["group"] = group,
            ["expectedSuccess"] = expectedSuccess,
            ["success"] = result.IsCompiled,
            ["issues"] = JsonSerializer.SerializeToNode(result.Issues),
            ["fingerprint"] = composition?.CompilationFingerprint,
            ["plan"] = composition is null ? null : CanonicalCatalogSnapshotDigest.DescribeComposition(composition),
        });
    }
}
