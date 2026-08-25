using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.InputInspection;

/// <summary>Closed artifact shape established from one resolved compiled composition.</summary>
public enum CompiledFirmwareArtifactKind
{
    /// <summary>The declared facts cannot distinguish this artifact safely.</summary>
    Unknown,

    /// <summary>The TP source is plausible while the complete DP/Initial-Code composition is absent.</summary>
    TpFirmware,

    /// <summary>The exact resolved container has plausible required DP/Initial-Code and TP content.</summary>
    FlashCode,
}

/// <summary>Closed evidence kinds used by canonical firmware artifact classification.</summary>
public enum CompiledFirmwareArtifactSignalKind
{
    /// <summary>The candidate length equals the resolved map capacity.</summary>
    DeclaredContainerCapacity,

    /// <summary>The candidate covers the complete compiled DP/Initial-Code source projection.</summary>
    DpSourceCoverage,

    /// <summary>The candidate covers the complete compiled TP source projection.</summary>
    TpSourceCoverage,

    /// <summary>All profile-declared DP/Initial-Code plausibility ranges are non-uniform.</summary>
    DpContentPlausibility,

    /// <summary>All profile-declared TP plausibility ranges are non-uniform.</summary>
    TpContentPlausibility,
}

/// <summary>Closed result state for one classification signal.</summary>
public enum CompiledFirmwareArtifactSignalStatus
{
    /// <summary>The resolved declaration and candidate bytes satisfy the signal.</summary>
    Satisfied,

    /// <summary>The declaration exists but the candidate does not satisfy it.</summary>
    NotSatisfied,

    /// <summary>The resolved profile did not declare enough authority to evaluate the signal.</summary>
    NotDeclared,
}

/// <summary>One immutable classification observation retained for UI, CLI, and reports.</summary>
public sealed record CompiledFirmwareArtifactSignal(
    CompiledFirmwareArtifactSignalKind Kind,
    CompiledFirmwareArtifactSignalStatus Status,
    string? AddressSpaceId,
    long RequiredEndExclusive,
    ByteRange? FailedRange);

/// <summary>Application-owned classification result over one immutable candidate snapshot.</summary>
public sealed class CompiledFirmwareArtifactClassification
{
    private readonly CompiledFirmwareArtifactSignal[] _signals;

    internal CompiledFirmwareArtifactClassification(
        CompiledFirmwareArtifactKind kind,
        IEnumerable<CompiledFirmwareArtifactSignal> signals)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown firmware artifact kind.");
        }

        ArgumentNullException.ThrowIfNull(signals);
        _signals = [.. signals];
        if (_signals.Length != Enum.GetValues<CompiledFirmwareArtifactSignalKind>().Length ||
            _signals.Select(static signal => signal.Kind).Distinct().Count() != _signals.Length)
        {
            throw new ArgumentException(
                "Firmware artifact classification requires exactly one signal of every declared kind.",
                nameof(signals));
        }

        Array.Sort(_signals, static (left, right) => left.Kind.CompareTo(right.Kind));
        Kind = kind;
        Signals = Array.AsReadOnly(_signals);
    }

    /// <summary>Canonical classification; never selects an IC, map, route, or support state.</summary>
    public CompiledFirmwareArtifactKind Kind { get; }

    /// <summary>Complete typed evidence used to establish <see cref="Kind"/>.</summary>
    public IReadOnlyList<CompiledFirmwareArtifactSignal> Signals { get; }

    /// <summary>Gets the terminal Application decision for DP metadata applicability.</summary>
    public bool IsDpMetadataApplicable => Kind != CompiledFirmwareArtifactKind.TpFirmware && _signals.Single(static signal => signal.Kind == CompiledFirmwareArtifactSignalKind.DpContentPlausibility).Status != CompiledFirmwareArtifactSignalStatus.NotSatisfied;
}

