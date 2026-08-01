namespace NvtFwCombiner.Application.Capabilities;

/// <summary>
/// Stable exact-route identity over selection axes only. Executable firmware
/// semantics belong to a separate capability fingerprint.
/// </summary>
public sealed record CapabilityRouteIdentity
{
    /// <summary>Creates one stable exact route identity.</summary>
    public CapabilityRouteIdentity(
        string icId,
        string workflowId,
        string icCountVariant,
        string mapVariant)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        IcId = icId;
        WorkflowId = RequireToken(workflowId, nameof(workflowId));
        IcCountVariant = RequireToken(icCountVariant, nameof(icCountVariant));
        MapVariant = RequireToken(mapVariant, nameof(mapVariant));
        RouteId = CreateRouteId();
    }

    /// <summary>Canonical IC identifier.</summary>
    public string IcId { get; }

    /// <summary>Canonical workflow identifier.</summary>
    public string WorkflowId { get; }

    /// <summary>Exact or explicitly selector-free IC Count variant.</summary>
    public string IcCountVariant { get; }

    /// <summary>Canonical resolved map variant.</summary>
    public string MapVariant { get; }

    /// <summary>Stable framed identity used by pinned decisions.</summary>
    public string RouteId { get; }

    internal static bool IsSha256(string value)
    {
        return value.Length == 64 &&
            value.All(static character =>
                character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    private string CreateRouteId()
    {
        string icToken = RequireToken(IcId.ToLowerInvariant(), nameof(IcId));
        return $"route-{Frame(icToken)}-{Frame(WorkflowId)}-" +
            $"{Frame(IcCountVariant)}-{Frame(MapVariant)}";
    }

    private static string Frame(string value)
    {
        return FormattableString.Invariant($"{value.Length}-{value}");
    }

    private static string RequireToken(string value, string parameterName)
    {
        return IsToken(value)
            ? value
            : throw new ArgumentException(
                "Capability route identity components must be lowercase kebab-case tokens.",
                parameterName);
    }

    private static bool IsToken(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Split('-').All(static segment =>
                segment.Length > 0 &&
                segment.All(static character =>
                    character is (>= 'a' and <= 'z') or (>= '0' and <= '9')));
    }
}
