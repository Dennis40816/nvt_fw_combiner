using NvtFwCombiner.Contracts.Profiles;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    private static CompositionProfileCompilationContext NormalizeCompilationContext(
        CompositionProfileDocument document)
    {
        if (!StringComparer.Ordinal.Equals(document.SchemaVersion, "2.4"))
        {
            return document.CompilationContext is not null || document.LogicalOutputBinding is not null
                ? throw Error("compilationContext", "Compilation contexts require composition-profile schema version '2.4'.")
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
                RequireObject(document.LogicalOutputBinding, "logicalOutputBinding")),
            "resolved-map" => throw Error("logicalOutputBinding", "Resolved-map profiles cannot declare logical-output binding."),
            "logical-output" => throw Error("mapBinding", "Logical-output profiles cannot declare map binding."),
            _ => throw Error("compilationContext.kind", "Unknown profile compilation context."),
        };
    }

    private static LogicalOutputProfileCompilationContext NormalizeLogicalOutputContext(
        CompositionProfileLogicalOutputBindingDocument document)
    {
        return Wrap("logicalOutputBinding", () => new LogicalOutputProfileCompilationContext(
            document.FamilyId,
            document.FamilyVersion,
            document.FamilyContentHash,
            RequireList(document.MemberIds, "logicalOutputBinding.memberIds")));
    }
}
