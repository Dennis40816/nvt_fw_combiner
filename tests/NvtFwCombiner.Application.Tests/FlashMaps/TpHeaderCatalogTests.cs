using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.ExternalTools;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.FlashMaps;

/// <summary>Executable checks for the TP header/write category catalog.</summary>
public sealed class TpHeaderCatalogTests
{
    /// <summary>
    /// The normal NT51926 header starts with the ILM source address field documented in the
    /// 925&amp;926 TDDI Flash Header worksheet. This is report semantics only; it does not grant a write range.
    /// </summary>
    [Fact]
    public void Nt51926HeaderModelsIlmStartAddressInBin()
    {
        Assert.True(TpBinaryModelCatalog.TryFind("NT51926", out TpBinaryModel? model));

        TpBinaryCategory header = Assert.Single(
            model!.Categories,
            category => category.CategoryId == TpBinaryCategoryIds.TpFlashHeader);
        TpHeaderLayout layout = Assert.IsType<TpHeaderLayout>(header.HeaderLayout);
        TpHeaderField field = Assert.Single(
            layout.Fields,
            candidate => candidate.FieldId == "ilm-start-address-in-bin");

        Assert.Equal(new ByteRange(0x0000, 0x0004), field.Range);
        Assert.Equal("ILM start address in BIN", field.DisplayName);
        Assert.True(TpHeaderCatalog.TryFindField("NT51926", field.Range, out TpHeaderField? resolved));
        Assert.Same(field, resolved);
    }

    /// <summary>
    /// NT51927 CRC-only postbuild ranges are mapped to the actual per-header field instead of a generic CRC bucket.
    /// </summary>
    [Fact]
    public void Nt51927HeaderModelsDlmCrcZero()
    {
        Assert.True(
            TpHeaderCatalog.TryFindField("NT51927", new ByteRange(0x23C, 0x4), out TpHeaderField? field));

        Assert.Equal("header-0-dlm-crc", field!.FieldId);
        Assert.Equal("DLM CRC 0", field.DisplayName);
    }

    /// <summary>Ambiguous header-field declarations fail before a report can select an arbitrary subject.</summary>
    [Fact]
    public void HeaderLayoutRejectsOverlappingFields()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() => new TpHeaderLayout(
            "overlapping-layout",
            "Overlapping layout",
            TpHeaderModelStatus.Workbook,
            [new ByteRange(0, 8)],
            [
                new TpHeaderField("first", "First", new ByteRange(0, 4)),
                new TpHeaderField("second", "Second", new ByteRange(2, 4)),
            ],
            "test evidence"));

        Assert.Contains("cannot overlap", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Every selectable IC exposes the same top-level TP image categories even when a particular category has no
    /// region in that IC's TP Overview row set.
    /// </summary>
    [Fact]
    public void EveryIcExposesOneStableTopLevelBinaryCategoryStructure()
    {
        foreach (string icId in TpFlashMapCatalog.IcIds)
        {
            Assert.True(TpBinaryModelCatalog.TryFind(icId, out TpBinaryModel? model));

            Assert.Equal(TpBinaryModel.DefaultRootId, model!.RootId);
            Assert.Equal(
                TpBinaryCategoryIds.All,
                model.Categories.Select(category => category.CategoryId));
            Assert.Contains(
                model.Categories,
                category => category.CategoryId == TpBinaryCategoryIds.TpFlashHeader &&
                            category.HeaderLayout is not null);
        }
    }

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

    /// <summary>All postbuild planner write-section ids must have report labels in the TP header catalog.</summary>
    [Fact]
    public void PostbuildPlannerWriteSectionsAreDeclaredInHeaderCatalog()
    {
        HashSet<string> knownSectionIds = [.. TpHeaderCatalog.All.Select(section => section.SectionId)];

        foreach (LegacyCombinerPostbuildCommandPlan plan in AllPostbuildPlans())
        {
            ByteRange[] stagedRanges =
            [
                .. LegacyCombinerPostbuildPlanner.GetStagedFileBlocks(plan)
                    .Select(block => block.FirmwareRange),
            ];
            long capacity = LegacyCombinerPostbuildPlanner.CalculateRequiredCapacity(plan, stagedRanges);
            LegacyCombinerPostbuildWriteRange[] sections =
            [
                .. LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForStagedSources(
                    plan,
                    capacity,
                    stagedRanges,
                    stagedRanges),
                .. LegacyCombinerPostbuildPlanner.GetAllowedWriteRangeSectionsForInPlaceRefresh(plan, capacity),
            ];

            Assert.NotEmpty(sections);
            Assert.All(sections, section =>
                Assert.Contains(section.SectionId, knownSectionIds));
        }
    }

    private static IEnumerable<LegacyCombinerPostbuildCommandPlan> AllPostbuildPlans()
    {
        foreach ((LegacyCombinerPostbuildProfile profile, IcNumberSelection selection) in
                 PostbuildSelectionTestCases.AllProfileBranchSelections())
        {
            yield return LegacyCombinerPostbuildPlanner.CreatePlan(profile, selection);
        }
    }
}
