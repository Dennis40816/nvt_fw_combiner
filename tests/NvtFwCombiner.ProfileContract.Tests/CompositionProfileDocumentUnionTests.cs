using System.Text.Json;
using NvtFwCombiner.Contracts.Profiles;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Locks every closed composition-profile-v2 union transport shape.</summary>
public sealed class CompositionProfileDocumentUnionTests
{
    /// <summary>Verifies input, capacity, initializer, space, and view unions omit unrelated fields.</summary>
    [Fact]
    public void InputAndSpaceUnionsRoundTripOnlyDeclaredMembers()
    {
        CompositionProfileLengthRuleDocument[] lengthRules = Deserialize<CompositionProfileLengthRuleDocument[]>(
                                 /*lang=json,strict*/
                                 """
            [
              { "kind": "exact-bytes", "bytes": 16 },
              { "kind": "exact-resolved-map-capacity" },
              { "kind": "bounded", "minimumBytes": 4, "maximumBytes": 32 },
              { "kind": "normal-dp-extract-with-warning", "issueCode": "DP_SIZE_WARNING" },
              { "kind": "tp-maximum-256k", "maximumBytes": 262144 }
            ]
            """);
        CompositionProfileInputNormalizationDocument[] normalizations =
            Deserialize<CompositionProfileInputNormalizationDocument[]>(
                                     /*lang=json,strict*/
                                     """
                [
                  { "kind": "none" },
                  { "kind": "pad-shorter", "fillByte": 255, "evidenceRef": "padding-evidence" },
                  {
                    "kind": "truncate-ctrlram",
                    "warningIssueCode": "CTRLRAM_TRUNCATED",
                    "evidenceRef": "truncation-evidence"
                  }
                ]
                """);
        CompositionProfileCapacityDocument[] capacities = Deserialize<CompositionProfileCapacityDocument[]>(
                                 /*lang=json,strict*/
                                 """
            [
              { "kind": "resolved-map" },
              { "kind": "fixed", "bytes": 64 }
            ]
            """);
        CompositionProfileInitializerDocument[] initializers = Deserialize<CompositionProfileInitializerDocument[]>(
                                 /*lang=json,strict*/
                                 """
            [
              { "kind": "blank", "fillByte": 255 },
              { "kind": "clone", "sourceSlotId": "reference-input" }
            ]
            """);
        CompositionProfileSpaceDocument[] spaces = Deserialize<CompositionProfileSpaceDocument[]>(
                                 /*lang=json,strict*/
                                 """
            [
              {
                "spaceId": "source",
                "kind": "input-artifact",
                "slotId": "source-input",
                "instancePolicy": "singleton"
              },
              {
                "spaceId": "output",
                "kind": "output-image",
                "capacity": { "kind": "resolved-map" },
                "initializer": { "kind": "blank", "fillByte": 255 }
              }
            ]
            """);
        CompositionProfileViewSelectorDocument[] selectors =
            Deserialize<CompositionProfileViewSelectorDocument[]>(
                                     /*lang=json,strict*/
                                     """
                [
                  { "kind": "map-region", "regionId": "dp-code" },
                  { "kind": "map-region-slice", "regionId": "dp-code", "offset": 4, "length": 8 },
                  { "kind": "space-range", "range": { "start": 12, "length": 4 } }
                ]
                """);

        using JsonDocument lengthJson = SerializeToDocument(lengthRules);
        AssertPropertyNames(lengthJson.RootElement[0], "bytes", "kind");
        AssertPropertyNames(lengthJson.RootElement[1], "kind");
        AssertPropertyNames(lengthJson.RootElement[2], "kind", "maximumBytes", "minimumBytes");
        AssertPropertyNames(lengthJson.RootElement[3], "issueCode", "kind");
        AssertPropertyNames(lengthJson.RootElement[4], "kind", "maximumBytes");

        using JsonDocument normalizationJson = SerializeToDocument(normalizations);
        AssertPropertyNames(normalizationJson.RootElement[0], "kind");
        AssertPropertyNames(normalizationJson.RootElement[1], "evidenceRef", "fillByte", "kind");
        AssertPropertyNames(
            normalizationJson.RootElement[2],
            "evidenceRef",
            "kind",
            "warningIssueCode");

        using JsonDocument capacityJson = SerializeToDocument(capacities);
        AssertPropertyNames(capacityJson.RootElement[0], "kind");
        AssertPropertyNames(capacityJson.RootElement[1], "bytes", "kind");

        using JsonDocument initializerJson = SerializeToDocument(initializers);
        AssertPropertyNames(initializerJson.RootElement[0], "fillByte", "kind");
        AssertPropertyNames(initializerJson.RootElement[1], "kind", "sourceSlotId");

        using JsonDocument spaceJson = SerializeToDocument(spaces);
        AssertPropertyNames(spaceJson.RootElement[0], "instancePolicy", "kind", "slotId", "spaceId");
        AssertPropertyNames(spaceJson.RootElement[1], "capacity", "initializer", "kind", "spaceId");

        using JsonDocument selectorJson = SerializeToDocument(selectors);
        AssertPropertyNames(selectorJson.RootElement[0], "kind", "regionId");
        AssertPropertyNames(selectorJson.RootElement[1], "kind", "length", "offset", "regionId");
        AssertPropertyNames(selectorJson.RootElement[2], "kind", "range");
    }

