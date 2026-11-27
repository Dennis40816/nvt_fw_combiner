// Copyright (c) 2026 Dennis Liu. All rights reserved.

using System.Collections.Immutable;
using System.Text.Json;

namespace NvtFwCombiner.Profiles.IcFiles;

// Migration seam: no document vocabulary or firmware facts belong in this reader.
internal static class RestrictedYamlReader
{
    internal static IcFileResult<YamlNode> Read(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        List<Row> rows = [];
        using StringReader reader = new(text);
        int number = 0;
        while (reader.ReadLine() is { } original)
        {
            number++;
            string line = number == 1 ? original.TrimStart('\uFEFF') : original;
            int indent = 0;
            while (indent < line.Length && line[indent] is ' ' or '\t')
            {
                if (line[indent] == '\t')
                {
                    return Failure(IcFileErrorCode.TabIndentation, new SourceMark(number, indent + 1));
                }

                indent++;
            }

            string content = line[indent..].TrimEnd();
            if (content.Length == 0 || content[0] == '#')
            {
                continue;
            }

            SourceMark mark = new(number, indent + 1);
            if (content is "---" or "..." || content.StartsWith("--- ", StringComparison.Ordinal))
            {
                return Failure(IcFileErrorCode.MultipleDocuments, mark);
            }

            if (indent % 2 != 0)
            {
                return Failure(IcFileErrorCode.InvalidIndentation, mark);
            }

            rows.Add(new Row(indent, content, mark));
        }

        if (rows.Count == 0)
        {
            return Failure(IcFileErrorCode.EmptyDocument, new SourceMark(1, 1));
        }

        int index = 0;
        IcFileResult<YamlNode> result = ReadBlock(rows, ref index, 0, 0);
        return result is IcFileResult<YamlNode>.Success && index != rows.Count
            ? Failure(IcFileErrorCode.InvalidIndentation, rows[index].Mark)
            : result;
    }

    private static IcFileResult<YamlNode> ReadBlock(List<Row> rows, ref int index, int indent, int depth)
    {
        SourceMark mark = index < rows.Count ? rows[index].Mark : rows[^1].Mark;
        if (depth > 128)
        {
            return Failure(IcFileErrorCode.NestingTooDeep, mark);
        }

        if (index >= rows.Count || rows[index].Indent != indent)
        {
            return Failure(IcFileErrorCode.InvalidIndentation, mark);
        }

        bool sequence = IsSequenceRow(rows[index].Text);
        if (!sequence && FindKeyEnd(rows[index].Text) < 0)
        {
            Row scalar = rows[index++];
            return ReadInline(scalar.Text, scalar.Mark);
        }

        return ReadCollection(rows, ref index, indent, depth, sequence);
    }

    private static IcFileResult<YamlNode> ReadCollection(
        List<Row> rows, ref int index, int indent, int depth, bool sequence)
    {
        SourceMark mark = rows[index].Mark;
        ImmutableArray<YamlNode>.Builder items = ImmutableArray.CreateBuilder<YamlNode>();
        ImmutableArray<YamlEntry>.Builder entries = ImmutableArray.CreateBuilder<YamlEntry>();
        HashSet<string> keys = new(StringComparer.Ordinal);
        while (index < rows.Count && rows[index].Indent == indent)
        {
            Row row = rows[index];
            if (IsSequenceRow(row.Text) != sequence)
            {
                return Failure(IcFileErrorCode.UnsupportedSyntax, row.Mark);
            }

            string valueText;
            SourceMark valueMark;
            YamlScalar? key = null;
            if (sequence)
            {
                valueText = row.Text.Length == 1 ? string.Empty : row.Text[2..];
                valueMark = row.Mark with { Column = row.Mark.Column + 2 };
            }
            else
            {
                IcFileResult<MappingHead> parsedHead = ReadMappingHead(row);
                if (parsedHead is IcFileResult<MappingHead>.Failure headFailure)
                {
                    return new IcFileResult<YamlNode>.Failure(headFailure.Error);
                }

                MappingHead head = ((IcFileResult<MappingHead>.Success)parsedHead).Value;
                key = head.Key;
                if (!keys.Add(key.Text))
                {
                    return Failure(IcFileErrorCode.DuplicateKey, row.Mark, key.Text);
                }

                valueText = head.ValueText;
                valueMark = head.ValueMark;
            }

            index++;
            IcFileResult<YamlNode> parsed = valueText.Length == 0
                ? ReadBlock(rows, ref index, indent + 2, depth + 1)
                : ReadInline(valueText, valueMark);
            if (parsed is IcFileResult<YamlNode>.Failure)
            {
                return parsed;
            }

            YamlNode value = ((IcFileResult<YamlNode>.Success)parsed).Value;
            if (sequence)
            {
                items.Add(value);
            }
            else
            {
                entries.Add(new YamlEntry(key!, value));
            }
        }

        YamlNode node = sequence
            ? new YamlSequence(items.ToImmutable(), mark)
            : new YamlMapping(entries.ToImmutable(), mark);
        return new IcFileResult<YamlNode>.Success(node);
    }

