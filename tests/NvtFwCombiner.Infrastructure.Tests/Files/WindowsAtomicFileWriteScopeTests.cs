using NvtFwCombiner.Infrastructure.Files;

namespace NvtFwCombiner.Infrastructure.Tests.Files;

/// <summary>Windows anchor failures retain NTSTATUS and distinguish redirected paths.</summary>
public sealed class WindowsAtomicFileWriteScopeTests
{
    /// <summary>Only STATUS_REPARSE_POINT_ENCOUNTERED maps to the stable redirect HRESULT.</summary>
    [Theory]
    [InlineData(unchecked((int)0xC000050B), unchecked((int)0x8007112B))]
    [InlineData(unchecked((int)0xC0000022), unchecked((int)0x80131620))]
    public void AnchorStatusPreservesDiagnosticAndClassifiesOnlyReparse(int status, int expectedHResult)
    {
        IOException failure = WindowsAtomicFileWriteScope.AnchorFailureForStatus(status);

        Assert.Equal(expectedHResult, failure.HResult);
        Assert.Contains($"NTSTATUS 0x{status:X8}", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Saved Rule", failure.Message, StringComparison.Ordinal);
    }
}
