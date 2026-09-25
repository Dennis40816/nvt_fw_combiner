using System.Text.Json;
using NvtFwCombiner.Contracts.Profiles;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles.V2;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.ProfileContract.Tests;

/// <summary>AB characteristics are declared once, never inferred from bank geometry.</summary>
public sealed class CompositionProfileV217AbTests
{
    private static readonly JsonSerializerOptions s_jsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Both explicit values survive normalization while older undeclared profiles remain valid.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    [InlineData(null)]
    public void ExplicitSymmetryOrAbsenceIsPreserved(bool? symmetric)
    {
        CompositionProfileDocument document = Document() with
        {
            Ab = symmetric is { } value ? new(value) : null,
        };
        CompositionProfileDefinition normalized = CompositionProfileNormalizer.Normalize(document);
        Assert.Equal(symmetric, normalized.Header.Ab?.IsFullySymmetric);
        Assert.Null(CompositionProfileNormalizer.Normalize(document with { SchemaVersion = "2.14", Ab = null }).Header.Ab);
    }

    /// <summary>Direct DTO callers cannot bypass the schema's declaration contract.</summary>
    [Theory]
    [InlineData("missing-boolean")]
    [InlineData("old-schema")]
    [InlineData("non-ab")]
    public void InvalidDeclarationIsRejected(string mutation)
    {
        CompositionProfileDocument document = Document();
        document = mutation switch
        {
            "missing-boolean" => document with { Ab = new(null) },
            "old-schema" => document with { SchemaVersion = "2.16" },
            "non-ab" => document with { Experience = document.Experience with { ExperienceId = ExperienceIds.StandardMerge } },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation)),
        };
        _ = Assert.Throws<CompositionProfileNormalizationException>(() => CompositionProfileNormalizer.Normalize(document));
    }

    private static CompositionProfileDocument Document()
    {
        return JsonSerializer.Deserialize<CompositionProfileDocument>(
            File.ReadAllText(RepositoryPaths.FromRepositoryRoot("profiles", "built-in",
                "nt51919-nt51929-nt51932-ab-merge", "profiles", "nt51929-ab-merge.json")), s_jsonOptions)!;
    }
}
