using System.Text.Json.Serialization;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Contracts.Profiles;

namespace NvtFwCombiner.Profiles.V2;

/// <summary>Strict source-generated JSON metadata used only while consuming a trusted bundle projection.</summary>
[JsonSourceGenerationOptions(
    AllowOutOfOrderMetadataProperties = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(FirmwareFamilyDocument))]
[JsonSerializable(typeof(CompositionProfileDocument))]
internal sealed partial class ProfileBundleSemanticJsonContext : JsonSerializerContext
{
}
