using NvtFwCombiner.Domain.Firmware;

namespace NvtFwCombiner.Domain.Tests.Firmware;

/// <summary>Tests typed metadata values and three-state applicability predicates.</summary>
public sealed class FirmwareMetadataPredicateTests
{
    /// <summary>Verifies scalar factories preserve closed value kinds.</summary>
    [Fact]
    public void ScalarFactoriesPreserveTypedValues()
    {
        byte[] source = [0, 2];
        var signed = FirmwareMetadataValue.FromSignedInteger(-2);
        var unsigned = FirmwareMetadataValue.FromUnsignedInteger(2);
        var bytes = FirmwareMetadataValue.FromBytes(source);
        var text = FirmwareMetadataValue.FromText("standard");
        var whitespace = FirmwareMetadataValue.FromText(" ");
        source[0] = 9;

        Assert.Equal(FirmwareMetadataValueKind.SignedInteger, signed.Kind);
        Assert.Equal(-2, signed.SignedIntegerValue);
        Assert.Equal(FirmwareMetadataValueKind.UnsignedInteger, unsigned.Kind);
        Assert.Equal(2UL, unsigned.UnsignedIntegerValue);
        Assert.Equal(FirmwareMetadataValueKind.Bytes, bytes.Kind);
        Assert.Equal("0002", bytes.BytesValue?.Hex);
        Assert.Equal(FirmwareMetadataValueKind.Text, text.Kind);
        Assert.Equal("standard", text.TextValue);
        Assert.Equal(" ", whitespace.TextValue);
    }

    /// <summary>Verifies byte values copy input and compare/hash by content.</summary>
    [Fact]
    public void ByteValuesUseStructuralImmutableIdentity()
    {
        byte[] source = [0, 1, 255];
        var first = FirmwareMetadataValue.FromBytes(source);
        var second = FirmwareMetadataValue.FromBytes([0, 1, 255]);
        var different = FirmwareMetadataValue.FromBytes([0, 1, 254]);
        source[0] = 9;

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.NotEqual(first, different);
        Assert.Equal("0001ff", first.BytesValue?.ToString());
    }

    /// <summary>Verifies signed, unsigned, bytes, and text never coerce during equality.</summary>
    [Fact]
    public void ScalarKindsRemainDistinct()
    {
        var signed = FirmwareMetadataValue.FromSignedInteger(2);
        var unsigned = FirmwareMetadataValue.FromUnsignedInteger(2);
        var bytes = FirmwareMetadataValue.FromBytes([2]);
        var text = FirmwareMetadataValue.FromText("2");

        Assert.NotEqual(signed, unsigned);
        Assert.NotEqual(unsigned, bytes);
        Assert.NotEqual(bytes, text);
    }

