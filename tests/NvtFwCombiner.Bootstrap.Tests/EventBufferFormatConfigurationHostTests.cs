using NvtFwCombiner.Application.Configuration;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Domain.Firmware;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Trusted configuration composition, lazy lifetime and real storage without firmware execution.</summary>
public sealed class EventBufferFormatConfigurationHostTests
{
    /// <summary>Host retries do not promise to replace the existing trusted catalog's cached initialization failure.</summary>
    [Fact]
    public void TrustedCatalogInitializationFailureRetainsItsExistingLifetime()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        var bundle = new BuiltInV2Bundle(workspace.PathFor("missing-bundle"), "1.0.0", new string('a', 64), "test-binding");
        IOException first = Assert.ThrowsAny<IOException>(() => bundle.GetFirmwareFamily("test-profile", "1.0.0"));
        IOException second = Assert.ThrowsAny<IOException>(() => bundle.GetFirmwareFamily("test-profile", "1.0.0"));
        Assert.Same(first, second);
    }

    /// <summary>Construction does no family IO; concurrent opens share one session, not one global mutable state.</summary>
    [Fact]
    public async Task HostDefersAndSharesConfigurationWithBuiltInDefaultsAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        int loads = 0;
        FirmwareFamilyResolutionDefinition Load()
        {
            _ = Interlocked.Increment(ref loads);
            return BuiltInV2RegistrationRegistry.FindAbMergeRegistration("NT51950", "nt51950-ab-merge-maps")!.GetFirmwareFamily();
        }

        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(), null,
            Load, workspace.PathFor("config.json"));
        Assert.Equal(0, loads);
        IEventBufferFormatConfigurationSession[] sessions = await Task.WhenAll(Enumerable.Range(0, 3)
            .Select(_ => host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(1, loads);
        Assert.All(sessions, session => Assert.Same(sessions[0], session));
        IEventBufferFormatConfigurationSession current = sessions[0];
        Assert.Equal(EventBufferFormatConfigurationStatus.Ready, current.Current.Status);
        Assert.NotNull(current.Current.Configuration);
        Assert.True(current.Current.UsesBuiltInDefaults);
        Assert.False(File.Exists(workspace.PathFor("config.json")));
        Assert.Equal("desay", Assert.Single(current.Catalog.Identities).UniqueId);
        Assert.Equal(3, current.Catalog.OutputEffects.Count);
        Assert.All(current.Catalog.OutputEffects, effect =>
        {
            Assert.Equal("desay", effect.UniqueId);
            Assert.Equal("flash", effect.AddressSpaceId);
            Assert.Equal(
                effect.MapId.EndsWith("1024k", StringComparison.Ordinal)
                    ? new ByteRange(0x8A000, 0x2D000)
                    : new ByteRange(0x4A000, 0x2D000),
                effect.TpBRange);
        });
        Assert.True((await current.SaveAsync(current.CreateDefaultsDraft(), TestContext.Current.CancellationToken)).Succeeded);
        CompositionHostServices restarted = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(), null,
            Load, workspace.PathFor("config.json"));
        IEventBufferFormatConfigurationSession reloaded = await restarted.GetEventBufferFormatConfigurationAsync(
            TestContext.Current.CancellationToken);
        Assert.NotSame(current, reloaded);
        Assert.Equal(EventBufferFormatConfigurationStatus.Ready, reloaded.Current.Status);
        Assert.Equal(current.Current.SourceSha256, reloaded.Current.SourceSha256);
    }

    /// <summary>A transient trusted-family load error is retryable instead of poisoning a cached Task.</summary>
    [Fact]
    public async Task FailedInitializationCanRetryAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        int loads = 0;
        FirmwareFamilyResolutionDefinition Load()
        {
            return Interlocked.Increment(ref loads) == 1
                ? throw new IOException("test unavailable catalog")
                : BuiltInV2RegistrationRegistry.FindAbMergeRegistration("NT51950", "nt51950-ab-merge-maps")!.GetFirmwareFamily();
        }

        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(), null,
            Load, workspace.PathFor("config.json"));
        _ = await Assert.ThrowsAsync<IOException>(() => host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken));
        IEventBufferFormatConfigurationSession session = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, loads);
        Assert.Equal(EventBufferFormatConfigurationStatus.Ready, session.Current.Status);
    }

    /// <summary>One abandoned waiter cannot cancel another consumer's shared initialization.</summary>
    [Fact]
    public async Task CallerCancellationDoesNotPoisonSharedInitializationAsync()
    {
        using TempWorkspace workspace = TempWorkspace.Create();
        using var release = new ManualResetEventSlim();
        using var callerCancellation = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int loads = 0;
        FirmwareFamilyResolutionDefinition Load()
        {
            _ = Interlocked.Increment(ref loads);
            started.SetResult();
            release.Wait(TestContext.Current.CancellationToken);
            return BuiltInV2RegistrationRegistry.FindAbMergeRegistration("NT51950", "nt51950-ab-merge-maps")!.GetFirmwareFamily();
        }

        CompositionHostServices host = CompositionHostServices.Create(new ExternalProcessorEnvironmentLoader(), null,
            Load, workspace.PathFor("config.json"));
        Task<IEventBufferFormatConfigurationSession> cancelled = host.GetEventBufferFormatConfigurationAsync(callerCancellation.Token);
        try
        {
            await started.Task.WaitAsync(TestContext.Current.CancellationToken);
            await callerCancellation.CancelAsync();
            _ = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => cancelled);
        }
        finally
        {
            release.Set();
        }

        IEventBufferFormatConfigurationSession session = await host.GetEventBufferFormatConfigurationAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, loads);
        Assert.Equal(EventBufferFormatConfigurationStatus.Ready, session.Current.Status);
    }

    /// <summary>Disclosure follows the supplied canonical map references, not a fixed Desay address in the editor.</summary>
    [Fact]
    public void EffectProjectionFollowsCanonicalVariantAndPreservesAllApplicability()
    {
        FirmwareFamilyResolutionDefinition original = BuiltInV2RegistrationRegistry.FindAbMergeRegistration("NT51950", "nt51950-ab-merge-maps")!.GetFirmwareFamily();
        FirmwareAbFormatPolicy policy = original.AbFormatPolicy!;
        var changedPolicy = new FirmwareAbFormatPolicy(policy.ScopeId, policy.CommonFormatId, policy.CommonDisplayName,
            policy.Formats, policy.PrimaryBindings, policy.Variants.Select(variant =>
                variant.MemberId == "NT51951" && variant.FormatId == "desay"
                    ? new FirmwareAbFormatVariant(variant.MemberId, variant.FormatId, "nt51951-ab-merge-1024k")
                    : variant));
        var changed = new FirmwareFamilyResolutionDefinition(original.FamilyId, original.FamilyVersion, original.FamilyContentHash,
            original.ImageMaps, original.MetadataSets, original.CapabilityBindings, original.FamilyRelationships, changedPolicy);
        EventBufferFormatConfigurationCatalog catalog = EventBufferFormatConfigurationCatalog.FromFamily(changed);
        Assert.Equal(3, catalog.OutputEffects.Count);
        Assert.Equal(new ByteRange(0x8A000, 0x2D000), Assert.Single(catalog.OutputEffects, effect => effect.MemberId == "NT51951").TpBRange);
        Assert.All(catalog.OutputEffects, effect =>
        {
            FirmwareImageMap map = changed.ImageMaps.Single(item => item.MapId == effect.MapId);
            Assert.Contains(effect.MemberId, map.Applicability.MemberIds);
            Assert.Equal(map.AddressSpaceId, effect.AddressSpaceId);
            Assert.Equal(map.Regions.Single(region => region.RegionId == "b-tp-code").Range, effect.TpBRange);
        });
    }
}
