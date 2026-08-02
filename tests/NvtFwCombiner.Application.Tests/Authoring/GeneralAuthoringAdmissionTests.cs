using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Application.Tests.Authoring;

/// <summary>Safety-contract tests for canonical General occupancy and resource admission.</summary>
public sealed class GeneralAuthoringAdmissionTests
{
    /// <summary>Admission snapshots reject null entries in every independently owned collection.</summary>
    [Fact]
    public void AdmissionSnapshotsRejectNullCollectionMembers()
    {
        var draft = new GeneralMappingDraftState([]);

        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralAuthoringAdmissionResult(
                draft,
                "parent",
                savedRuleId: null,
                effectiveLimits: null,
                inputResources: [null!],
                occupancySegments: [],
                issues: []));
        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralAuthoringAdmissionResult(
                draft,
                "parent",
                savedRuleId: null,
                effectiveLimits: null,
                inputResources: [],
                occupancySegments: [null!],
                issues: []));
        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralAuthoringAdmissionResult(
                draft,
                "parent",
                savedRuleId: null,
                effectiveLimits: null,
                inputResources: [],
                occupancySegments: [],
                issues: [null!]));

        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralAuthoringAdmissionSummary(
                "parent",
                savedRuleId: null,
                effectiveLimits: null,
                inputResources: [null!],
                occupancySegments: [],
                issues: []));
        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralAuthoringAdmissionSummary(
                "parent",
                savedRuleId: null,
                effectiveLimits: null,
                inputResources: [],
                occupancySegments: [null!],
                issues: []));
        _ = Assert.Throws<ArgumentException>(() =>
            new GeneralAuthoringAdmissionSummary(
                "parent",
                savedRuleId: null,
                effectiveLimits: null,
                inputResources: [],
                occupancySegments: [],
                issues: [null!]));
    }

    /// <summary>Every authored source kind enters one order-independent occupancy ledger.</summary>
    [Fact]
    public void RejectsEveryTargetIntersectionWithStableIdsAndExactHalfOpenRange()
    {
        GeneralMappingDraftRow[] rows =
        [
            CreateRow("file", GeneralMappingSource.File("file.bin"), 0x10, 0x10),
            CreateRow("overwrite", GeneralMappingSource.HexOverwrite("AA"), 0x18, 0x08),
            CreateRow("fill", GeneralMappingSource.HexFill("FF"), 0x0F, 0x02),
        ];

        GeneralAuthoringAdmissionResult forward = Evaluate(rows);
        GeneralAuthoringAdmissionResult reordered = Evaluate([rows[2], rows[0], rows[1]]);

        Assert.False(forward.IsAdmitted);
        Assert.Equal(
            forward.Issues.Select(issue => issue.IssueId),
            reordered.Issues.Select(issue => issue.IssueId));
        GeneralAuthoringAdmissionIssue fileOverwrite = Assert.Single(
            forward.Issues,
            issue => issue.MappingIds.SequenceEqual(["file", "overwrite"]));
        Assert.Equal(GeneralAuthoringIssueCodes.TargetIntersection, fileOverwrite.Code);
        Assert.Equal(new ByteRange(0x18, 0x08), fileOverwrite.Intersection);
        Assert.Contains("[0x18, 0x20)", fileOverwrite.Message, StringComparison.Ordinal);
    }

    /// <summary>Boundary-touching half-open ranges are admitted and retain separate occupancy.</summary>
    [Fact]
    public void AdmitsBoundaryTouchingRanges()
    {
        GeneralAuthoringAdmissionResult result = Evaluate(
        [
            CreateRow("left", GeneralMappingSource.File("left.bin"), 0x00, 0x10),
            CreateRow("right", GeneralMappingSource.File("right.bin"), 0x10, 0x10),
        ]);

        Assert.True(result.IsAdmitted);
        Assert.Equal(2, result.OccupancySegments.Count);
    }

    /// <summary>Containment reports the contained half-open interval rather than row-order wording.</summary>
    [Fact]
    public void ReportsExactContainedIntersection()
    {
        GeneralAuthoringAdmissionResult result = Evaluate(
        [
            CreateRow("outer", GeneralMappingSource.File("outer.bin"), 0x10, 0x20),
            CreateRow("inner", GeneralMappingSource.HexFill("00"), 0x18, 0x04),
        ]);

        GeneralAuthoringAdmissionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(new ByteRange(0x18, 0x04), issue.Intersection);
        Assert.Equal(["inner", "outer"], issue.MappingIds);
    }

    /// <summary>The resolved ceiling is the strict intersection of technical, Parent, and Saved Rule limits.</summary>
    [Fact]
    public void ResolvesTechnicalParentAndSavedRuleIntersection()
    {
        var technical = new GeneralResourceLimits(
            maximumMappingCount: 8,
            maximumTotalWriteBytes: 0x100,
            maximumFileBytes: 0x100,
            maximumSafeMaterializationBytes: 0x80,
            [new GeneralSlotLengthLimits(
                "slot",
                0x20,
                0x60,
                [0x20, 0x40, 0x60])]);
        var parent = new GeneralResourceLimits(
            maximumMappingCount: 6,
            maximumTotalWriteBytes: 0x80,
            maximumFileBytes: 0x90,
            maximumSafeMaterializationBytes: 0x60,
            [new GeneralSlotLengthLimits("slot", 0x10, 0x80, [0x20, 0x40, 0x80])]);
        var savedRule = new GeneralResourceLimits(
            maximumMappingCount: 4,
            maximumTotalWriteBytes: 0x40,
            maximumFileBytes: 0x70,
            maximumSafeMaterializationBytes: 0x50,
            [new GeneralSlotLengthLimits("slot", 0x20, 0x40, [0x20, 0x40])]);

        GeneralResourceResolutionResult result = GeneralResourceLimitResolver.Resolve(
            technical,
            Parent(parent),
            Saved(savedRule));

        Assert.True(result.IsResolved);
        Assert.Equal(4, result.EffectiveLimits!.MaximumMappingCount);
        Assert.Equal(0x40, result.EffectiveLimits.MaximumTotalWriteBytes);
        Assert.Equal(0x70, result.EffectiveLimits.MaximumFileBytes);
        Assert.Equal(0x50, result.EffectiveLimits.MaximumSafeMaterializationBytes);
        GeneralSlotLengthLimits slot = Assert.Single(result.EffectiveLimits.SlotLimits);
        Assert.Equal(0x20, slot.MinimumBytes);
        Assert.Equal(0x40, slot.MaximumBytes);
        Assert.Equal([0x20, 0x40], slot.AllowedLengths);
    }

    /// <summary>A Saved Rule cannot broaden its exact Trusted Parent or create an empty slot intersection.</summary>
    [Fact]
    public void RejectsBroaderOrEmptySavedRuleLimits()
    {
        GeneralResourceLimits technical = CreateLimits();
        GeneralResourceLimits parent = new(
            8,
            0x100,
            0x100,
            0x100,
            [new GeneralSlotLengthLimits("slot", 0x10, 0x40, [0x20, 0x40])]);
        GeneralResourceLimits broader = new(
            9,
            0x100,
            0x100,
            0x100,
            [new GeneralSlotLengthLimits("slot", 0x08, 0x40, [0x20, 0x40])]);
        GeneralResourceLimits empty = new(
            8,
            0x100,
            0x100,
            0x100,
            [new GeneralSlotLengthLimits("slot", 0x10, 0x40, [0x30])]);

        GeneralResourceResolutionResult broaderResult =
            GeneralResourceLimitResolver.Resolve(
                technical,
                Parent(parent),
                Saved(broader));
        GeneralResourceResolutionResult emptyResult =
            GeneralResourceLimitResolver.Resolve(
                technical,
                Parent(parent),
                Saved(empty));

        Assert.False(broaderResult.IsResolved);
        Assert.Contains(
            broaderResult.Issues,
            issue => issue.Code == GeneralAuthoringIssueCodes.SavedRuleBroadensParent);
        Assert.False(emptyResult.IsResolved);
        Assert.Contains(
            emptyResult.Issues,
            issue => issue.Code == GeneralAuthoringIssueCodes.EffectiveLimitsEmpty);
    }

    /// <summary>A caller cannot attach Saved Rule provenance for a different exact Parent.</summary>
    [Fact]
    public void RejectsSavedRuleWhoseExactParentDoesNotMatchResolvedAuthority()
    {
        GeneralResourceLimits limits = CreateLimits();
        SavedRuleParentIdentity trustedParent = ExactParent("map-a");
        var savedRuleIdentity = new SavedRuleExecutionIdentity(
            "rule",
            "1.0.0",
            new string('a', 64),
            ExactParent("map-b"));
        GeneralAuthoringAdmissionResult result = GeneralAuthoringAdmission.Evaluate(
            new GeneralMappingDraftState(
            [
                CreateRow(
                    "fill",
                    GeneralMappingSource.HexFill("A5"),
                    targetStart: 0,
                    length: 1),
            ]),
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.OutputImage] = 1,
            },
            [],
            limits,
            new GeneralTrustedParentResourcePolicy(trustedParent, limits),
            new GeneralSavedRuleResourcePolicy(
                TrustedPublication(savedRuleIdentity),
                limits));

        Assert.False(result.IsAdmitted);
        Assert.Null(result.SavedRuleId);
        Assert.Null(result.SavedRule);
        Assert.Contains(
            result.Issues,
            issue => issue.Code ==
                GeneralAuthoringIssueCodes.SavedRuleParentMismatch);
    }

    /// <summary>Count, total-write, whole-file, slot, and inline allocation ceilings block before execution.</summary>
    [Fact]
    public void ReportsEveryResourceCeilingBeforeExecution()
    {
        var technical = new GeneralResourceLimits(
            maximumMappingCount: 1,
            maximumTotalWriteBytes: 3,
            maximumFileBytes: 5,
            maximumSafeMaterializationBytes: 2,
            [new GeneralSlotLengthLimits("file", 1, 4, [4])]);
        GeneralMappingDraftRow[] rows =
        [
            CreateRow("file", GeneralMappingSource.File("file.bin"), 0, 2),
            CreateRow("fill", GeneralMappingSource.HexFill("FF"), 4, 3),
        ];

        GeneralAuthoringAdmissionResult result = GeneralAuthoringAdmission.Evaluate(
            new GeneralMappingDraftState(rows),
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.OutputImage] = 0x20,
            },
            [new GeneralInputResource("file", 6)],
            technical,
            Parent(technical),
            savedRule: null);

        Assert.False(result.IsAdmitted);
        Assert.Contains(result.Issues, issue => issue.Code == GeneralAuthoringIssueCodes.MappingCountExceeded);
        Assert.Contains(result.Issues, issue => issue.Code == GeneralAuthoringIssueCodes.TotalWriteBytesExceeded);
        Assert.Contains(result.Issues, issue => issue.Code == GeneralAuthoringIssueCodes.FileSizeExceeded);
        Assert.Contains(result.Issues, issue => issue.Code == GeneralAuthoringIssueCodes.SlotLengthRejected);
        Assert.Contains(result.Issues, issue => issue.Code == GeneralAuthoringIssueCodes.InlineMaterializationExceeded);
    }

    /// <summary>An oversized overwrite payload is measured without allocating its byte array.</summary>
    [Fact]
    public void RejectsOversizedInlineOverwritePayload()
    {
        GeneralMappingDraftRow row = new(
            "overwrite",
            ExplicitMappingOperationKind.ReplaceRange,
            GeneralMappingSource.HexOverwrite(new string('A', 18), "overwrite"),
            new ByteRange(0, 1),
            CompositionAddressSpaceIds.OutputImage,
            new ByteRange(0, 1),
            OverlapPolicy.Reject,
            alignment: 1,
            "Test overwrite.");
        GeneralResourceLimits limits = new(
            maximumMappingCount: 1,
            maximumTotalWriteBytes: 1,
            maximumFileBytes: 1,
            maximumSafeMaterializationBytes: 4);

        GeneralAuthoringAdmissionResult result = GeneralAuthoringAdmission.Evaluate(
            new GeneralMappingDraftState([row]),
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.OutputImage] = 1,
            },
            [],
            limits,
            Parent(limits),
            savedRule: null);

        GeneralAuthoringAdmissionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(GeneralAuthoringIssueCodes.InlineMaterializationExceeded, issue.Code);
        Assert.Contains("requires 9 bytes", issue.Message, StringComparison.Ordinal);
    }

    /// <summary>Unused file tails remain valid when whole-file and resolved slot admission allow them.</summary>
    [Fact]
    public void AllowsUnreferencedFileTailWithinResolvedLimits()
    {
        GeneralMappingDraftRow row =
            CreateRow("file", GeneralMappingSource.File("file.bin"), 0x10, 0x10);

        GeneralResourceLimits limits = CreateLimits(
            [new GeneralSlotLengthLimits("file", 1, 0x1000)]);
        GeneralAuthoringAdmissionResult result = GeneralAuthoringAdmission.Evaluate(
            new GeneralMappingDraftState([row]),
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.OutputImage] = 0x40,
            },
            [new GeneralInputResource("file", 0x30)],
            limits,
            Parent(limits),
            savedRule: null);

        Assert.True(result.IsAdmitted);
    }

    /// <summary>The production use case observes lengths through its inward port and fails closed for an undeclared Parent slot.</summary>
    [Fact]
    public void UseCaseRejectsFileSlotMissingFromExactTrustedParent()
    {
        GeneralMappingDraftRow row =
            CreateRow("file", GeneralMappingSource.File("opaque.bin"), 0, 4);
        GeneralResourceLimits parent = CreateLimits();
        var observer = new FakeResourceObserver(
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["opaque.bin"] = 4,
            });
        var useCase = new GeneralAuthoringAdmissionUseCase(observer);

        GeneralAuthoringAdmissionResult result = useCase.Resolve(
            new GeneralAuthoringAdmissionRequest(
                new GeneralMappingDraftState([row]),
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    [CompositionAddressSpaceIds.OutputImage] = 4,
                },
                Parent(parent)));

        Assert.False(result.IsAdmitted);
        Assert.Equal(4, Assert.Single(result.InputResources).LengthBytes);
        GeneralAuthoringAdmissionIssue issue = Assert.Single(result.Issues);
        Assert.Equal(
            GeneralAuthoringIssueCodes.TrustedParentSlotMissing,
            issue.Code);
        _ = Assert.Throws<InvalidOperationException>(
            result.RequireAdmittedDraft);
    }

    /// <summary>Observed facts and Saved Rule narrowing are retained by the one admitted result.</summary>
    [Fact]
    public void UseCaseAppliesSavedRuleNarrowingToObservedLength()
    {
        GeneralMappingDraftRow row =
            CreateRow("file", GeneralMappingSource.File("opaque.bin"), 0, 4);
        GeneralResourceLimits parent = CreateLimits(
            [new GeneralSlotLengthLimits("file", 1, 0x40)]);
        GeneralResourceLimits savedRule = new(
            maximumMappingCount: 1,
            maximumTotalWriteBytes: 4,
            maximumFileBytes: 0x20,
            maximumSafeMaterializationBytes: 0x20,
            [new GeneralSlotLengthLimits("file", 0x10, 0x20)]);
        var useCase = new GeneralAuthoringAdmissionUseCase(
            new FakeResourceObserver(
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    ["opaque.bin"] = 0x28,
                }));

        GeneralAuthoringAdmissionResult result = useCase.Resolve(
            new GeneralAuthoringAdmissionRequest(
                new GeneralMappingDraftState([row]),
                new Dictionary<string, long>(StringComparer.Ordinal)
                {
                    [CompositionAddressSpaceIds.OutputImage] = 4,
                },
                new GeneralTrustedParentResourcePolicy(ExactParent("map-a"), parent),
                Saved(savedRule)));

        Assert.False(result.IsAdmitted);
        Assert.Equal("profile", result.TrustedParentId);
        Assert.Equal("test-rule", result.SavedRuleId);
        Assert.Equal(0x20, result.EffectiveLimits!.MaximumFileBytes);
        Assert.Equal(0x28, Assert.Single(result.InputResources).LengthBytes);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == GeneralAuthoringIssueCodes.FileSizeExceeded);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == GeneralAuthoringIssueCodes.SlotLengthRejected);
    }

    /// <summary>Total authored write arithmetic fails closed on Int64 overflow.</summary>
    [Fact]
    public void RejectsTotalWriteLengthOverflow()
    {
        long largeLength = (long.MaxValue / 2) + 1;
        GeneralMappingDraftRow first = CreateInlineRow(
            "first",
            "first-output",
            largeLength);
        GeneralMappingDraftRow second = CreateInlineRow(
            "second",
            "second-output",
            largeLength);
        GeneralResourceLimits limits = new(
            maximumMappingCount: 2,
            maximumTotalWriteBytes: long.MaxValue,
            maximumFileBytes: long.MaxValue,
            maximumSafeMaterializationBytes: long.MaxValue);

        GeneralAuthoringAdmissionResult result = GeneralAuthoringAdmission.Evaluate(
            new GeneralMappingDraftState([first, second]),
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["first-output"] = long.MaxValue,
                ["second-output"] = long.MaxValue,
            },
            [],
            limits,
            Parent(limits),
            savedRule: null);

        Assert.False(result.IsAdmitted);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == GeneralAuthoringIssueCodes.TotalWriteBytesOverflow);
    }

    /// <summary>Out-of-bounds authored writes block, while profile POSTBUILD never enters authored occupancy.</summary>
    [Fact]
    public void RejectsOutOfBoundsWithoutInventingPostbuildOccupancy()
    {
        GeneralAuthoringAdmissionResult result = Evaluate(
        [
            CreateRow("authored", GeneralMappingSource.File("file.bin"), 0x1F, 0x02),
        ],
        targetCapacity: 0x20);

        Assert.False(result.IsAdmitted);
        _ = Assert.Single(result.OccupancySegments);
        Assert.Equal("authored", result.OccupancySegments[0].MappingId);
        Assert.Contains(
            result.Issues,
            issue => issue.Code == GeneralAuthoringIssueCodes.TargetOutOfBounds);
        Assert.DoesNotContain(
            result.OccupancySegments,
            segment => segment.MappingId.Contains("postbuild", StringComparison.OrdinalIgnoreCase));
    }

    private static GeneralAuthoringAdmissionResult Evaluate(
        IReadOnlyList<GeneralMappingDraftRow> rows,
        long targetCapacity = 0x100)
    {
        GeneralInputResource[] inputs =
        [
            .. rows
                .Where(row => row.Source.Kind == GeneralMappingSourceKind.FileArtifact)
                .Select(row => new GeneralInputResource(
                    row.MappingId,
                    Math.Max(row.SourceRange.EndExclusive, 0x40))),
        ];
        GeneralResourceLimits limits = CreateLimits();
        GeneralResourceLimits parentLimits = CreateLimits(
            rows
                .Where(row =>
                    row.Source.Kind == GeneralMappingSourceKind.FileArtifact)
                .Select(row =>
                    new GeneralSlotLengthLimits(
                        row.MappingId,
                        1,
                        0x1000)));
        return GeneralAuthoringAdmission.Evaluate(
            new GeneralMappingDraftState(rows),
            new Dictionary<string, long>(StringComparer.Ordinal)
            {
                [CompositionAddressSpaceIds.OutputImage] = targetCapacity,
            },
            inputs,
            limits,
            Parent(parentLimits),
            savedRule: null);
    }

    private static GeneralResourceLimits CreateLimits(
        IEnumerable<GeneralSlotLengthLimits>? slots = null)
    {
        return new GeneralResourceLimits(
            maximumMappingCount: 64,
            maximumTotalWriteBytes: 0x1000,
            maximumFileBytes: 0x1000,
            maximumSafeMaterializationBytes: 0x1000,
            slots);
    }

    private static GeneralTrustedParentResourcePolicy Parent(
        GeneralResourceLimits limits)
    {
        return new GeneralTrustedParentResourcePolicy("test-parent", limits);
    }

    private static GeneralSavedRuleResourcePolicy Saved(
        GeneralResourceLimits limits)
    {
        var identity = new SavedRuleExecutionIdentity(
            "test-rule",
            "1.0.0",
            new string('a', 64),
            ExactParent("map-a"));
        return new GeneralSavedRuleResourcePolicy(
            TrustedPublication(identity),
            limits);
    }

    private static SavedRuleParentIdentity ExactParent(string mapId)
    {
        return new SavedRuleParentIdentity(
            "bundle",
            "1.0.0",
            new string('b', 64),
            "profile",
            "1.0.0",
            new string('c', 64),
            "family",
            "1.0.0",
            new string('d', 64),
            mapId);
    }

    private static SavedRuleLifecycleSnapshot TrustedPublication(
        SavedRuleExecutionIdentity identity)
    {
        return new SavedRuleLifecycleSnapshot(
            identity,
            SavedRuleStorageKind.TrustedCatalog,
            SavedRuleLifecycleState.Published,
            hasApproval: true,
            hasEvidence: true,
            isTrusted: true);
    }

    private static GeneralMappingDraftRow CreateRow(
        string id,
        GeneralMappingSource source,
        long targetStart,
        long length)
    {
        return new GeneralMappingDraftRow(
            id,
            source.Kind == GeneralMappingSourceKind.FileArtifact
                ? ExplicitMappingOperationKind.CopyRange
                : ExplicitMappingOperationKind.ReplaceRange,
            source,
            new ByteRange(0, length),
            CompositionAddressSpaceIds.OutputImage,
            new ByteRange(targetStart, length),
            OverlapPolicy.Reject,
            alignment: 1,
            "Test mapping.");
    }

    private static GeneralMappingDraftRow CreateInlineRow(
        string id,
        string targetSpaceId,
        long length)
    {
        return new GeneralMappingDraftRow(
            id,
            ExplicitMappingOperationKind.ReplaceRange,
            GeneralMappingSource.HexFill("00", id),
            new ByteRange(0, length),
            targetSpaceId,
            new ByteRange(0, length),
            OverlapPolicy.Reject,
            alignment: 1,
            "Test inline mapping.");
    }

    private sealed class FakeResourceObserver(
        IReadOnlyDictionary<string, long> lengths) :
        IGeneralInputResourceObservationPort
    {
        public bool TryObserveLength(
            GeneralInputResourceObservationRequest request,
            out long lengthBytes)
        {
            return lengths.TryGetValue(
                request.ResourceReference,
                out lengthBytes);
        }
    }
}
