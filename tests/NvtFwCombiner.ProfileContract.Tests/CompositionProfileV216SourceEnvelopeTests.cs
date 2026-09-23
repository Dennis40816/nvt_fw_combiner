using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>Checks source-envelope DTO normalization and closed profile graph ownership.</summary>
public sealed class CompositionProfileV216SourceEnvelopeTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Normalizing 2.16 retains the explicit template and source-sized output declaration.</summary>
    [Fact]
    public void StandardEnvelopeNormalizesExistingTemplateAndSourceSizedOutput()
    {
        CompositionProfileDefinition definition = CompositionProfileNormalizer.Normalize(Document());

        SourceEnvelopeProfileBinding binding = Assert.IsType<SourceEnvelopeProfileBinding>(
            definition.Header.SourceEnvelopeBinding);
        Assert.Equal("dp-input", binding.SourceSlotId);
        Assert.Equal("nt51950-standard-merge-256k", binding.LayoutTemplateMapId);
        Assert.Equal([0x40000, 0x80000, 0x100000], binding.ExpectedOuterLengths);
        Assert.Equal("DP_NONSTANDARD_SIZE_WARNING", binding.UnexpectedLengthIssueCode);
    }

    /// <summary>An optional complete source retains the existing FF blank image only when absent.</summary>
    [Fact]
    public void OptionalEnvelopeAcceptsExistingFfBlankInitializer()
    {
        CompositionProfileDefinition definition = CompositionProfileNormalizer.Normalize(
            Document(allowsAbsentSource: true, fillByte: 0xFF));

        Assert.True(Assert.IsType<SourceEnvelopeProfileBinding>(
            definition.Header.SourceEnvelopeBinding).AllowsAbsentSource);
    }

    /// <summary>The required source keeps zero-fill and optional source admits no arbitrary fill.</summary>
    [Theory]
    [InlineData(false, 0xFF)]
    [InlineData(true, 0x11)]
    public void EnvelopeRejectsUnapprovedBlankFill(bool allowsAbsentSource, int fillByte)
    {
        _ = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(Document(allowsAbsentSource, fillByte)));
    }

    /// <summary>Graph admission refuses a different output source slot or a pre-2.16 envelope.</summary>
    [Theory]
    [InlineData("wrong-output-slot")]
    [InlineData("old-schema")]
    public void EnvelopeRejectsMismatchedProfileContract(string mutation)
    {
        CompositionProfileDocument document = Document();
        document = mutation switch
        {
            "wrong-output-slot" => document with
            {
                Spaces = [.. document.Spaces.Select(space => space.Kind == "output-image"
                    ? space with { Capacity = space.Capacity! with { SourceSlotId = "tp-input" } }
                    : space)],
            },
            "old-schema" => document with { SchemaVersion = "2.15" },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation."),
        };

        _ = Assert.Throws<CompositionProfileNormalizationException>(() =>
            CompositionProfileNormalizer.Normalize(document));
    }

    private static CompositionProfileDocument Document(
        bool allowsAbsentSource = false, int fillByte = 0)
    {
        string path = RepositoryPaths.FromRepositoryRoot("profiles", "built-in",
            "nt51950-nt51951-standard-merge", "profiles", "nt51950-standard-merge.json");
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(path)));
        profile["schemaVersion"] = "2.16";
        profile["sourceEnvelopeBinding"] = new JsonObject
        {
            ["sourceSlotId"] = "dp-input",
            ["layoutTemplateMapId"] = "nt51950-standard-merge-256k",
            ["rootRegionId"] = "dp-container",
            ["whenSourceAbsent"] = allowsAbsentSource ? "resolved-map" : "reject",
            ["expectedOuterLengths"] = new JsonArray(0x40000, 0x80000, 0x100000),
            ["unexpectedLengthIssueCode"] = "DP_NONSTANDARD_SIZE_WARNING",
        };
        JsonObject output = Assert.Single(
            Assert.IsType<JsonArray>(profile["spaces"]).Select(static item => Assert.IsType<JsonObject>(item)),
            static space => space["kind"]?.GetValue<string>() == "output-image");
        output["capacity"] = new JsonObject { ["kind"] = "source-slot", ["sourceSlotId"] = "dp-input" };
        Assert.IsType<JsonObject>(output["initializer"])["fillByte"] = fillByte;
        if (allowsAbsentSource)
        {
            JsonObject dpSlot = Assert.Single(
                Assert.IsType<JsonArray>(profile["inputSlots"]).Select(static item => Assert.IsType<JsonObject>(item)),
                static slot => slot["slotId"]?.GetValue<string>() == "dp-input");
            dpSlot["required"] = false;
            dpSlot["cardinality"] = "zero-or-one";
        }
        return Assert.IsType<CompositionProfileDocument>(JsonSerializer.Deserialize<CompositionProfileDocument>(
            profile.ToJsonString(), s_jsonOptions));
    }
}