    private static IcFileResult<MappingHead> ReadMappingHead(Row row)
    {
        int colon = FindKeyEnd(row.Text);
        if (colon <= 0)
        {
            return new IcFileResult<MappingHead>.Failure(new IcFileError(
                ProhibitedToken(row.Text) ?? IcFileErrorCode.UnsupportedSyntax, row.Mark));
        }

        IcFileResult<YamlScalar> parsedKey = ReadKey(row.Text[..colon], row.Mark);
        if (parsedKey is IcFileResult<YamlScalar>.Failure keyFailure)
        {
            return new IcFileResult<MappingHead>.Failure(keyFailure.Error);
        }

        int valueOffset = colon + 1;
        if (valueOffset < row.Text.Length && row.Text[valueOffset] != ' ')
        {
            return new IcFileResult<MappingHead>.Failure(new IcFileError(IcFileErrorCode.UnsupportedSyntax, row.Mark));
        }

        SkipSpaces(row.Text, ref valueOffset);
        return new IcFileResult<MappingHead>.Success(new MappingHead(
            ((IcFileResult<YamlScalar>.Success)parsedKey).Value, row.Text[valueOffset..],
            row.Mark with { Column = row.Mark.Column + valueOffset }));
    }

    private static IcFileResult<YamlNode> ReadInline(string text, SourceMark mark)
    {
        if (ProhibitedToken(text) is { } code)
        {
            return Failure(code, mark);
        }

        if (text[0] == '[')
        {
            return ReadFlowSequence(text, mark);
        }

        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement value = document.RootElement;
            if (value.ValueKind == JsonValueKind.String)
            {
                return DecodeString(value, mark);
            }

            YamlScalarKind? kind = value.ValueKind switch
            {
                JsonValueKind.True or JsonValueKind.False => YamlScalarKind.Boolean,
                JsonValueKind.Null => YamlScalarKind.Null,
                JsonValueKind.Number => IsInteger(text) ? YamlScalarKind.Integer : null,
                JsonValueKind.Undefined or JsonValueKind.Object or JsonValueKind.Array or JsonValueKind.String => null,
                _ => null,
            };
            return kind is { } scalarKind
                ? new IcFileResult<YamlNode>.Success(new YamlScalar(scalarKind, text, mark))
                : Failure(IcFileErrorCode.InvalidScalar, mark);
        }
        catch (JsonException)
        {
            return Failure(IcFileErrorCode.InvalidScalar, mark);
        }
    }

    private static IcFileResult<YamlNode> DecodeString(JsonElement value, SourceMark mark)
    {
        try
        {
            return new IcFileResult<YamlNode>.Success(new YamlScalar(YamlScalarKind.String, value.GetString()!, mark));
        }
        catch (InvalidOperationException)
        {
            // JsonDocument accepts escape syntax before GetString validates surrogate pairs.
            return Failure(IcFileErrorCode.InvalidScalar, mark);
        }
    }

    private static IcFileResult<YamlNode> ReadFlowSequence(string text, SourceMark mark)
    {
        ImmutableArray<YamlNode>.Builder items = ImmutableArray.CreateBuilder<YamlNode>();
        int offset = 1;
        while (true)
        {
            SkipSpaces(text, ref offset);
            if (offset < text.Length && text[offset] == ']' && items.Count == 0)
            {
                return FinishFlow(text, offset, items, mark);
            }

            IcFileResult<YamlNode> item = ReadFlowItem(text, ref offset, mark);
            if (item is IcFileResult<YamlNode>.Failure)
            {
                return item;
            }

            items.Add(((IcFileResult<YamlNode>.Success)item).Value);
            SkipSpaces(text, ref offset);
            if (offset < text.Length && text[offset] == ']')
            {
                return FinishFlow(text, offset, items, mark);
            }

            if (offset >= text.Length || text[offset] != ',')
            {
                return Failure(IcFileErrorCode.InvalidFlowSequence, mark with { Column = mark.Column + offset });
            }

            offset++;
            SkipSpaces(text, ref offset);
            if (offset < text.Length && text[offset] == ']')
            {
                return Failure(IcFileErrorCode.InvalidFlowSequence, mark with { Column = mark.Column + offset });
            }
        }
    }

    private static IcFileResult<YamlNode> ReadFlowItem(string text, ref int offset, SourceMark mark)
    {
        int start = offset;
        SourceMark itemMark = mark with { Column = mark.Column + start };
        if (offset >= text.Length)
        {
            return Failure(IcFileErrorCode.InvalidFlowSequence, itemMark);
        }

        if (text[offset] == '"')
        {
            offset = QuotedEnd(text, offset);
        }
        else
        {
            while (offset < text.Length && text[offset] is not ',' and not ']')
            {
                offset++;
            }
        }

        if (offset < 0)
        {
            return Failure(IcFileErrorCode.InvalidScalar, itemMark);
        }

        string token = text[start..offset].TrimEnd();
        return token.Length == 0 || (token[0] is not '"' and not '-' && !char.IsAsciiDigit(token[0]))
            ? Failure(ProhibitedToken(token.Length == 0 ? " " : token) ?? IcFileErrorCode.InvalidFlowSequence, itemMark)
            : ReadInline(token, itemMark);
    }

    private static IcFileResult<YamlNode> FinishFlow(
        string text, int offset, ImmutableArray<YamlNode>.Builder items, SourceMark mark)
    {
        offset++;
        SkipSpaces(text, ref offset);
        return offset == text.Length
            ? new IcFileResult<YamlNode>.Success(new YamlSequence(items.ToImmutable(), mark))
            : Failure(IcFileErrorCode.InvalidFlowSequence, mark);
    }

    private static IcFileResult<YamlScalar> ReadKey(string text, SourceMark mark)
    {
        if (text[0] == '"')
        {
            IcFileResult<YamlNode> parsed = ReadInline(text, mark);
            return parsed is IcFileResult<YamlNode>.Success { Value: YamlScalar scalar }
                ? new IcFileResult<YamlScalar>.Success(scalar)
                : new IcFileResult<YamlScalar>.Failure(new IcFileError(IcFileErrorCode.InvalidScalar, mark));
        }

        return text.All(static character => char.IsAsciiLetterOrDigit(character) || character is '_' or '.' or '-')
            ? new IcFileResult<YamlScalar>.Success(new YamlScalar(YamlScalarKind.String, text, mark))
            : new IcFileResult<YamlScalar>.Failure(new IcFileError(
                ProhibitedToken(text) ?? IcFileErrorCode.UnsupportedSyntax, mark));
    }

    private static int FindKeyEnd(string text)
    {
        if (text[0] == '[')
        {
            return -1;
        }

        int offset = text[0] == '"' ? QuotedEnd(text, 0) : text.IndexOf(':');
        return offset > 0 && offset < text.Length && text[offset] == ':' ? offset : -1;
    }

    private static int QuotedEnd(string text, int start)
    {
        for (int index = start + 1; index < text.Length; index++)
        {
            if (text[index] == '\\')
            {
                index++;
            }
            else if (text[index] == '"')
            {
                return index + 1;
            }
        }

        return -1;
    }

    private static bool IsSequenceRow(string text)
    {
        return text == "-" || text.StartsWith("- ", StringComparison.Ordinal);
    }

    private static bool IsInteger(string text)
    {
        return text.AsSpan(text[0] == '-' ? 1 : 0).Length > 0 &&
            text.Skip(text[0] == '-' ? 1 : 0).All(char.IsAsciiDigit);
    }

    private static IcFileErrorCode? ProhibitedToken(string text)
    {
        return text[0] switch
        {
            '&' => IcFileErrorCode.AnchorNotAllowed,
            '*' => IcFileErrorCode.AliasNotAllowed,
            '!' => IcFileErrorCode.TagNotAllowed,
            '|' or '>' => IcFileErrorCode.MultilineScalarNotAllowed,
            _ => null,
        };
    }

    private static void SkipSpaces(string text, ref int offset)
    {
        while (offset < text.Length && text[offset] == ' ')
        {
            offset++;
        }
    }

    private static IcFileResult<YamlNode>.Failure Failure(IcFileErrorCode code, SourceMark mark, string? subject = null)
    {
        return new(new IcFileError(code, mark, subject));
    }

    private sealed record Row(int Indent, string Text, SourceMark Mark);

    private sealed record MappingHead(YamlScalar Key, string ValueText, SourceMark ValueMark);
}
