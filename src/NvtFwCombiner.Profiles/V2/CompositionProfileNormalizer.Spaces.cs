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
        ArgumentNullException.ThrowIfNull(document);
        return document.Kind switch
        {
            "input-artifact" => Wrap(path, () => new InputArtifactProfileSpace(
                document.SpaceId,
                document.SlotId ?? throw Error($"{path}.slotId", "Input slot reference is missing."),
                NormalizeInstancePolicy(
                    document.InstancePolicy ?? throw Error(
                        $"{path}.instancePolicy",
                        "Input instance policy is missing."),
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
        ArgumentNullException.ThrowIfNull(document);
        CompositionProfileViewSelectorDocument selector = document.Selector ?? throw Error(
            $"{path}.selector",
            "View selector is missing.");
        return Wrap(path, () => new CompositionProfileView(
            document.ViewId,
            document.SpaceId,
            NormalizeViewSelector(selector, $"{path}.selector")));
    }

    private static MutableCompositionProfileSpace NormalizeMutableSpace(
        CompositionProfileSpaceDocument document,
        CompositionProfileSpaceKind kind,
        string path)
    {
        CompositionProfileCapacityDocument capacity = document.Capacity ?? throw Error(
            $"{path}.capacity",
            "Mutable-space capacity is missing.");
        CompositionProfileInitializerDocument initializer = document.Initializer ?? throw Error(
            $"{path}.initializer",
            "Mutable-space initializer is missing.");
        return Wrap(path, () => new MutableCompositionProfileSpace(
            document.SpaceId,
            kind,
            NormalizeCapacity(capacity, $"{path}.capacity"),
            NormalizeInitializer(initializer, $"{path}.initializer")));
    }

    private static CompositionProfileInstancePolicy NormalizeInstancePolicy(string value, string path)
    {
        return value switch
        {
            "singleton" => CompositionProfileInstancePolicy.Singleton,
            "per-binding" => CompositionProfileInstancePolicy.PerBinding,
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
                Require(document.Bytes, $"{path}.bytes"),
                1,
                long.MaxValue,
                $"{path}.bytes"))),
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
                Require(document.FillByte, $"{path}.fillByte"),
                $"{path}.fillByte")),
            "clone" => Wrap(path, () => new CloneProfileInitializer(
                document.SourceSlotId ?? throw Error(
                    $"{path}.sourceSlotId",
                    "Clone source slot is missing."))),
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
                document.RegionId ?? throw Error($"{path}.regionId", "Map region is missing."))),
            "map-region-slice" => Wrap(path, () => new MapRegionSliceViewSelector(
                document.RegionId ?? throw Error($"{path}.regionId", "Map region is missing."),
                ReadRange(
                    Require(document.Offset, $"{path}.offset"),
                    Require(document.Length, $"{path}.length"),
                    path,
                    "offset"))),
            "space-range" => new SpaceRangeViewSelector(ReadRange(
                document.Range ?? throw Error($"{path}.range", "Space range is missing."),
                $"{path}.range")),
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
