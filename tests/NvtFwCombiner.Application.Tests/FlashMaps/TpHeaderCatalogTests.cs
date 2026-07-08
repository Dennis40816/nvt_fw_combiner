using NvtFwCombiner.Application.FlashMaps;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

/// <summary>Executable checks for the TP header/write category catalog.</summary>
public sealed class TpHeaderCatalogTests
{
    /// <summary>Section ids are stable machine-readable keys used by reports and write-range policies.</summary>
    [Fact]
    public void SectionIdsAreUnique()
    {
        string[] ids = [.. TpHeaderCatalog.All.Select(section => section.SectionId)];

        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Known postbuild header blocks resolve to named TP header sections.</summary>
    [Theory]
    [InlineData("fw-config-backup", false, TpHeaderSectionIds.FirmwareConfigBackup)]
    [InlineData("final-header-backup", false, TpHeaderSectionIds.HeaderCopyFinalBackup)]
    [InlineData("header-refresh-master", false, TpHeaderSectionIds.HeaderCopyMaster)]
    [InlineData("header-right-copy", false, TpHeaderSectionIds.HeaderCopyRight)]
    [InlineData("header-left-copy", false, TpHeaderSectionIds.HeaderCopyLeft)]
    [InlineData("header-copy-final", false, TpHeaderSectionIds.HeaderCopyFinal)]
    [InlineData("header-copy", false, TpHeaderSectionIds.HeaderCopy)]
    [InlineData("copy-right-window", false, TpHeaderSectionIds.WindowCopyRight)]
    [InlineData("copy-left-window", false, TpHeaderSectionIds.WindowCopyLeft)]
    public void KnownPostbuildBlocksResolveToHeaderSections(
        string blockId,
        bool isStagedFileBlock,
        string expectedSectionId)
    {
        string sectionId = TpHeaderCatalog.ResolvePostbuildBlockSectionId(blockId, isStagedFileBlock);

        Assert.Equal(expectedSectionId, sectionId);
    }

    /// <summary>Staged files default to declared CtrlRAM replacement, while firmware blocks default to postbuild copy.</summary>
    [Theory]
    [InlineData(true, TpHeaderSectionIds.CtrlRamReplacement)]
    [InlineData(false, TpHeaderSectionIds.PostbuildCopy)]
    public void UnknownPostbuildBlocksResolveBySourceKind(bool isStagedFileBlock, string expectedSectionId)
    {
        string sectionId = TpHeaderCatalog.ResolvePostbuildBlockSectionId("unnamed-body", isStagedFileBlock);

        Assert.Equal(expectedSectionId, sectionId);
    }

    /// <summary>Report labels come from the same catalog as postbuild write-range classification.</summary>
    [Fact]
    public void DisplayNamesAreCentralizedForReports()
    {
        Assert.Equal(
            "TP flash header / CRC fields",
            TpHeaderCatalog.GetDisplayName(TpHeaderSectionIds.FlashHeaderCrc));
        Assert.Equal(
            "Header copy / final backup",
            TpHeaderCatalog.GetDisplayName(TpHeaderSectionIds.HeaderCopyFinalBackup));
        Assert.Equal(
            "Postbuild write range",
            TpHeaderCatalog.GetDisplayName("unknown-section"));
    }

    /// <summary>CRC/header labels win over broader postbuild ranges when normalized segments overlap.</summary>
    [Fact]
    public void HeaderCrcHasHigherPriorityThanGenericHeaderCopy()
    {
        Assert.True(
            TpHeaderCatalog.GetPriority(TpHeaderSectionIds.FlashHeaderCrc) >
            TpHeaderCatalog.GetPriority(TpHeaderSectionIds.HeaderCopy));
        Assert.True(
            TpHeaderCatalog.GetPriority(TpHeaderSectionIds.HeaderCopy) >
            TpHeaderCatalog.GetPriority(TpHeaderSectionIds.CtrlRamReplacement));
    }
}
