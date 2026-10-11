// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text.RegularExpressions;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Architecture.Tests;

public sealed class GoldenAssertionHygieneTests
{
    private static readonly string[] _knownException = [];

    [Fact]
    public void GoldenAssertions_AllTestSources_UsePrivateDiagnostics()
    {
        Assert.True(HasUnsafeEquality("Assert.Equal(File.ReadAllBytes(path), actual);"));
        Assert.True(HasUnsafeEquality("Assert.Equal(\"0102\", Convert.ToHexString(image, 0, 2));"));
        Assert.True(HasUnsafeEquality("Assert.Equal(expected, image.AsSpan(0, 2).ToArray());"));
        Assert.True(HasUnsafeEquality("Assert.Equal(expected, image.AsSpan(0, 2).ToArray()); Hash(image);"));
        Assert.True(HasUnsafeEquality("Hash(image); Assert.Equal(File.ReadAllBytes(path), actual);"));
        Assert.True(HasUnsafeMessage("Assert.True(ok, Serialize(report));"));
        Assert.True(HasUnsafeMessage("Assert.True(Check(a, b),\n result.StandardOutput);"));
        Assert.False(HasUnsafeEquality("Assert.Equal(expectedSha, Convert.ToHexString(SHA256.HashData(image)));"));
        Assert.False(HasUnsafeMessage("Assert.True(result.StandardOutput.Length == 0, \"Expected empty output.\");"));

        Assert.Empty(FindViolations());
    }

    private static List<string> FindViolations()
    {
        string root = RepositoryPaths.FindRepositoryRoot();
        var violations = new List<string>();
        foreach (string path in Directory.EnumerateFiles(Path.Combine(root, "tests"), "*.cs", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            string name = Path.GetFileName(path);
            if (name is "GoldenAssertionHygieneTests.cs" or "GoldenAssertTests.cs" ||
                relative.Split('/').Any(static segment => segment is "bin" or "obj"))
            {
                continue;
            }

            string[] lines = RepositoryBoundaryTestSupport.ReadLines(relative);
            for (int index = 0; index < lines.Length; index++)
            {
                string key = $"{relative}:{index + 1}";
                if (HasUnsafeEquality(lines[index]) && !_knownException.Contains(key, StringComparer.Ordinal))
                {
                    violations.Add(key + ": unsafe firmware equality diagnostic");
                }
            }

            if (HasUnsafeMessage(string.Join('\n', lines)) &&
                !_knownException.Contains(relative, StringComparer.Ordinal))
            {
                violations.Add(relative + ": unsafe assertion message");
            }
        }

        return violations;
    }

    private static bool HasUnsafeEquality(string line)
    {
        foreach (Match match in Regex.Matches(line,
                     @"Assert\.Equal\((?<call>(?:[^()]|\((?<depth>)|\)(?<-depth>))*)(?(depth)(?!))\)"))
        {
            string call = match.Groups["call"].Value;
            if (!call.Contains("SHA256.HashData(", StringComparison.Ordinal) &&
                !call.Contains("Sha256Hex(", StringComparison.Ordinal) &&
                !call.Contains("Hash(", StringComparison.Ordinal) &&
                (call.Contains("ReadAllBytes(", StringComparison.Ordinal) ||
                 call.Contains("Convert.ToHexString(", StringComparison.Ordinal) ||
                 (call.Contains(".ToArray()", StringComparison.Ordinal) &&
                  (call.Contains(".AsSpan(", StringComparison.Ordinal) || call.Contains(".Span", StringComparison.Ordinal)) &&
                  Regex.IsMatch(call, @"\b(?:image|output|expected|actual|reference|[A-Za-z_]*[Bb]ytes|[A-Za-z_]*[Ii]mage)\b"))))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasUnsafeMessage(string source)
    {
        foreach (Match match in Regex.Matches(source,
                     @"Assert\.True\((?<call>(?:[^()]|\((?<depth>)|\)(?<-depth>))*)(?(depth)(?!))\)",
                     RegexOptions.Singleline))
        {
            string arguments = match.Groups["call"].Value;
            int depth = 0;
            bool quoted = false;
            for (int index = 0; index < arguments.Length; index++)
            {
                char current = arguments[index];
                if (current == '"' && (index == 0 || arguments[index - 1] != '\\'))
                {
                    quoted = !quoted;
                }
                else if (!quoted)
                {
                    depth += current == '(' ? 1 : current == ')' ? -1 : 0;
                    if (current == ',' && depth == 0)
                    {
                        string message = arguments[(index + 1)..];
                        if (message.Contains("Serialize(", StringComparison.Ordinal) ||
                            message.Contains("StandardOutput", StringComparison.Ordinal))
                        {
                            return true;
                        }

                        break;
                    }
                }
            }
        }

        return false;
    }
}