/// <summary>
/// Classifies one candidate only after an IC/capability has resolved to a compiled profile.
/// Filename, PID, version, CMI, hash, and informational hints are deliberately absent.
/// </summary>
public static class CompiledFirmwareArtifactClassifier
{
    /// <summary>Classifies one immutable candidate without changing operation admission.</summary>
    public static CompiledFirmwareArtifactClassification Classify(
        CompiledComposition compiledComposition,
        ReadOnlySpan<byte> candidate)
    {
        ArgumentNullException.ThrowIfNull(compiledComposition);

        V2CompiledCompositionDetails details = compiledComposition.V2Details;
        if (details is null)
        {
            return Unknown(capacity: 0);
        }

        long capacity = details.Provenance.ResolvedMap.CapacityBytes;
        CompiledFirmwareArtifactSignal capacitySignal = new(
            CompiledFirmwareArtifactSignalKind.DeclaredContainerCapacity,
            candidate.Length == capacity
                ? CompiledFirmwareArtifactSignalStatus.Satisfied
                : CompiledFirmwareArtifactSignalStatus.NotSatisfied,
            AddressSpaceId: null,
            RequiredEndExclusive: capacity,
            FailedRange: null);
        SectionSignals dp = InspectSection(
            compiledComposition,
            candidate,
            CompiledInputArtifactClass.DpFirmware,
            CompiledFirmwareArtifactSignalKind.DpSourceCoverage,
            CompiledFirmwareArtifactSignalKind.DpContentPlausibility);
        SectionSignals tp = InspectSection(
            compiledComposition,
            candidate,
            CompiledInputArtifactClass.TpFirmware,
            CompiledFirmwareArtifactSignalKind.TpSourceCoverage,
            CompiledFirmwareArtifactSignalKind.TpContentPlausibility);

        bool completeFlashCode =
            capacitySignal.Status == CompiledFirmwareArtifactSignalStatus.Satisfied &&
            dp.Coverage.Status == CompiledFirmwareArtifactSignalStatus.Satisfied &&
            dp.Plausibility.Status == CompiledFirmwareArtifactSignalStatus.Satisfied &&
            tp.Coverage.Status == CompiledFirmwareArtifactSignalStatus.Satisfied &&
            tp.Plausibility.Status == CompiledFirmwareArtifactSignalStatus.Satisfied;
        bool declaredTpOnly =
            tp.Coverage.Status == CompiledFirmwareArtifactSignalStatus.Satisfied &&
            tp.Plausibility.Status == CompiledFirmwareArtifactSignalStatus.Satisfied &&
            (dp.Coverage.Status == CompiledFirmwareArtifactSignalStatus.NotSatisfied ||
             dp.Plausibility.Status == CompiledFirmwareArtifactSignalStatus.NotSatisfied);
        CompiledFirmwareArtifactKind kind = completeFlashCode
            ? CompiledFirmwareArtifactKind.FlashCode
            : declaredTpOnly
                ? CompiledFirmwareArtifactKind.TpFirmware
                : CompiledFirmwareArtifactKind.Unknown;

        return new CompiledFirmwareArtifactClassification(
            kind,
            [capacitySignal, dp.Coverage, tp.Coverage, dp.Plausibility, tp.Plausibility]);
    }

