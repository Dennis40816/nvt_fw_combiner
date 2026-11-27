// Copyright (c) 2026 Dennis Liu. All rights reserved.

using NvtFwCombiner.Profiles.IcFiles;

namespace NvtFwCombiner.ProfileContract.Tests.IcFiles;

/// <summary>Verifies ic File Id Index Tests.</summary>
public sealed class IcFileIdIndexTests
{
    /// <summary>Verifies the Same Id May Exist In Different Opaque Document Kinds.</summary>
    [Fact]
    public void TheSameIdMayExistInDifferentOpaqueDocumentKinds()
    {
        IcFileIdIndex index = IcFileAssertions.Success(IcFileIdIndex.Create(
            [new IdDeclaration("kind-a", "item", new SourceMark(1, 1)),
             new IdDeclaration("kind-b", "item", new SourceMark(2, 1))]));

        Assert.Empty(index.CheckReferences(
            [new IdReference("owner", "kind-a", "item", new SourceMark(4, 3)),
             new IdReference("owner", "kind-b", "item", new SourceMark(5, 3))]));
    }

    /// <summary>Verifies duplicate Id Within One Kind Is Rejected At The Second Declaration.</summary>
    [Fact]
    public void DuplicateIdWithinOneKindIsRejectedAtTheSecondDeclaration()
    {
        IcFileResult<IcFileIdIndex> result = IcFileIdIndex.Create(
            [new IdDeclaration("kind-a", "item", new SourceMark(1, 1)),
             new IdDeclaration("kind-a", "item", new SourceMark(7, 1))]);

        Assert.Equal(new IcFileError(IcFileErrorCode.DuplicateId, new SourceMark(7, 1), "item", "kind-a"),
            IcFileAssertions.Failure(result, IcFileErrorCode.DuplicateId));
    }

    /// <summary>Verifies every Dangling Reference Is Reported In Input Order With Ordinal Identity.</summary>
    [Fact]
    public void EveryDanglingReferenceIsReportedInInputOrderWithOrdinalIdentity()
    {
        IcFileIdIndex index = IcFileAssertions.Success(IcFileIdIndex.Create(
            [new IdDeclaration("kind-a", "item", new SourceMark(1, 1))]));

        Assert.Equal<IcFileError>(
            [new IcFileError(IcFileErrorCode.DanglingReference, new SourceMark(2, 3), "owner-a", "kind-a:Item"),
             new IcFileError(IcFileErrorCode.DanglingReference, new SourceMark(4, 3), "owner-b", "kind-b:absent")],
            index.CheckReferences(
                [new IdReference("owner-a", "kind-a", "Item", new SourceMark(2, 3)),
                 new IdReference("owner-a", "kind-a", "item", new SourceMark(3, 3)),
                 new IdReference("owner-b", "kind-b", "absent", new SourceMark(4, 3))]));
    }
}
