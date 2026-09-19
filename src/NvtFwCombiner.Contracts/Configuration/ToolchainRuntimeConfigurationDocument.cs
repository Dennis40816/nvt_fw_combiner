using System.Text.Json.Serialization;

namespace NvtFwCombiner.Contracts.Configuration;

/// <summary>Versioned transport only; selection shape and candidate admission belong to Application.</summary>
public sealed record ToolchainRuntimeConfigurationDocument(
    [property: JsonRequired] int SchemaVersion,
    [property: JsonRequired] string Source,
    string? Path = null,
    string? Sha256 = null);
