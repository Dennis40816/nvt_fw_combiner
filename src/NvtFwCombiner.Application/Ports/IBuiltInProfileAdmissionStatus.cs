using NvtFwCombiner.Application.Diagnostics;

namespace NvtFwCombiner.Application.Ports;

/// <summary>Passive process-lifetime observation; reading never performs admission or I/O.</summary>
public interface IBuiltInProfileAdmissionStatus
{
    /// <summary>Null until the owning registry publishes its completed source decision.</summary>
    BuiltInProfileAdmission? Current { get; }
}