    /// <summary>Verifies every operation and validation union retains exact typed JSON values.</summary>
    [Fact]
    public void OperationAndValidationUnionsRoundTripOnlyDeclaredMembers()
    {
        CompositionProfileOperationDocument[] operations = Deserialize<CompositionProfileOperationDocument[]>(
                                 /*lang=json,strict*/
                                 """
            [
              {
                "operationId": "copy",
                "sequence": 0,
                "overlapPolicy": "reject",
                "reason": "copy",
                "kind": "copy-range",
                "sourceViewId": "source",
                "targetViewId": "target"
              },
              {
                "operationId": "fill",
                "sequence": 1,
                "overlapPolicy": "replace-existing",
                "reason": "fill",
                "kind": "fill-range",
                "targetViewId": "target",
                "fillByte": 255
              },
              {
                "operationId": "patch",
                "sequence": 2,
                "overlapPolicy": "allow-declared",
                "reason": "patch",
                "kind": "patch-scalar",
                "targetViewId": "target",
                "valueHex": "0102"
              },
              {
                "operationId": "transform",
                "sequence": 3,
                "overlapPolicy": "replace-existing",
                "reason": "relocate",
                "kind": "transform-scalar",
                "sourceViewId": "source-scalar",
                "targetViewId": "target-scalar",
                "widthBytes": 4,
                "byteOrder": "little",
                "valueInterpretation": "unsigned",
                "addend": -16,
                "expectedBefore": 32,
                "overflowPolicy": "reject"
              },
              {
                "operationId": "postbuild",
                "sequence": 4,
                "overlapPolicy": "replace-existing",
                "reason": "postbuild",
                "kind": "run-processor",
                "processorStageId": "legacy-postbuild"
              }
            ]
            """);
        CompositionProfileValidationDocument[] validations =
            Deserialize<CompositionProfileValidationDocument[]>(
                                     /*lang=json,strict*/
                                     """
                [
                  {
                    "ruleId": "version",
                    "stage": "input-load",
                    "severity": "error",
                    "issueCode": "VERSION_INVALID",
                    "kind": "metadata-value",
                    "field": { "bindingId": "cmd", "fieldId": "major" },
                    "operator": "one-of",
                    "expectedValues": [1, 2]
                  },
                  {
                    "ruleId": "pid",
                    "stage": "input-load",
                    "severity": "error",
                    "issueCode": "PID_INVALID",
                    "kind": "pid-sanity",
                    "field": { "bindingId": "fwconfig", "fieldId": "pid" }
                  },
                  {
                    "ruleId": "parity",
                    "stage": "profile-compile",
                    "severity": "error",
                    "issueCode": "VERSION_MISMATCH",
                    "kind": "metadata-equality",
                    "left": { "bindingId": "cmd", "fieldId": "major" },
                    "right": { "bindingId": "legacy", "fieldId": "major" }
                  },
                  {
                    "ruleId": "identity",
                    "stage": "input-load",
                    "severity": "error",
                    "issueCode": "IDENTITY_INVALID",
                    "kind": "reject-metadata-byte-pattern",
                    "field": { "bindingId": "fwconfig", "fieldId": "pid" },
                    "rejectedPatterns": ["all-zero", "all-ff"]
                  },
                  {
                    "ruleId": "header",
                    "stage": "final-output",
                    "severity": "error",
                    "issueCode": "HEADER_INVALID",
                    "kind": "view-byte-assertion",
                    "viewId": "header",
                    "expectedHex": "a0",
                    "maskHex": "f0"
                  }
                ]
                """);

        Assert.Equal(-16, operations[3].Addend?.GetInt32());
        Assert.Equal(JsonValueKind.Number, validations[0].ExpectedValues![0].ValueKind);

        using JsonDocument operationJson = SerializeToDocument(operations);
        AssertPropertyNames(
            operationJson.RootElement[0],
            "kind", "operationId", "overlapPolicy", "reason", "sequence", "sourceViewId", "targetViewId");
        AssertPropertyNames(
            operationJson.RootElement[1],
            "fillByte", "kind", "operationId", "overlapPolicy", "reason", "sequence", "targetViewId");
        AssertPropertyNames(
            operationJson.RootElement[2],
            "kind", "operationId", "overlapPolicy", "reason", "sequence", "targetViewId", "valueHex");
        AssertPropertyNames(
            operationJson.RootElement[3],
            "addend", "byteOrder", "expectedBefore", "kind", "operationId", "overflowPolicy",
            "overlapPolicy", "reason", "sequence", "sourceViewId", "targetViewId",
            "valueInterpretation", "widthBytes");
        AssertPropertyNames(
            operationJson.RootElement[4],
            "kind", "operationId", "overlapPolicy", "processorStageId", "reason", "sequence");

        using JsonDocument validationJson = SerializeToDocument(validations);
        AssertPropertyNames(
            validationJson.RootElement[0],
            "expectedValues", "field", "issueCode", "kind", "operator", "ruleId", "severity", "stage");
        AssertPropertyNames(
            validationJson.RootElement[1],
            "field", "issueCode", "kind", "ruleId", "severity", "stage");
        AssertPropertyNames(
            validationJson.RootElement[2],
            "issueCode", "kind", "left", "right", "ruleId", "severity", "stage");
        AssertPropertyNames(
            validationJson.RootElement[3],
            "field", "issueCode", "kind", "rejectedPatterns", "ruleId", "severity", "stage");
        AssertPropertyNames(
            validationJson.RootElement[4],
            "expectedHex", "issueCode", "kind", "maskHex", "ruleId", "severity", "stage", "viewId");
    }

