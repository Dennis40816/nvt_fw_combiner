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

    private static JsonObject ValidGeneralMergeV2RuleObject(
        long capacity = 4,
        int fillByte = 0xA5)
    {
        return JsonNode.Parse(
            $$"""
            {
              "schemaVersion": "2.0",
              "ruleId": "copy-display-window",
              "ruleVersion": "1.0.0",
              "displayName": "Copy display window",
              "compositionKind": "merge",
              "sourceExperienceId": "general-merge",
              "imageInitialization": {
                "kind": "blank",
                "capacity": {{capacity}},
                "fillByte": {{fillByte}}
              },
              "parentBinding": {
                "bundleId": "nt51950-nt51951-general-merge-logical-candidate",
                "bundleVersion": "0.10.1-tp-header-closure.1",
                "bundleContentHash": "5ed0646fba9c0f01994222f6a7860c8d9c8fc97be415f0771042cf886977f6f0",
                "profileId": "nt51950-general-merge-logical-candidate",
                "profileVersion": "0.1.0",
                "profileContentHash": "fe68c0fc0b6a8d60d72e95133ea10975d68a20dd4b03f9c383769f3376ce1c1d",
                "familyId": "nt51950-nt51951-dp-perspective",
                "familyVersion": "1.3.0",
                "familyContentHash": "c9bc25ffe2137c58754f9aa425b63c34ad1f90b946f03a6ffe6e15f2077d5fec",
                "mapId": "logical-output"
              },
              "promotion": {
                "stage": "executable-candidate",
                "blockers": []
              },
              "slotTemplates": [
                {
                  "slotTemplateId": "source-bin",
                  "role": "source",
                  "cardinality": "one",
                  "acceptedExtensions": [".bin"]
                }
              ],
              "mappingFragments": [
                {
                  "fragmentId": "reviewed-copy-operation",
                  "operationKind": "copy-range",
                  "sourceSlot": {
                    "kind": "rule-slot",
                    "slotTemplateId": "source-bin"
                  },
                  "sourceRange": {
                    "start": 0,
                    "length": 1
                  },
                  "targetRegionId": "general-output",
                  "targetOffset": 1,
                  "overlapPolicy": "reject",
                  "reason": "Reviewed General Merge v2 mapping."
                }
              ],
              "accessEnvelope": {
                "allowedRegionIds": ["general-output"],
                "maximumMappingCount": 1,
                "maximumTotalWriteBytes": 1,
                "protectedRangePolicy": "parent-profile"
              },
              "validationRuleIds": [],
              "processorStageIds": [],
              "owner": "firmware-owner",
              "reviewers": ["architecture-reviewer"],
              "evidenceRefs": ["initializer-evidence"]
            }
            """)!.AsObject();
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
