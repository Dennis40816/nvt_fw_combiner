using NvtFwCombiner.Application.Diagnostics;
using NvtFwCombiner.Application.Ports;

namespace NvtFwCombiner.Infrastructure.Diagnostics;

/// <summary>Observation slot written once by the selecting registry; contains no bundle data or selection logic.</summary>
internal sealed class BuiltInProfileAdmissionStatus : IBuiltInProfileAdmissionStatus
{
    internal static BuiltInProfileAdmissionStatus Instance { get; } = new();
    private BuiltInProfileAdmission? _current;

    public BuiltInProfileAdmission? Current => Volatile.Read(ref _current);

    internal void Publish(BuiltInProfileAdmission admission)
    {
        ArgumentNullException.ThrowIfNull(admission);
        if (Interlocked.CompareExchange(ref _current, admission, null) is not null)
        {
            throw new InvalidOperationException("Built-in admission was already published.");
        }
    }
}
