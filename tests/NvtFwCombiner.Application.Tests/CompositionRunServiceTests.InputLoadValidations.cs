using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Application.Tests;

public sealed partial class CompositionRunServiceTests
{
    /// <summary>A blocking uniform-range rule evaluates the same immutable bytes used by execution.</summary>
    [Fact]
    public async Task UniformInputRangeErrorBlocksExecution()
    {
        CompositionRunRequest request = CreateUniformInputValidationRequest(
            CompiledValidationSeverity.Error);
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["uniform-artifact"] = [0xAA, 0xAA, 0x10, 0x20],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp]));

        CompositionRunResult result = await service.PreviewAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(CompositionExecutionStatus.Failed, result.Status);
        CompositionIssue issue = Assert.Single(
            result.Report.Issues,
            candidate => candidate.Code == "input.uniform-placeholder");
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
        ValidationRunSummary validation = Assert.Single(result.Report.Validations);
        Assert.Equal(CompiledValidationStage.InputLoad, validation.Stage);
        Assert.Equal(ValidationRunStatus.Failed, validation.Status);
    }

    /// <summary>The same generic content rule can remain warning-only without creating another validator.</summary>
    [Fact]
    public async Task UniformInputRangeWarningIsReportedWithoutBlocking()
    {
        CompositionRunRequest request = CreateUniformInputValidationRequest(
            CompiledValidationSeverity.Warning);
        var service = new CompositionRunService(
            new FakeArtifactReader(new Dictionary<string, byte[]>
            {
                ["uniform-artifact"] = [0xFF, 0xFF, 0x10, 0x20],
            }),
            new FakeClock([FirstTimestamp, SecondTimestamp]));

        CompositionRunResult result = await service.PreviewAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(CompositionExecutionStatus.Succeeded, result.Status);
        CompositionIssue issue = Assert.Single(
            result.Report.Issues,
            candidate => candidate.Code == "input.uniform-placeholder");
        Assert.Equal(CompositionIssueSeverity.Warning, issue.Severity);
        ValidationRunSummary validation = Assert.Single(result.Report.Validations);
        Assert.Equal(CompiledValidationSeverity.Warning, validation.Severity);
        Assert.Equal(ValidationRunStatus.Failed, validation.Status);
    }

    private static CompositionRunRequest CreateUniformInputValidationRequest(
        CompiledValidationSeverity severity)
    {
        AddressSpace[] spaces =
        [
            new("input", 4, AddressSpaceMutability.Immutable),
            new("output-image", 4, AddressSpaceMutability.Mutable),
        ];
        var plan = new CompositionPlan(
            ImageInitialization.Blank("output-image", 4, 0),
            spaces,
            [
                CompositionOperation.CopyRange(
                    "copy-input",
                    10,
                    "input",
                    new ByteRange(0, 4),
                    "output-image",
                    new ByteRange(0, 4),
                    OverlapPolicy.Reject,
                    "copy validated input"),
            ]);
        CompiledValidationRequirement validation =
            CompiledValidationRequirements.RejectUniformInputRanges(
                "reject-uniform-input",
                severity,
                "input.uniform-placeholder",
                "input",
                [new ByteRange(0, 2)]);
        CompiledComposition compiled = CreateCompiledComposition(
            plan,
            new LegacyCompiledCompositionIdentity(
                "uniform-input-validation",
                "1.0.0",
                "NT-SYNTHETIC",
                "uniform-input",
                "general-merge",
                CompositionKind.Merge),
            "uniform.bin",
            validationRequirements: [validation]);
        return new CompositionRunRequest(
            "run-uniform-input-validation",
            compiled,
            [new InputArtifactBinding("input", "input", "uniform-artifact")],
            "uniform.bin");
    }
}
