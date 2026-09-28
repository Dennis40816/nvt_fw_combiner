using System.Text.Json.Serialization;
using NvtFwCombiner.Contracts.Bundles;

namespace NvtFwCombiner.Infrastructure.Bundles;

/// <summary>
/// Source-generated strict JSON metadata for the canonical bundle manifest root. Family and profile documents bind
/// through their one metadata owner, <see cref="Profiles.V2.ProfileBundleSemanticJsonContext"/>.
/// </summary>
[JsonSourceGenerationOptions(
    AllowOutOfOrderMetadataProperties = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = false,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(ProfileBundleDocument))]
internal sealed partial class ProfileBundleJsonContext : JsonSerializerContext
{
}
