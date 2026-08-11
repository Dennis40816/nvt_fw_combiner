using System.Text.Json;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionProfileSpace NormalizeSpace(
        CompositionProfileSpaceDocument document,
        string path = "spaces[0]")
    {
        return document.Kind switch
        {
            "input-artifact" => Wrap(path, () => new InputArtifactProfileSpace(
                document.SpaceId,
                document.SlotId!,
                NormalizeInstancePolicy(
                    document.InstancePolicy!,
                    $"{path}.instancePolicy"))),
            "work-buffer" => NormalizeMutableSpace(
                document,
                CompositionProfileSpaceKind.WorkBuffer,
                path),
            CompositionProfileWireTokens.OutputImageSpaceKind => NormalizeMutableSpace(
                document,
                CompositionProfileSpaceKind.OutputImage,
                path),
            _ => throw Error($"{path}.kind", "Unknown profile space kind."),
        };
    }

    internal static CompositionProfileView NormalizeView(
        CompositionProfileViewDocument document,
        string path = "views[0]")
    {
        return Wrap(path, () => new CompositionProfileView(
            document.ViewId,
            document.SpaceId,
            NormalizeViewSelector(document.Selector, $"{path}.selector")));
    }

    private static MutableCompositionProfileSpace NormalizeMutableSpace(
        CompositionProfileSpaceDocument document,
        CompositionProfileSpaceKind kind,
        string path)
    {
        return Wrap(path, () => new MutableCompositionProfileSpace(
            document.SpaceId,
            kind,
            NormalizeCapacity(document.Capacity!, $"{path}.capacity"),
            NormalizeInitializer(document.Initializer!, $"{path}.initializer")));
    }

    private static CompiledInputInstancePolicy NormalizeInstancePolicy(string value, string path)
    {
        return value switch
        {
            "singleton" => CompiledInputInstancePolicy.Singleton,
            "per-binding" => CompiledInputInstancePolicy.PerBinding,
            _ => throw Error(path, "Unknown input instance policy."),
        };
    }

    private static CompositionProfileCapacity NormalizeCapacity(
        CompositionProfileCapacityDocument document,
        string path)
    {
        return document.Kind switch
        {
            "resolved-map" => new ResolvedMapProfileCapacity(),
            "fixed" => Wrap(path, () => new FixedProfileCapacity(ReadInt64(
                document.Bytes!.Value,
                1,
                long.MaxValue,
                $"{path}.bytes"))),
            "runtime-request" => new RuntimeRequestProfileCapacity(),
            _ => throw Error($"{path}.kind", "Unknown profile capacity kind."),
        };
    }

    private static CompositionProfileInitializer NormalizeInitializer(
        CompositionProfileInitializerDocument document,
        string path)
    {
        return document.Kind switch
        {
            "blank" => new BlankProfileInitializer(ReadByte(
                document.FillByte!.Value,
                $"{path}.fillByte")),
            "clone" => Wrap(path, () => new CloneProfileInitializer(
                document.SourceSlotId!)),
            _ => throw Error($"{path}.kind", "Unknown profile initializer kind."),
        };
    }

    private static CompositionProfileViewSelector NormalizeViewSelector(
        CompositionProfileViewSelectorDocument document,
        string path)
    {
        return document.Kind switch
        {
            "map-region" => Wrap(path, () => new MapRegionViewSelector(
                document.RegionId!)),
            "map-region-slice" => Wrap(path, () => new MapRegionSliceViewSelector(
                document.RegionId!,
                ReadRange(
                    document.Offset!.Value,
                    document.Length!.Value,
                    path,
                    "offset"))),
            "space-range" => new SpaceRangeViewSelector(ReadRange(
                document.Range!,
                $"{path}.range")),
            "region-template-range" => Wrap(path, () => new RegionTemplateRangeViewSelector(
                document.RegionInstanceId!,
                document.TemplateRegionId!)),
            _ => throw Error($"{path}.kind", "Unknown profile view selector kind."),
        };
    }

    private static ByteRange ReadRange(CompositionProfileRelativeRangeDocument document, string path)
    {
        return ReadRange(document.Start, document.Length, path, "start");
    }

    private static ByteRange ReadRange(
        JsonElement start,
        JsonElement length,
        string path,
        string startPropertyName)
    {
        long normalizedStart = ReadInt64(start, 0, long.MaxValue, $"{path}.{startPropertyName}");
        long normalizedLength = ReadInt64(length, 1, long.MaxValue, $"{path}.length");
        return Wrap(path, () => new ByteRange(normalizedStart, normalizedLength));
    }
}
