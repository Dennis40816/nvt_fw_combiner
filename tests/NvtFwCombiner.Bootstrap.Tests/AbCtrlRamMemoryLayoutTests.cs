using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.MemoryLayout;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

/// <summary>Declared bank-local effects projected into the complete Reference address space.</summary>
public sealed class AbCtrlRamMemoryLayoutTests
{
    /// <summary>Only selected local effects are shown; bank transfer operations do not color the whole image.</summary>
    [Theory]
    [InlineData(AbCtrlRamBankSelection.A)]
    [InlineData(AbCtrlRamBankSelection.B)]
    [InlineData(AbCtrlRamBankSelection.Both)]
    public void BankCoverageUsesDeclaredRangesAndOriginalOperations(AbCtrlRamBankSelection selection)
    {
        (Dictionary<string, string> paths, Dictionary<string, byte[]> bytes) = AbCtrlRamAuthoringTests.Inputs();
        ActiveSessionSnapshot session = BootstrapTestHost.Canonical.CtrlRamAuthoring.PrepareSession(
            new(ExperienceIds.CtrlRamReplace), "NT51929", "single", paths, bytes, new AbCtrlRamDraftState(selection)).AcceptedSession!;
        CompiledComposition composition = session.ExactCapability!.CompiledComposition;
        MemoryLayoutSnapshot layout = MemoryLayoutProjector.Project(session.ExactCapability, session, composition);
        Assert.Equal(0x80000, layout.Capacity);
        Assert.All(layout.BeforeSegments, static segment => Assert.Equal(MemoryWorkflowDisposition.Kept, segment.Disposition));
        for (int i = 0; i < 2; i++)
        {
            int start = i * 0x40000;
            bool selected = selection == AbCtrlRamBankSelection.Both || (i == 0 ? selection == AbCtrlRamBankSelection.A : selection == AbCtrlRamBankSelection.B);
            MemoryLayoutSegment[] bank = [.. layout.AfterSegments.Where(segment => segment.Range.Start >= start && segment.Range.EndExclusive <= start + 0x40000)];
            Assert.NotEmpty(bank);
            MemoryLayoutSegment nf = Assert.Single(bank, segment => segment.Range.Contains(new ByteRange(start + 0x1FC00, 1)));
            MemoryLayoutSegment retained = Assert.Single(bank, segment => segment.Range.Contains(new ByteRange(start + 0x32000, 1)));
            Assert.Equal(MemoryWorkflowDisposition.Kept, retained.Disposition);
            Assert.Equal("reference-base", retained.ContentSource!.SourceSpaceId);
            if (selected)
            {
                Assert.Equal(MemoryWorkflowDisposition.WillReplace, nf.Disposition);
                Assert.Equal("replace-ctrlram-nf", nf.SourceSlotId);
                Assert.Equal("replace-ctrlram-nf", nf.ContentSource!.SourceSlotId);
                Assert.Equal(nf.LogicalCoverageGroupId, retained.LogicalCoverageGroupId);
                Assert.Contains(bank, static segment => segment.ProcessorEffect == MemoryProcessorEffect.DeclaredWrite);
                Assert.Equal(MemoryProcessorEffect.DeclaredWrite,
                    Assert.Single(bank, segment => segment.Range.Contains(new ByteRange(start + 0x2E000, 1))).ProcessorEffect);
                if (i == 1)
                {
                    MemoryLayoutSegment header = Assert.Single(bank, segment => segment.Range.Contains(new ByteRange(0x47164, 4)));
                    Assert.Contains(header.ContributingOperations, static operation => operation.OperationId == "b-bank/normalize/relocate-tpb-ilm");
                    Assert.Contains(header.ContributingOperations, static operation => operation.OperationId == "b-bank/restore/relocate-tpb-ilm");
                }
            }
            else
            {
                Assert.All(bank, static segment => Assert.Equal(MemoryWorkflowDisposition.Kept, segment.Disposition));
            }
        }
        Assert.Contains(layout.SectionLocators, static section => section.ContentRole == MemoryContentRole.Tp);
        Assert.All(layout.AfterSegments, segment =>
        {
            Assert.Equal(MemoryObservedChange.NotObserved, segment.ObservedChange);
            Assert.All(segment.ContributingOperations, operation =>
            {
                Assert.DoesNotContain("/seed", operation.OperationId, StringComparison.Ordinal);
                Assert.DoesNotContain("/publish", operation.OperationId, StringComparison.Ordinal);
                Assert.Contains(composition.Plan.OrderedOperations, original => ReferenceEquals(original, operation));
            });
        });
    }
}
