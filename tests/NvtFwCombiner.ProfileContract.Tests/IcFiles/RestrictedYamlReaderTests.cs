// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Profiles.IcFiles;

namespace NvtFwCombiner.ProfileContract.Tests.IcFiles;

/// <summary>Verifies restricted Yaml Reader Tests.</summary>
public sealed class RestrictedYamlReaderTests
{
    /// <summary>Verifies nested Collections Retain Every Node And Key Position.</summary>
    [Fact]
    public void NestedCollectionsRetainEveryNodeAndKeyPosition()
    {
        const string yaml = "# synthetic\nitems:\n  -\n    name: \"a\\nb\"\n    refs: [\"x\", \"y\"]\n    empty: []\n";

        YamlMapping root = Assert.IsType<YamlMapping>(IcFileAssertions.Success(RestrictedYamlReader.Read(yaml)));
        YamlEntry entry = Assert.Single(root.Entries);
        YamlSequence items = Assert.IsType<YamlSequence>(entry.Value);
        YamlMapping item = Assert.IsType<YamlMapping>(Assert.Single(items.Items));
        YamlSequence refs = Assert.IsType<YamlSequence>(item.Entries[1].Value);

        Assert.Equal(new SourceMark(2, 1), root.Mark);
        Assert.Equal(new SourceMark(2, 1), entry.Key.Mark);
        Assert.Equal(new SourceMark(3, 3), items.Mark);
        Assert.Equal(new SourceMark(4, 5), item.Mark);
        Assert.Equal(new YamlScalar(YamlScalarKind.String, "a\nb", new SourceMark(4, 11)), item.Entries[0].Value);
        Assert.Equal(new SourceMark(5, 11), refs.Mark);
        Assert.Equal<YamlNode>(
            [new YamlScalar(YamlScalarKind.String, "x", new SourceMark(5, 12)),
             new YamlScalar(YamlScalarKind.String, "y", new SourceMark(5, 17))], refs.Items);
        Assert.Empty(Assert.IsType<YamlSequence>(item.Entries[2].Value).Items);
    }

    /// <summary>Verifies draft Json Scalars Remain Lossless.</summary>
    [Theory]
    [InlineData("\"text\"", (int)YamlScalarKind.String, "text")]
    [InlineData("-12", (int)YamlScalarKind.Integer, "-12")]
    [InlineData("true", (int)YamlScalarKind.Boolean, "true")]
    [InlineData("false", (int)YamlScalarKind.Boolean, "false")]
    [InlineData("null", (int)YamlScalarKind.Null, "null")]
    public void DraftJsonScalarsRemainLossless(string yaml, int kind, string text)
    {
        YamlNode node = IcFileAssertions.Success(RestrictedYamlReader.Read(yaml));

        Assert.Equal(new YamlScalar((YamlScalarKind)kind, text, new SourceMark(1, 1)), node);
    }

    /// <summary>Verifies quoted Keys And Inline Sequence Items Use Decoded Text.</summary>
    [Fact]
    public void QuotedKeysAndInlineSequenceItemsUseDecodedText()
    {
        const string yaml = "\uFEFF\"a:b\":\r\n  - \"&not-an-anchor\"\r\n  - [\"!not-a-tag\"]\r\n";

        YamlMapping root = Assert.IsType<YamlMapping>(IcFileAssertions.Success(RestrictedYamlReader.Read(yaml)));
        YamlEntry entry = Assert.Single(root.Entries);
        YamlSequence sequence = Assert.IsType<YamlSequence>(entry.Value);

        Assert.Equal("a:b", entry.Key.Text);
        Assert.Equal(new YamlScalar(YamlScalarKind.String, "&not-an-anchor", new SourceMark(2, 5)), sequence.Items[0]);
        Assert.Equal(new YamlScalar(YamlScalarKind.String, "!not-a-tag", new SourceMark(3, 6)),
            Assert.Single(Assert.IsType<YamlSequence>(sequence.Items[1]).Items));
    }

    /// <summary>Verifies empty Flow Sequence Is AValid Root.</summary>
    [Fact]
    public void EmptyFlowSequenceIsAValidRoot()
    {
        YamlNode root = IcFileAssertions.Success(RestrictedYamlReader.Read("[]"));

        Assert.Empty(Assert.IsType<YamlSequence>(root).Items);
    }

    /// <summary>Verifies draft Integer Flow Items Retain Their Positions And Values.</summary>
    [Fact]
    public void DraftIntegerFlowItemsRetainTheirPositionsAndValues()
    {
        YamlSequence node = Assert.IsType<YamlSequence>(IcFileAssertions.Success(RestrictedYamlReader.Read("[1, -2]")));

        Assert.Equal<YamlNode>(
            [new YamlScalar(YamlScalarKind.Integer, "1", new SourceMark(1, 2)),
             new YamlScalar(YamlScalarKind.Integer, "-2", new SourceMark(1, 5))], node.Items);
    }

    /// <summary>Verifies colon characters in flow strings do not become mapping delimiters.</summary>
    [Theory]
    [InlineData("[\"x:y\"]", 1, 2)]
    [InlineData("values:\n  [\"x:y\"]", 2, 4)]
    [InlineData("values: [\"x:y\"]", 1, 10)]
    public void FlowStringsContainingColonsWorkAtEveryCollectionPosition(string yaml, int line, int column)
    {
        YamlNode root = IcFileAssertions.Success(RestrictedYamlReader.Read(yaml));
        YamlSequence sequence = root is YamlMapping mapping
            ? Assert.IsType<YamlSequence>(Assert.Single(mapping.Entries).Value)
            : Assert.IsType<YamlSequence>(root);

        Assert.Equal(new YamlScalar(YamlScalarKind.String, "x:y", new SourceMark(line, column)), Assert.Single(sequence.Items));
    }

