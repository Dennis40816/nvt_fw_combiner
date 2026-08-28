using System.Reflection;
using System.Runtime.ExceptionServices;
using NvtFwCombiner.Presentation.Avalonia;

namespace NvtFwCombiner.UiSmoke.Tests;

public sealed partial class ShellNavigationSystemTests
{
    private static PresentationHostServices WithCapabilities(
        PresentationHostServices services,
        ICompositionCapabilityExperience capabilities)
    {
        PresentationCompositionServices current = services.Composition;
        var composition = new PresentationCompositionServices(
            capabilities,
            current.StandardMergeAuthoring,
            current.AbMergeAuthoring,
            current.DpReplaceAuthoring,
            current.GeneralAuthoring,
            current.CtrlRamAuthoring,
            current.FirmwareInspection,
            current.OutputNaming,
            current.Execution);
        return new PresentationHostServices(
            composition,
            services.FileReveal,
            services.SupportMatrix,
            services.SystemInformation,
            services.SystemDiagnosticsExporter,
            services.RawBinaryEditorFileSessions,
            services.CanonicalCatalogLoader,
            services.ExternalEnvironmentLoader,
            services.LocalFiles,
            services.VersionManagement,
            services.ManagedApplicationStartup,
            services.StableLauncherHandoff);
    }

    /// <summary>Fails a forbidden second publication read during one catalog refresh.</summary>
    public class SelectorPublicationSentinel : DispatchProxy
    {
        private ICompositionCapabilityExperience? _inner;
        private int _armed;
        private int _armedSelectorReadCount;

        /// <summary>Creates an unconfigured proxy base for <see cref="DispatchProxy"/>.</summary>
        public SelectorPublicationSentinel()
        {
        }

        internal int ArmedSelectorReadCount => Volatile.Read(ref _armedSelectorReadCount);

        internal static (
            ICompositionCapabilityExperience Port,
            SelectorPublicationSentinel Sentinel) Wrap(
                ICompositionCapabilityExperience inner)
        {
            ICompositionCapabilityExperience port =
                Create<ICompositionCapabilityExperience, SelectorPublicationSentinel>();
            var sentinel = (SelectorPublicationSentinel)(object)port;
            sentinel._inner = inner;
            return (port, sentinel);
        }

        internal void Arm()
        {
            _ = Interlocked.Exchange(ref _armedSelectorReadCount, 0);
            Volatile.Write(ref _armed, 1);
        }

        /// <summary>Forwards all calls while substituting only a forbidden second selector read.</summary>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (Volatile.Read(ref _armed) == 1 &&
                StringComparer.Ordinal.Equals(
                    targetMethod.Name,
                    nameof(ICompositionCapabilityExperience.GetSelectorPublication)))
            {
                int read = Interlocked.Increment(ref _armedSelectorReadCount);
                if (read == 2)
                {
                    throw new InvalidOperationException(
                        "A catalog refresh must not read a second selector publication.");
                }
            }

            try
            {
                return targetMethod.Invoke(_inner, args);
            }
            catch (TargetInvocationException exception) when (exception.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
                return null;
            }
        }
    }
}
