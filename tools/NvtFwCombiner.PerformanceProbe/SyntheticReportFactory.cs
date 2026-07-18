using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NvtFwCombiner.Bootstrap;

namespace NvtFwCombiner.PerformanceProbe;

internal static class SyntheticReportFactory
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    internal static SyntheticReport Create(int differenceCount, int sectionCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(differenceCount);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sectionCount);
        object[] differences = [
            .. Enumerable.Range(0, differenceCount).Select(index => new
            {
                DifferenceId = $"diff-{index:D5}",
                Range = CreateRange(index * 4L, (index * 4L) + 4),
                ChangedByteCount = 4,
                Classification = index == differenceCount - 1
                    ? WorkbenchOutputDifferenceClassifications.Unexpected
                    : WorkbenchOutputDifferenceClassifications.DeclaredReplacement,
                IsAccepted = index != differenceCount - 1,
                Evidence = $"evidence-{index:D5}",
                Explanation = $"synthetic difference {index}",
                SectionLabel = $"Section {index % sectionCount:D3}",
                BeforeSha256 = "11111111111111111111",
                AfterSha256 = "22222222222222222222",
                BeforeHexPreview = "AABBCCDD",
                AfterHexPreview = "11223344",
                HexPreviewByteCount = 4,
                IsHexPreviewComplete = true,
            }),
        ];
        var report = new
        {
            ProfileId = "synthetic-performance-report",
            IcId = "NT-SYNTHETIC",
            ModeId = "ctrlram-replace",
            ExperienceId = "ctrlram-replace",
            CompositionKind = "Replace",
            RunId = $"report-{differenceCount}-{sectionCount}",
            StartedAtUtc = "2026-07-18T00:00:00Z",
            Inputs = Array.Empty<object>(),
            Operations = Array.Empty<object>(),
            Mutations = Array.Empty<object>(),
            OutputDifferences = differences,
            Issues = Array.Empty<object>(),
            Output = new
            {
                FileName = "synthetic.bin",
                Size = differenceCount * 4L,
                Committed = true,
                Sha256 = "0123456789abcdef012345",
            },
        };
        string json = JsonSerializer.Serialize(report, JsonOptions);
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        return new SyntheticReport(json, utf8.Length, Convert.ToHexStringLower(SHA256.HashData(utf8)));
    }

    private static object CreateRange(long start, long endExclusive)
    {
        return new
        {
            Start = start,
            EndExclusive = endExclusive,
            Length = endExclusive - start,
        };
    }
}

internal sealed record SyntheticReport(string Json, int Utf8ByteCount, string Sha256);