    /// <summary>Verifies CRC and legacy processor unions cannot invent one another's fields.</summary>
    [Fact]
    public void ProcessorUnionsRoundTripOnlyDeclaredMembers()
    {
        CompositionProfileProcessorStageDocument[] processors =
            Deserialize<CompositionProfileProcessorStageDocument[]>(
                                     /*lang=json,strict*/
                                     """
                [
                  {
                    "processorStageId": "crc-check",
                    "kind": "crc-worker-v1",
                    "contractVersion": "1.0.0",
                    "calculationSetId": "display-crc",
                    "targetSpaceId": "output",
                    "authority": "calculate",
                    "purpose": "checksum",
                    "integrityDisposition": "verify-existing",
                    "allowedReadViewIds": ["output-image"],
                    "allowedWriteViewIds": [],
                    "failurePolicy": "fail-closed"
                  },
                  {
                    "processorStageId": "legacy-postbuild",
                    "kind": "legacy-combiner-v1",
                    "toolBindingId": "combiner-1-13",
                    "invocationProfileId": "nt51950-ab-b-code",
                    "targetSpaceId": "output",
                    "authority": "transform",
                    "purpose": "header-and-integrity",
                    "integrityDisposition": "recalculate-and-write",
                    "allowedReadViewIds": ["output-image"],
                    "allowedWriteViewIds": ["header", "crc"],
                    "stagedSourceBindings": [
                      { "sourceViewId": "tp-source", "targetViewId": "staged-tp" }
                    ],
                    "evidenceRef": "combiner-evidence",
                    "failurePolicy": "fail-closed"
                  }
                ]
                """);

        Assert.Equal("1.0.0", processors[0].ContractVersion);
        Assert.Null(processors[0].ToolBindingId);
        Assert.Equal("combiner-1-13", processors[1].ToolBindingId);
        Assert.Null(processors[1].CalculationSetId);
        Assert.Equal("tp-source", Assert.Single(processors[1].StagedSourceBindings!).SourceViewId);

        using JsonDocument processorJson = SerializeToDocument(processors);
        AssertPropertyNames(
            processorJson.RootElement[0],
            "allowedReadViewIds", "allowedWriteViewIds", "authority", "calculationSetId",
            "contractVersion", "failurePolicy", "integrityDisposition", "kind", "processorStageId",
            "purpose", "targetSpaceId");
        AssertPropertyNames(
            processorJson.RootElement[1],
            "allowedReadViewIds", "allowedWriteViewIds", "authority", "evidenceRef", "failurePolicy",
            "integrityDisposition", "invocationProfileId", "kind", "processorStageId", "purpose",
            "stagedSourceBindings", "targetSpaceId", "toolBindingId");
    }

    private static T Deserialize<T>(string json)
    {
        return Assert.IsType<T>(JsonSerializer.Deserialize<T>(
            json,
            CompositionProfileDocumentTests.StrictOptions()));
    }

    private static JsonDocument SerializeToDocument<T>(T value)
    {
        return JsonDocument.Parse(JsonSerializer.Serialize(
            value,
            CompositionProfileDocumentTests.StrictOptions()));
    }

    private static void AssertPropertyNames(JsonElement element, params string[] expectedNames)
    {
        Assert.Equal(
            expectedNames.Order(StringComparer.Ordinal),
            element.EnumerateObject().Select(static property => property.Name).Order(StringComparer.Ordinal));
    }
}
