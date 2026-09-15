using NvtFwCombiner.Application.Configuration;

namespace NvtFwCombiner.Application.Tests.Configuration;

/// <summary>Behavioral coverage of scoped configuration admission without firmware execution authority.</summary>
public sealed class EventBufferFormatConfigurationAdmissionTests
{
    private const string Scope = "test-ab-scope";
    private static readonly EventBufferFormatIdentity[] Catalog =
    [new("format-a", "Format A"), new("format-b", "Format B")];

    /// <summary>Verifies recognition values are an or set and removal does not change an earlier snapshot.</summary>
    [Fact]
    public void RecognitionValuesAreAnOrSetAndRemovalDoesNotChangeAnEarlierSnapshot()
    {
        EventBufferFormatConfiguration original = Admit(new EventBufferFormatDraftEntry("format-a", "Custom", [0x97, 0xA6]));
        EventBufferFormatConfiguration updated = Admit(new EventBufferFormatDraftEntry("format-a", "Custom", [0xA6, 0x98]));

        Assert.Equal("format-a", original.Match(Scope, 0x97)!.UniqueId);
        Assert.Equal("format-a", original.Match(Scope, 0xA6)!.UniqueId);
        Assert.Null(updated.Match(Scope, 0x97));
        Assert.Equal("format-a", updated.Match(Scope, 0x98)!.UniqueId);
        Assert.Equal("format-a", original.Match(Scope, 0x97)!.UniqueId);
    }

    /// <summary>Verifies alias changes only the display name.</summary>
    [Theory]
    [InlineData(null, "Format A")]
    [InlineData("", "Format A")]
    [InlineData("   ", "Format A")]
    [InlineData("Customer label", "Customer label")]
    public void AliasChangesOnlyTheDisplayName(string? alias, string expectedLabel)
    {
        EventBufferFormatConfiguration config = Admit(new EventBufferFormatDraftEntry("format-a", alias, [0x97]));
        EventBufferFormatEntry entry = Assert.Single(config.Entries);

        Assert.Equal("format-a", entry.UniqueId);
        Assert.Equal(expectedLabel, entry.DisplayName);
        Assert.Equal(alias, entry.AliasName);
        Assert.Same(entry, config.Match(Scope, 0x97));
    }

    /// <summary>Verifies both byte boundaries are valid.</summary>
    [Fact]
    public void BothByteBoundariesAreValid()
    {
        EventBufferFormatConfiguration config = Admit(new EventBufferFormatDraftEntry("format-a", null, [255, 0]));

        Assert.Equal<byte>([0, 255], Assert.Single(config.Entries).RecognitionValues);
        Assert.NotNull(config.Match(Scope, byte.MinValue));
        Assert.NotNull(config.Match(Scope, byte.MaxValue));
    }

    /// <summary>Verifies out of range values are rejected rather than truncated.</summary>
    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    [InlineData(int.MaxValue)]
    public void OutOfRangeValuesAreRejectedRatherThanTruncated(int value)
    {
        EventBufferFormatConfigurationIssue issue = Reject(new EventBufferFormatDraftEntry("format-a", null, [value]));

        Assert.Equal(EventBufferFormatConfigurationIssueCode.ValueOutOfRange, issue.Code);
        Assert.Equal(value, issue.RecognitionValue);
        Assert.Equal(0, issue.EntryIndex);
    }

    /// <summary>Verifies identity must exactly match the supplied scoped catalog.</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("FORMAT-A")]
    [InlineData(null)]
    public void IdentityMustExactlyMatchTheSuppliedScopedCatalog(string? identity)
    {
        Assert.Equal(EventBufferFormatConfigurationIssueCode.UnknownIdentity,
            Reject(new EventBufferFormatDraftEntry(identity, null, [0x97])).Code);
    }

    /// <summary>Verifies duplicate byte membership is not silently deduplicated.</summary>
    [Fact]
    public void DuplicateByteMembershipIsNotSilentlyDeduplicated()
    {
        Assert.Equal(EventBufferFormatConfigurationIssueCode.DuplicateValue,
            Reject(new EventBufferFormatDraftEntry("format-a", null, [0x97, 0x97])).Code);
    }

    /// <summary>Verifies conflicting identity assignments name both entries.</summary>
    [Fact]
    public void ConflictingIdentityAssignmentsNameBothEntries()
    {
        EventBufferFormatConfigurationIssue issue = Reject(
            new("format-a", null, [0x97]), new("format-b", null, [0x97]));

        Assert.Equal(EventBufferFormatConfigurationIssueCode.ConflictingValue, issue.Code);
        Assert.Equal(1, issue.EntryIndex);
        Assert.Equal(0, issue.RelatedEntryIndex);
        Assert.Equal(0x97, issue.RecognitionValue);
    }

