using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    private readonly record struct NormalizedCompilationContext(
        V2CompilationContextKind Kind,
        CompositionProfileMapBinding? MapBinding,
        string FamilyId,
        string FamilyVersion,
        string FamilyContentHash,
        IReadOnlyList<string> LogicalOutputMemberIds,
        bool AllowsConditionalProcessor);

    private static NormalizedCompilationContext NormalizeCompilationContext(
        CompositionProfileDocument document)
    {
        if (document.SchemaVersion is not ("2.4" or "2.5" or "2.6" or "2.7" or "2.8" or "2.9" or "2.10" or "2.11" or "2.12" or "2.13" or "2.14" or "2.15"))
        {
            return document.CompilationContext is not null || document.LogicalOutputBinding is not null
                ? throw Error("compilationContext", "Compilation contexts require composition-profile schema version '2.4' through '2.15'.")
                : MapBoundContext(
                    V2CompilationContextKind.ResolvedMap,
                    NormalizeMapBinding(
                        RequireObject(document.MapBinding, "mapBinding"),
                        "mapBinding"),
                    allowsConditionalProcessor: false);
        }

        CompositionProfileCompilationContextDocument context = RequireObject(
            document.CompilationContext,
            "compilationContext");
        return context.Kind switch
        {
            "resolved-map" when document.LogicalOutputBinding is null => MapBoundContext(
                V2CompilationContextKind.ResolvedMap,
                NormalizeMapBinding(RequireObject(document.MapBinding, "mapBinding"), "mapBinding"),
                allowsConditionalProcessor: false),
            "logical-output" when document.MapBinding is null => NormalizeLogicalOutputContext(
                document.SchemaVersion,
                RequireObject(document.LogicalOutputBinding, "logicalOutputBinding")),
            "runtime-reference-replace" when
                (document.SchemaVersion is "2.6" or "2.7" or "2.8" or "2.9" or "2.10" or "2.11" or "2.12" or "2.13" or "2.14" or "2.15") && document.LogicalOutputBinding is null =>
                MapBoundContext(
                    V2CompilationContextKind.RuntimeReferenceReplace,
                    NormalizeMapBinding(RequireObject(document.MapBinding, "mapBinding"), "mapBinding"),
                    allowsConditionalProcessor: document.SchemaVersion is
                        "2.9" or "2.10" or "2.11" or "2.12" or "2.13" or "2.14" or "2.15"),
            "resolved-map" => throw Error("logicalOutputBinding", "Resolved-map profiles cannot declare logical-output binding."),
            "logical-output" => throw Error("mapBinding", "Logical-output profiles cannot declare map binding."),
            "runtime-reference-replace" when document.SchemaVersion is not "2.6" and not "2.7" and not "2.8" and not "2.9" and not "2.10" and not "2.11" and not "2.12" and not "2.13" and not "2.14" and not "2.15" => throw Error(
                "compilationContext.kind",
                "The runtime-reference-replace context requires composition-profile schema version '2.6' through '2.15'."),
            "runtime-reference-replace" => throw Error(
                "logicalOutputBinding",
                "Runtime reference-replace profiles cannot declare logical-output binding."),
            _ => throw Error("compilationContext.kind", "Unknown profile compilation context."),
        };
    }

    private static NormalizedCompilationContext NormalizeLogicalOutputContext(
        string schemaVersion,
        CompositionProfileLogicalOutputBindingDocument document)
    {
        return Wrap("logicalOutputBinding", () =>
        {
            string familyId = CompositionProfileValueRules.RequireId(document.FamilyId, "familyId");
            string familyVersion = CompositionProfileValueRules.RequireSemanticVersion(
                document.FamilyVersion,
                "familyVersion");
            string familyContentHash = RequireFamilyContentHash(document.FamilyContentHash);

            return new NormalizedCompilationContext(
                V2CompilationContextKind.LogicalOutput,
                null,
                familyId,
                familyVersion,
                familyContentHash,
                CompositionProfileValueRules.SnapshotLogicalMemberIds(
                    schemaVersion,
                    RequireList(document.MemberIds, "logicalOutputBinding.memberIds"),
                    "memberIds"),
                AllowsConditionalProcessor: false);
        });
    }

    private static NormalizedCompilationContext MapBoundContext(
        V2CompilationContextKind kind,
        CompositionProfileMapBinding mapBinding,
        bool allowsConditionalProcessor)
    {
        return new NormalizedCompilationContext(
            kind,
            mapBinding,
            mapBinding.FamilyId,
            mapBinding.FamilyVersion,
            mapBinding.FamilyContentHash,
            [],
            allowsConditionalProcessor);
    }

    private static string RequireFamilyContentHash(string familyContentHash)
    {
        return CompositionProfileValueRules.IsLowercaseSha256(familyContentHash)
            ? familyContentHash
            : throw new ArgumentException(
                "Family content hash must be 64 lowercase hexadecimal characters.",
                nameof(familyContentHash));
    }
}
