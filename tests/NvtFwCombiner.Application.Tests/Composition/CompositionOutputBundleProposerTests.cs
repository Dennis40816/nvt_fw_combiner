using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Composition;

/// <summary>Tests editable bundle defaults derived from accepted typed naming facts.</summary>
public sealed class CompositionOutputBundleProposerTests
{
    private static readonly DateTimeOffset ResolvedAtUtc =
        new(2026, 8, 20, 23, 59, 0, TimeSpan.Zero);

    /// <summary>Standard Merge uses exact typed IC, DP, TP, and injected UTC date facts.</summary>
    [Fact]
    public void StandardMergeUsesTypedAcceptedNamingFacts()
    {
        CompositionOutputPreparation output = Preparation(
            "operator-name.bin",
            "NT51950_FlashCode_DCC00T0400_20260820.bin",
            [
                new("ic", "NT51950", true, null, null, "compiled-profile"),
                new("dp-version", "CC00", true, "dp", "a", "canonical-dpcmi"),
                new("tp-version", "0400", true, "tp", "b", "canonical-firmware-config"),
                new("date", "20260820", true, null, null, "utc-clock"),
            ]);

        string actual = CompositionOutputBundleProposer.CreateFolderName(
            ExperienceIds.StandardMerge,
            output);

        Assert.Equal("NT51950_DCC00T0400_20260820", actual);
    }

    /// <summary>Other routes use the canonical automatic output basename and ignore an operator override.</summary>
    [Fact]
    public void OtherRouteUsesCanonicalAutomaticOutputBasename()
    {
        CompositionOutputPreparation output = Preparation(
            "operator-name.bin",
            "NT51950_TPFW_T0400_20260820.bin",
            [
                new("ic", "NT51950", true, null, null, "compiled-profile"),
                new("tp-version", "0400", true, "tp", "b", "canonical-firmware-config"),
                new("date", "20260820", true, null, null, "utc-clock"),
            ]);

        string actual = CompositionOutputBundleProposer.CreateFolderName(
            ExperienceIds.CtrlRamReplace,
            output);

        Assert.Equal("NT51950_TPFW_T0400_20260820", actual);
    }

    /// <summary>A Standard Merge date cannot drift from the one injected UTC naming instant.</summary>
    [Fact]
    public void StandardMergeRejectsDateFromDifferentClockInstant()
    {
        CompositionOutputPreparation output = Preparation(
            "output.bin",
            "output.bin",
            [
                new("ic", "NT51950", true, null, null, "compiled-profile"),
                new("dp-version", "CC00", true, "dp", "a", "canonical-dpcmi"),
                new("tp-version", "0400", true, "tp", "b", "canonical-firmware-config"),
                new("date", "20260821", true, null, null, "utc-clock"),
            ]);

        _ = Assert.Throws<InvalidOperationException>(() =>
            CompositionOutputBundleProposer.CreateFolderName(ExperienceIds.StandardMerge, output));
    }

    /// <summary>Compiled unknown placeholders remain typed values instead of being inferred from a filename.</summary>
    [Fact]
    public void StandardMergeRetainsTypedUnknownVersionPlaceholders()
    {
        CompositionOutputPreparation output = Preparation(
            "output.bin",
            "NT51950_FlashCode_DxxxxTxxxx_20260820.bin",
            [
                new("ic", "NT51950", true, null, null, "compiled-profile"),
                new("dp-version", "xxxx", false, "dp", "a", "canonical-dpcmi"),
                new("tp-version", "xxxx", false, "tp", "b", "canonical-firmware-config"),
                new("date", "20260820", true, null, null, "utc-clock"),
            ]);

        string actual = CompositionOutputBundleProposer.CreateFolderName(
            ExperienceIds.StandardMerge,
            output);

        Assert.Equal("NT51950_DxxxxTxxxx_20260820", actual);
    }

    private static CompositionOutputPreparation Preparation(
        string actualFileName,
        string automaticFileName,
        IReadOnlyList<OutputNamingTokenSummary> tokens)
    {
        OutputNamingSummary naming = new(
            "normal-flashcode-v1",
            "template.bin",
            automaticFileName,
            actualFileName,
            isExplicitOverride: true,
            "utc",
            ResolvedAtUtc,
            tokens);
        return new CompositionOutputPreparation(
            new CompositionOutputNamePreview(actualFileName, naming, []),
            []);
    }
}
