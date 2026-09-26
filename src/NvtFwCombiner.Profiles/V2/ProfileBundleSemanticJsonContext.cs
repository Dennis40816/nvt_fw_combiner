using System.Text.Json.Serialization;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Contracts.Profiles;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>
/// The one strict source-generated JSON metadata for canonical family and profile documents: the Infrastructure
/// trusted projection verifies DTO compatibility with it, and the Profiles catalog factory deserializes the same
/// trusted JSON trees with it.
/// </summary>
[JsonSourceGenerationOptions(
    AllowOutOfOrderMetadataProperties = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(FirmwareFamilyDocument))]
[JsonSerializable(typeof(CompositionProfileDocument))]
[JsonSerializable(typeof(CompositionProfileRegionInstanceDeltaAddendDocument))]
internal sealed partial class ProfileBundleSemanticJsonContext : JsonSerializerContext
{
}
