using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles;

public static partial class CompositionProfileCompiler
{
    private static CompositionOperation CompileExplicitMapping(
        CompositionProfileDefinition profile,
        ExplicitMapping mapping)
    {
        return mapping.OperationKind == ExplicitMappingOperationKind.ReplaceRange
            ? CompositionOperation.ReplaceRange(
                mapping.MappingId,
                mapping.Sequence,
                mapping.SourceBindingId,
                mapping.SourceRange,
                mapping.TargetSpaceId,
                mapping.TargetRange,
                mapping.OverlapPolicy,
                mapping.Reason,
                mapping.Provenance)
            : throw new ArgumentException(
                $"Explicit mapping '{mapping.MappingId}' kind is incompatible with profile composition kind '{profile.CompositionKind}'.",
                nameof(mapping));
    }
}
