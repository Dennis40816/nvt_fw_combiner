using System.Text.Json.Nodes;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class SavedRuleCliCommandTests
{
    private static string ValidGeneralMergeRuleJson()
    {
        return /*lang=json,strict*/ """
            {
              "schemaVersion": "1.0",
              "ruleId": "copy-display-window",
              "ruleVersion": "1.0.0",
              "displayName": "Copy display window",
              "compositionKind": "merge",
              "sourceExperience": "general-merge",
              "supportStatus": "draft",
              "compatibility": {
                "profileIds": ["nt51950-general-merge-workbench"],
                "icIds": ["NT51950"],
                "modeIds": ["general-merge"]
              },
              "inputSlotTemplates": [
                {
                  "slotTemplateId": "source-bin",
                  "role": "Source BIN",
                  "cardinality": "one",
                  "acceptedExtensions": [".bin"]
                }
              ],
              "mappingRows": [
                {
                  "rowId": "copy-fw-window",
                  "sourceSlotTemplateId": "source-bin",
                  "sourceRange": { "start": 16, "length": 32 },
                  "targetAddressSpaceId": "output-image",
                  "targetRange": { "start": 256, "length": 32 },
                  "overlapPolicy": "reject",
                  "reason": "Reviewed General Merge mapping."
                }
              ],
              "operationFragments": [
                {
                  "operationId": "copy-fw-window",
                  "kind": "copy-range",
                  "reason": "Compile mapping row to a copy operation.",
                  "mappingRowIds": ["copy-fw-window"]
                }
              ],
              "validationRuleIds": ["reviewed-bounds"],
              "owner": "firmware-owner",
              "evidenceRefs": []
            }
            """;
    }

    private static JsonObject ValidGeneralMergeRuleObject()
    {
        return JsonNode.Parse(ValidGeneralMergeRuleJson())!.AsObject();
    }

    private static JsonObject ValidGeneralReplaceRuleObject()
    {
        JsonObject json = ValidGeneralMergeRuleObject();
        json["compositionKind"] = "replace";
        json["sourceExperience"] = "general-replace";
        json["compatibility"]!["modeIds"] = new JsonArray("general-replace");
        OperationFragments(json)[0]!.AsObject()["kind"] = "replace-range";
        return json;
    }

    private static JsonArray MappingRows(JsonObject json)
    {
        return json["mappingRows"]!.AsArray();
    }

    private static JsonArray OperationFragments(JsonObject json)
    {
        return json["operationFragments"]!.AsArray();
    }

    private static JsonObject CloneObject(JsonNode node)
    {
        return JsonNode.Parse(node.ToJsonString())!.AsObject();
    }

    private static async Task<string> WriteRuleAsync(TempWorkspace workspace, JsonObject json)
    {
        string rule = workspace.PathFor("rule.json");
        await File.WriteAllTextAsync(rule, json.ToJsonString(), TestContext.Current.CancellationToken);
        return rule;
    }

    private static Task<CliRunResult> RunCliAsync(string[] args)
    {
        return CliTestHarness.RunAsync(args, TestContext.Current.CancellationToken);
    }
}
