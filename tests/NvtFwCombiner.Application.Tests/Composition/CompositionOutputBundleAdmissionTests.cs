using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Application.Tests.Composition;

/// <summary>Tests fail-closed bundle request option admission.</summary>
public sealed class CompositionOutputBundleAdmissionTests
{
    /// <summary>Bundle mode is exclusive with every legacy primary and AB delivery selector.</summary>
    [Theory]
    [InlineData(false, false, false, false, false, false, false)]
    [InlineData(true, true, false, false, false, false, false)]
    [InlineData(true, false, true, false, false, false, false)]
    [InlineData(true, false, false, true, false, false, false)]
    [InlineData(true, false, false, false, true, false, false)]
    [InlineData(true, false, false, false, false, true, false)]
    [InlineData(true, false, false, false, false, false, true)]
    public void BundleRejectsLegacyOutputAndAdditionalDeliveryOptions(
        bool build,
        bool hasOutputPath,
        bool hasPreviewOutputFileName,
        bool hasAutomaticOutputDirectory,
        bool outputPathUsesAutomaticName,
        bool hasAdditionalDeliveryOutputPath,
        bool additionalDeliveryOutputPathUsesAutomaticName)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            CompositionExecutionExperience.ValidateBundleOptionCombination(
                hasBundle: true,
                build,
                hasOutputPath,
                hasPreviewOutputFileName,
                hasAutomaticOutputDirectory,
                outputPathUsesAutomaticName,
                hasAdditionalDeliveryOutputPath,
                additionalDeliveryOutputPathUsesAutomaticName));
    }

    /// <summary>Bundle-off is unchanged and bundle-on accepts only the prepared exclusive path.</summary>
    [Fact]
    public void DisabledAndExclusiveBundleOptionsRemainAccepted()
    {
        CompositionExecutionExperience.ValidateBundleOptionCombination(
            hasBundle: false,
            build: false,
            hasOutputPath: true,
            hasPreviewOutputFileName: true,
            hasAutomaticOutputDirectory: true,
            outputPathUsesAutomaticName: true,
            hasAdditionalDeliveryOutputPath: true,
            additionalDeliveryOutputPathUsesAutomaticName: true);
        CompositionExecutionExperience.ValidateBundleOptionCombination(
            hasBundle: true,
            build: true,
            hasOutputPath: false,
            hasPreviewOutputFileName: false,
            hasAutomaticOutputDirectory: false,
            outputPathUsesAutomaticName: false,
            hasAdditionalDeliveryOutputPath: false,
            additionalDeliveryOutputPathUsesAutomaticName: false);
    }

    /// <summary>Any accepted-session publication drift invalidates a prepared bundle admission.</summary>
    [Theory]
    [InlineData("other-workflow", "route", "resolution", 7, "capability", "compilation", true, true)]
    [InlineData("workflow", "other-route", "resolution", 7, "capability", "compilation", true, true)]
    [InlineData("workflow", "route", "other-resolution", 7, "capability", "compilation", true, true)]
    [InlineData("workflow", "route", "resolution", 8, "capability", "compilation", true, true)]
    [InlineData("workflow", "route", "resolution", 7, "other-capability", "compilation", true, true)]
    [InlineData("workflow", "route", "resolution", 7, "capability", "other-compilation", true, true)]
    [InlineData("workflow", "route", "resolution", 7, "capability", "compilation", false, true)]
    [InlineData("workflow", "route", "resolution", 7, "capability", "compilation", true, false)]
    public void AdmissionIdentityRejectsEveryStalePublicationCoordinate(
        string workflowId,
        string routeId,
        string resolutionToken,
        long revision,
        string capabilityFingerprint,
        string compilationFingerprint,
        bool exactCapabilityMatches,
        bool executionAdmitted)
    {
        CompositionOutputBundleAdmissionIdentity identity = new(
            "workflow",
            "route",
            new ResolutionToken("resolution"),
            new AuthoringRevision(7),
            "capability",
            "compilation");

        Assert.False(identity.Matches(
            workflowId,
            routeId,
            new ResolutionToken(resolutionToken),
            new AuthoringRevision(revision),
            capabilityFingerprint,
            compilationFingerprint,
            exactCapabilityMatches,
            executionAdmitted));
    }
}
