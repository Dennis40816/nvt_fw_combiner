using System.Text.Json;
using System.Text.Json.Serialization;

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
        bool committed = false,
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

    public static string ReplaceWithAcceptedOutputDifferences(string runId = "replace-diff")
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
                    "PostbuildCrcHeader",
                    isAccepted: true,
                    "postbuild-single: legacy-combiner",
                    "Accepted: this range is inside the NT51927 / single approved postbuild CRC/header write ranges."),
            ]);
    }

    public static string CtrlRamWarning(
        string runId = "ui-smoke-warning",
        string issueCode = "input.address-space.truncated",
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
        bool committed,
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
        string explanation)
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
            BeforeSha256 = "11111111111111111111",
            AfterSha256 = "22222222222222222222",
        };
    }

    private static object Input()
    {
        return new
        {
            AddressSpaceId = "base-input",
            BindingId = "base",
            Size = 524288,
            Sha256 = "abcdef0123456789",
            ArtifactId = "base.bin",
        };
    }

    private static object CommandOperation(long length, IReadOnlyList<object> allowedWriteRanges)
    {
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
            Provenance = new
            {
                Kind = "built-in-profile",
            },
            Reason = "Run approved staged Combiner command: Combiner.exe /bin work.bin /mmap mmap.h.",
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