    /// <summary>Verifies invalid Unicode escape sequences remain inside the typed error boundary.</summary>
    [Theory]
    [InlineData("a: \"\\uD800\"")]
    [InlineData("a: \"\\uDC00\"")]
    [InlineData("a: \"\\uD800A\"")]
    [InlineData("\"\\uD800\": \"x\"")]
    [InlineData("[\"\\uDC00\"]")]
    [InlineData("[\"\\uD800A\"]")]
    public void InvalidSurrogateEscapesReturnTypedScalarErrors(string yaml)
    {
        _ = IcFileAssertions.Failure(RestrictedYamlReader.Read(yaml), IcFileErrorCode.InvalidScalar);
    }

    /// <summary>Verifies valid Unicode surrogate pairs decode in keys and scalar values.</summary>
    [Fact]
    public void ValidSurrogatePairsDecodeInKeysAndFlowValues()
    {
        YamlMapping root = Assert.IsType<YamlMapping>(IcFileAssertions.Success(
            RestrictedYamlReader.Read("\"\\uD83D\\uDE00\": [\"\\uD83D\\uDE00\"]")));
        YamlEntry entry = Assert.Single(root.Entries);

        Assert.Equal("\U0001F600", entry.Key.Text);
        Assert.Equal("\U0001F600", Assert.IsType<YamlScalar>(
            Assert.Single(Assert.IsType<YamlSequence>(entry.Value).Items)).Text);
    }

    /// <summary>Verifies unsupported Input Returns Typed Failure At Its Line.</summary>
    [Theory]
    [InlineData("a: &anchor\n", (int)IcFileErrorCode.AnchorNotAllowed, 1)]
    [InlineData("a: *alias\n", (int)IcFileErrorCode.AliasNotAllowed, 1)]
    [InlineData("a: !tag \"x\"\n", (int)IcFileErrorCode.TagNotAllowed, 1)]
    [InlineData("a: []\n---\nb: []\n", (int)IcFileErrorCode.MultipleDocuments, 2)]
    [InlineData("a: []\n...\n", (int)IcFileErrorCode.MultipleDocuments, 2)]
    [InlineData("a: |\n  plain\n", (int)IcFileErrorCode.MultilineScalarNotAllowed, 1)]
    [InlineData("a: >\n  plain\n", (int)IcFileErrorCode.MultilineScalarNotAllowed, 1)]
    [InlineData("a: plain\n  continued\n", (int)IcFileErrorCode.InvalidScalar, 1)]
    [InlineData("a:\n\t- \"x\"\n", (int)IcFileErrorCode.TabIndentation, 2)]
    [InlineData("a: []\n\"a\": []\n", (int)IcFileErrorCode.DuplicateKey, 2)]
    [InlineData("a:\n   b: []\n", (int)IcFileErrorCode.InvalidIndentation, 2)]
    [InlineData("a:\n    b: []\n", (int)IcFileErrorCode.InvalidIndentation, 2)]
    [InlineData("a: []\n  b: []\n", (int)IcFileErrorCode.InvalidIndentation, 2)]
    [InlineData("a:\n", (int)IcFileErrorCode.InvalidIndentation, 1)]
    [InlineData("a: []\n- \"x\"\n", (int)IcFileErrorCode.UnsupportedSyntax, 2)]
    [InlineData("a: {}\n", (int)IcFileErrorCode.InvalidScalar, 1)]
    [InlineData("a: 'single'\n", (int)IcFileErrorCode.InvalidScalar, 1)]
    [InlineData("a: 1.5\n", (int)IcFileErrorCode.InvalidScalar, 1)]
    [InlineData("a: \"x\" # inline\n", (int)IcFileErrorCode.InvalidScalar, 1)]
    [InlineData("a: \"bad\\q\"\n", (int)IcFileErrorCode.InvalidScalar, 1)]
    [InlineData("a: [true]\n", (int)IcFileErrorCode.InvalidFlowSequence, 1)]
    [InlineData("a: [null]\n", (int)IcFileErrorCode.InvalidFlowSequence, 1)]
    [InlineData("a: [\"x\", ]\n", (int)IcFileErrorCode.InvalidFlowSequence, 1)]
    [InlineData("a: [\"x\"] extra\n", (int)IcFileErrorCode.InvalidFlowSequence, 1)]
    [InlineData("a: [\"x\"\n", (int)IcFileErrorCode.InvalidFlowSequence, 1)]
    [InlineData("a: [\"x\" \"y\"]\n", (int)IcFileErrorCode.InvalidFlowSequence, 1)]
    [InlineData("# comment\n", (int)IcFileErrorCode.EmptyDocument, 1)]
    public void UnsupportedInputReturnsTypedFailureAtItsLine(string yaml, int code, int line)
    {
        IcFileError error = IcFileAssertions.Failure(RestrictedYamlReader.Read(yaml), (IcFileErrorCode)code);

        Assert.Equal(line, error.Mark.Line);
    }

    /// <summary>Verifies excessive Nesting Returns Typed Failure.</summary>
    [Fact]
    public void ExcessiveNestingReturnsTypedFailure()
    {
        string yaml = string.Join('\n', Enumerable.Range(0, 131).Select(index => new string(' ', index * 2) + "a:"));

        _ = IcFileAssertions.Failure(RestrictedYamlReader.Read(yaml), IcFileErrorCode.NestingTooDeep);
    }
}
