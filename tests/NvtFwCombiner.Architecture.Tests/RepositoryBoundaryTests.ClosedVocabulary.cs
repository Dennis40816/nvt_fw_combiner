using System.Text;
using System.Text.RegularExpressions;

namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class RepositoryBoundaryTests
{
    /// <summary>Domain closed-enum admission has one reusable validation owner.</summary>
    [Fact]
    public void DomainClosedEnumAdmissionUsesOneCanonicalGuard()
    {
        const string guardPath = "src/NvtFwCombiner.Domain/ImmutableStringSnapshot.cs";
        _ = Assert.Single(DirectEnumIsDefinedRegex().Matches(StripCommentsAndLiterals(ReadText(guardPath))));

        string domainDirectory = Path.Combine(Root.FullName, "src", "NvtFwCombiner.Domain");
        string guardFullPath = Path.Combine(Root.FullName, guardPath.Replace('/', Path.DirectorySeparatorChar));
        string binSegment = $"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}";
        string objSegment = $"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}";
        string[] duplicateOwners =
        [
            .. Directory.GetFiles(domainDirectory, "*.cs", SearchOption.AllDirectories)
                .Where(path => !StringComparer.OrdinalIgnoreCase.Equals(path, guardFullPath))
                .Where(path => !path.Contains(binSegment, StringComparison.OrdinalIgnoreCase))
                .Where(path => !path.Contains(objSegment, StringComparison.OrdinalIgnoreCase))
                .Where(path => DirectEnumIsDefinedRegex().IsMatch(
                    StripCommentsAndLiterals(File.ReadAllText(path))))
                .Select(path => Path.GetRelativePath(Root.FullName, path)),
        ];

        Assert.Empty(duplicateOwners);
    }

    /// <summary>The lexical guard recognizes formatting variants without treating trivia or literals as owners.</summary>
    [Theory]
    [InlineData("Enum.IsDefined(value)", 1)]
    [InlineData("Enum . IsDefined (value)", 1)]
    [InlineData("Enum.IsDefined\n(value)", 1)]
    [InlineData("Enum.IsDefined<SomeEnum>(value)", 1)]
    [InlineData("// Enum.IsDefined(value)", 0)]
    [InlineData("/* Enum . IsDefined<SomeEnum>(value) */", 0)]
    [InlineData("\"Enum.IsDefined(value)\"", 0)]
    public void ClosedEnumOwnerMatcherIgnoresTriviaAndLiterals(string source, int expected)
    {
        ArgumentNullException.ThrowIfNull(source);
        Assert.Equal(expected, DirectEnumIsDefinedRegex().Count(StripCommentsAndLiterals(source)));
    }

    private static string StripCommentsAndLiterals(string source)
    {
        var code = new StringBuilder(source.Length);
        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            if (current == '/' && index + 1 < source.Length)
            {
                if (source[index + 1] == '/')
                {
                    index += 2;
                    while (index < source.Length && source[index] is not ('\r' or '\n'))
                    {
                        index++;
                    }

                    if (index < source.Length)
                    {
                        _ = code.Append(source[index]);
                    }

                    continue;
                }

                if (source[index + 1] == '*')
                {
                    index += 2;
                    while (index + 1 < source.Length &&
                           (source[index] != '*' || source[index + 1] != '/'))
                    {
                        if (source[index] is '\r' or '\n')
                        {
                            _ = code.Append(source[index]);
                        }

                        index++;
                    }

                    index++;
                    continue;
                }
            }

            bool interpolated = index > 0 && source[index - 1] == '$';
            if (current == '"' && !interpolated)
            {
                int delimiterLength = CountRepeated(source, index, '"');
                index = delimiterLength >= 3
                    ? SkipRawString(source, index, delimiterLength, code)
                    : SkipQuotedLiteral(source, index, '"', code);
                continue;
            }

            if (current == '\'')
            {
                index = SkipQuotedLiteral(source, index, '\'', code);
                continue;
            }

            if (current == '@' && index + 1 < source.Length && source[index + 1] == '"')
            {
                index = SkipVerbatimString(source, index + 1, code);
                continue;
            }

            _ = code.Append(current);
        }

        return code.ToString();
    }

    private static int SkipQuotedLiteral(string source, int start, char delimiter, StringBuilder code)
    {
        for (int index = start + 1; index < source.Length; index++)
        {
            if (source[index] == '\\')
            {
                index++;
            }
            else if (source[index] == delimiter)
            {
                return index;
            }
            else if (source[index] is '\r' or '\n')
            {
                _ = code.Append(source[index]);
            }
        }

        return source.Length - 1;
    }

    private static int SkipVerbatimString(string source, int start, StringBuilder code)
    {
        for (int index = start + 1; index < source.Length; index++)
        {
            if (source[index] is '\r' or '\n')
            {
                _ = code.Append(source[index]);
            }
            else if (source[index] == '"')
            {
                if (index + 1 < source.Length && source[index + 1] == '"')
                {
                    index++;
                }
                else
                {
                    return index;
                }
            }
        }

        return source.Length - 1;
    }

    private static int SkipRawString(
        string source,
        int start,
        int delimiterLength,
        StringBuilder code)
    {
        for (int index = start + delimiterLength; index < source.Length; index++)
        {
            if (source[index] is '\r' or '\n')
            {
                _ = code.Append(source[index]);
            }

            if (source[index] == '"' && CountRepeated(source, index, '"') >= delimiterLength)
            {
                return index + delimiterLength - 1;
            }
        }

        return source.Length - 1;
    }

    private static int CountRepeated(string source, int start, char value)
    {
        int count = 0;
        while (start + count < source.Length && source[start + count] == value)
        {
            count++;
        }

        return count;
    }

    [GeneratedRegex(@"\bEnum\s*\.\s*IsDefined(?:\s*<[^>]+>)?\s*\(", RegexOptions.CultureInvariant)]
    private static partial Regex DirectEnumIsDefinedRegex();
}
