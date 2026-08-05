using System.Text.Json.Serialization;
using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Infrastructure.Diagnostics;

/// <summary>Source-generated serialization contract for privacy-filtered diagnostics.</summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    WriteIndented = true,
    UseStringEnumConverter = true,
    GenerationMode = JsonSourceGenerationMode.Serialization)]
[JsonSerializable(typeof(SystemDiagnosticsBundle))]
internal sealed partial class SystemDiagnosticsJsonContext : JsonSerializerContext
{
}