    private static SectionSignals InspectSection(
        CompiledComposition composition,
        ReadOnlySpan<byte> candidate,
        CompiledInputArtifactClass artifactClass,
        CompiledFirmwareArtifactSignalKind coverageKind,
        CompiledFirmwareArtifactSignalKind plausibilityKind)
    {
        V2CompiledCompositionDetails details = composition.V2Details;
        CompiledInputSlotRequirement[] slots =
        [
            .. details.InputContract.Slots.Where(slot => slot.ArtifactClass == artifactClass),
        ];
        if (slots.Length != 1)
        {
            return SectionSignals.NotDeclared(coverageKind, plausibilityKind);
        }

        string[] addressSpaceIds =
        [
            .. details.InputContract.SpaceBindings
                .Where(binding => StringComparer.Ordinal.Equals(binding.SlotId, slots[0].SlotId))
                .Select(static binding => binding.AddressSpaceId),
        ];
        if (addressSpaceIds.Length != 1)
        {
            return SectionSignals.NotDeclared(coverageKind, plausibilityKind);
        }

        string addressSpaceId = addressSpaceIds[0];
        AddressSpace? addressSpace = composition.Plan.AddressSpaces.SingleOrDefault(space =>
            StringComparer.Ordinal.Equals(space.AddressSpaceId, addressSpaceId));
        if (addressSpace is null)
        {
            return SectionSignals.NotDeclared(coverageKind, plausibilityKind);
        }

        long requiredEndExclusive = addressSpace.Length;
        CompiledFirmwareArtifactSignal coverage = new(
            coverageKind,
            candidate.Length >= requiredEndExclusive
                ? CompiledFirmwareArtifactSignalStatus.Satisfied
                : CompiledFirmwareArtifactSignalStatus.NotSatisfied,
            addressSpaceId,
            requiredEndExclusive,
            FailedRange: candidate.Length >= requiredEndExclusive
                ? null
                : ByteRange.FromStartEndExclusive(candidate.Length, requiredEndExclusive));
        CompiledUniformInputRangeValidation[] validations =
        [
            .. composition.V2Details.Provenance.ValidationRequirements
                .OfType<CompiledUniformInputRangeValidation>()
                .Where(validation =>
                    StringComparer.Ordinal.Equals(validation.AddressSpaceId, addressSpaceId)),
        ];
        if (validations.Length == 0)
        {
            return new SectionSignals(
                coverage,
                new CompiledFirmwareArtifactSignal(
                    plausibilityKind,
                    CompiledFirmwareArtifactSignalStatus.NotDeclared,
                    addressSpaceId,
                    requiredEndExclusive,
                    FailedRange: null));
        }

        ByteRange? failedRange = null;
        foreach (ByteRange range in validations.SelectMany(static validation => validation.Ranges))
        {
            if (range.EndExclusive > candidate.Length ||
                IsUniform(candidate.Slice(checked((int)range.Start), checked((int)range.Length))))
            {
                failedRange = range;
                break;
            }
        }

        return new SectionSignals(
            coverage,
            new CompiledFirmwareArtifactSignal(
                plausibilityKind,
                failedRange is null
                    ? CompiledFirmwareArtifactSignalStatus.Satisfied
                    : CompiledFirmwareArtifactSignalStatus.NotSatisfied,
                addressSpaceId,
                requiredEndExclusive,
                failedRange));
    }

    private static bool IsUniform(ReadOnlySpan<byte> bytes)
    {
        return bytes.IsEmpty || bytes[1..].IndexOfAnyExcept(bytes[0]) < 0;
    }

    private static CompiledFirmwareArtifactClassification Unknown(long capacity)
    {
        return new CompiledFirmwareArtifactClassification(
            CompiledFirmwareArtifactKind.Unknown,
            [
                new(
                    CompiledFirmwareArtifactSignalKind.DeclaredContainerCapacity,
                    CompiledFirmwareArtifactSignalStatus.NotDeclared,
                    AddressSpaceId: null,
                    RequiredEndExclusive: capacity,
                    FailedRange: null),
                .. SectionSignals.NotDeclared(
                    CompiledFirmwareArtifactSignalKind.DpSourceCoverage,
                    CompiledFirmwareArtifactSignalKind.DpContentPlausibility).AsArray(),
                .. SectionSignals.NotDeclared(
                    CompiledFirmwareArtifactSignalKind.TpSourceCoverage,
                    CompiledFirmwareArtifactSignalKind.TpContentPlausibility).AsArray(),
            ]);
    }

    private sealed record SectionSignals(
        CompiledFirmwareArtifactSignal Coverage,
        CompiledFirmwareArtifactSignal Plausibility)
    {
        internal static SectionSignals NotDeclared(
            CompiledFirmwareArtifactSignalKind coverageKind,
            CompiledFirmwareArtifactSignalKind plausibilityKind)
        {
            return new SectionSignals(
                new CompiledFirmwareArtifactSignal(
                    coverageKind,
                    CompiledFirmwareArtifactSignalStatus.NotDeclared,
                    AddressSpaceId: null,
                    RequiredEndExclusive: 0,
                    FailedRange: null),
                new CompiledFirmwareArtifactSignal(
                    plausibilityKind,
                    CompiledFirmwareArtifactSignalStatus.NotDeclared,
                    AddressSpaceId: null,
                    RequiredEndExclusive: 0,
                    FailedRange: null));
        }

        internal CompiledFirmwareArtifactSignal[] AsArray()
        {
            return [Coverage, Plausibility];
        }
    }
}

