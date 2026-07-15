using NvtFwCombiner.Contracts.Profiles;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    private static CompositionProfileCompilationContext NormalizeCompilationContext(
        CompositionProfileDocument document)
    {
        if (!StringComparer.Ordinal.Equals(document.SchemaVersion, "2.4") &&
            !StringComparer.Ordinal.Equals(document.SchemaVersion, "2.5") &&
            !StringComparer.Ordinal.Equals(document.SchemaVersion, "2.6") &&
            !StringComparer.Ordinal.Equals(document.SchemaVersion, "2.7"))
        {
            return document.CompilationContext is not null || document.LogicalOutputBinding is not null
                ? throw Error("compilationContext", "Compilation contexts require composition-profile schema version '2.4', '2.5', '2.6', or '2.7'.")
                : new ResolvedMapProfileCompilationContext(NormalizeMapBinding(
                    RequireObject(document.MapBinding, "mapBinding"),
                    "mapBinding"));
        }

        CompositionProfileCompilationContextDocument context = RequireObject(
            document.CompilationContext,
            "compilationContext");
        return context.Kind switch
        {
            "resolved-map" when document.LogicalOutputBinding is null => new ResolvedMapProfileCompilationContext(
                NormalizeMapBinding(RequireObject(document.MapBinding, "mapBinding"), "mapBinding")),
            "logical-output" when document.MapBinding is null => NormalizeLogicalOutputContext(
                document.SchemaVersion,
                RequireObject(document.LogicalOutputBinding, "logicalOutputBinding")),
            "runtime-reference-replace" when (document.SchemaVersion == "2.6" || document.SchemaVersion == "2.7") && document.LogicalOutputBinding is null =>
                new RuntimeReferenceReplaceProfileCompilationContext(
                    NormalizeMapBinding(RequireObject(document.MapBinding, "mapBinding"), "mapBinding")),
            "resolved-map" => throw Error("logicalOutputBinding", "Resolved-map profiles cannot declare logical-output binding."),
            "logical-output" => throw Error("mapBinding", "Logical-output profiles cannot declare map binding."),
            "runtime-reference-replace" when document.SchemaVersion is not "2.6" and not "2.7" => throw Error(
                "compilationContext.kind",
                "The runtime-reference-replace context requires composition-profile schema version '2.6' or '2.7'."),
            "runtime-reference-replace" => throw Error(
                "logicalOutputBinding",
                "Runtime reference-replace profiles cannot declare logical-output binding."),
            _ => throw Error("compilationContext.kind", "Unknown profile compilation context."),
        };
    }

    private static LogicalOutputProfileCompilationContext NormalizeLogicalOutputContext(
        string schemaVersion,
        CompositionProfileLogicalOutputBindingDocument document)
    {
        return Wrap("logicalOutputBinding", () => new LogicalOutputProfileCompilationContext(
            schemaVersion,
            document.FamilyId,
            document.FamilyVersion,
            document.FamilyContentHash,
            RequireList(document.MemberIds, "logicalOutputBinding.memberIds")));
    }
}
