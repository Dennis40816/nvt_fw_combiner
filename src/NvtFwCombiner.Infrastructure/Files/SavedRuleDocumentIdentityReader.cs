using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Contracts;

namespace NvtFwCombiner.Infrastructure.Files;

/// <summary>
/// Reads the complete execution identity only from one strict, schema-valid
/// Saved Rule v2 document snapshot.
/// </summary>
public sealed class SavedRuleDocumentIdentityReader :
    ISavedRuleDocumentIdentityReader
{
    /// <inheritdoc />
    public SavedRuleExecutionIdentity? TryReadIdentity(
        ReadOnlyMemory<byte> documentBytes)
    {
        if (documentBytes.IsEmpty)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(documentBytes);
            JsonElement root = document.RootElement;
            if (!HasUniqueProperties(root) ||
                !SavedCompositionRuleV2Schema.IsValid(root))
            {
                return null;
            }

            JsonElement parent = root.GetProperty("parentBinding");
            return new SavedRuleExecutionIdentity(
                root.GetProperty("ruleId").GetString()!,
                root.GetProperty("ruleVersion").GetString()!,
                SavedCompositionRuleV2ContentHasher.Calculate(root),
                new SavedRuleParentIdentity(
                    parent.GetProperty("bundleId").GetString()!,
                    parent.GetProperty("bundleVersion").GetString()!,
                    parent.GetProperty("bundleContentHash").GetString()!,
                    parent.GetProperty("profileId").GetString()!,
                    parent.GetProperty("profileVersion").GetString()!,
                    parent.GetProperty("profileContentHash").GetString()!,
                    parent.GetProperty("familyId").GetString()!,
                    parent.GetProperty("familyVersion").GetString()!,
                    parent.GetProperty("familyContentHash").GetString()!,
                    parent.GetProperty("mapId").GetString()!));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasUniqueProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().All(HasUniqueProperties);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return true;
        }

        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!names.Add(property.Name) ||
                !HasUniqueProperties(property.Value))
            {
                return false;
            }
        }

        return true;
    }
}
