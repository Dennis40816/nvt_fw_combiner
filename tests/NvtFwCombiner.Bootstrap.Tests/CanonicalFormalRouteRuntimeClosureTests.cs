using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Executes the complete reviewed Standard, AB, and CtrlRAM route denominator.</summary>
public sealed class CanonicalFormalRouteRuntimeClosureTests
{
    /// <summary>The policy denominator and honest witness classes remain exact.</summary>
    [Fact]
    public void FormalRouteFixtureCatalogCoversTheExactReviewedDenominator()
    {
        IReadOnlyList<CanonicalFormalRouteRuntimeFixture> fixtures =
            CanonicalFormalRouteRuntimeFixtureCatalog.Create();

        Assert.Equal(74, fixtures.Count);
        Assert.Equal(74, fixtures.Select(static fixture => fixture.RouteId)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(14, fixtures.Count(static fixture =>
            fixture.Policy.Identity.WorkflowId == ExperienceIds.StandardMerge));
        Assert.Equal(6, fixtures.Count(static fixture =>
            fixture.Policy.Identity.WorkflowId == ExperienceIds.AbMerge));
        Assert.Equal(54, fixtures.Count(static fixture =>
            fixture.Policy.Identity.WorkflowId == ExperienceIds.CtrlRamReplace));
        // The v1.1.4 optional-DP definitions retain Normal Golden cases, but their
        // expanded route evidence is ContractOnly (policy catalog 1.11.0).
        Assert.Equal(26, fixtures.Count(static fixture =>
            fixture.PolicyEvidenceClass == CanonicalFormalRuntimePolicyEvidenceClass.DirectGolden));
        Assert.Equal(7, fixtures.Count(static fixture =>
            fixture.PolicyEvidenceClass == CanonicalFormalRuntimePolicyEvidenceClass.ApprovedAlias));
        Assert.Equal(4, fixtures.Count(static fixture =>
            fixture.PolicyEvidenceClass == CanonicalFormalRuntimePolicyEvidenceClass.SyntheticOracle));
        Assert.Equal(37, fixtures.Count(static fixture =>
            fixture.PolicyEvidenceClass == CanonicalFormalRuntimePolicyEvidenceClass.ContractOnly));
    }

    /// <summary>Every catalog route runs in exactly one shard and all 94 cases remain assigned.</summary>
    [Fact]
    public void FormalRouteRuntimeShardsCoverEveryCatalogRouteExactlyOnce()
    {
        IReadOnlyList<CanonicalFormalRouteRuntimeFixture> fixtures =
            CanonicalFormalRouteRuntimeFixtureCatalog.Create();
        var shards = CanonicalFormalRouteRuntimeShardCatalog.Shards;
        string[] assignedRouteIds =
        [
            .. shards.SelectMany(static shard => shard.RouteIds),
        ];

        Assert.Equal(shards.Length, shards.Select(static shard => shard.Name)
            .Distinct(StringComparer.Ordinal).Count());
        Assert.All(shards, static shard =>
            Assert.Equal(shard.ExpectedRouteCount, shard.RouteIds.Length));
        Assert.Equal(74, assignedRouteIds.Length);
        Assert.Equal(assignedRouteIds.Length,
            assignedRouteIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            fixtures.Select(static fixture => fixture.RouteId).Order(StringComparer.Ordinal),
            assignedRouteIds.Order(StringComparer.Ordinal));
        // Each shard also asserts its actual materialized and executed case count.
        Assert.Equal(94, shards.Sum(static shard => shard.ExpectedCaseCount));
    }
}

internal enum CanonicalFormalRuntimePolicyEvidenceClass
{
    DirectGolden,
    ApprovedAlias,
    SyntheticOracle,
    ContractOnly,
}

internal sealed record CanonicalFormalRouteRuntimeFixture(
    CanonicalCapabilityPolicyRoute Policy,
    CanonicalFormalRuntimePolicyEvidenceClass PolicyEvidenceClass)
{
    internal string RouteId => Policy.Identity.RouteId;
}

internal sealed record CanonicalFormalRouteRuntimeCase(
    string CaseId,
    CanonicalFormalRouteRuntimeFixture Fixture,
    string ExpectedMapId,
    string? SelectionToken,
    IReadOnlyDictionary<string, string> SlotPaths,
    IReadOnlyList<CanonicalFormalRuntimeWitnessProvenance> WitnessProvenance,
    int? ExpectedFirmwareConfigChipCount = null,
    int? ExpectedResolvedIcCount = null,
    IReadOnlyDictionary<string, int>? ExpectedFirmwareConfigChipCounts = null);

internal enum CanonicalFormalRuntimeWitnessKind
{
    DirectCanonicalInput,
    ApprovedAlias,
    CanonicalDerived,
    Synthetic,
}

internal enum CanonicalFormalRuntimeParityClaim
{
    RuntimeContractOnly,
    DirectGoldenParity,
}

internal sealed record CanonicalFormalRuntimeWitnessProvenance(
    string SlotId,
    CanonicalFormalRuntimeWitnessKind Kind,
    string? SourceWorkflowId,
    string? SourceIcId,
    string? SourceCaseId,
    CanonicalFormalRuntimeParityClaim ParityClaim);

/// <summary>Deterministic constrained processor used only to close the host execution contract.</summary>
internal sealed class CanonicalFormalRuntimePassThroughProcessor : IExternalProcessor
{
    private readonly List<ExternalProcessorRequest> _requests = [];

    internal IReadOnlyList<ExternalProcessorRequest> Requests => _requests;

    public ValueTask<ExternalProcessorResult> TransformAsync(
        ExternalProcessorRequest request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request);
        if (request.AllowedWriteRanges.Count == 0 ||
            request.AllowedWriteRanges.Any(range =>
                range.Start < 0 || range.EndExclusive > request.InputBytes.Length))
        {
            throw new InvalidOperationException(
                $"Processor authority for '{request.ProcessorId}' is empty or outside its staging image.");
        }
        _requests.Add(request);
        return ValueTask.FromResult(ExternalProcessorResult.Success(request.InputBytes, [], []));
    }
}
