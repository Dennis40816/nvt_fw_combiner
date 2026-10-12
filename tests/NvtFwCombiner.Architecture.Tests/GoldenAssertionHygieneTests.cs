// Copyright (c) 2026 Dennis Liu. All rights reserved.
using System.Text.RegularExpressions;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Architecture.Tests;

public sealed class GoldenAssertionHygieneTests
{
    private static readonly string[] _knownException = [];

    // Three theories keep their `string sourceCase` parameter. Changing a signature makes the ledger count the
    // member as new debt (its banned-API and method-length findings are keyed by the signature).
    private static readonly Dictionary<string, int> _caseTheoryCount = new(StringComparer.Ordinal)
    {
        ["tests/NvtFwCombiner.Bootstrap.Tests/AbCtrlRamAuthoringTests.cs"] = 1,
        ["tests/NvtFwCombiner.Bootstrap.Tests/AbCtrlRamMemoryLayoutTests.cs"] = 1,
        ["tests/NvtFwCombiner.Bootstrap.Tests/AbCtrlRamReferencePlanTests.cs"] = 1,
    };

    // One statement keeps its 1.3.x text because the code-health ledger keys it by text. It returns a tuple of
    // reads and the first one is a golden artifact. Any other direct golden read fails.
    private static readonly Dictionary<string, int> _directGoldenReadCount = new(StringComparer.Ordinal)
    {
        ["tests/NvtFwCombiner.Bootstrap.Tests/AbCtrlRamAuthoringTests.cs"] = 1,
    };

    // Synthetic data, not firmware: the scenario writes two 16-byte buffers itself and compares the output
    // with literal bytes. The statement keeps its 1.3.x text because the code-health ledger keys it by text.
    private static readonly Dictionary<string, int> _syntheticEqualityCount = new(StringComparer.Ordinal)
    {
        ["tests/NvtFwCombiner.UiSmoke.Tests/ShellViewModelTests.Merge.cs"] = 2,
    };

