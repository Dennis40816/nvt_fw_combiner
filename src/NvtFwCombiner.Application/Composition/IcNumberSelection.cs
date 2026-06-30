using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Composition;

/// <summary>IC number context selected by the user before a Replace run is planned.</summary>
public sealed class IcNumberSelection
{
    /// <summary>Creates a validated IC number selection.</summary>
    public IcNumberSelection(IcNumberInputMode mode, IReadOnlyList<string> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        if (parts.Count == 0)
        {
            throw new ArgumentException("IC number selection must contain at least one value.", nameof(parts));
        }

        if (parts.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("IC number selection values cannot be blank.", nameof(parts));
        }

        Mode = mode;
        Parts = [.. parts.Select(part => part.Trim())];
    }

    /// <summary>Gets the profile-declared input mode used to collect this selection.</summary>
    public IcNumberInputMode Mode { get; }

    /// <summary>Gets the selected IC number values in UI order.</summary>
    public IReadOnlyList<string> Parts { get; }

    /// <summary>Formats the selection as a stable token for reports and preview approval.</summary>
    public string ToStableToken()
    {
        return $"{Mode}:{string.Join("/", Parts)}";
    }
}
