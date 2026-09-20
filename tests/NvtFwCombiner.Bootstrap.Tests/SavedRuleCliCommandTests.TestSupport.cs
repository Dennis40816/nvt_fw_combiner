using System.Text.Json.Nodes;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class SavedRuleCliCommandTests
{
    private static string ValidGeneralMergeRuleV1Json()
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
              "inputSlotTemplates": [],
              "mappingRows": [],
              "operationFragments": [],
              "validationRuleIds": [],
              "owner": "firmware-owner",
              "evidenceRefs": []
            }
            """;
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
                "bundleVersion": "1.1.10-full-image-metadata.1",
                "bundleContentHash": "101a97d0101abae37e27568f21b7af9c491ef47af3d26b0a6d5d4f5cb0945a05",
                "profileId": "nt51950-general-merge-logical-candidate",
                "profileVersion": "0.1.0",
                "profileContentHash": "af9b5f2d3270cbcd920b69dca30361f2509587d33b26070f301419dab5b3d795",
                "familyId": "nt51950-nt51951-dp-perspective",
                "familyVersion": "1.4.1",
                "familyContentHash": "17b04684520efb5297e8096dec5fe20e57532e1ae1cc80ac26d6b86c27cdfb54",
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

    private static async Task<string> WriteRuleAsync(
        TempWorkspace workspace,
        JsonObject json,
        string fileName = "rule.json")
    {
        string rule = workspace.PathFor(fileName);
        await File.WriteAllTextAsync(
            rule,
            json.ToJsonString(),
            TestContext.Current.CancellationToken);
        return rule;
    }

    private static Task<CliRunResult> RunCliAsync(string[] args)
    {
        return CliTestHarness.RunAsync(args, TestContext.Current.CancellationToken);
    }
}
