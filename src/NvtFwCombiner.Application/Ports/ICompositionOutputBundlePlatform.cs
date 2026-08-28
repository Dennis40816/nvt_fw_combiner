using NvtFwCombiner.Application.Composition;

#pragma warning disable CS1591 // Infrastructure adapter contracts are not end-user API.

namespace NvtFwCombiner.Application.Ports;

/// <summary>Platform-owned normalization for one accepted physical artifact locator.</summary>
public interface ICompositionArtifactIdentityPolicy
{
    CompositionAcceptedArtifactIdentity Resolve(string artifactLocator);
}

/// <summary>Opaque canonical identity and safe original name returned by the platform adapter.</summary>
public sealed record CompositionAcceptedArtifactIdentity(
    string CanonicalIdentity,
    string OriginalFileName);

/// <summary>Platform validation of one prepared bundle destination.</summary>
public interface ICompositionOutputBundleDestinationValidator
{
    CompositionOutputBundleDestinationValidation Validate(
        CompositionOutputBundleIntent intent);
}
