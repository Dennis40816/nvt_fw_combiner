using System.Text.Json;
using System.Text.Json.Serialization;
using NvtFwCombiner.Contracts.Reports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.UiSmoke.Tests;

/// <summary>Small report JSON samples used by UI report projection smoke tests.</summary>
internal static class ReportJsonSamples
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string Succeeded(
        string profileId = "nt51927-standard-merge-gen-flash",
        string icId = "NT51927",
        string modeId = "standard-merge",
        string experienceId = "standard-merge",
        string compositionKind = "Merge",
        string runId = "ui-smoke",
        string startedAtUtc = "2026-07-01T00:00:00Z",
        string outputFileName = "preview.bin",
        long outputSize = 0,
        bool? committed = false,
        string outputSha256 = "abcdef")
    {
        return Create(
            profileId,
            icId,
            modeId,
            experienceId,
            compositionKind,
            runId,
            startedAtUtc,
            [],
            [],
            [],
            outputFileName,
            outputSize,
            committed,
            outputSha256);
    }

    public static string CtrlRamCommandSucceeded(string runId = "build-run")
    {
        return Create(
            "nt51927-ctrlram-replace",
            "NT51927",
            "ctrlram-replace",
            "ctrlram-replace",
            "Replace",
            runId,
            "2026-07-01T00:05:00Z",
            [],
            [CommandOperation(32, [])],
            [],
            "build.bin",
            32,
            committed: true,
            "0123456789abcdef012345");
    }

    public static string CtrlRamCommandTrace(int runtimeInvocationCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runtimeInvocationCount);
        return Create(
            "nt51927-ctrlram-replace",
            "NT51927",
            "ctrlram-replace",
            "ctrlram-replace",
            "Replace",
            "runtime-trace",
            "2026-07-01T00:05:00Z",
            [],
            [CommandOperation(32, [], runtimeInvocationCount)],
            [],
            "build.bin",
            32,
            committed: true,
            "0123456789abcdef012345");
    }

    public static string RuntimeOnlyCommandTrace()
    {
        return Create(
            "runtime-only-profile",
            "NT51927",
            "ctrlram-replace",
            "ctrlram-replace",
            "Replace",
            "runtime-only-trace",
            "2026-07-01T00:05:00Z",
            [],
            [CommandOperation(32, [], includeDeclaredCommand: false)],
            [],
            "build.bin",
            32,
            committed: false,
            "0123456789abcdef012345");
    }

    public static string ReplaceWithAcceptedOutputDifferences(
        string runId = "replace-diff",
        bool isHexPreviewComplete = true,
        int hexPreviewByteCount = 4)
    {
        return Create(
            "nt51927-ctrlram-replace",
            "NT51927",
            "ctrlram-replace",
            "ctrlram-replace",
            "Replace",
            runId,
            "2026-07-01T00:05:00Z",
            [],
            [CommandOperation(32, [Range(28, 32)])],
            [],
            "build.bin",
            32,
            committed: true,
            "0123456789abcdef012345",
            outputDifferences:
            [
                OutputDifference(
                    "diff-001",
                    Range(28, 32),
                    4,
                    OutputDifferenceClassifications.PostbuildCrcHeader,
                    isAccepted: true,
                    "postbuild-single: legacy-combiner",
                    "Accepted: this range is inside the NT51927 / single approved TP flash header / CRC fields postbuild write ranges.",
                    "TP flash header / CRC fields",
                    new
                    {
                        CategoryId = "tp-flash-header",
                        CategoryLabel = "TP Flash Header",
                        ParentId = "tp-header",
                        ParentLabel = "Header",
                        SubjectId = "nt51927-header:header-0-dlm-crc",
                        SubjectLabel = "DLM CRC 0",
                        Explanation = "Expected: postbuild recalculated DLM CRC 0.",
                    },
                    hexPreviewByteCount,
                    isHexPreviewComplete),
            ]);
    }

    public static string CtrlRamWarning(
        string runId = "ui-smoke-warning",
        string issueCode = CompositionIssueCodes.InputAddressSpaceTruncated,
        string? severity = "warning",
        string message = "Input ctrlram-input actual 6 bytes exceeded declared 4 bytes and was truncated.",
        string operationId = "replace-ctrlram")
    {
        return Create(
            "nt51927-ctrlram-replace",
            "NT51927",
            "ctrlram-replace",
            "ctrlram-replace",
            "Replace",
            runId,
            "2026-07-01T00:00:00Z",
            [],
            [],
            [Issue(issueCode, message, operationId, severity)],
            "preview.bin",
            32,
            committed: false,
            "abcdef012345");
    }

    public static string CtrlRamCommandIssue()
    {
        return Create(
            "nt51927-ctrlram-replace",
            "NT51927",
            "ctrlram-replace",
            "ctrlram-replace",
            "Replace",
            "ui-smoke-command",
            "2026-07-01T00:00:00Z",
            [Input()],
            [CommandOperation(524288, [Range(28928, 28932), Range(28952, 28956)])],
            [Issue("processor.tool.missing", "Combiner executable is not available.", "run-ctrlram-postbuild")],
            "No output",
            0,
            committed: false,
            string.Empty);
    }

    public static string CtrlRamInputs()
    {
        return Create(
            "nt51927-ctrlram-replace",
            "NT51927",
            "ctrlram-replace",
            "ctrlram-replace",
            "Replace",
            "ui-smoke-inputs",
            "2026-07-01T00:00:00Z",
            [
                Input("reference-base", "base.bin", 262144),
                Input("replace-ctrlram-vn", "replace-ctrlram-vn", 5728),
                Input("replace-ctrlram-normal-slave-r", "replace-ctrlram-normal-slave-r", 12288),
            ],
            [],
            [],
            "preview.bin",
            262144,
            committed: false,
            "abcdef012345");
    }

    public static string ReplaceWithManyOutputDifferences(
        int count,
        int sectionCount,
        string sectionPrefix = "Section")
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sectionCount);
        object[] differences = [
            .. Enumerable.Range(0, count).Select(index => OutputDifference(
                $"diff-{index:D5}",
                Range(index * 4L, (index * 4L) + 4),
                4,
                index == count - 1
                    ? OutputDifferenceClassifications.Unexpected
                    : OutputDifferenceClassifications.DeclaredReplacement,
                isAccepted: index != count - 1,
                $"evidence-{index:D5}",
                $"difference {index}",
                $"{sectionPrefix} {index % sectionCount:D2}")),
        ];
        return Create(
            "nt51927-ctrlram-replace",
            "NT51927",
            "ctrlram-replace",
            "ctrlram-replace",
            "Replace",
            "large-difference-report",
            "2026-07-01T00:05:00Z",
            [],
            [],
            [],
            "build.bin",
            count * 4L,
            committed: true,
            "0123456789abcdef012345",
            differences);
    }

    public static string ReplaceWithFullHexOutputDifference(int byteCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(byteCount);
        string beforeHex = new('A', checked(byteCount * 2));
        string afterHex = new('1', checked(byteCount * 2));
        return Create(
            "nt51927-ctrlram-replace",
            "NT51927",
            "ctrlram-replace",
            "ctrlram-replace",
            "Replace",
            "legacy-full-hex-report",
            "2026-07-01T00:05:00Z",
            [],
            [],
            [],
            "build.bin",
            byteCount,
            committed: true,
            "0123456789abcdef012345",
            [
                OutputDifference(
                    "diff-full-hex",
                    Range(0, byteCount),
                    byteCount,
                    OutputDifferenceClassifications.DeclaredReplacement,
                    isAccepted: true,
                    "legacy-full-hex",
                    "legacy full hex evidence",
                    "Payload",
                    beforeFullHex: beforeHex,
                    afterFullHex: afterHex,
                    beforeHexPreview: string.Empty,
                    afterHexPreview: string.Empty),
            ]);
    }

    private static string Create(
        string profileId,
        string icId,
        string modeId,
        string experienceId,
        string compositionKind,
        string runId,
        string startedAtUtc,
        IReadOnlyList<object> inputs,
        IReadOnlyList<object> operations,
        IReadOnlyList<object> issues,
        string outputFileName,
        long outputSize,
        bool? committed,
        string outputSha256,
        IReadOnlyList<object>? outputDifferences = null)
    {
        var report = new
        {
            ProfileId = profileId,
            IcId = icId,
            ModeId = modeId,
            ExperienceId = experienceId,
            CompositionKind = compositionKind,
            RunId = runId,
            StartedAtUtc = startedAtUtc,
            Inputs = inputs,
            Operations = operations,
            Mutations = Array.Empty<object>(),
            OutputDifferences = outputDifferences ?? [],
            Issues = issues,
            Output = new
            {
                FileName = outputFileName,
                Size = outputSize,
                Committed = committed,
                Sha256 = outputSha256,
            },
        };
        return JsonSerializer.Serialize(report, JsonOptions);
    }

    private static object OutputDifference(
        string differenceId,
        object range,
        long changedByteCount,
        string classification,
        bool isAccepted,
        string evidence,
        string explanation,
        string? sectionLabel = null,
        object? semantic = null,
        int hexPreviewByteCount = 4,
        bool isHexPreviewComplete = true,
        string? beforeFullHex = null,
        string? afterFullHex = null,
        string beforeHexPreview = "AABBCCDD",
        string afterHexPreview = "11223344")
    {
        return new
        {
            DifferenceId = differenceId,
            Range = range,
            ChangedByteCount = changedByteCount,
            Classification = classification,
            IsAccepted = isAccepted,
            Evidence = evidence,
            Explanation = explanation,
            SectionLabel = sectionLabel,
            Semantic = semantic,
            BeforeSha256 = "11111111111111111111",
            AfterSha256 = "22222222222222222222",
            BeforeHex = beforeFullHex,
            AfterHex = afterFullHex,
            BeforeHexPreview = beforeHexPreview,
            AfterHexPreview = afterHexPreview,
            HexPreviewByteCount = hexPreviewByteCount,
            IsHexPreviewComplete = isHexPreviewComplete,
        };
    }

    private static object Input()
    {
        return Input("base-input", "base.bin", 524288);
    }

    private static object Input(string addressSpaceId, string artifactId, long size)
    {
        return new
        {
            AddressSpaceId = addressSpaceId,
            BindingId = artifactId,
            Size = size,
            Sha256 = "abcdef0123456789",
            ArtifactId = artifactId,
        };
    }

    private static object CommandOperation(
        long length,
        IReadOnlyList<object> allowedWriteRanges,
        int runtimeInvocationCount = 1,
        bool includeDeclaredCommand = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(runtimeInvocationCount);
        return new
        {
            Sequence = 900,
            OperationId = "run-ctrlram-postbuild",
            Kind = "run-external-processor",
            Status = "planned",
            OverlapPolicy = "reject",
            TargetSpaceId = "output-image",
            TargetRange = Range(0, length),
            ProcessorId = "legacy-combiner",
            ToolBindingId = "legacy-combiner-1.13.0",
            ProcessorAllowedReadRanges = new[] { Range(0, length) },
            ProcessorAllowedWriteRanges = allowedWriteRanges,
            ExecutedCommands = Enumerable.Range(1, runtimeInvocationCount)
                .Select(index => new
                {
                    ExecutablePath = "C:\\tools\\legacy-combiner\\Combiner.exe",
                    WorkingDirectory = runtimeInvocationCount == 1
                        ? "C:\\staging\\ui-smoke-command"
                        : $"C:\\staging\\ui-smoke-command-{index:D2}",
                    Arguments = new[]
                    {
                        index == 1 ? "MERGE_MODE" : "NT51927BASED_GEN_CRC_MODE",
                        "C:\\staging\\ui-smoke-command\\output\\nt51927_fw.bin",
                        "C:\\staging\\ui-smoke-command\\BIN\\Normal_Ctrlram.bin",
                        "0x0",
                        "0x22800",
                        "12288",
                    },
                })
                .ToArray(),
            Provenance = new
            {
                Kind = "built-in-profile",
            },
            Reason = includeDeclaredCommand
                ? "Run approved staged Combiner command: Combiner.exe /bin work.bin /mmap mmap.h."
                : "Run external processor.",
        };
    }

    private static Dictionary<string, object> Issue(
        string code,
        string message,
        string operationId,
        string? severity = null)
    {
        var issue = new Dictionary<string, object>
        {
            ["Code"] = code,
            ["Message"] = message,
            ["OperationId"] = operationId,
        };
        if (!string.IsNullOrWhiteSpace(severity))
        {
            issue["Severity"] = severity;
        }

        return issue;
    }

    private static object Range(long start, long endExclusive)
    {
        return new
        {
            Start = start,
            EndExclusive = endExclusive,
            Length = endExclusive - start,
        };
    }
}
