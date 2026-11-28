using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Metadata;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>Resolves profile-owned output templates from accepted typed field values.</summary>
internal static partial class CompiledOutputNameResolver
{
    internal static OutputNameResolution Resolve(
        CompositionRunRequest request,
        IReadOnlyDictionary<string, byte[]> inputBytes,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        DateTimeOffset startedAtUtc,
        CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        CompiledOutputNamingRequirement output =
            request.CompiledComposition.V2Details.OutputNamingRequirement;
        return output?.RendererKind switch
        {
            null or CompiledOutputNameRendererKind.Static =>
                OutputNameResolution.Static(request.OutputFileName),
            CompiledOutputNameRendererKind.AbCodeV1 =>
                ResolveBankFields(
                    request,
                    inputBytes,
                    inputSummaries,
                    startedAtUtc),
            CompiledOutputNameRendererKind.NormalFlashCodeV1 or
                CompiledOutputNameRendererKind.TpFirmwareV1 =>
                    ResolveNormal(
                        request.CompiledComposition.V2Details.Provenance.Context.MemberId,
                        output,
                        request.OutputFileName,
                        request.IsOutputFileNameOverride,
                        request.OutputNamingInspection,
                        inputSummaries,
                        startedAtUtc,
                        request.OutputNamingAdmission,
                        ctrlRamVersionEdit,
                        UnverifiedCandidateOutputName.Applies(request.CompiledComposition)),
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
        OutputNamingAdmissionIdentity? admission = null,
        CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit = null,
        bool markUnverified = false)
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

        if (markUnverified && isExplicitOverride)
        {
            return new OutputNameResolution(
                requestedFileName,
                Summary: null,
                [
                    new CompositionIssue(
                        "output-naming.unverified-override-rejected",
                        "An unverified candidate output cannot use a custom file name; the unverified marker is part of the output name.",
                        severity: CompositionIssueSeverity.Error),
                ]);
        }

        OutputNameTokenValue ic = OutputNameTokenValue.ForIc(compiledIcId);
        List<CompositionIssue> issues = [];
        List<OutputNameTokenValue> tokenValues =
        [
            ic,
        ];

        if (output.RendererKind == CompiledOutputNameRendererKind.NormalFlashCodeV1)
        {
            OutputNameTokenValue dpVersion = ResolveDpVersion(
                output,
                acceptedInspection,
                inputSummaries,
                issues);
            tokenValues.Add(dpVersion);
        }

        OutputNameTokenValue tpVersion =
            ResolveTpVersion(
                output,
                acceptedInspection,
                inputSummaries,
                issues,
                ctrlRamVersionEdit);
        tokenValues.Add(tpVersion);
        tokenValues.Add(OutputNameTokenValue.ForDate(startedAtUtc));
        string automaticFileName = ResolveTemplate(output.FileNameTemplate, tokenValues);
        List<OutputNamingTokenSummary> tokens = [.. tokenValues.Select(static token => token.Summary)];
        if (markUnverified)
        {
            automaticFileName = UnverifiedCandidateOutputName.Apply(automaticFileName);
            tokens.Add(new OutputNamingTokenSummary(
                "verification",
                "unverified",
                IsKnown: true,
                null,
                null,
                "compiled-promotion-stage"));
        }

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
        OutputNamingSummary summary = new(
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

    private static OutputNameTokenValue ResolveDpVersion(
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
        OutputNameTokenValue? known = acceptedInspection is not null &&
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
            ? new OutputNameTokenValue(
                TokenId,
                facts.VersionToken,
                sourceSpaceId,
                sourceSha256,
                "canonical-dpcmi")
            : null;
        return known ?? ResolveMissingToken(
                output,
                TokenId,
                sourceSpaceId,
                sourceSha256,
                "canonical-dpcmi",
                issues);
    }

    private static OutputNameTokenValue ResolveTpVersion(
        CompiledOutputNamingRequirement output,
        AcceptedOutputNamingInspection? acceptedInspection,
        IReadOnlyList<InputArtifactSummary> inputSummaries,
        List<CompositionIssue> issues,
        CtrlRamFirmwareVersionDraftState? ctrlRamVersionEdit)
    {
        const string TokenId = "tp-version";
        if (ctrlRamVersionEdit is not null)
        {
            return new OutputNameTokenValue(
                TokenId,
                FormattableString.Invariant(
                    $"{ctrlRamVersionEdit.FirmwareVersion:X2}{ctrlRamVersionEdit.FirmwareSubVersion:X2}"),
                SourceAddressSpaceId: null,
                AcceptedSnapshotSha256: null,
                "accepted-ctrlram-version-draft");
        }

        CompiledOutputTokenRequirement requirement =
            GetTokenRequirement(
                output,
                TokenId,
                CompiledOutputTokenSourceKind.FirmwareConfigTpVersion);
        string? sourceSpaceId = null;
        string? sourceSha256 = null;
        OutputNameTokenValue? known = acceptedInspection is not null &&
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
            ? new OutputNameTokenValue(
                TokenId,
                FormattableString.Invariant(
                    $"{facts.TpFirmwareVersion:X2}{facts.TpFirmwareSubVersion:X2}"),
                sourceSpaceId,
                sourceSha256,
                "canonical-firmware-config-general-parameters")
            : null;
        return known ?? ResolveMissingToken(
                output,
                TokenId,
                sourceSpaceId,
                sourceSha256,
                "canonical-firmware-config-general-parameters",
                issues);
    }

    private static OutputNameTokenValue ResolveMissingToken(
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
        return new OutputNameTokenValue(
            tokenId,
            Value: null,
            sourceSpaceId,
            sourceSha256,
            parserId,
            MissingValue: value);
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
}
