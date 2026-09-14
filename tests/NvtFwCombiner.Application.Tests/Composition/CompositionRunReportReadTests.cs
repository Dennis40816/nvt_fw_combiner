using System.Text.Json;
using System.Text.Json.Nodes;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Composition;

/// <summary>Completeness is independent of firmware support, later extensions and output production.</summary>
public sealed class CompositionRunReportReadTests
{
    /// <summary>The serializer's actual report and its supported minimal legacy form are recognized.</summary>
    [Fact]
    public void SerializedAndLegacyReportsAreRecognizedWithoutNewFields()
    {
        JsonObject report = CreateSerializedReport();
        Assert.Equal(CompositionRunReportJson.ReadCompleteness.Recognized, Assess(report));
        foreach (string field in new[] { "Output", "ProfileVersion", "CompletedAtUtc", "Validations", "OutputDifferences", "InputDiagnostics" })
        {
            _ = report.Remove(field);
        }
        Assert.Equal(CompositionRunReportJson.ReadCompleteness.Recognized, Assess(report));
    }

    /// <summary>A legacy report without AB format evidence remains readable without inventing a Common format.</summary>
    [Fact]
    public void LegacyReportWithoutAbMergeFormatIsRecognizedWithoutFabricatingCommon()
    {
        JsonObject report = CreateSerializedReport();
        _ = report.Remove("AbMergeFormat");
        using var document = JsonDocument.Parse(report.ToJsonString());

        Assert.False(document.RootElement.TryGetProperty("AbMergeFormat", out _));
        Assert.Equal(
            CompositionRunReportJson.ReadCompleteness.Recognized,
            CompositionRunReportJson.AssessReadCompleteness(document.RootElement, TestContext.Current.CancellationToken));
        Assert.False(document.RootElement.TryGetProperty("AbMergeFormat", out _));
    }

    /// <summary>Each legacy identity field is required as data, not checked against a support catalog.</summary>
    [Theory]
    [InlineData("ProfileId")]
    [InlineData("IcId")]
    [InlineData("ModeId")]
    [InlineData("ExperienceId")]
    [InlineData("CompositionKind")]
    [InlineData("RunId")]
    [InlineData("StartedAtUtc")]
    public void MissingEmptyOrNonStringIdentityIsUnknown(string field)
    {
        JsonObject report = CreateSerializedReport();
        foreach (JsonNode? value in new JsonNode?[] { null, JsonValue.Create(" "), JsonValue.Create(3), new JsonObject() })
        {
            report[field] = value;
            Assert.Equal(CompositionRunReportJson.ReadCompleteness.Unknown, Assess(report));
        }
        _ = report.Remove(field);
        Assert.Equal(CompositionRunReportJson.ReadCompleteness.Unknown, Assess(report));
    }

    /// <summary>Invalid issue shapes cannot collapse to an admitted clean issue collection.</summary>
    [Theory]
    [InlineData("null")]
    [InlineData("\"bad\"")]
    [InlineData("[null]")]
    [InlineData("[3]")]
    [InlineData("[{}]")]
    [InlineData("[{\"Code\":3,\"Message\":\"test\"}]")]
    [InlineData("[{\"Code\":\"test\",\"Message\":null}]")]
    [InlineData("[{\"Code\":\"test\",\"Message\":\"test\",\"Severity\":false}]")]
    [InlineData("[{\"Code\":\"test\",\"Message\":\"test\",\"Severity\":\"info\",\"severity\":\"error\"}]")]
    public void MalformedIssueEvidenceIsUnknown(string issues)
    {
        JsonObject report = CreateSerializedReport();
        report["Issues"] = JsonNode.Parse(issues);
        Assert.Equal(CompositionRunReportJson.ReadCompleteness.Unknown, Assess(report));
        _ = report.Remove("Issues");
        Assert.Equal(CompositionRunReportJson.ReadCompleteness.Unknown, Assess(report));
    }

    /// <summary>Conflicting duplicate evidence never resolves implicitly into a successful run.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DuplicateRootOrIssuePropertiesAreUnknown(bool issueProperty)
    {
        JsonObject report = CreateSerializedReport();
        string json = issueProperty
            ? report.ToJsonString().Replace("\"Issues\":[]", "\"Issues\":[{\"Code\":\"a\",\"Message\":\"a\",\"Severity\":\"error\",\"Severity\":\"info\"}]", StringComparison.Ordinal)
            : report.ToJsonString().Insert(1, "\"Issues\":[],");
        using var document = JsonDocument.Parse(json);
        Assert.Equal(CompositionRunReportJson.ReadCompleteness.Unknown,
            CompositionRunReportJson.AssessReadCompleteness(document.RootElement, TestContext.Current.CancellationToken));
    }

    /// <summary>Cancelled publication can stop before assessing any imported evidence.</summary>
    [Fact]
    public void CancelledAssessmentDoesNotPublishCompleteness()
    {
        using var document = JsonDocument.Parse(CreateSerializedReport().ToJsonString());
        _ = Assert.Throws<OperationCanceledException>(() => CompositionRunReportJson.AssessReadCompleteness(
            document.RootElement, new CancellationToken(canceled: true)));
    }

    private static CompositionRunReportJson.ReadCompleteness Assess(JsonObject report)
    {
        using var document = JsonDocument.Parse(report.ToJsonString());
        return CompositionRunReportJson.AssessReadCompleteness(document.RootElement);
    }

    private static JsonObject CreateSerializedReport()
    {
        var report = new CompositionRunReport("run", "profile", "1.0", "unknown-ic", "mode", "experience",
            CompositionKind.Merge, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch,
            [], [], [], [], new OutputArtifactSummary("preview.bin", 0, "hash", committed: false));
        var result = new CompositionRunResult(CompositionExecutionStatus.Succeeded, ReadOnlyMemory<byte>.Empty,
            report, null, null, null, null, null, null);
        return JsonNode.Parse(CompositionRunReportJson.Serialize(result))!.AsObject();
    }
}
