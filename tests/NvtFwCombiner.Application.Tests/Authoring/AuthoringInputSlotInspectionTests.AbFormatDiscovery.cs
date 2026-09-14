using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

public sealed partial class AuthoringInputSlotInspectionTests
{
    /// <summary>A retained source is confined to a blocked declaration with its matching stable file identity.</summary>
    [Theory]
    [InlineData("valid")]
    [InlineData("wrong-stamp")]
    [InlineData("compiled")]
    [InlineData("accepted")]
    [InlineData("unblocked")]
    public void CapturedSourceCannotGrantFirmwareAdmission(string state)
    {
        ResolvedCapabilityRoute route = CreateRoute(ExperienceIds.StandardMerge);
        byte[] bytes = [1, 2, 3];
        FileStamp stamp = FileStamp.FromBytes(bytes);
        var source = new SelectedFileContentInspection(stamp, acceptedBytes: bytes);
        InputSelectionMemberReadiness readiness = ReadySelection() with
        {
            Readiness = state == "unblocked" ? ResolvedChildReadiness.Ready : ResolvedChildReadiness.Blocked,
        };
        AuthoringInputSlotStatus Create()
        {
            return new AuthoringInputSlotStatus(route.Identity, route.ResolutionToken, new AuthoringRevision(1),
                route.CapabilityFingerprint, state == "compiled" ? CapabilityFingerprint : null,
                readiness, SourceSpace, inspectionLifecycle: null,
                state == "wrong-stamp" ? FileStamp.FromBytes([4, 5, 6]) : stamp,
                inspection: null, selectedPathHint: "source.bin",
                acceptedBytes: state == "accepted" ? new ReadOnlyMemory<byte>(bytes) : (ReadOnlyMemory<byte>?)null,
                capturedSource: source);
        }
        if (state != "valid")
        {
            _ = Assert.Throws<ArgumentException>(Create);
            return;
        }
        AuthoringInputSlotStatus status = Create();
        Assert.Null(status.AcceptedBytes);
        Assert.Null(status.CompilationFingerprint);
        Assert.Null(status.InspectionLifecycle);
        Assert.Equal(ResolvedChildReadiness.Blocked, status.Readiness);
        Assert.Same(source, status.CapturedSource);
    }
}
