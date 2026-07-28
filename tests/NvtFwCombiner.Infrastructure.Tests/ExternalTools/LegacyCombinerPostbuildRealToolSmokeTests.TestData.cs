using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Infrastructure.Tests.ExternalTools;

public sealed partial class LegacyCombinerPostbuildRealToolSmokeTests
{
    /// <summary>Accepted 16-byte direct-combiner CtrlRAM self-replacement matrix.</summary>
    public static TheoryData<string, string, IcNumberInputMode, string, long[]> SixteenByteSelfReplacementCases()
    {
        return new TheoryData<string, string, IcNumberInputMode, string, long[]>
        {
            { "NT51923", "51923", IcNumberInputMode.SingleSelector, "single", Nt51923RangeValues() },
            { "NT51923", "51923", IcNumberInputMode.CascadeSelector, "cascade", Nt51923RangeValues() },
            { "NT51929", "51929", IcNumberInputMode.SingleSelector, "single", Nt51929RangeValues() },
            { "NT51929", "51929", IcNumberInputMode.CascadeSelector, "cascade_2to8", Nt51929RangeValues() },
            { "NT51932", "51932", IcNumberInputMode.SingleSelector, "single", Nt51929RangeValues() },
            { "NT51932", "51932", IcNumberInputMode.CascadeSelector, "cascade_2to8", Nt51929RangeValues() },
            { "NT51950", "51950", IcNumberInputMode.SingleSelector, "single", Nt51950RangeValues() },
            { "NT51950", "51950", IcNumberInputMode.CascadeSelector, "cascade", Nt51950RangeValues() },
            { "NT51951", "51951", IcNumberInputMode.SingleSelector, "single", Nt51950RangeValues() },
            { "NT51951", "51951", IcNumberInputMode.CascadeSelector, "cascade", Nt51950RangeValues() },
        };
    }

    /// <summary>Representative command-family cases used to compare pre-paste and pure Combiner pasteback.</summary>
    public static TheoryData<string, string, IcNumberInputMode, string> PureCombinerPastebackEquivalenceCases()
    {
        return new TheoryData<string, string, IcNumberInputMode, string>
        {
            { "NT51923", "51923", IcNumberInputMode.CascadeSelector, "cascade" },
            { "NT51926", "51926", IcNumberInputMode.SingleSelector, "single" },
            { "NT51927", "51927", IcNumberInputMode.SingleSelector, "single" },
            { "NT51950", "51950", IcNumberInputMode.SingleSelector, "single" },
        };
    }

    private static long[] Nt51923RangeValues()
    {
        return
        [
            0x1C, 4,
            0xFC, 4,
            0x3032C, 4,
            0x3040C, 4,
        ];
    }

    private static long[] Nt51929RangeValues()
    {
        return
        [
            0x7100, 4,
            0x7118, 4,
            0x27FF0, 4,
            0x28008, 4,
        ];
    }

    private static long[] Nt51950RangeValues()
    {
        return
        [
            0xA11C, 4,
            0xA130, 4,
            0x2D428, 4,
            0x2D43C, 4,
        ];
    }
}
