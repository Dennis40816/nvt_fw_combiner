using NvtFwCombiner.Infrastructure.Bundles;

namespace NvtFwCombiner.Infrastructure.Tests.Bundles;

/// <summary>Locks the published canonical candidate-root entry hash projection.</summary>
public sealed class CandidateEvidenceEntryArrayHasherTests
{
    /// <summary>Matches the RFC 8785 candidate-entry-array known-answer vector regardless of caller order.</summary>
    [Fact]
    public void CalculateContentHashMatchesPublishedKnownAnswerVector()
    {
        string actual = CandidateEvidenceEntryArrayHasher.CalculateContentHash(
        [
            Entry("owner-record", "artifact", "artifacts/owner-record.txt", 'b', 2),
            Entry("candidate-schema", "schema", "schemas/candidate-evidence-v1.schema.json", 'a', 1),
        ]);

        Assert.Equal("37c0cad56b7fcf7cd033fac12eb2decd9e5a350bb1095988e998ecb0cd1167ef", actual);
    }

    /// <summary>Changes the root content identity when a hashed entry field changes.</summary>
    [Fact]
    public void CalculateContentHashIncludesEntrySize()
    {
        string baseline = CandidateEvidenceEntryArrayHasher.CalculateContentHash(
        [
            Entry("candidate-schema", "schema", "schemas/candidate-evidence-v1.schema.json", 'a', 1),
        ]);
        string changed = CandidateEvidenceEntryArrayHasher.CalculateContentHash(
        [
            Entry("candidate-schema", "schema", "schemas/candidate-evidence-v1.schema.json", 'a', 2),
        ]);

        Assert.NotEqual(baseline, changed);
    }

    private static CandidateEvidenceEntryHashInput Entry(
        string entryId,
        string kind,
        string path,
        char hashCharacter,
        int sizeBytes)
    {
        return new CandidateEvidenceEntryHashInput(
            entryId,
            kind,
            path,
            new string(hashCharacter, 64),
            sizeBytes);
    }
}