    /// <summary>Verifies equality distinguishes matching, conflicting, and missing facts.</summary>
    [Fact]
    public void EqualReturnsThreeStateResult()
    {
        var predicate = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(2)]);

        Assert.Equal(FirmwarePredicateResult.Missing, predicate.Evaluate(Facts()).Result);
        Assert.Equal(FirmwarePredicateResult.Match, predicate.Evaluate(Facts(("chip-number", 2))).Result);
        Assert.Equal(FirmwarePredicateResult.NoMatch, predicate.Evaluate(Facts(("chip-number", 1))).Result);
    }

    /// <summary>Verifies not-equal and one-of comparisons use typed equality.</summary>
    [Fact]
    public void OtherComparisonsUseTypedEquality()
    {
        var notEqual = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.NotEqual,
            [FirmwareMetadataValue.FromUnsignedInteger(1)]);
        var oneOf = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.OneOf,
            [FirmwareMetadataValue.FromUnsignedInteger(2), FirmwareMetadataValue.FromUnsignedInteger(3)]);

        Assert.Equal(FirmwarePredicateResult.Match, notEqual.Evaluate(Facts(("chip-number", 2))).Result);
        Assert.Equal(FirmwarePredicateResult.NoMatch, oneOf.Evaluate(Facts(("chip-number", 1))).Result);
        Assert.Equal(FirmwarePredicateResult.Match, oneOf.Evaluate(Facts(("chip-number", 3))).Result);
    }

    /// <summary>Verifies equal-looking values of different scalar kinds never compare equal.</summary>
    [Fact]
    public void ComparisonsPreserveScalarKind()
    {
        var predicate = new FirmwareMetadataPredicate(
            "firmware-config",
            "value",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(2)]);

        Assert.Equal(
            FirmwarePredicateResult.NoMatch,
            predicate.Evaluate(new Dictionary<string, FirmwareMetadataValue>(StringComparer.Ordinal)
            {
                ["value"] = FirmwareMetadataValue.FromText("2"),
            }).Result);
        Assert.Equal(
            FirmwarePredicateResult.NoMatch,
            predicate.Evaluate(new Dictionary<string, FirmwareMetadataValue>(StringComparer.Ordinal)
            {
                ["value"] = FirmwareMetadataValue.FromSignedInteger(2),
            }).Result);
        Assert.Equal(
            FirmwarePredicateResult.NoMatch,
            predicate.Evaluate(new Dictionary<string, FirmwareMetadataValue>(StringComparer.Ordinal)
            {
                ["value"] = FirmwareMetadataValue.FromBytes([2]),
            }).Result);
    }

    /// <summary>Verifies outcomes retain only the predicate and exact typed actual value.</summary>
    [Fact]
    public void EvaluateReturnsPredicateAndTypedActualValue()
    {
        var predicate = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(2)]);

        FirmwareMetadataPredicateOutcome match = predicate.Evaluate(Facts(("chip-number", 2)));
        FirmwareMetadataPredicateOutcome noMatch = predicate.Evaluate(Facts(("chip-number", 1)));
        FirmwareMetadataPredicateOutcome missing = predicate.Evaluate(Facts());

        Assert.Same(predicate, match.Predicate);
        Assert.Equal(FirmwarePredicateResult.Match, match.Result);
        Assert.Equal(FirmwareMetadataValue.FromUnsignedInteger(2), match.ActualValue);
        Assert.Equal(FirmwarePredicateResult.NoMatch, noMatch.Result);
        Assert.Equal(FirmwareMetadataValue.FromUnsignedInteger(1), noMatch.ActualValue);
        Assert.Equal(FirmwarePredicateResult.Missing, missing.Result);
        Assert.Null(missing.ActualValue);
    }

    /// <summary>Verifies identical field ids remain distinct across metadata structures.</summary>
    [Fact]
    public void StructureIdentityScopesSameFieldPredicates()
    {
        var primary = new FirmwareMetadataPredicate(
            "firmware-config-primary",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(2)]);
        var copy = new FirmwareMetadataPredicate(
            "firmware-config-copy",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(2)]);

        Assert.NotEqual(primary.MetadataStructureId, copy.MetadataStructureId);
        Assert.Equal(primary.FieldId, copy.FieldId);
    }

    /// <summary>Verifies constructor snapshots cannot be mutated through the public list.</summary>
    [Fact]
    public void ExpectedValuesExposeReadOnlySnapshot()
    {
        FirmwareMetadataValue[] source = [FirmwareMetadataValue.FromUnsignedInteger(2)];
        var predicate = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            source);
        source[0] = FirmwareMetadataValue.FromUnsignedInteger(3);

        Assert.Equal("firmware-config", predicate.MetadataStructureId);
        IList<FirmwareMetadataValue> exposed = Assert.IsType<IList<FirmwareMetadataValue>>(
            predicate.ExpectedValues,
            exactMatch: false);
        Assert.True(exposed.IsReadOnly);
        _ = Assert.Throws<NotSupportedException>(() =>
            exposed[0] = FirmwareMetadataValue.FromUnsignedInteger(4));
        Assert.Equal(FirmwareMetadataValue.FromUnsignedInteger(2), predicate.ExpectedValues[0]);
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
            [FirmwareMetadataValue.FromUnsignedInteger(2), FirmwareMetadataValue.FromUnsignedInteger(2)]));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(2), FirmwareMetadataValue.FromUnsignedInteger(3)]));
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
            [FirmwareMetadataValue.FromUnsignedInteger(2)]));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataPredicate(
            " ",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(2)]));
        _ = Assert.Throws<ArgumentException>(() => new FirmwareMetadataPredicate(
            "firmware-config",
            " ",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(2)]));
        _ = Assert.Throws<ArgumentException>(() => FirmwareMetadataValue.FromText(string.Empty));
        _ = Assert.Throws<ArgumentException>(() => FirmwareMetadataValue.FromBytes([]));
    }

    /// <summary>Verifies field matching is ordinal regardless of the caller's dictionary comparer.</summary>
    [Fact]
    public void EvaluateUsesOrdinalFieldIdentity()
    {
        var predicate = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(2)]);
        var facts = new Dictionary<string, FirmwareMetadataValue>(StringComparer.OrdinalIgnoreCase)
        {
            ["CHIP-NUMBER"] = FirmwareMetadataValue.FromUnsignedInteger(2),
        };

        Assert.Equal(FirmwarePredicateResult.Missing, predicate.Evaluate(facts).Result);
    }

    /// <summary>Verifies null facts are missing and a null facts collection is rejected.</summary>
    [Fact]
    public void EvaluateHandlesNullBoundariesFailClosed()
    {
        var predicate = new FirmwareMetadataPredicate(
            "firmware-config",
            "chip-number",
            FirmwareMetadataPredicateOperator.Equal,
            [FirmwareMetadataValue.FromUnsignedInteger(2)]);
        var facts = new Dictionary<string, FirmwareMetadataValue>(StringComparer.Ordinal)
        {
            ["chip-number"] = null!,
        };

        Assert.Equal(FirmwarePredicateResult.Missing, predicate.Evaluate(facts).Result);
        _ = Assert.Throws<ArgumentNullException>(() => predicate.Evaluate(null!));
    }

    private static Dictionary<string, FirmwareMetadataValue> Facts(
        params (string FieldId, ulong Value)[] entries)
    {
        return entries.ToDictionary(
            entry => entry.FieldId,
            entry => FirmwareMetadataValue.FromUnsignedInteger(entry.Value),
            StringComparer.Ordinal);
    }
}
