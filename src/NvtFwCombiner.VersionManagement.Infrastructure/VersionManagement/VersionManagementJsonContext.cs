using System.Text.Json.Serialization;
using NvtFwCombiner.Contracts.VersionManagement;

namespace NvtFwCombiner.Infrastructure.VersionManagement;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(UpdateCatalogDocument))]
[JsonSerializable(typeof(UpdateCatalogV2Document))]
[JsonSerializable(typeof(UpdateSourceRegistryDocument))]
[JsonSerializable(typeof(VersionManagerStateDocument))]
[JsonSerializable(typeof(ManagedVersionAdmissionFileDocument))]
[JsonSerializable(typeof(ReleaseManifestDocument))]
[JsonSerializable(typeof(LauncherBootstrapStateDocument))]
[JsonSerializable(typeof(ManagedSetupTransactionDocument))]
[JsonSerializable(typeof(ManagedSetupPayloadAdmissionDescriptorDocument))]
internal sealed partial class VersionManagementJsonContext : JsonSerializerContext;
