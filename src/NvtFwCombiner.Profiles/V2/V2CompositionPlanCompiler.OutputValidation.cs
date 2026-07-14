using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class V2CompositionPlanCompiler
{
    private static void ValidateMapBoundOutputShape(
        CompositionProfileDefinition profile,
        MutableCompositionProfileSpace output,
        List<CompositionIssue> issues)
    {
        MutableCompositionProfileSpace? runtimeRequestSpace = profile.Spaces
            .OfType<MutableCompositionProfileSpace>()
            .FirstOrDefault(static space => space.Capacity is RuntimeRequestProfileCapacity);
        if (runtimeRequestSpace is not null)
        {
            AddUnsupported(
                issues,
                runtimeRequestSpace.Kind == CompositionProfileSpaceKind.OutputImage
                    ? "runtime-request output capacity requires logical-output V2 lowering"
                    : "runtime-request capacity is valid only for a logical-output V2 output image");
            return;
        }

        if (output.Capacity is not ResolvedMapProfileCapacity ||
            (profile.CompositionKind == CompositionKind.Merge &&
             output.Initializer is not BlankProfileInitializer) ||
            (profile.CompositionKind == CompositionKind.Replace &&
             output.Initializer is not CloneProfileInitializer))
        {
            AddUnsupported(issues, "the output image must use resolved-map blank initialization for Merge or reference clone initialization for Replace");
        }
    }
}