    [Fact]
    public void GoldenAssertions_AllTestSources_UsePrivateDiagnostics()
    {
        Assert.True(HasUnsafeEquality("Assert.Equal(File.ReadAllBytes(path), actual);"));
        Assert.True(HasUnsafeEquality("Assert.Equal(\"0102\", Convert.ToHexString(image, 0, 2));"));
        Assert.True(HasUnsafeEquality("Assert.Equal(expectedHex, Hex(stamp));"));
        Assert.True(HasUnsafeEquality("Assert.Equal(expectedSha256, Hex(image));"));
        Assert.True(HasUnsafeEquality("Assert.Equal(expectedSha256, ToHexStringLower(image));"));
        Assert.True(HasUnsafeEquality("Assert.Equal(expectedSha256, Convert.ToHexStringLower(image));"));
        Assert.True(HasUnsafeEquality("Assert.Equal(expectedHex,\n Hex(stamp));"));
        Assert.True(HasUnsafeEquality("Assert.Equal(\"0102\", ToHexString(image));"));
        Assert.True(HasUnsafeEquality("Assert.Equal(\"0102\", Convert.ToHexStringLower(image));"));
        Assert.True(HasUnsafeEquality("Assert.Equal(expected, image.AsSpan(0, 2).ToArray());"));
        Assert.True(HasUnsafeEquality("Assert.Equal(expected, image.AsSpan(0, 2).ToArray()); Hash(image);"));
        Assert.True(HasUnsafeEquality("Hash(image); Assert.Equal(File.ReadAllBytes(path), actual);"));
        Assert.True(HasUnsafeMessage("Assert.True(ok, Serialize(report));"));
        Assert.True(HasUnsafeMessage("Assert.True(result.Failed, result.Output);"));
        Assert.True(HasUnsafeMessage("Assert.False(Check(a, b),\n result.Output);"));
        Assert.True(HasUnsafeMessage("Assert.True(Check(a, b),\n result.StandardOutput);"));
        Assert.False(HasUnsafeEquality("Assert.Equal(expectedSha, Convert.ToHexString(SHA256.HashData(image)));"));
        Assert.False(HasUnsafeEquality("Assert.Equal(expectedSha, Convert.ToHexStringLower(SHA256.HashData(image)));"));
        Assert.False(HasUnsafeEquality("Assert.Equal(expectedSha256, Convert.ToHexStringLower(actual));"));
        Assert.False(HasUnsafeEquality("Assert.Equal(expectedSha, PinnedJsonCatalogLoader.ComputeSha256(File.ReadAllBytes(path)));"));
        Assert.False(HasUnsafeMessage("Assert.True(result.StandardOutput.Length == 0, \"Expected empty output.\");"));

        Assert.False(HasUnsafeMessage("Assert.True(result.Output.Length == 0, \"Expected empty output.\");"));
        Assert.False(HasUnsafeMessage("Assert.False(result.Output.Length == 0, GoldenDiagnostics.TextSummary(result.Output));"));
        Assert.False(HasUnsafeMessage("Assert.True(ok, GoldenDiagnostics.TextSummary(result.StandardOutput));"));
        Assert.True(HasUnsafeMessage("Assert.True(ok, GoldenDiagnostics.TextSummary(result.StandardOutput) + result.StandardOutput);"));
        Assert.True(HasUnsafeMessage("Assert.True(ok, GoldenDiagnostics.TextSummary(result.Output) + \"|\" + result.Output);"));
        Assert.False(HasUnsafeMessage("Assert.True(ok, \"Expected result.Output\");"));

        Assert.True(HasUnsafeScalarEquality("Assert.Equal(expected, GoldenFileIo.ReadAllBytes(path)[offset]);"));
        Assert.True(HasUnsafeScalarEquality("byte[] bytes = File.ReadAllBytes(GoldenPath()); Assert.Equal(expected, bytes[offset]);"));
        Assert.True(HasUnsafeScalarEquality("byte[] bytes = GoldenFileIo.ReadAllBytes(GoldenPath()); Assert.Equal(expected, BinaryPrimitives.ReadUInt32LittleEndian(bytes));"));
        Assert.True(HasUnsafeScalarEquality("byte[] bytes = GoldenFileIo.ReadAllBytes(path); Assert.Equal(expected, bytes[offset]);"));
        Assert.True(HasUnsafeScalarEquality("var bytes = await GoldenFileIo.ReadAllBytesAsync(path, token); Assert.Equal(expected, bytes[offset]);"));
        Assert.False(HasUnsafeScalarEquality("byte[] bytes = GoldenFileIo.ReadAllBytes(path); Assert.Equal(size, bytes.Length);"));
        Assert.False(HasUnsafeScalarEquality("Assert.Equal(size, GoldenFileIo.ReadAllBytes(path).Length);"));
        Assert.False(HasUnsafeScalarEquality("byte[] bytes = [1]; Assert.Equal(expected, bytes[offset]);"));
        Assert.False(HasUnsafeScalarEquality("GoldenAssert.AssertValueEqualsGolden(expected, bytes[offset], label);"));
        Assert.Equal(1, CountDirectGoldenReads("byte[] bytes = File.ReadAllBytes(FindGoldenExpectedOutput(\"51927\"));"));
        Assert.Equal(1, CountDirectGoldenReads("byte[] bytes = File.ReadAllBytes(golden.ManifestPath(item));"));
        Assert.Equal(1, CountDirectGoldenReads("var text = File.ReadAllText(goldenPath);"));
        Assert.Equal(0, CountDirectGoldenReads("byte[] bytes = GoldenFileIo.ReadAllBytes(FindGoldenExpectedOutput(\"51927\"));"));
        Assert.Equal(0, CountDirectGoldenReads("byte[] bytes = File.ReadAllBytes(path);"));
        Assert.True(HasUnsafeHashExpectation($"Assert.Equal(\"{new string('a', 64)}\", actual);"));
        Assert.False(HasUnsafeHashExpectation("Assert.Equal(\"abcd\", actual);"));
        Assert.False(HasUnsafeHashExpectation($"GoldenAssert.AssertHashEqualsGolden(\"{new string('a', 64)}\", actual, label);"));
        Assert.True(HasUnsafeCaseTheoryData("[Theory][InlineData(value)] public void Example(string caseId)"));
        Assert.True(HasUnsafeCaseTheoryData("[Theory][MemberData(nameof(Rows))] public void Example(string sourceCaseId)"));
        Assert.True(HasUnsafeCaseTheoryData("[Theory][ClassData(typeof(Rows))] public void Example(string baseCaseId)"));
        Assert.True(HasUnsafeCaseTheoryData("[Theory][InlineData(value)] public void Example(string member, string sourceCase, int count)"));
        Assert.False(HasUnsafeCaseTheoryData("[Theory][MemberData(nameof(Rows))] public void Example(GoldenCaseId sourceCase)"));
        Assert.False(HasUnsafeCaseTheoryData("[Theory][MemberData(nameof(Rows))] public void Example(GoldenCaseId caseId)"));
        Assert.False(HasUnsafeCaseTheoryData("[Theory][InlineData(value)] public void Example(string workflowId)"));
        Assert.False(HasUnsafeCaseTheoryData("[Fact] public void Example(string caseId)"));

        List<string> violations = FindViolations();
        Assert.True(violations.Count == 0, string.Join('\n', violations));
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
            string source = string.Join('\n', lines);
            if (CountUnsafeEquality(source) != _syntheticEqualityCount.GetValueOrDefault(relative) &&
                !_knownException.Contains(relative, StringComparer.Ordinal))
            {
                violations.Add(relative + ": unsafe firmware equality diagnostic");
            }

            if (HasUnsafeMessage(source) &&
                !_knownException.Contains(relative, StringComparer.Ordinal))
            {
                violations.Add(relative + ": unsafe assertion message");
            }

            if (HasUnsafeScalarEquality(source) || HasUnsafeHashExpectation(source))
            {
                violations.Add(relative + ": unsafe scalar or hash diagnostic");
            }

            if (CountUnsafeCaseTheories(source) != _caseTheoryCount.GetValueOrDefault(relative))
            {
                violations.Add(relative + ": theory with a case id parameter");
            }

            if (CountDirectGoldenReads(source) != _directGoldenReadCount.GetValueOrDefault(relative))
            {
                violations.Add(relative + ": direct golden file read");
            }
        }

