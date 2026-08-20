using NvtFwCombiner.Application.Composition;

namespace NvtFwCombiner.Application.Ports;

/// <summary>Platform-owned normalization for one accepted physical artifact locator.</summary>
internal interface ICompositionArtifactIdentityPolicy
{
    CompositionAcceptedArtifactIdentity Resolve(string artifactLocator);
}

/// <summary>Opaque canonical identity and safe original name returned by the platform adapter.</summary>
internal sealed record CompositionAcceptedArtifactIdentity(
    string CanonicalIdentity,
    string OriginalFileName);

/// <summary>Platform validation of one prepared bundle destination.</summary>
internal interface ICompositionOutputBundleDestinationValidator
{
    CompositionOutputBundleDestinationValidation Validate(
        CompositionOutputBundleIntent intent);
}
