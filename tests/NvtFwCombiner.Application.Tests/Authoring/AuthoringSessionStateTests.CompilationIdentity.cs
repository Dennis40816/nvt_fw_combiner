using NvtFwCombiner.Application.Authoring;

namespace NvtFwCombiner.Application.Tests.Authoring;

public sealed partial class AuthoringSessionStateTests
{
    /// <summary>Compilation-bound work cannot publish through a lease for another compilation.</summary>
    [Fact]
    public void PublicationCompilationFingerprintMustMatchItsLease()
    {
        const string firstCompilation =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string secondCompilation =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        var session = new AuthoringSessionState("dp-replace");
        _ = Activate(
            session,
            Catalog(
                "dp-replace",
                "dp-token",
                Route(
                    "NT51929",
                    "dp-replace",
                    "selector-free",
                    "nt51929-map",
                    "dp-29-fingerprint",
                    "reference",
                    "dp")));
        AuthoringPublicationLease lease = session.CapturePublicationLease(
            AuthoringDerivedResultKind.Inspection,
            firstCompilation);

        AuthoringPublicationResult wrongCompilation = session.TryPublish(
            lease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Inspection,
                "inspection-other-compilation",
                secondCompilation));
        AuthoringPublicationResult matchingCompilation = session.TryPublish(
            lease,
            new AuthoringDerivedPublication(
                AuthoringDerivedResultKind.Inspection,
                "inspection-current-compilation",
                firstCompilation));

        Assert.False(wrongCompilation.Succeeded);
        Assert.Equal(
            AuthoringSessionIssueCodes.InvalidPublication,
            wrongCompilation.Issue!.Code);
        Assert.True(matchingCompilation.Succeeded);
    }
}
