using System.Text.Json.Serialization;
using NvtFwCombiner.Contracts.Bundles;
using NvtFwCombiner.Contracts.Firmware;
using NvtFwCombiner.Contracts.Profiles;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>Source-generated strict JSON metadata for canonical bundle document roots.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ProfileBundleDocument))]
[JsonSerializable(typeof(FirmwareFamilyDocument))]
[JsonSerializable(typeof(CompositionProfileDocument))]
internal sealed partial class ProfileBundleJsonContext : JsonSerializerContext
{
}
