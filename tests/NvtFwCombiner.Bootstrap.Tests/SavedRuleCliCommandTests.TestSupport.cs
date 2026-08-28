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
                "bundleVersion": "0.10.5-family-reference.1",
                "bundleContentHash": "2891a94e01bf07d68bc751599038ae88eeb95c9d1f705722912fbd55bf8806ec",
                "profileId": "nt51950-general-merge-logical-candidate",
                "profileVersion": "0.1.0",
                "profileContentHash": "5b431b9498b534945ee15fbc56095a87f30cbc7da0cabe10554d678a2ce48c0c",
                "familyId": "nt51950-nt51951-dp-perspective",
                "familyVersion": "1.4.0",
                "familyContentHash": "02597d709affd69adfbd92fac4a9a75f245385fb7c0954a5de1c86035e7babf6",
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