    /// <summary>Verifies duplicate identity entries are rejected even with disjoint values.</summary>
    [Fact]
    public void DuplicateIdentityEntriesAreRejectedEvenWithDisjointValues()
    {
        Assert.Equal(EventBufferFormatConfigurationIssueCode.DuplicateIdentity,
            Reject(new EventBufferFormatDraftEntry("format-a", "First", [0x97]), new("format-a", "Second", [0xA6])).Code);
    }

    /// <summary>Verifies no match and different scope never invent afallback identity.</summary>
    [Fact]
    public void NoMatchAndDifferentScopeNeverInventAFallbackIdentity()
    {
        EventBufferFormatConfiguration config = Admit(new EventBufferFormatDraftEntry("format-a", null, [0x97]));

        Assert.Null(config.Match(Scope, 0x84));
        Assert.Null(config.Match("other-scope", 0x97));
        Assert.Null(config.Match("TEST-AB-SCOPE", 0x97));
        Assert.Equal(Scope, config.ScopeId);
    }

    /// <summary>Verifies empty value sets and empty configuration match nothing.</summary>
    [Fact]
    public void EmptyValueSetsAndEmptyConfigurationMatchNothing()
    {
        Assert.Null(Admit(new EventBufferFormatDraftEntry("format-a", null, [])).Match(Scope, 0x97));
        EventBufferFormatConfiguration empty = Admit();
        Assert.Empty(empty.Entries);
        Assert.Null(empty.Match(Scope, 0));
    }

    /// <summary>Verifies missing entries and missing value lists are not treated as empty sets.</summary>
    [Fact]
    public void MissingEntriesAndMissingValueListsAreNotTreatedAsEmptySets()
    {
        EventBufferFormatConfigurationAdmissionResult result =
            EventBufferFormatConfigurationAdmission.Admit(Scope, Catalog, null);
        Assert.Null(result.Configuration);
        Assert.Equal(EventBufferFormatConfigurationIssueCode.MissingEntries,
            Assert.Single(result.Issues).Code);
        Assert.Equal(EventBufferFormatConfigurationIssueCode.MissingValues,
            Reject(new EventBufferFormatDraftEntry("format-a", null, null)).Code);
        Assert.Equal(EventBufferFormatConfigurationIssueCode.MissingEntry,
            Reject((EventBufferFormatDraftEntry?)null).Code);
    }

    /// <summary>Verifies admitted order is deterministic without changing the draft.</summary>
    [Fact]
    public void AdmittedOrderIsDeterministicWithoutChangingTheDraft()
    {
        int[] values = [0xA6, 0x97];
        EventBufferFormatConfiguration config = Admit(
            new("format-b", "B", values), new("format-a", "A", [0xFF, 0]));

        Assert.Equal(["format-a", "format-b"], config.Entries.Select(entry => entry.UniqueId));
        Assert.Equal<byte>([0, 0xFF], config.Entries[0].RecognitionValues);
        Assert.Equal<byte>([0x97, 0xA6], config.Entries[1].RecognitionValues);
        Assert.Equal([0xA6, 0x97], values);
    }

    /// <summary>Verifies snapshot does not retain mutable draft or catalog collections.</summary>
    [Fact]
    public void SnapshotDoesNotRetainMutableDraftOrCatalogCollections()
    {
        int[] values = [0x97];
        EventBufferFormatDraftEntry?[] draft = [new("format-a", null, values)];
        EventBufferFormatIdentity[] catalog = [new("format-a", "Original")];
        EventBufferFormatConfigurationAdmissionResult result =
            EventBufferFormatConfigurationAdmission.Admit(Scope, catalog, draft);
        EventBufferFormatConfiguration config = Assert.IsType<EventBufferFormatConfiguration>(result.Configuration);

        values[0] = 0xA6;
        draft[0] = new("other", "Changed", []);
        catalog[0] = new("other", "Changed");

        Assert.Equal("Original", config.Match(Scope, 0x97)!.DisplayName);
        Assert.Null(config.Match(Scope, 0xA6));
        Assert.Equal<byte>([0x97], Assert.Single(config.Entries).RecognitionValues);
        _ = Assert.Throws<NotSupportedException>(() =>
            ((IList<byte>)config.Entries[0].RecognitionValues)[0] = 0);
        _ = Assert.Throws<NotSupportedException>(() =>
            ((IList<EventBufferFormatEntry>)config.Entries).Clear());
    }

    private static EventBufferFormatConfiguration Admit(params EventBufferFormatDraftEntry?[] entries)
    {
        EventBufferFormatConfigurationAdmissionResult result =
            EventBufferFormatConfigurationAdmission.Admit(Scope, Catalog, entries);
        Assert.Empty(result.Issues);
        return Assert.IsType<EventBufferFormatConfiguration>(result.Configuration);
    }

    private static EventBufferFormatConfigurationIssue Reject(params EventBufferFormatDraftEntry?[] entries)
    {
        EventBufferFormatConfigurationAdmissionResult result =
            EventBufferFormatConfigurationAdmission.Admit(Scope, Catalog, entries);
        Assert.Null(result.Configuration);
        return Assert.Single(result.Issues);
    }
}
