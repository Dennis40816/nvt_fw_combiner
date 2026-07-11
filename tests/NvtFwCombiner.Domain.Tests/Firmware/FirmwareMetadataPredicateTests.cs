using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests typed metadata values and three-state applicability predicates.</summary>
public sealed class FirmwareMetadataPredicateTests
{
    /// <summary>Verifies scalar factories preserve closed value kinds.</summary>
    [Fact]
    public void ScalarFactoriesPreserveTypedValues()
    {
        var flag = FirmwareMetadataValue.FromFlag(true);
        var integer = FirmwareMetadataValue.FromInteger(2);
        var text = FirmwareMetadataValue.FromText("standard");
        var whitespace = FirmwareMetadataValue.FromText(" ");

        Assert.Equal(FirmwareMetadataValueKind.Flag, flag.Kind);
        Assert.True(flag.FlagValue);
        Assert.Equal(FirmwareMetadataValueKind.SignedInteger, integer.Kind);
        Assert.Equal(2, integer.IntegerValue);
        Assert.Equal(FirmwareMetadataValueKind.Text, text.Kind);
        Assert.Equal("standard", text.TextValue);
        Assert.Equal(" ", whitespace.TextValue);
    }

    /// <summary>Verifies equality distinguishes matching, conflicting, and missing facts.</summary>
    [Fact]
    public void EqualReturnsThreeStateResult()
    {
        var predicate = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromInteger(2)]);

        Assert.Equal(FirmwarePredicateResult.Missing, predicate.Evaluate(Facts()));
        Assert.Equal(FirmwarePredicateResult.Match, predicate.Evaluate(Facts(("chip-number", 2))));
        Assert.Equal(FirmwarePredicateResult.NoMatch, predicate.Evaluate(Facts(("chip-number", 1))));
    }

    /// <summary>Verifies not-equal and one-of comparisons use typed equality.</summary>
    [Fact]
    public void OtherComparisonsUseTypedEquality()
    {
        var notEqual = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.NotEqual,
            [FirmwareMetadataValue.FromInteger(1)]);
        var oneOf = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.OneOf,
            [FirmwareMetadataValue.FromInteger(2), FirmwareMetadataValue.FromInteger(3)]);

        Assert.Equal(FirmwarePredicateResult.Match, notEqual.Evaluate(Facts(("chip-number", 2))));
        Assert.Equal(FirmwarePredicateResult.NoMatch, oneOf.Evaluate(Facts(("chip-number", 1))));
        Assert.Equal(FirmwarePredicateResult.Match, oneOf.Evaluate(Facts(("chip-number", 3))));
    }

    /// <summary>Verifies equal-looking values of different scalar kinds never compare equal.</summary>
    [Fact]
    public void ComparisonsPreserveScalarKind()
    {
        var predicate = new FirmwareMetadataPredicate(
            "firmware-config",
            "value",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromInteger(2)]);

        Assert.Equal(
            FirmwarePredicateResult.NoMatch,
            predicate.Evaluate(new Dictionary<string, FirmwareMetadataValue>(StringComparer.Ordinal)
            {
                ["value"] = FirmwareMetadataValue.FromText("2"),
            }));
        Assert.Equal(
            FirmwarePredicateResult.NoMatch,
            predicate.Evaluate(new Dictionary<string, FirmwareMetadataValue>(StringComparer.Ordinal)
            {
                ["value"] = FirmwareMetadataValue.FromFlag(true),
            }));
    }

    /// <summary>Verifies identical field ids remain distinct across metadata structures.</summary>
    [Fact]
    public void StructureIdentityScopesSameFieldPredicates()
    {
        var primary = new FirmwareMetadataPredicate(
            "firmware-config-primary",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromInteger(2)]);
        var copy = new FirmwareMetadataPredicate(
            "firmware-config-copy",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromInteger(2)]);

        Assert.NotEqual(primary.MetadataStructureId, copy.MetadataStructureId);
        Assert.Equal(primary.FieldId, copy.FieldId);
    }

    /// <summary>Verifies constructor snapshots cannot be mutated through the public list.</summary>
    [Fact]
    public void ExpectedValuesExposeReadOnlySnapshot()
    {
        FirmwareMetadataValue[] source = [FirmwareMetadataValue.FromInteger(2)];
        var predicate = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            source);
        source[0] = FirmwareMetadataValue.FromInteger(3);

        Assert.Equal("firmware-config", predicate.MetadataStructureId);
        IList<FirmwareMetadataValue> exposed = Assert.IsType<IList<FirmwareMetadataValue>>(
            predicate.ExpectedValues,
            exactMatch: false);
        Assert.True(exposed.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() => exposed[0] = FirmwareMetadataValue.FromInteger(4));
        Assert.Equal(FirmwareMetadataValue.FromInteger(2), predicate.ExpectedValues[0]);
    }

    /// <summary>Verifies constructor cardinality and uniqueness rules fail closed.</summary>
    [Fact]
    public void ConstructorRejectsInvalidExpectedValues()
    {
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.OneOf,
            []));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.OneOf,
            [FirmwareMetadataValue.FromInteger(2), FirmwareMetadataValue.FromInteger(2)]));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromInteger(2), FirmwareMetadataValue.FromInteger(3)]));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [null!]));
    }

    /// <summary>Verifies undefined comparisons and blank scalar text are rejected.</summary>
    [Fact]
    public void ConstructorsRejectInvalidBoundaries()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            (FirmwareMetadataPredicateOperator)int.MaxValue,
            [FirmwareMetadataValue.FromInteger(2)]));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataPredicate(
            " ",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromInteger(2)]));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataPredicate(
            "firmware-config",
            " ",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromInteger(2)]));
        _ = Assert.Throws<ArgumentException>(() => FirmwareMetadataValue.FromText(string.Empty));
    }

    /// <summary>Verifies field matching is ordinal regardless of the caller's dictionary comparer.</summary>
    [Fact]
    public void EvaluateUsesOrdinalFieldIdentity()
    {
        var predicate = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromInteger(2)]);
        var facts = new Dictionary<string, FirmwareMetadataValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["CHIP-NUMBER"] = FirmwareMetadataValue.FromInteger(2),
        };

        Assert.Equal(FirmwarePredicateResult.Missing, predicate.Evaluate(facts));
    }

    /// <summary>Verifies null facts are missing and a null facts collection is rejected.</summary>
    [Fact]
    public void EvaluateHandlesNullBoundariesFailClosed()
    {
        var predicate = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromInteger(2)]);
        var facts = new Dictionary<string, FirmwareMetadataValue>(StringComparer.Ordinal)
        {
            ["chip-number"] = null!,
        };

        Assert.Equal(FirmwarePredicateResult.Missing, predicate.Evaluate(facts));
        _ = Assert.Throws<ArgumentNullException>(() => predicate.Evaluate(null!));
    }

    private static Dictionary<string, FirmwareMetadataValue> Facts(
        params (string FieldId, long Value)[] entries)
    {
        return entries.ToDictionary(
            entry => entry.FieldId,
            entry => FirmwareMetadataValue.FromInteger(entry.Value),
            StringComparer.Ordinal);
    }
}