        return violations;
    }

    private static bool HasUnsafeEquality(string line)
    {
        return CountUnsafeEquality(line) > 0;
    }

    private static int CountUnsafeEquality(string line)
    {
        int count = 0;
        foreach (Match match in Regex.Matches(line,
                     @"Assert\.Equal\((?<call>(?:[^()]|\((?<depth>)|\)(?<-depth>))*)(?(depth)(?!))\)"))
        {
            string call = match.Groups["call"].Value;
            if (!call.Contains("SHA256.HashData(", StringComparison.Ordinal) &&
                !call.Contains("Sha256Hex(", StringComparison.Ordinal) &&
                !call.Contains("PinnedJsonCatalogLoader.ComputeSha256(", StringComparison.Ordinal) &&
                !call.Contains("Hash(", StringComparison.Ordinal) &&
                !Regex.IsMatch(call, @"\A\s*expectedSha256\s*,\s*Convert\.ToHexStringLower\(\s*actual\s*\)\s*\z") &&
                (call.Contains("ReadAllBytes(", StringComparison.Ordinal) ||
                 Regex.IsMatch(call, @"\b(?:Hex|ToHexString|ToHexStringLower)\s*\(") ||
                 (call.Contains(".ToArray()", StringComparison.Ordinal) &&
                  (call.Contains(".AsSpan(", StringComparison.Ordinal) || call.Contains(".Span", StringComparison.Ordinal)) &&
                  Regex.IsMatch(call, @"\b(?:image|output|expected|actual|reference|[A-Za-z_]*[Bb]ytes|[A-Za-z_]*[Ii]mage)\b"))))
            {
                count++;
            }
        }

        return count;
    }

    private static bool HasUnsafeMessage(string source)
    {
        foreach (Match match in Regex.Matches(source,
                     @"Assert\.(?:True|False)\((?<call>(?:[^()]|\((?<depth>)|\)(?<-depth>))*)(?(depth)(?!))\)",
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
                        if (Regex.IsMatch(message,
                                @"\A\s*GoldenDiagnostics\.TextSummary\((?:[^()]|\((?<message>)|\)(?<-message>))*(?(message)(?!))\)\s*\z"))
                        {
                            break;
                        }

                        if (message.Contains("Serialize(", StringComparison.Ordinal) ||
                            message.Contains("StandardOutput", StringComparison.Ordinal) ||
                            Regex.IsMatch(message, @"\.Output\s*$"))
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

    private static bool HasUnsafeScalarEquality(string source)
    {
        string[] goldenReads = Regex.Matches(source,
                @"\b(?:byte\s*\[\s*\]|var)\s+(?<name>\w+)\s*=\s*(?:await\s+)?(?:GoldenFileIo\.ReadAllBytes(?:Async)?\([^;]*;|File\.ReadAllBytes(?:Async)?\([^;]*(?:Golden|golden)[^;]*;)")
            .Select(static match => match.Groups["name"].Value).ToArray();
        foreach (Match match in Regex.Matches(source,
                     @"Assert\.Equal\((?<call>(?:[^()]|\((?<depth>)|\)(?<-depth>))*)(?(depth)(?!))\)"))
        {
            string call = match.Groups["call"].Value;
            if (Regex.IsMatch(call, @"(?:File|GoldenFileIo)\.ReadAllBytes(?:Async)?\([^;]+\)\s*\[") ||
                goldenReads.Any(name => Regex.IsMatch(call, @"\b" + Regex.Escape(name) + @"\s*(?:\.Span\s*)?\[") ||
                    (call.Contains("BinaryPrimitives.Read", StringComparison.Ordinal) &&
                     Regex.IsMatch(call, @"\b" + Regex.Escape(name) + @"\b"))))
            {
                return true;
            }
        }

        return false;
    }

    private static int CountDirectGoldenReads(string source)
    {
        return Regex.Matches(source, @"\bFile\.ReadAll(?:Bytes|Text)(?:Async)?\([^;]*")
            .Count(static match => Regex.IsMatch(match.Value,
                @"FindGoldenExpectedOutput\(|CanonicalGoldenTestData\.|\bgolden\w*\.\w+\(|\bgolden\w*Path\b"));
    }

    private static bool HasUnsafeHashExpectation(string source)
    {
        return Regex.Matches(source,
                @"Assert\.Equal\((?<call>(?:[^()]|\((?<depth>)|\)(?<-depth>))*)(?(depth)(?!))\)")
            .Any(static match => Regex.IsMatch(match.Groups["call"].Value, "\"[0-9a-fA-F]{64}\""));
    }

    private static bool HasUnsafeCaseTheoryData(string source)
    {
        return CountUnsafeCaseTheories(source) > 0;
    }

    private static int CountUnsafeCaseTheories(string source)
    {
        return Regex.Matches(source,
                @"\[Theory\b[^\]]*\](?<data>.*?)(?:public|internal)\s+(?:async\s+)?(?:void|Task(?:<[^>]+>)?)\s+\w+\s*\((?<parameters>[^)]*)\)",
                RegexOptions.Singleline)
            .Count(static match => Regex.IsMatch(match.Groups["data"].Value, @"\[(?:InlineData|MemberData|ClassData)\b") &&
                Regex.IsMatch(match.Groups["parameters"].Value, @"\bstring\??\s+\w*case(?:Id)?\b", RegexOptions.IgnoreCase));
    }
}
