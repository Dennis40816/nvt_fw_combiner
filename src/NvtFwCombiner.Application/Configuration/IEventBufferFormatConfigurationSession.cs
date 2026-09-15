namespace NvtFwCombiner.Application.Configuration;

/// <summary>One host-scoped configuration owner; successful persistence is not firmware execution admission.</summary>
public interface IEventBufferFormatConfigurationSession
{
    /// <summary>Current immutable publication; missing/invalid state has no effective configuration.</summary>
    EventBufferFormatConfigurationState Current { get; }

    /// <summary>Trusted selectable identities and declared effects, not user-editable firmware geometry.</summary>
    EventBufferFormatConfigurationCatalog Catalog { get; }

    /// <summary>Creates independent default editor values without activating or persisting them.</summary>
    IReadOnlyList<EventBufferFormatDraftEntry?> CreateDefaultsDraft();

    /// <summary>Creates independent last-saved recovery values; never an effective fallback.</summary>
    IReadOnlyList<EventBufferFormatDraftEntry?>? CreateSavedDraft();

    /// <summary>Validates and persists before publication; rejected saves preserve the previous state.</summary>
    ValueTask<EventBufferFormatConfigurationOperationResult> SaveAsync(
        IReadOnlyList<EventBufferFormatDraftEntry?>? draft, CancellationToken cancellationToken);

    /// <summary>Admits current persisted bytes; invalid external configuration clears effective state.</summary>
    ValueTask<EventBufferFormatConfigurationOperationResult> ReloadAsync(CancellationToken cancellationToken);
}
