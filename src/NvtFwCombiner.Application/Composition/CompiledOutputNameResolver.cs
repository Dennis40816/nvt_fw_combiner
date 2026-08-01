using System.Globalization;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Executes the one renderer selected by the compiled output-name contract.</summary>
internal static class CompiledOutputNameResolver
{
    internal static OutputNameResolution Resolve(
        CompositionRunRequest request,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        DateTimeOffset startedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        CompiledOutputNamingRequirement? output =
            request.CompiledComposition.V2Details?.OutputNamingRequirement;
        return output?.RendererKind switch
        {
            null or CompiledOutputNameRendererKind.Static =>
                OutputNameResolution.Static(request.OutputFileName),
            CompiledOutputNameRendererKind.AbCodeV1 =>
                AbCodeOutputNameResolver.Resolve(
                    request,
                    inputBytes,
                    inputSummaries,
                    startedAtUtc),
            CompiledOutputNameRendererKind.NormalFlashCodeV1 or
                CompiledOutputNameRendererKind.TpFirmwareV1 =>
                    ResolveNormal(
                        request.CompiledComposition.IcId,
                        output,
                        request.OutputFileName,
                        request.IsOutputFileNameOverride,
                        request.OutputNamingInspection,
                        inputSummaries,
                        startedAtUtc,
                        request.OutputNamingAdmission),
            CompiledOutputNameRendererKind.DeferredTokenTemplate =>
                throw new InvalidOperationException(
                    "A deferred output-name template cannot execute."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(request),
                output.RendererKind,
                "Unknown compiled output-name renderer."),
        };
    }

    internal static OutputNameResolution ResolveNormal(
        string compiledIcId,
        CompiledOutputNamingRequirement output,
        string requestedFileName,
        bool isExplicitOverride,
        AcceptedOutputNamingInspection? acceptedInspection,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        DateTimeOffset startedAtUtc,
        OutputNamingAdmissionIdentity? admission = null)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentException.ThrowIfNullOrWhiteSpace(requestedFileName);
        ArgumentNullException.ThrowIfNull(inputSummaries);
        if (output.RendererKind is not (
                CompiledOutputNameRendererKind.NormalFlashCodeV1 or
                CompiledOutputNameRendererKind.TpFirmwareV1))
        {
            throw new ArgumentException(
                "Normal output resolution requires a compiled normal FlashCode or TP-firmware renderer.",
                nameof(output));
        }

        if (acceptedInspection is not null &&
            !acceptedInspection.IsCurrent(inputSummaries))
        {
            return new OutputNameResolution(
                requestedFileName,
                Summary: null,
                [
                    new CompositionIssue(
                        "output-naming.inspection-stale",
                        "The accepted metadata inspection does not match the immutable execution input; no inspected value was used for output naming.",
                        severity: CompositionIssueSeverity.Error),
                ]);
        }

        string canonicalIcId = GetCanonicalIcIdentity(compiledIcId);
        var issues = new List<CompositionIssue>();
        var tokens = new List<OutputNamingTokenSummary>
        {
            new("ic", canonicalIcId, IsKnown: true, null, null, "compiled-profile"),
        };

        TokenResolution? dpVersion = null;
        if (output.RendererKind == CompiledOutputNameRendererKind.NormalFlashCodeV1)
        {
            dpVersion = ResolveDpVersion(
                output,
                acceptedInspection,
                inputSummaries,
                issues);
            tokens.Add(dpVersion.Summary);
        }

        TokenResolution tpVersion =
            ResolveTpVersion(
                output,
                acceptedInspection,
                inputSummaries,
                issues);
        tokens.Add(tpVersion.Summary);
        string date = startedAtUtc.UtcDateTime.ToString(
            "yyyyMMdd",
            CultureInfo.InvariantCulture);
        tokens.Add(new OutputNamingTokenSummary(
            "date",
            date,
            IsKnown: true,
            null,
            null,
            "utc-clock"));

