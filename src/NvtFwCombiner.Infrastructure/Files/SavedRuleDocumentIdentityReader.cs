using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Infrastructure.Bundles;
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
            using JsonDocument document = StrictJsonDocumentReader.Parse(
                documentBytes,
                documentBytes.Length,
                maximumDepth: 64);
            JsonElement root = document.RootElement;
            if (!SavedCompositionRuleV2Schema.IsValid(root))
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

}
