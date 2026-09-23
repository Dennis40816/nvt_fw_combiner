using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;

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

    private static CompositionProfileDocument Document()
    {
        string path = FindRepositoryFile();
        JsonObject profile = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(path)));
        profile["schemaVersion"] = "2.16";
        profile["sourceEnvelopeBinding"] = new JsonObject
        {
            ["sourceSlotId"] = "dp-input",
            ["layoutTemplateMapId"] = "nt51950-standard-merge-256k",
            ["rootRegionId"] = "dp-container",
            ["whenSourceAbsent"] = "reject",
            ["expectedOuterLengths"] = new JsonArray(0x40000, 0x80000, 0x100000),
            ["unexpectedLengthIssueCode"] = "DP_NONSTANDARD_SIZE_WARNING",
        };
        JsonObject output = Assert.Single(
            Assert.IsType<JsonArray>(profile["spaces"]).Select(static item => Assert.IsType<JsonObject>(item)),
            static space => space["kind"]?.GetValue<string>() == "output-image");
        output["capacity"] = new JsonObject { ["kind"] = "source-slot", ["sourceSlotId"] = "dp-input" };
        return Assert.IsType<CompositionProfileDocument>(JsonSerializer.Deserialize<CompositionProfileDocument>(
            profile.ToJsonString(), s_jsonOptions));
    }

    private static string FindRepositoryFile()
    {
        for (DirectoryInfo? directory = new(AppContext.BaseDirectory);
             directory is not null; directory = directory.Parent)
        {
            string path = Path.Combine(directory.FullName, "profiles", "built-in",
                "nt51950-nt51951-standard-merge", "profiles", "nt51950-standard-merge.json");
            if (File.Exists(path))
            {
                return path;
            }
        }

        throw new InvalidOperationException("Canonical profile fixture not found.");
    }
}
