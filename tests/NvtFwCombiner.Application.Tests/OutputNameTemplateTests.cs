// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Tests the shared template contract independently of profile-specific metadata readers.</summary>
public sealed class OutputNameTemplateTests
{
    private const string AcceptedHash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>The imported template shape consumes independent values from both banks.</summary>
    [Fact]
    public void BankFieldsNameBothBanksUsingTheSameFieldIdentity()
    {
        OutputNameTokenValue[] tokens =
        [
            OutputNameTokenValue.ForIc("NT51929", numberOnly: true),
            new("dp", "D8205", "dp-input", AcceptedHash, "accepted-field", BankId: "a"),
            new("tp", "T8004", "tp-a-input", AcceptedHash, "accepted-field", BankId: "a"),
            new("dp", "D9306", "dp-input", AcceptedHash, "accepted-field", BankId: "b"),
            new("tp", "T9105", "tp-b-input", AcceptedHash, "accepted-field", BankId: "b"),
            OutputNameTokenValue.ForDate(new DateTimeOffset(2026, 7, 28, 12, 0, 0, TimeSpan.Zero)),
        ];

        string name = CompiledOutputNameResolver.ResolveTemplate(
            "NT{ic}_FlashCode_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin",
            tokens);

        Assert.Equal("NT51929_FlashCode_A_D8205T8004_B_D9306T9105_20260728.bin", name);
    }

    /// <summary>An unreadable bank field retains its source identity and uses the unknown marker.</summary>
    [Theory]
    [InlineData("a", "A_xxxx.bin")]
    [InlineData("b", "B_xxxx.bin")]
    public void MissingBankFieldUsesUnknownVersionPlaceholder(string bankId, string expected)
    {
        ArgumentNullException.ThrowIfNull(bankId);
        OutputNameTokenValue field = new("dp", null, "dp-input", AcceptedHash, "accepted-field", bankId);

        string name = CompiledOutputNameResolver.ResolveTemplate(
            $"{bankId.ToUpperInvariant()}_{{dp-{bankId}}}.bin",
            [field]);

        Assert.Equal(expected, name);
        Assert.Equal(
            new OutputNamingTokenSummary($"dp-{bankId}", "xxxx", false, "dp-input", AcceptedHash, "accepted-field"),
            field.Summary);
    }

    /// <summary>Field identity belongs to the template rather than a fixed AB token vocabulary.</summary>
    [Fact]
    public void BankFieldsCanUseAnOrdinaryFieldOutsideTheLegacyAbVocabulary()
    {
        string name = CompiledOutputNameResolver.ResolveTemplate(
            "bank_{revision-a}_{revision-b}.bin",
            [
                new("revision", "01", null, null, "accepted-field", BankId: "a"),
                new("revision", "02", null, null, "accepted-field", BankId: "b"),
            ]);

        Assert.Equal("bank_01_02.bin", name);
    }

    /// <summary>Repeated references consume the same immutable value.</summary>
    [Fact]
    public void RepeatedFieldUsesOneAcceptedValueAtEveryOccurrence()
    {
        string name = CompiledOutputNameResolver.ResolveTemplate(
            "{dp-a}_{dp-a}.bin",
            [new("dp", "8205", null, null, "accepted-field", BankId: "a")]);

        Assert.Equal("8205_8205.bin", name);
    }

    /// <summary>Template syntax and supplied text cannot escape canonical filename safety.</summary>
    [Theory]
    [InlineData("../{dp-a}.bin", "8205")]
    [InlineData("{dp-a}.bin", "../outside")]
    [InlineData("{dp-a}.bin", "bad?")]
    [InlineData("{dp-a}.bin", "CON")]
    [InlineData("{dp-a}.bin", "{date}")]
    [InlineData("{dp-a}.bin", "{")]
    [InlineData("{dp-a.bin", "8205")]
    [InlineData("dp-a}.bin", "8205")]
    [InlineData("{dp-b}.bin", "8205")]
    public void InvalidTemplateOrFieldCannotBypassLiteralNameValidation(string template, string value)
    {
        _ = Assert.Throws<ArgumentException>(() => CompiledOutputNameResolver.ResolveTemplate(
            template,
            [new("dp", value, null, null, "accepted-field", BankId: "a")]));
    }

    /// <summary>Two supplied values cannot silently compete for the same bank field.</summary>
    [Fact]
    public void DuplicateFieldInOneBankIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() => CompiledOutputNameResolver.ResolveTemplate(
            "{dp-a}.bin",
            [
                new("dp", "8205", null, null, "accepted-field", BankId: "a"),
                new("dp", "9306", null, null, "accepted-field", BankId: "a"),
            ]));
    }

    /// <summary>IC tokens retain Domain admission before either canonical or numeric rendering.</summary>
    [Theory]
    [InlineData("51929")]
    [InlineData("nt51929")]
    [InlineData("NT5192X")]
    public void IcTokenRejectsNonCanonicalIdentity(string icId)
    {
        _ = Assert.Throws<ArgumentException>(() => OutputNameTokenValue.ForIc(icId));
    }

    /// <summary>The typed date uses the supplied instant in UTC even across a local date boundary.</summary>
    [Fact]
    public void DateTokenUsesUtcAcrossALocalDateBoundary()
    {
        string name = CompiledOutputNameResolver.ResolveTemplate(
            "{ic}_{date}.bin",
            [
                OutputNameTokenValue.ForIc("NT51929"),
                OutputNameTokenValue.ForDate(new DateTimeOffset(2026, 7, 29, 1, 0, 0, TimeSpan.FromHours(8))),
            ]);

        Assert.Equal("NT51929_20260728.bin", name);
    }

    /// <summary>The shared expander fills repeated tokens and keeps literal text between them.</summary>
    [Fact]
    public void ExpandTemplateFillsRepeatedTokens()
    {
        Dictionary<string, string> tokens = new(StringComparer.Ordinal)
        {
            ["ic"] = "NT51950",
            ["date"] = "20260728",
        };

        Assert.Equal(
            "NT51950_NT51950_20260728",
            CompiledOutputNameResolver.ExpandTemplate("{ic}_{ic}_{date}", tokens));
    }

    /// <summary>The shared expander refuses what the name resolver refuses, without choosing an exception.</summary>
    [Theory]
    [InlineData("{ic}_{dp}")]
    [InlineData("{ic}_}")]
    [InlineData("{ic}_{date")]
    [InlineData("{bad}")]
    public void ExpandTemplateReturnsNullForAnUnresolvableTemplate(string template)
    {
        Dictionary<string, string> tokens = new(StringComparer.Ordinal)
        {
            ["ic"] = "NT51950",
            ["date"] = "20260728",
            ["bad"] = "a{b}",
        };

        Assert.Null(CompiledOutputNameResolver.ExpandTemplate(template, tokens));
    }
}
