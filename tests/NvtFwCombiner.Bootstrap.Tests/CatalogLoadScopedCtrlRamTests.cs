using System.Collections.Concurrent;
using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Infrastructure.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Complete CtrlRAM expansion belongs to one load, never the shared source or process.</summary>
public sealed class CatalogLoadScopedCtrlRamTests
{
    /// <summary>All published identities retain policy fingerprints while each load expands once.</summary>
    [Fact]
    public void EveryLoadExpandsOnceAndPreservesAllPolicyFingerprints()
    {
        CanonicalCapabilityPolicySnapshot policy = BuiltInCanonicalCapabilityPolicy.Load();
        var expansions = new List<int[]>();
        var catalog = new CanonicalCapabilityCatalog(CreateSource(() =>
        {
            int[] count = [0];
            expansions.Add(count);
            return CanonicalDynamicRouteInventory.CreateResolver(() =>
            {
                count[0]++;
                return LoadDefinitions();
            });
        }));
        CanonicalCapabilityCatalogSnapshot? previous = null;
        for (int load = 0; load < 2; load++)
        {
            CapabilityCatalogReloadResult result = catalog.Reload(TestContext.Current.CancellationToken);
            Assert.True(result.Succeeded);
            CanonicalCapabilityCatalogSnapshot snapshot = Assert.IsType<CanonicalCapabilityCatalogSnapshot>(result.Snapshot);
            Assert.NotSame(previous, snapshot);
            Assert.Equal(policy.Routes.OrderBy(route => route.Identity.RouteId)
                .Select(route => (route.Identity.RouteId, route.CapabilityFingerprint)),
                snapshot.Capabilities.Select(route => (route.Identity.RouteId, route.CapabilityFingerprint))
                    .Concat(snapshot.DynamicRoutes.Select(route => (route.Identity.RouteId, route.CapabilityFingerprint)))
                    .OrderBy(route => route.RouteId));
            previous = snapshot;
            Assert.Equal(load + 1, expansions.Count);
            Assert.All(expansions, count => Assert.Equal(1, count[0]));
        }
    }

    /// <summary>Even concurrent callers sharing a source obtain independent expansion scopes.</summary>
    [Fact]
    public async Task ConcurrentLoadsDoNotShareExpansionState()
    {
        var expansions = new ConcurrentBag<int[]>();
        CanonicalCapabilityCatalogSource source = CreateSource(() =>
        {
            int[] count = [0];
            expansions.Add(count);
            return CanonicalDynamicRouteInventory.CreateResolver(() =>
            {
                _ = Interlocked.Increment(ref count[0]);
                return LoadDefinitions();
            });
        });
        CapabilityCatalogLoadResult[] results = await Task.WhenAll(Enumerable.Range(0, 2)
            .Select(_ => Task.Run(() => source.Load(TestContext.Current.CancellationToken), TestContext.Current.CancellationToken)));
        Assert.All(results, result => Assert.True(result.Succeeded));
        Assert.Equal(2, expansions.Count);
        Assert.All(expansions, count => Assert.Equal(1, count[0]));
        Assert.NotSame(results[0].Candidate, results[1].Candidate);
    }

    /// <summary>A dynamic route without CtrlRAM never touches the CtrlRAM definition loader.</summary>
    [Fact]
    public void NonCtrlRamResolutionDoesNotExpandCtrlRam()
    {
        CanonicalCapabilityPolicyRoute policy = BuiltInCanonicalCapabilityPolicy.Load().Routes
            .First(route => route.Identity.WorkflowId == ExperienceIds.GeneralMerge);
        Func<CapabilityRouteIdentity, CanonicalDynamicRoute> resolver = CanonicalDynamicRouteInventory.CreateResolver(
            () => throw new InvalidOperationException("CtrlRAM must remain deferred."));
        Assert.Equal(policy.CapabilityFingerprint, resolver(policy.Identity).CapabilityFingerprint);
    }

