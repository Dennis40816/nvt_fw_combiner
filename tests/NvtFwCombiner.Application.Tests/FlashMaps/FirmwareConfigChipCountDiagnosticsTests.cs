using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

/// <summary>Tests the route-declared policy for canonical FWConfig <c>Chip_Num</c>.</summary>
public sealed class FirmwareConfigChipCountDiagnosticsTests
{
    /// <summary>Single-input admission distinguishes unknown metadata, read zero and positive values.</summary>
    [Theory]
    [InlineData("positive", 3, null)]
    [InlineData("zero", 0, FirmwareConfigChipCountDiagnostics.RequiredIssueCode)]
    [InlineData("missing", null, FirmwareConfigChipCountDiagnostics.UnreadableIssueCode)]
    [InlineData("duplicate", null, FirmwareConfigChipCountDiagnostics.UnreadableIssueCode)]
    [InlineData("complement", null, FirmwareConfigChipCountDiagnostics.UnreadableIssueCode)]
    public void PositiveAssessmentPreservesActualNullableCount(string defect, int? expectedCount, string? code)
    {
        byte[] bytes = new byte[0x3000];
        bytes[0x1000] = 0x81;
        bytes[0x1001] = defect == "complement" ? (byte)0 : (byte)0x7E;
        bytes[0x1017] = defect == "zero" ? (byte)0 : (byte)3;
        if (defect != "missing") { "\0NVT"u8.CopyTo(bytes.AsSpan(0x1FFC)); }
        if (defect == "duplicate") { "\0NVT"u8.CopyTo(bytes.AsSpan(0x2FFC)); }
        CompositionIssue? issue = FirmwareConfigChipCountDiagnostics.AssessPositive(bytes, "tp-slot", out byte? actual);
        Assert.Equal<int?>(expectedCount, actual);
        Assert.Equal(code, issue?.Code);
        if (issue is not null)
        {
            Assert.Equal("tp-slot", issue.OperationId);
            Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
            Assert.Contains(expectedCount == 0 ? "read as 0" : "unreadable", issue.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("AB", issue.Message, StringComparison.Ordinal);
        }
    }

    /// <summary>Count-invariant routes expose zero as a non-blocking warning.</summary>
    [Fact]
    public void ZeroCountOnInvariantRouteCreatesWarning()
    {
        CompositionIssue? issue = FirmwareConfigChipCountDiagnostics.CreateZeroIssue(
            CreateMetadata(chipNumber: 0),
            FirmwareConfigChipCountRequirement.WarningIfZero,
            "inspect-fwconfig");

        Assert.NotNull(issue);
        Assert.Equal(FirmwareConfigChipCountDiagnostics.ZeroWarningIssueCode, issue.Code);
        Assert.Equal(CompositionIssueSeverity.Warning, issue.Severity);
        Assert.Equal("inspect-fwconfig", issue.OperationId);
        Assert.Contains("offset 0x17", issue.Message, StringComparison.Ordinal);
        Assert.Contains("Build may continue", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Count-dependent routes expose zero as a blocking error with the declared reason.</summary>
    [Fact]
    public void ZeroCountOnDependentRouteCreatesBlockingError()
    {
        CompositionIssue? issue = FirmwareConfigChipCountDiagnostics.CreateZeroIssue(
            CreateMetadata(chipNumber: 0),
            FirmwareConfigChipCountRequirement.RequiredPositive,
            "resolve-dynamic-diffdlm",
            "Dynamic DiffDLM uses IC Count to resolve active records.");

        Assert.NotNull(issue);
        Assert.Equal(FirmwareConfigChipCountDiagnostics.RequiredIssueCode, issue.Code);
        Assert.Equal(CompositionIssueSeverity.Error, issue.Severity);
        Assert.Equal("resolve-dynamic-diffdlm", issue.OperationId);
        Assert.Contains("offset 0x17", issue.Message, StringComparison.Ordinal);
        Assert.Contains("Dynamic DiffDLM", issue.Message, StringComparison.Ordinal);
        Assert.Contains("before Build", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>A positive canonical count requires no diagnostic for either route policy.</summary>
    [Theory]
    [InlineData(FirmwareConfigChipCountRequirement.WarningIfZero)]
    [InlineData(FirmwareConfigChipCountRequirement.RequiredPositive)]
    public void PositiveCountCreatesNoIssue(FirmwareConfigChipCountRequirement requirement)
    {
        CompositionIssue? issue = FirmwareConfigChipCountDiagnostics.CreateZeroIssue(
            CreateMetadata(chipNumber: 4),
            requirement,
            "inspect-fwconfig");

        Assert.Null(issue);
    }

    /// <summary>A blocking policy must explain the route dependency rather than name an IC.</summary>
    [Fact]
    public void RequiredZeroCountRejectsMissingDependencyReason()
    {
        _ = Assert.Throws<ArgumentNullException>(() =>
            FirmwareConfigChipCountDiagnostics.CreateZeroIssue(
                CreateMetadata(chipNumber: 0),
                FirmwareConfigChipCountRequirement.RequiredPositive,
                "resolve-route"));
    }

    /// <summary>Unknown policy values fail closed.</summary>
    [Fact]
    public void UnknownRequirementFailsClosed()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            FirmwareConfigChipCountDiagnostics.CreateZeroIssue(
                CreateMetadata(chipNumber: 0),
                (FirmwareConfigChipCountRequirement)99,
                "resolve-route"));
    }

    private static FirmwareConfigMetadata CreateMetadata(byte chipNumber)
    {
        return new FirmwareConfigMetadata(
            StructureStart: 0,
            FirmwareVersion: 0,
            FirmwareVersionBar: 0,
            IsFirmwareVersionBarValid: false,
            FirmwareSubVersion: 0,
            ChipNumber: chipNumber,
            CommonFwMajorVersion: 0,
            CommonFwMinorVersion: 0,
            CommonFwAdditionalVersion: 0,
            ProjectId: 0,
            Hardware: default);
    }
}
