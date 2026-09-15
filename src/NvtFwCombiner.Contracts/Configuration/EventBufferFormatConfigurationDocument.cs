namespace NvtFwCombiner.Contracts.Configuration;

/// <summary>Versioned transport document; semantic identity and membership admission belongs to Application.</summary>
public sealed record EventBufferFormatConfigurationDocument(
    int SchemaVersion,
    string ScopeId,
    IReadOnlyList<EventBufferFormatConfigurationEntryDocument> Entries);

/// <summary>Display alias and hexadecimal recognition bytes, never a firmware effect definition.</summary>
public sealed record EventBufferFormatConfigurationEntryDocument(
    string UniqueId,
    string? AliasName,
    IReadOnlyList<string> RecognitionValues);
