using System.Text.Json;
using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Infrastructure.Composition;

/// <summary>Shallow v2 admission shared by inspection and execution loaders.</summary>
internal static class SavedRuleSchemaVersionGate
{
    internal static SavedRuleValidationIssue? Validate(JsonElement root)
    {
        string? schemaVersion = root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("schemaVersion", out JsonElement value) &&
            value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
        if (StringComparer.Ordinal.Equals(schemaVersion, "2.0"))
        {
            return null;
        }

        string message = StringComparer.Ordinal.Equals(schemaVersion, "1.0")
            ? "Saved Rule v1 is retired; migrate the document to Saved Rule v2 before validation or execution."
            : "Saved Rule schemaVersion must be '2.0'.";
        return new SavedRuleValidationIssue(
            SavedRuleIssueCodes.SchemaVersionUnsupported,
            message,
            "$.schemaVersion");
    }
}
