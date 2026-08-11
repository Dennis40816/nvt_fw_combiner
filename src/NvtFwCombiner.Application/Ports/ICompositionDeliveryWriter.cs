namespace NvtFwCombiner.Application.Ports;

/// <summary>Preflights and atomically commits one caller-selected compiled additional delivery.</summary>
public interface ICompositionDeliveryWriter
{
    /// <summary>Stable compiled delivery role selected for this Build.</summary>
    string DeliveryKind { get; }

    /// <summary>Admits the primary and additional output targets before byte execution.</summary>
    string EnsureCanCommit(
        string primaryOutputFileName,
        string suggestedDeliveryFileName);

    /// <summary>Commits the exact Application-extracted bytes and returns the adapter-owned destination id.</summary>
    ValueTask<string> CommitAsync(
        string deliveryFileName,
        ReadOnlyMemory<byte> outputBytes,
        CancellationToken cancellationToken);
}
