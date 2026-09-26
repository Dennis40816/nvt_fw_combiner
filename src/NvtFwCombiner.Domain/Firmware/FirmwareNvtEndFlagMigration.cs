using System.Collections.Frozen;

namespace NvtFwCombiner.Domain.Firmware;

/// <summary>
/// NVT-END-FLAG-1113-01 migration inventory (ADR 0076, owner decision 35): the only firmware families whose layouts may
/// resolve without an NVT end-flag declaration. For them readers keep the existing compatibility behavior: the
/// profile's existing template locators and the existing whole-image compatibility read, unchanged and not newly
/// authorized. Every other family must declare its end flag or its readers fail closed. The inventory may only shrink
/// through an admitted change; when it is empty the compatibility state and the no-declaration reader overloads are
/// deleted in the same batch (enforced by NvtEndFlagDeclarationTests).
/// </summary>
public static class FirmwareNvtEndFlagMigration
{
    /// <summary>
    /// Runtime-registered families pending an end-flag declaration and their follow-up owners: the fixed-layout
    /// NT51917/923/926/927/928 families move with the later small R3 item of owner decision 35; the NT51919/929/932
    /// families (count-derived Backup end, including their AB family without an NVT locator) move with the later
    /// count-derived item. Metadata-provider-only families (for example the shared-facts <c>nt51927</c>) are not
    /// reader layouts; their template locators stay unchanged and move with the same items.
    /// </summary>
    public static IReadOnlySet<string> LegacyCompatibilityFamilyIds { get; } = new[]
    {
        // Fixed layout, later small R3 item (decision 35).
        "nt51917-nt51927-nt51928-canonical-container",
        "nt51923-nt51926",
        "nt51923-ctrlram-replace",
        "nt51926-ctrlram-replace",
        "nt51927-ctrlram-replace",
        "nt51928-ctrlram-replace",

        // Count-derived Backup end, later item (decision 35).
        "nt51929-nt51932",
        "nt51929-ctrlram-replace",
        "nt51932-ctrlram-replace",
        "nt51919-nt51929-nt51932-ab-merge",
    }.ToFrozenSet(StringComparer.Ordinal);
}