    /// <summary>Missing or duplicate exact identities keep the original rejection semantics.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void MissingOrDuplicateIdentityIsRejected(int matches)
    {
        CanonicalCtrlRamDefinition definition = LoadDefinitions().First();
        Func<CapabilityRouteIdentity, CanonicalDynamicRoute> resolver = CanonicalDynamicRouteInventory.CreateResolver(
            () => Enumerable.Repeat(definition, matches));
        InvalidDataException failure = Assert.Throws<InvalidDataException>(() => resolver(definition.Identity));
        Assert.Contains($"matched {matches} reviewed definitions", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Materialization takes a private copy rather than retaining a mutable loader array.</summary>
    [Fact]
    public void ResolverRetainsItsOwnCompleteMaterialization()
    {
        CanonicalCtrlRamDefinition[] definitions = [.. LoadDefinitions()];
        CapabilityRouteIdentity identity = definitions[0].Identity;
        Func<CapabilityRouteIdentity, CanonicalDynamicRoute> resolver = CanonicalDynamicRouteInventory.CreateResolver(() => definitions);
        CanonicalDynamicRoute first = resolver(identity);
        definitions[0] = definitions[0] with { PlanFingerprint = new string('f', 64) };
        Assert.Equal(first.CapabilityFingerprint, resolver(identity).CapabilityFingerprint);
    }

    /// <summary>An error after a matching definition cannot be hidden by short-circuit lookup.</summary>
    [Fact]
    public void NonmatchingTailFailureStillRejectsTheWholeExpansion()
    {
        CanonicalCtrlRamDefinition first = LoadDefinitions().First();
        Func<CapabilityRouteIdentity, CanonicalDynamicRoute> resolver = CanonicalDynamicRouteInventory.CreateResolver(BrokenTail);
        InvalidDataException failure = Assert.Throws<InvalidDataException>(() => resolver(first.Identity));
        Assert.Equal("Malformed later definition.", failure.Message);

        IEnumerable<CanonicalCtrlRamDefinition> BrokenTail()
        {
            yield return first;
            throw new InvalidDataException("Malformed later definition.");
        }
    }

    /// <summary>Failed or cancelled expansion cannot disclose/publish, and the next load starts fresh.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InterruptedExpansionRetainsPublicationAndNextLoadRetries(bool cancel)
    {
        int attempt = 0;
        int disclosures = 0;
        using var cancellation = new CancellationTokenSource();
        var source = new CanonicalCapabilityCatalogSource(
            BuiltInCanonicalCapabilityPolicy.Load,
            CanonicalDynamicRouteInventory.IsDynamic,
            CanonicalCompiledRouteInventory.Resolve,
            () =>
            {
                int current = ++attempt;
                return CanonicalDynamicRouteInventory.CreateResolver(() =>
                {
                    if (current == 2)
                    {
                        if (!cancel) { throw new InvalidDataException("Transient definition failure."); }
                        cancellation.Cancel();
                    }
                    return LoadDefinitions();
                });
            },
            (definitions, dynamicDefinitions) =>
            {
                disclosures++;
                return CanonicalCapabilityDisclosureInventory.Create(definitions, dynamicDefinitions);
            },
            CanonicalFullImageMetadataInventory.Create);
        var catalog = new CanonicalCapabilityCatalog(source);
        CapabilityCatalogReloadResult first = catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(first.Succeeded);
        if (cancel)
        {
            _ = Assert.ThrowsAny<OperationCanceledException>(() => catalog.Reload(cancellation.Token));
        }
        else
        {
            CapabilityCatalogReloadResult failed = catalog.Reload(TestContext.Current.CancellationToken);
            Assert.False(failed.Succeeded);
            Assert.True(failed.RetainedLastKnownGood);
            Assert.Equal(CapabilityCatalogIssueCodes.SourceInvalid, Assert.Single(failed.Issues).Code);
        }
        Assert.Same(first.Snapshot, catalog.GetCurrentSnapshot());
        Assert.Equal(1, disclosures);
        CapabilityCatalogReloadResult retry = catalog.Reload(TestContext.Current.CancellationToken);
        Assert.True(retry.Succeeded);
        Assert.NotSame(first.Snapshot, retry.Snapshot);
        Assert.Equal(3, attempt);
        Assert.Equal(2, disclosures);
    }

    private static CanonicalCapabilityCatalogSource CreateSource(
        Func<Func<CapabilityRouteIdentity, CanonicalDynamicRoute>> factory)
    {
        return new(BuiltInCanonicalCapabilityPolicy.Load,
            CanonicalDynamicRouteInventory.IsDynamic,
            CanonicalCompiledRouteInventory.Resolve,
            factory,
            CanonicalCapabilityDisclosureInventory.Create,
            CanonicalFullImageMetadataInventory.Create);
    }

    private static IEnumerable<CanonicalCtrlRamDefinition> LoadDefinitions()
    {
        return CtrlRamV2RouteRegistry.All.SelectMany(CanonicalDynamicRouteInventory.CreateCtrlRamDefinitions);
    }
}