        string automaticFileName = output.RendererKind switch
        {
            CompiledOutputNameRendererKind.NormalFlashCodeV1 =>
                $"{canonicalIcId}_FlashCode_D{dpVersion!.Summary.Value}T{tpVersion.Summary.Value}_{date}.bin",
            CompiledOutputNameRendererKind.TpFirmwareV1 =>
                $"{canonicalIcId}_TPFW_T{tpVersion.Summary.Value}_{date}.bin",
            CompiledOutputNameRendererKind.Static or
                CompiledOutputNameRendererKind.DeferredTokenTemplate or
                CompiledOutputNameRendererKind.AbCodeV1 =>
                    throw new InvalidOperationException(
                        "Normal renderer changed during resolution."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(output),
                output.RendererKind,
                "Unknown compiled output-name renderer."),
        };
        CompiledOutputNamingRequirement.ValidateRuntimeLiteralFileName(
            automaticFileName,
            nameof(automaticFileName));
        if (isExplicitOverride)
        {
            CompiledOutputNamingRequirement.ValidateRuntimeLiteralFileName(
                requestedFileName,
                nameof(requestedFileName));
        }

        string actualFileName =
            isExplicitOverride ? requestedFileName : automaticFileName;
        string rendererId = output.RendererKind switch
        {
            CompiledOutputNameRendererKind.NormalFlashCodeV1 =>
                "normal-flashcode-v1",
            CompiledOutputNameRendererKind.TpFirmwareV1 =>
                "tp-firmware-v1",
            CompiledOutputNameRendererKind.Static or
                CompiledOutputNameRendererKind.DeferredTokenTemplate or
                CompiledOutputNameRendererKind.AbCodeV1 =>
                    throw new InvalidOperationException(
                        "Normal renderer changed during summary creation."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(output),
                output.RendererKind,
                "Unknown compiled output-name renderer."),
        };
        var summary = new OutputNamingSummary(
            rendererId,
            output.FileNameTemplate,
            automaticFileName,
            actualFileName,
            isExplicitOverride,
            "utc",
            startedAtUtc,
            tokens,
            admission is null
                ? null
                : new OutputNamingAdmissionSummary(
                    admission.RouteId,
                    admission.CompilationFingerprint,
                    admission.ResolutionToken.Value,
                    admission.AuthoringRevision));
        return new OutputNameResolution(actualFileName, summary, issues);
    }

    internal static string GetCanonicalIcIdentity(string compiledIcId)
    {
        CompiledOutputNamingRequirement.ValidateCanonicalIcIdentity(
            compiledIcId,
            nameof(compiledIcId));
        return compiledIcId;
    }

    private static TokenResolution ResolveDpVersion(
        CompiledOutputNamingRequirement output,
        AcceptedOutputNamingInspection? acceptedInspection,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        List<CompositionIssue> issues)
    {
        const string TokenId = "dp-version";
        CompiledOutputTokenRequirement requirement =
            GetTokenRequirement(
                output,
                TokenId,
                CompiledOutputTokenSourceKind.DpcmiVersion);
        string? sourceSpaceId = null;
        string? sourceSha256 = null;
        TokenResolution? known = acceptedInspection is not null &&
            acceptedInspection.TryGetOutputNamingSource(
                requirement.MetadataBindingId!,
                requirement.MetadataSpaceId!,
                inputSummaries,
                out sourceSpaceId,
                out sourceSha256) &&
            DpcmiMetadataProjector.TryProject(
                acceptedInspection.Snapshot,
                requirement.MetadataBindingId!,
                out DpcmiMetadataFacts facts)
            ? new TokenResolution(new OutputNamingTokenSummary(
                TokenId,
                facts.VersionToken,
                IsKnown: true,
                sourceSpaceId,
                sourceSha256,
                "canonical-dpcmi"))
            : null;
        return known ?? ResolveMissingToken(
                output,
                TokenId,
                sourceSpaceId,
                sourceSha256,
                "canonical-dpcmi",
                issues);
    }

    private static TokenResolution ResolveTpVersion(
        CompiledOutputNamingRequirement output,
        AcceptedOutputNamingInspection? acceptedInspection,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        List<CompositionIssue> issues)
    {
        const string TokenId = "tp-version";
        CompiledOutputTokenRequirement requirement =
            GetTokenRequirement(
                output,
                TokenId,
                CompiledOutputTokenSourceKind.FirmwareConfigTpVersion);
        string? sourceSpaceId = null;
        string? sourceSha256 = null;
        TokenResolution? known = acceptedInspection is not null &&
            acceptedInspection.TryGetOutputNamingSource(
                requirement.MetadataBindingId!,
                requirement.MetadataSpaceId!,
                inputSummaries,
                out sourceSpaceId,
                out sourceSha256) &&
            FirmwareConfigGeneralParametersProjector.TryProject(
                acceptedInspection.Snapshot,
                requirement.MetadataBindingId!,
                out FirmwareConfigGeneralParametersFacts facts) &&
            facts.IsTpFirmwareVersionComplementValid
            ? new TokenResolution(new OutputNamingTokenSummary(
                TokenId,
                FormattableString.Invariant(
                    $"{facts.TpFirmwareVersion:X2}{facts.TpFirmwareSubVersion:X2}"),
                IsKnown: true,
                sourceSpaceId,
                sourceSha256,
                "canonical-firmware-config-general-parameters"))
            : null;
        return known ?? ResolveMissingToken(
                output,
                TokenId,
                sourceSpaceId,
                sourceSha256,
                "canonical-firmware-config-general-parameters",
                issues);
    }

    private static TokenResolution ResolveMissingToken(
        CompiledOutputNamingRequirement output,
        string tokenId,
        string? sourceSpaceId,
        string? sourceSha256,
        string parserId,
        List<CompositionIssue> issues)
    {
        CompiledOutputTokenRequirement requirement =
            output.TokenRequirements.Single(candidate =>
                StringComparer.Ordinal.Equals(candidate.TokenId, tokenId));
        bool hasPlaceholder =
            requirement.MissingPolicy == CompiledOutputTokenMissingPolicy.UsePlaceholder;
        string value = hasPlaceholder
            ? requirement.Placeholder!
            : string.Empty;
        issues.Add(new CompositionIssue(
            hasPlaceholder
                ? "output-naming.metadata-unknown"
                : "output-naming.metadata-required",
            hasPlaceholder
                ? $"Output filename token '{tokenId}' could not be read from the accepted canonical metadata inspection; the compiled placeholder '{value}' was used."
                : $"Output filename token '{tokenId}' is required but unavailable from the accepted canonical metadata inspection.",
            sourceSpaceId,
            hasPlaceholder
                ? CompositionIssueSeverity.Warning
                : CompositionIssueSeverity.Error));
        return new TokenResolution(new OutputNamingTokenSummary(
            tokenId,
            value,
            IsKnown: false,
            sourceSpaceId,
            sourceSha256,
            parserId));
    }

    private static CompiledOutputTokenRequirement GetTokenRequirement(
        CompiledOutputNamingRequirement output,
        string tokenId,
        CompiledOutputTokenSourceKind sourceKind)
    {
        CompiledOutputTokenRequirement requirement =
            output.TokenRequirements.Single(candidate =>
                StringComparer.Ordinal.Equals(candidate.TokenId, tokenId));
        return requirement.SourceKind != sourceKind ||
               requirement.MetadataBindingId is null ||
               requirement.MetadataSpaceId is null
            ? throw new InvalidOperationException(
                $"Compiled output token '{tokenId}' does not retain its typed metadata source.")
            : requirement;
    }

    private sealed record TokenResolution(OutputNamingTokenSummary Summary);
}
