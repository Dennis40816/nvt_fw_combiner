using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests;

/// <summary>Verifies validation report outcomes reject unknown closed discriminators precisely.</summary>
public sealed class ValidationRunSummaryTests
{
    /// <summary>Verifies each invalid enum reports the parameter that supplied it.</summary>
    [Fact]
    public void ConstructorReportsTheInvalidDiscriminatorParameter()
    {
        ArgumentOutOfRangeException stageException = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(stage: (CompiledValidationStage)(-1)));
        ArgumentOutOfRangeException statusException = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(status: (ValidationRunStatus)(-1)));
        ArgumentOutOfRangeException severityException = Assert.Throws<ArgumentOutOfRangeException>(() =>
            Create(severity: (CompiledValidationSeverity)(-1)));

        Assert.Equal("stage", stageException.ParamName);
        Assert.Equal("status", statusException.ParamName);
        Assert.Equal("severity", severityException.ParamName);
    }

    private static ValidationRunSummary Create(
        CompiledValidationStage stage = CompiledValidationStage.FinalOutput,
        ValidationRunStatus status = ValidationRunStatus.Passed,
        CompiledValidationSeverity severity = CompiledValidationSeverity.Error)
    {
        return new ValidationRunSummary(
            "validate-output",
            stage,
            status,
            severity,
            "output.invalid");
    }
}
