using NvtFwCombiner.Application.Capabilities;

namespace NvtFwCombiner.Bootstrap.Tests;

internal static class BootstrapTestHost
{
    internal static CompositionHostServices Services { get; } =
        CompositionHostServices.Create();

    internal static CanonicalTestContext Canonical { get; } = new(Services);
}

internal sealed class IsolatedBootstrapTestHost
{
    internal IsolatedBootstrapTestHost()
    {
        Services = CompositionHostServices.Create();
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
