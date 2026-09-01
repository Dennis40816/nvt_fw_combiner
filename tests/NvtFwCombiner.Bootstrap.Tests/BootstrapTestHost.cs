using NvtFwCombiner.Application.Capabilities;
using NvtFwCombiner.Infrastructure.ExternalTools;
using NvtFwCombiner.TestSupport;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class BootstrapTestHost
{
    internal static CompositionHostServices Services { get; } =
        CreateServices();

    internal static CompositionHostServices ProductServices { get; } =
        CreateProductServices();

    internal static CompositionHostServices RetainedDpReplaceServices { get; } =
        Services;

    internal static CanonicalTestContext Canonical { get; } = new(Services);

    internal static CanonicalTestContext ProductCanonical { get; } = new(ProductServices);

    internal static CompositionHostServices CreateServices()
    {
        var services = CompositionHostServices.Create(
            CreateExternalEnvironmentLoader(),
            NvtFwCombiner.TestSupport.RetainedDpReplaceRegressionPolicy.Load);
        return services.ExternalEnvironmentLoader
            .LoadToCompletionAsync(null, CancellationToken.None)
            .GetAwaiter().GetResult().Succeeded
            ? services
            : throw new InvalidOperationException("The test external environment did not load.");
    }

    internal static CompositionHostServices CreateProductServices()
    {
        var services = CompositionHostServices.Create(CreateExternalEnvironmentLoader());
        if (!services.ExternalEnvironmentLoader
            .LoadToCompletionAsync(null, CancellationToken.None)
            .GetAwaiter().GetResult().Succeeded)
        {
            throw new InvalidOperationException(
                "The product-policy test external environment did not load.");
        }

        CapabilityCatalogReloadResult? result = null;
        IAsyncEnumerator<CanonicalCapabilityCatalogLoadUpdate> updates =
            services.CanonicalCatalogLoader.LoadAsync(CancellationToken.None)
                .GetAsyncEnumerator(CancellationToken.None);
        try
        {
            while (updates.MoveNextAsync().AsTask().GetAwaiter().GetResult())
            {
                result = updates.Current.Result ?? result;
            }
        }
        finally
        {
            updates.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }

        return result?.Succeeded == true
            ? services
            : throw new InvalidOperationException(
                "The product-policy canonical catalog did not load.");
    }

    private static ExternalProcessorEnvironmentLoader CreateExternalEnvironmentLoader()
    {
        return new ExternalProcessorEnvironmentLoader(
            RepositoryPaths.FromRepositoryRoot("external-tools"));
    }
}

internal sealed class IsolatedBootstrapTestHost
{
    internal IsolatedBootstrapTestHost()
    {
        Services = BootstrapTestHost.CreateServices();
        Canonical = new CanonicalTestContext(Services);
    }

    internal CompositionHostServices Services { get; }

    internal CanonicalTestContext Canonical { get; }

    internal CanonicalCapabilityCatalog Catalog => Services.Catalog;
}

internal sealed class CanonicalTestContext
{
    internal CanonicalTestContext(CompositionHostServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        Catalog = services.Catalog;
        Compiler = services.Compiler;
        Projection = (CanonicalCapabilityExperience)services.CompositionCapabilityExperience;
        GeneralAuthoring = services.GeneralAuthoring;
        CtrlRamAuthoring = services.CtrlRamAuthoring;
        FirmwareInspection = (BuiltInFirmwareInspection)services.FirmwareInspectionExperience;
    }

    internal ICanonicalCapabilityQuery Catalog { get; }

    internal CanonicalCapabilityCompilerAdapter Compiler { get; }

    internal CanonicalCapabilityExperience Projection { get; }

    internal IGeneralAuthoring GeneralAuthoring { get; }

    internal ICtrlRamAuthoring CtrlRamAuthoring { get; }

    internal BuiltInFirmwareInspection FirmwareInspection { get; }

    public static implicit operator BuiltInFirmwareInspection(
        CanonicalTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.FirmwareInspection;
    }

    public static implicit operator CanonicalCapabilityExperience(
        CanonicalTestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.Projection;
    }
}
