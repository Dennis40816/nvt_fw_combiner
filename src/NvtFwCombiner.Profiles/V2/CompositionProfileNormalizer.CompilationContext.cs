using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain;
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
        bool AllowsConditionalProcessor,
        SourceEnvelopeProfileBinding? SourceEnvelopeBinding);

    private static NormalizedCompilationContext NormalizeCompilationContext(
        CompositionProfileDocument document)
    {
        SourceEnvelopeProfileBinding? envelope = document.SourceEnvelopeBinding is null
            ? null
            : NormalizeSourceEnvelopeBinding(document);
        return document.CompilationContext is null
            ? MapBoundContext(
                V2CompilationContextKind.ResolvedMap,
                NormalizeMapBinding(document.MapBinding!, "mapBinding"),
                allowsConditionalProcessor: false,
                envelope)
            : document.CompilationContext.Kind switch
            {
                "resolved-map" => MapBoundContext(
                    V2CompilationContextKind.ResolvedMap,
                    NormalizeMapBinding(document.MapBinding!, "mapBinding"),
                    allowsConditionalProcessor: false,
                    envelope),
                "logical-output" => NormalizeLogicalOutputContext(document.LogicalOutputBinding!),
                "runtime-reference-replace" =>
                    MapBoundContext(
                        V2CompilationContextKind.RuntimeReferenceReplace,
                        NormalizeMapBinding(document.MapBinding!, "mapBinding"),
                        allowsConditionalProcessor: document.SchemaVersion is not ("2.6" or "2.7" or "2.8")),
                _ => throw Error("compilationContext.kind", "Unknown profile compilation context."),
            };
    }

    private static NormalizedCompilationContext NormalizeLogicalOutputContext(
        CompositionProfileLogicalOutputBindingDocument document)
    {
        return Wrap("logicalOutputBinding", () =>
        {
            string familyId = CanonicalPolicyValueRules.RequireCanonicalId(document.FamilyId, "familyId");
            string familyVersion = CanonicalProfileValueRules.RequireSemanticVersion(
                document.FamilyVersion,
                "familyVersion");
            string familyContentHash = RequireFamilyContentHash(document.FamilyContentHash);

            return new NormalizedCompilationContext(
                V2CompilationContextKind.LogicalOutput,
                null,
                familyId,
                familyVersion,
                familyContentHash,
                document.MemberIds,
                AllowsConditionalProcessor: false,
                SourceEnvelopeBinding: null);
        });
    }

    private static NormalizedCompilationContext MapBoundContext(
        V2CompilationContextKind kind,
        CompositionProfileMapBinding mapBinding,
        bool allowsConditionalProcessor,
        SourceEnvelopeProfileBinding? sourceEnvelopeBinding = null)
    {
        return new NormalizedCompilationContext(
            kind,
            mapBinding,
            mapBinding.FamilyId,
            mapBinding.FamilyVersion,
            mapBinding.FamilyContentHash,
            [],
            allowsConditionalProcessor,
            sourceEnvelopeBinding);
    }

    private static SourceEnvelopeProfileBinding NormalizeSourceEnvelopeBinding(
        CompositionProfileDocument document)
    {
        if (document.SchemaVersion != "2.16" ||
            document.CompilationContext?.Kind != "resolved-map")
        {
            throw Error("sourceEnvelopeBinding", "Source envelope requires schema 2.16 resolved-map compilation.");
        }

        CompositionProfileSourceEnvelopeBindingDocument binding = document.SourceEnvelopeBinding!;
        bool allowsAbsentSource = binding.WhenSourceAbsent switch
        {
            "reject" => false,
            "resolved-map" => true,
            _ => throw Error("sourceEnvelopeBinding.whenSourceAbsent", "Unknown absent-source policy."),
        };
        return Wrap("sourceEnvelopeBinding", () => new SourceEnvelopeProfileBinding(
            binding.SourceSlotId,
            binding.LayoutTemplateMapId,
            binding.RootRegionId,
            allowsAbsentSource,
            binding.ExpectedOuterLengths,
            binding.UnexpectedLengthIssueCode));
    }

    private static string RequireFamilyContentHash(string familyContentHash)
    {
        return CanonicalSha256.Require(familyContentHash, nameof(familyContentHash));
    }
}