/// <summary>
/// Resolves the current published Standard Merge geometries and returns one
/// terminal artifact classification. Missing, stale, or ambiguous authority
/// fails closed.
/// </summary>
public interface IFirmwareArtifactClassificationResolver
{
    /// <summary>Classifies one immutable accepted candidate.</summary>
    CompiledFirmwareArtifactClassification? Resolve(
        string icId,
        ResolvedCapability? exactCapability,
        ReadOnlySpan<byte> candidate);
}

/// <summary>Application-owned route selection and artifact classification policy.</summary>
internal sealed class FirmwareArtifactClassificationResolver(
    ICanonicalCapabilityQuery catalog,
    CanonicalCapabilityCompilerAdapter compiler) : IFirmwareArtifactClassificationResolver
{
    private readonly ICanonicalCapabilityQuery _catalog =
        catalog ?? throw new ArgumentNullException(nameof(catalog));
    private readonly CanonicalCapabilityCompilerAdapter _compiler =
        compiler ?? throw new ArgumentNullException(nameof(compiler));

    /// <inheritdoc />
    public CompiledFirmwareArtifactClassification? Resolve(
        string icId,
        ResolvedCapability? exactCapability,
        ReadOnlySpan<byte> candidate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        string normalizedIcId = IcIdentifier.Normalize(icId);
        CanonicalCapabilityCatalogSnapshot? snapshot = _catalog.TryGetCurrentSnapshot();
        if (snapshot is null ||
            (exactCapability is not null &&
             !IsCurrentCapability(snapshot, normalizedIcId, exactCapability)))
        {
            return null;
        }

        if (exactCapability is not null && StringComparer.Ordinal.Equals(
            exactCapability.Identity.WorkflowId,
            ExperienceIds.StandardMerge))
        {
            CompiledFirmwareArtifactClassification classification =
                CompiledFirmwareArtifactClassifier.Classify(
                    exactCapability.CompiledComposition,
                    candidate);
            return IsCurrentSnapshot(snapshot) ? classification : null;
        }

        CompiledComposition[]? compositions = ResolveCurrentCompositions(
            snapshot,
            normalizedIcId);
        if (compositions is null || compositions.Length == 0)
        {
            return null;
        }

        long? authoritativeCapacity = exactCapability?
            .CompiledComposition.Plan.OutputInitialization.Capacity;
        if (authoritativeCapacity is not null &&
            TryClassifyExactCapacity(
                compositions,
                authoritativeCapacity.Value,
                candidate,
                out CompiledFirmwareArtifactClassification? authoritative))
        {
            return IsCurrentSnapshot(snapshot) ? authoritative : null;
        }

        if (authoritativeCapacity is null && TryClassifyExactCapacity(
                compositions,
                candidate.Length,
                candidate,
                out CompiledFirmwareArtifactClassification? exact))
        {
            return IsCurrentSnapshot(snapshot) ? exact : null;
        }

        var classifications = new CompiledFirmwareArtifactClassification[compositions.Length];
        for (int index = 0; index < compositions.Length; index++)
        {
            classifications[index] = CompiledFirmwareArtifactClassifier.Classify(
                compositions[index],
                candidate);
        }
        CompiledFirmwareArtifactKind kind = classifications[0].Kind;
        CompiledFirmwareArtifactClassification? consensus =
            kind != CompiledFirmwareArtifactKind.TpFirmware ||
            classifications.Any(classification => classification.Kind != kind)
                ? null
                : CreateConsensus(classifications, kind);
        return IsCurrentSnapshot(snapshot) ? consensus : null;
    }

    private CompiledComposition[]? ResolveCurrentCompositions(
        CanonicalCapabilityCatalogSnapshot snapshot,
        string icId)
    {
        var compositions = new List<CompiledComposition>();
        foreach (ResolvedCapability capability in snapshot.Capabilities.Where(capability =>
                     StringComparer.Ordinal.Equals(capability.Identity.IcId, icId) &&
                     StringComparer.Ordinal.Equals(
                         capability.Identity.WorkflowId,
                         ExperienceIds.StandardMerge)))
        {
            AddUnique(compositions, capability.CompiledComposition);
        }

        ResolvedCapabilityRoute[] dynamicRoutes =
        [
            .. snapshot.DynamicRoutes.Where(route =>
                StringComparer.Ordinal.Equals(route.Identity.IcId, icId) &&
                StringComparer.Ordinal.Equals(
                    route.Identity.WorkflowId,
                    ExperienceIds.StandardMerge)),
        ];
        if (dynamicRoutes.Length > 1)
        {
            return null;
        }

        if (dynamicRoutes.Length == 0)
        {
            return [.. compositions];
        }

        ResolvedCapabilityRoute route = dynamicRoutes[0];
        IReadOnlyList<string> memberSlotIds =
            route.CompilationContract.SemanticBindingIds;
        string[][] selections = memberSlotIds.Count == 0
            ? [[]]
            : [[], [.. memberSlotIds]];
        var dynamicCompositions = new List<CompiledComposition>(selections.Length);
        foreach (string[] selection in selections)
        {
            if (!_compiler.TryCompilePublishedClassificationCandidate(
                    route,
                    selection,
                    out ResolvedCapability? capability) ||
                capability is null ||
                !IsCurrentCapability(snapshot, icId, capability))
            {
                return null;
            }

            AddUnique(dynamicCompositions, capability.CompiledComposition);
        }

        string[] compiledMapIds =
        [
            .. dynamicCompositions
                .Select(static composition =>
                    composition.V2Details.Provenance.ResolvedMap.ImageMap.MapId)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
        if (!compiledMapIds.SequenceEqual(
                route.CompilationContract.AllowedMapVariantIds,
                StringComparer.Ordinal))
        {
            return null;
        }

        foreach (CompiledComposition composition in dynamicCompositions)
        {
            AddUnique(compositions, composition);
        }

        return [.. compositions];
    }

    private bool IsCurrentSnapshot(CanonicalCapabilityCatalogSnapshot snapshot)
    {
        return _catalog.TryGetCurrentSnapshot()?.ResolutionToken == snapshot.ResolutionToken;
    }

    private static bool IsCurrentCapability(
        CanonicalCapabilityCatalogSnapshot snapshot,
        string icId,
        ResolvedCapability capability)
    {
        return capability.ResolutionToken == snapshot.ResolutionToken &&
            StringComparer.Ordinal.Equals(capability.Identity.IcId, icId) &&
            (snapshot.Capabilities.Any(current => ReferenceEquals(current, capability)) ||
             snapshot.DynamicRoutes.Any(route =>
                 StringComparer.Ordinal.Equals(
                     route.Identity.RouteId,
                     capability.Identity.RouteId) &&
                 StringComparer.Ordinal.Equals(
                     route.CapabilityFingerprint,
                     capability.CapabilityFingerprint)));
    }

    private static bool TryClassifyExactCapacity(
        CompiledComposition[] compositions,
        long capacity,
        ReadOnlySpan<byte> candidate,
        out CompiledFirmwareArtifactClassification? classification)
    {
        CompiledComposition[] exact =
        [
            .. compositions.Where(composition =>
                composition.Plan.OutputInitialization.Capacity == capacity),
        ];
        classification = exact.Length == 1
            ? CompiledFirmwareArtifactClassifier.Classify(exact[0], candidate)
            : null;
        return exact.Length != 0;
    }

    private static CompiledFirmwareArtifactClassification CreateConsensus(
        CompiledFirmwareArtifactClassification[] classifications,
        CompiledFirmwareArtifactKind kind)
    {
        var signals = new List<CompiledFirmwareArtifactSignal>();
        foreach (CompiledFirmwareArtifactSignalKind signalKind in
                 Enum.GetValues<CompiledFirmwareArtifactSignalKind>())
        {
            CompiledFirmwareArtifactSignal first = classifications[0].Signals
                .Single(signal => signal.Kind == signalKind);
            signals.Add(classifications.All(classification => classification.Signals
                    .Single(signal => signal.Kind == signalKind) == first)
                ? first
                : new CompiledFirmwareArtifactSignal(
                    signalKind,
                    CompiledFirmwareArtifactSignalStatus.NotDeclared,
                    AddressSpaceId: null,
                    RequiredEndExclusive: 0,
                    FailedRange: null));
        }

        return new CompiledFirmwareArtifactClassification(kind, signals);
    }

    private static void AddUnique(
        List<CompiledComposition> compositions,
        CompiledComposition candidate)
    {
        if (compositions.All(existing => !StringComparer.Ordinal.Equals(
                existing.CompilationFingerprint,
                candidate.CompilationFingerprint)))
        {
            compositions.Add(candidate);
        }
    }
}
