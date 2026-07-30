using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;
using static NvtFwCombiner.Bootstrap.SavedRuleIssueCodes;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Verifies Saved Rule v2 closes over the exact General Merge initializer.</summary>
public sealed class SavedRuleV2GeneralMergeInitializerTests
{
    /// <summary>Omitted fill round-trips as the canonical zero default.</summary>
    [Fact]
    public void OmittedFillLoadsAsZeroAndRoundTrips()
    {
        using var document = JsonDocument.Parse(RuleJson(
            /*lang=json,strict*/ """{ "kind": "blank", "capacity": 16 }"""));

        SavedRuleV2GeneralMergeInitializerLoadResult load =
            SavedRuleV2GeneralMergeInitializerLoader.Parse(document.RootElement);

        GeneralMergeOutputInitializer initializer =
            Assert.IsType<GeneralMergeOutputInitializer>(load.Initializer);
        Assert.True(load.IsValid);
        Assert.Equal(16, initializer.Capacity);
        Assert.Equal(0x00, initializer.FillByte);

        JsonElement serialized =
            SavedRuleV2GeneralMergeInitializerLoader.Serialize(initializer);
        Assert.Equal("blank", serialized.GetProperty("kind").GetString());
        Assert.Equal(16, serialized.GetProperty("capacity").GetInt64());
        Assert.Equal(0, serialized.GetProperty("fillByte").GetInt32());
    }

    /// <summary>Explicit FF remains exact through the versioned loader.</summary>
    [Fact]
    public void ExplicitFillLoadsExactly()
    {
        using var document = JsonDocument.Parse(RuleJson(
            /*lang=json,strict*/ """{ "kind": "blank", "capacity": 16, "fillByte": 255 }"""));

        SavedRuleV2GeneralMergeInitializerLoadResult load =
            SavedRuleV2GeneralMergeInitializerLoader.Parse(document.RootElement);

        Assert.True(load.IsValid);
        Assert.Equal(0xFF, load.Initializer!.FillByte);
    }

    /// <summary>The loaded v2 initializer reaches the existing compiler and executor unchanged.</summary>
    [Fact]
    public async Task LoadedInitializerCompilesThroughCanonicalGeneralMergePath()
    {
        using var document = JsonDocument.Parse(RuleJson(
            /*lang=json,strict*/ """{ "kind": "blank", "capacity": 4, "fillByte": 255 }"""));
        SavedRuleV2GeneralMergeInitializerLoadResult load =
            SavedRuleV2GeneralMergeInitializerLoader.Parse(document.RootElement);
        using var workspace = TempWorkspace.Create();
        string source = workspace.Write("source.bin", [0x10]);
        string output = workspace.PathFor("output.bin");
        var mappings = new GeneralMappingDraftState(
        [
            new GeneralMappingDraftRow(
                "saved-rule-copy",
                ExplicitMappingOperationKind.CopyRange,
                GeneralMappingSource.File(source),
                new ByteRange(0, 1),
                CompositionAddressSpaceIds.OutputImage,
                new ByteRange(1, 1),
                OverlapPolicy.Reject,
                alignment: 1,
                "Saved Rule v2 initializer round-trip.",
                WorkbenchGeneralMergeIds.OutputRegionId),
        ]);

        WorkbenchRunResult result =
            await WorkbenchCompositionService.RunGeneralMergeDraftAsync(
                "NT51950",
                new GeneralMergeDraftState(
                    load.Initializer!,
                    mappings),
                build: true,
                TestContext.Current.CancellationToken,
                output);

        Assert.True(result.Succeeded);
        Assert.Equal(
            [0xFF, 0x10, 0xFF, 0xFF],
            await File.ReadAllBytesAsync(
                output,
                TestContext.Current.CancellationToken));
    }

    /// <summary>Malformed or incomplete initializer shapes fail closed with stable issues.</summary>
    [Theory]
    [InlineData(/*lang=json,strict*/ """{ "kind": "blank" }""", InitializerCapacityInvalid)]
    [InlineData(/*lang=json,strict*/ """{ "kind": "blank", "capacity": 0 }""", InitializerCapacityInvalid)]
    [InlineData(/*lang=json,strict*/ """{ "kind": "blank", "capacity": 16, "fillByte": 256 }""", InitializerFillByteInvalid)]
    [InlineData(/*lang=json,strict*/ """{ "kind": "blank", "capacity": 16, "unknown": true }""", PropertyUnknown)]
    public void InvalidInitializerIsRejected(string initializerJson, string expectedCode)
    {
        using var document = JsonDocument.Parse(RuleJson(initializerJson));

        SavedRuleV2GeneralMergeInitializerLoadResult load =
            SavedRuleV2GeneralMergeInitializerLoader.Parse(document.RootElement);

        Assert.False(load.IsValid);
        Assert.Contains(load.Issues, issue => issue.Code == expectedCode);
    }

    /// <summary>General Merge cannot omit the complete initializer object.</summary>
    [Fact]
    public void MergeRuleRejectsMissingInitializer()
    {
        using var document = JsonDocument.Parse(
            /*lang=json,strict*/ """
            {
              "schemaVersion": "2.0",
              "compositionKind": "merge",
              "sourceExperienceId": "general-merge"
            }
            """);

        SavedRuleV2GeneralMergeInitializerLoadResult load =
            SavedRuleV2GeneralMergeInitializerLoader.Parse(document.RootElement);

        Assert.False(load.IsValid);
        Assert.Contains(load.Issues, issue => issue.Code == InitializerRequired);
    }

    /// <summary>General Replace cannot declare a blank-output initializer.</summary>
    [Fact]
    public void ReplaceRuleRejectsGeneralMergeInitializer()
    {
        using var document = JsonDocument.Parse(
            RuleJson(
                /*lang=json,strict*/ """{ "kind": "blank", "capacity": 16 }""",
                compositionKind: "replace",
                sourceExperienceId: "general-replace"));

        SavedRuleV2GeneralMergeInitializerLoadResult load =
            SavedRuleV2GeneralMergeInitializerLoader.Parse(document.RootElement);

        Assert.False(load.IsValid);
        Assert.Contains(load.Issues, issue => issue.Code == InitializerForbidden);
    }

    private static string RuleJson(
        string initializerJson,
        string compositionKind = "merge",
        string sourceExperienceId = "general-merge")
    {
        return $$"""
            {
              "schemaVersion": "2.0",
              "ruleId": "initializer-contract-test",
              "ruleVersion": "1.0.0",
              "displayName": "Initializer contract test",
              "compositionKind": "{{compositionKind}}",
              "sourceExperienceId": "{{sourceExperienceId}}",
              "imageInitialization": {{initializerJson}},
              "parentBinding": {},
              "promotion": {},
              "slotTemplates": [],
              "mappingFragments": [],
              "accessEnvelope": {},
              "validationRuleIds": [],
              "processorStageIds": [],
              "owner": "test-owner",
              "reviewers": [],
              "evidenceRefs": ["test-evidence"]
            }
            """;
    }
}
