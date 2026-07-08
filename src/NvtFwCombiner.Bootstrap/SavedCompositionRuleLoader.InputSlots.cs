using System.Globalization;
using System.Text.Json;

namespace NvtFwCombiner.Bootstrap;

internal static partial class SavedCompositionRuleLoader
{
    private static HashSet<string> ReadInputSlotTemplateIds(JsonElement root, List<SavedRuleValidationIssue> issues)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        if (!root.TryGetProperty("inputSlotTemplates", out JsonElement templates))
        {
            return ids;
        }

        if (templates.ValueKind != JsonValueKind.Array)
        {
            issues.Add(Issue("saved-rule.input-slot-templates.invalid", "inputSlotTemplates must be an array.", "$.inputSlotTemplates"));
            return ids;
        }

        List<string> values = [];
        int index = 0;
        foreach (JsonElement template in templates.EnumerateArray())
        {
            string path = string.Create(CultureInfo.InvariantCulture, $"$.inputSlotTemplates[{index++}]");
            if (template.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue("saved-rule.input-slot-template.invalid", "Input slot template must be an object.", path));
                continue;
            }

            ValidateProperties(template, InputSlotTemplateProperties, path, issues);
            string slotTemplateId = RequiredId(template, "slotTemplateId", $"{path}.slotTemplateId", issues);
            if (!string.IsNullOrWhiteSpace(slotTemplateId))
            {
                values.Add(slotTemplateId);
                _ = ids.Add(slotTemplateId);
            }

            _ = RequiredString(template, "role", $"{path}.role", issues);
            _ = RequiredEnum(template, "cardinality", $"{path}.cardinality", InputSlotCardinalityValues, issues);
            _ = ReadExtensionArray(template, "acceptedExtensions", $"{path}.acceptedExtensions", issues);
        }

        AddDuplicateIssues(
            values,
            "saved-rule.input-slot-template.duplicate",
            "Input slot template id is duplicated.",
            "$.inputSlotTemplates",
            issues);
        return ids;
    }
}
