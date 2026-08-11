namespace NvtFwCombiner.Architecture.Tests;

public sealed partial class ApplicationBoundaryTests : RepositoryBoundaryTestBase
{
    /// <summary>Application does not retain test-only Hex Editor and diagnostics facades.</summary>
    [Fact]
    public void ApplicationConvergenceKeepsOnlyProductionOwnedHexAndDiagnosticContracts()
    {
        string session = string.Concat(
            ReadText("src/NvtFwCombiner.Application/HexEditor/RawBinaryEditorSession.cs"),
            '\n',
            ReadText("src/NvtFwCombiner.Application/HexEditor/RawBinaryEditorSession.ChangeTracking.cs"));
        string contracts = ReadText(
            "src/NvtFwCombiner.Application/HexEditor/RawBinaryEditorContracts.cs");
        string diagnostics = ReadText(
            "src/NvtFwCombiner.Application/Diagnostics/SystemInformationService.cs");

        Assert.DoesNotContain("private const int ViewportRowCount", session, StringComparison.Ordinal);
        Assert.DoesNotContain("private const int ViewportContextRows", session, StringComparison.Ordinal);
        Assert.DoesNotContain("public RawBinaryEditorViewport CreateViewport", session, StringComparison.Ordinal);
        Assert.DoesNotContain("public RawBinaryEditorSearchResult FindAscii", session, StringComparison.Ordinal);
        Assert.DoesNotContain("private RawBinaryEditorSearchResult SearchFailure", session, StringComparison.Ordinal);
        Assert.DoesNotContain("public bool HasOriginalValue =>", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("public bool HasOriginalValueAtAddress =>", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain("public bool HasChanges =>", contracts, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "IReadOnlyList<SystemDiagnosticTransition> Transitions { get; }",
            diagnostics,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "public IReadOnlyList<SystemDiagnosticTransition> Transitions",
            diagnostics,
            StringComparison.Ordinal);
    }
}
