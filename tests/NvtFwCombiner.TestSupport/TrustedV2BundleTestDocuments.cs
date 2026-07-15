using System.Text.Json;

namespace NvtFwCombiner.TestSupport;

/// <summary>Complete non-confidential canonical V2 document fixtures shared by trusted-bundle tests.</summary>
public static class TrustedV2BundleTestDocuments
{
    /// <summary>Returns one immutable firmware-family-v1 JSON tree with one map-owned physical region.</summary>
    public static JsonElement Family(
        string familyId = "family",
        string familyVersion = "1.0.0",
        string mapId = "map")
    {
        using var document = JsonDocument.Parse(FamilyJson(familyId, familyVersion, mapId));
        return document.RootElement.Clone();
    }

    /// <summary>Returns one immutable composition-profile-v2 JSON tree bound to one exact family/map identity.</summary>
    public static JsonElement Profile(
        string familyContentHash,
        string profileId = "profile",
        string profileVersion = "1.0.0",
        string familyId = "family",
        string familyVersion = "1.0.0",
        string mapId = "map")
    {
        using var document = JsonDocument.Parse(ProfileJson(
            familyContentHash,
            profileId,
            profileVersion,
            familyId,
            familyVersion,
            mapId));
        return document.RootElement.Clone();
    }

    /// <summary>Returns a complete family document as UTF-8-safe JSON text.</summary>
    public static string FamilyJson(
        string familyId = "family",
        string familyVersion = "1.0.0",
        string mapId = "map")
    {
        return $$"""
            {
              "schemaVersion": "1.1",
              "familyId": "{{familyId}}",
              "familyVersion": "{{familyVersion}}",
              "members": [
                { "memberId": "NT00001", "displayName": "Synthetic IC" }
              ],
              "capabilities": [],
              "regionSets": [
                {
                  "regionSetId": "physical",
                  "addressSpaceId": "flash",
                  "regions": [
                    {
                      "regionId": "root",
                      "owner": "system",
                      "kind": "image",
                      "range": { "start": 0, "length": 16 },
                      "writeConstraint": "forbidden",
                      "alignment": 1
                    }
                  ],
                  "evidenceRefs": ["region-evidence"]
                }
              ],
              "metadataSets": [],
              "imageMaps": [
                {
                  "mapId": "{{mapId}}",
                  "addressSpaceId": "flash",
                  "applicability": {
                    "memberIds": ["NT00001"],
                    "modeIds": ["standard"],
                    "topologyRequirement": { "kind": "none" },
                    "capacityBytes": 16
                  },
                  "coveragePolicy": "complete-with-explicit-gaps",
                  "regionSetIds": ["physical"],
                  "metadataSetIds": [],
                  "evidenceRefs": ["map-evidence"]
                }
              ],
              "factAliases": [],
              "evidenceRefs": ["family-evidence"]
            }
            """;
    }

    /// <summary>Returns a complete profile document as UTF-8-safe JSON text.</summary>
    public static string ProfileJson(
        string familyContentHash,
        string profileId = "profile",
        string profileVersion = "1.0.0",
        string familyId = "family",
        string familyVersion = "1.0.0",
        string mapId = "map")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(familyContentHash);
        return $$"""
            {
              "schemaVersion": "2.0",
              "profileId": "{{profileId}}",
              "profileVersion": "{{profileVersion}}",
              "promotion": {
                "stage": "known",
                "blockers": [
                  {
                    "blockerId": "golden-missing",
                    "kind": "golden",
                    "reason": "Synthetic profile has no owner-approved golden.",
                    "evidenceRefs": []
                  }
                ]
              },
              "compositionKind": "merge",
              "experience": {
                "experienceId": "display-merge",
                "audience": "system",
                "layoutPolicy": "fixed",
                "inputPolicy": "fixed",
                "topologyAuthoring": "hidden",
                "displayNameKey": "profile.synthetic.merge"
              },
              "mapBinding": {
                "familyId": "{{familyId}}",
                "familyVersion": "{{familyVersion}}",
                "familyContentHash": "{{familyContentHash}}",
                "mapIds": ["{{mapId}}"],
                "requiredRegionIds": ["root"],
                "requiredMetadataStructureIds": [],
                "requiredCapabilityIds": []
              },
              "inputSlots": [
                {
                  "slotId": "tp-input",
                  "role": "tp",
                  "artifactClass": "tp-firmware",
                  "required": true,
                  "cardinality": "exactly-one",
                  "acceptedExtensions": [".bin"],
                  "acceptance": {
                    "lengthRule": { "kind": "tp-maximum-256k", "maximumBytes": 262144 },
                    "normalization": { "kind": "none" }
                  }
                }
              ],
              "spaces": [
                {
                  "spaceId": "tp-source",
                  "kind": "input-artifact",
                  "slotId": "tp-input",
                  "instancePolicy": "singleton"
                },
                {
                  "spaceId": "output",
                  "kind": "output-image",
                  "capacity": { "kind": "resolved-map" },
                  "initializer": { "kind": "blank", "fillByte": 255 }
                }
              ],
              "views": [
                {
                  "viewId": "tp-code",
                  "spaceId": "tp-source",
                  "selector": { "kind": "map-region", "regionId": "root" }
                },
                {
                  "viewId": "output-code",
                  "spaceId": "output",
                  "selector": { "kind": "space-range", "range": { "start": 0, "length": 16 } }
                }
              ],
              "metadataBindings": [],
              "regionAccessRules": [
                {
                  "regionId": "root",
                  "access": "read-only",
                  "reason": "Synthetic source is immutable."
                }
              ],
              "operations": [
                {
                  "operationId": "copy-code",
                  "sequence": 0,
                  "overlapPolicy": "reject",
                  "reason": "Copy the declared source view.",
                  "kind": "copy-range",
                  "sourceViewId": "tp-code",
                  "targetViewId": "output-code"
                }
              ],
              "validations": [],
              "processorStages": [],
              "output": {
                "fileNameTemplate": "{original-name}_merged.bin",
                "allowOverride": false,
                "invalidCharacterPolicy": "replace-underscore",
                "requiredTokenIds": ["original-name"]
              },
              "evidenceRefs": ["synthetic-evidence"]
            }
            """;
    }
}
