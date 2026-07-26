using System.Security.Cryptography;
using System.Text;

namespace NvtFwCombiner.Application.Support;

/// <summary>Exact route identity over IC, workflow, IC Count, map, and integrity behavior.</summary>
public sealed record SupportRouteIdentity
{
    private const string NotApplicable = "not-applicable";

    /// <summary>Initializes one exact route and derives its stable policy identity.</summary>
    public SupportRouteIdentity(
        string icId,
        string workflowId,
        string icCountVariant,
        string mapVariant,
        string integrityRouteId = NotApplicable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId);
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        ArgumentException.ThrowIfNullOrWhiteSpace(icCountVariant);
        ArgumentException.ThrowIfNullOrWhiteSpace(mapVariant);
        ArgumentException.ThrowIfNullOrWhiteSpace(integrityRouteId);

        IcId = icId;
        WorkflowId = RequireRouteToken(workflowId, nameof(workflowId));
        IcCountVariant = RequireRouteToken(icCountVariant, nameof(icCountVariant));
        MapVariant = RequireRouteToken(mapVariant, nameof(mapVariant));
        IntegrityRouteId = integrityRouteId;
        RouteId = CreateRouteId();
    }

    /// <summary>Canonical IC identifier.</summary>
    public string IcId { get; }

    /// <summary>Canonical workflow identifier.</summary>
    public string WorkflowId { get; }

    /// <summary>Exact or explicitly selector-free IC Count variant.</summary>
    public string IcCountVariant { get; }

    /// <summary>Canonical resolved map identifier.</summary>
    public string MapVariant { get; }

    /// <summary>Canonical integrity route, or <c>not-applicable</c>.</summary>
    public string IntegrityRouteId { get; }

    /// <summary>Stable route reference used by publication and evidence snapshots.</summary>
    public string RouteId { get; }

    internal static bool IsCanonicalId(string value)
    {
        return IsRouteToken(value) &&
            value[0] is >= 'a' and <= 'z';
    }

    private string CreateRouteId()
    {
        string icToken = RequireRouteToken(IcId.ToLowerInvariant(), nameof(IcId));
        string routeId =
            $"route-{FrameAxis(icToken)}-{FrameAxis(WorkflowId)}-" +
            $"{FrameAxis(IcCountVariant)}-{FrameAxis(MapVariant)}";
        if (IntegrityRouteId != NotApplicable)
        {
            routeId = $"{routeId}-integrity-{IntegrityRouteHash(IntegrityRouteId)}";
        }

        return RequireRouteToken(routeId, nameof(RouteId));
    }

    private static string FrameAxis(string value)
    {
        return FormattableString.Invariant($"{value.Length}-{value}");
    }

    private static string RequireRouteToken(string value, string parameterName)
    {
        return IsRouteToken(value)
            ? value
            : throw new ArgumentException(
                "Support route identity components must be lowercase kebab-case tokens.",
                parameterName);
    }

    private static bool IsRouteToken(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
            value.Split('-').All(static segment =>
                segment.Length > 0 &&
                segment.All(static character =>
                    character is (>= 'a' and <= 'z') or (>= '0' and <= '9')));
    }

    private static string IntegrityRouteHash(string value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
