using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.Composition;
using NvtFwCombiner.Application.Ports;
using NvtFwCombiner.Domain.Composition;
using System.Security.Cryptography;

namespace NvtFwCombiner.Application.Tests.Composition;

/// <summary>Tests immutable accepted source evidence retained for atomic delivery.</summary>
public sealed class CompositionExecutionBundleDeliveryTests
{
    /// <summary>One bundle selects only a compiled delivery retained by its exact preparation.</summary>
    [Fact]
    public void AdditionalDeliverySelectionUsesExactPreparedPlan()
    {
        CompositionAdditionalDeliveryPlan prepared = new(
            "profile",
            CompiledAdditionalDelivery.AbAFlashCodeKind,
            new ByteRange(0, 4),
            "a-flashcode.bin");

        Assert.Same(
            prepared,
            CompositionExecutionBundleDelivery.ResolveAdditionalDelivery(
                [prepared],
                CompiledAdditionalDelivery.AbAFlashCodeKind));
        Assert.Null(CompositionExecutionBundleDelivery.ResolveAdditionalDelivery(
            [prepared],
            additionalDeliveryKind: null));
        _ = Assert.Throws<ArgumentException>(() =>
            CompositionExecutionBundleDelivery.ResolveAdditionalDelivery(
                [prepared],
                "not-declared"));
    }

    /// <summary>Bundle receipt must prove exact A-only role, kind, bytes, and accepted-source provenance.</summary>
    [Fact]
    public void ReceiptValidationAcceptsExactAdditionalDeliveryManifest()
    {
        byte[] output = [9, 8, 7, 6];
        byte[] sourceBytes = [1, 2];
        CompositionExecutionBundleSource source = new(
            "dp",
            "dp-slot",
            "source-id",
            FileStamp.FromBytes(sourceBytes),
            "input.bin",
            sourceBytes);
        CompositionAdditionalDeliveryPlan additional = new(
            "profile",
            CompiledAdditionalDelivery.AbAFlashCodeKind,
            new ByteRange(0, 2),
            "a.bin");
        CompositionOutputBundleCommitReceipt receipt = new(
            "C:/delivery/bundle",
            [
                Artifact("output", null, "output.bin", output),
                Artifact(
                    "additional-delivery",
                    CompiledAdditionalDelivery.AbAFlashCodeKind,
                    "a.bin",
                    output.AsSpan(0, 2)),
                Artifact("source", "dp", "input.bin", sourceBytes),
            ]);

        CompositionOutputBundleDeliverySummary actual =
            CompositionRunService.ValidateBundleReceipt(
                receipt,
                [source],
                additional,
                "output.bin",
                output);

        Assert.Equal(
            ["output", "additional-delivery", "source"],
            actual.Artifacts.Select(static artifact => artifact.Role));
        Assert.Equal(
            CompiledAdditionalDelivery.AbAFlashCodeKind,
            actual.Artifacts[1].BindingId);
        Assert.Equal(2, actual.Artifacts[1].Size);
        Assert.Equal(Sha256(output.AsSpan(0, 2)), actual.Artifacts[1].Sha256);
    }

    /// <summary>A receipt that relabels A-only bytes as a source fails closed.</summary>
    [Fact]
    public void ReceiptValidationRejectsAdditionalDeliveryRoleDrift()
    {
        byte[] output = [9, 8];
        CompositionAdditionalDeliveryPlan additional = new(
            "profile",
            CompiledAdditionalDelivery.AbAFlashCodeKind,
            new ByteRange(0, 1),
            "a.bin");
        CompositionOutputBundleCommitReceipt receipt = new(
            "C:/delivery/bundle",
            [
                Artifact("output", null, "output.bin", output),
                Artifact("source", additional.DeliveryKind, "a.bin", output.AsSpan(0, 1)),
            ]);

        _ = Assert.Throws<InvalidOperationException>(() =>
            CompositionRunService.ValidateBundleReceipt(
                receipt,
                [],
                additional,
                "output.bin",
                output));
    }

    private static CompositionOutputBundleArtifactReceipt Artifact(
        string role,
        string? bindingId,
        string fileName,
        ReadOnlySpan<byte> bytes)
    {
        return new CompositionOutputBundleArtifactReceipt(
            role,
            bindingId,
            fileName,
            bytes.Length,
            Sha256(bytes));
    }

    private static string Sha256(ReadOnlySpan<byte> bytes)
    {
        return Convert.ToHexStringLower(SHA256.HashData(bytes));
    }

    /// <summary>Retained delivery bytes are snapshotted with exact path-free manifest facts.</summary>
    [Fact]
    public void SourceSnapshotsAcceptedBytesAndPublishesPathFreeFacts()
    {
        byte[] accepted = [1, 2, 3];
        FileStamp stamp = FileStamp.FromBytes(accepted);

        CompositionExecutionBundleSource source = new(
            "dp",
            "dp-slot",
            "opaque-canonical-identity",
            stamp,
            "dp.bin",
            accepted);
        accepted[0] = 0xFF;

        Assert.Equal("dp", source.Summary.BindingId);
        Assert.Equal("dp-slot", source.Summary.SlotId);
        Assert.Equal("dp.bin", source.Summary.OriginalFileName);
        Assert.Equal(stamp.AcceptedLength, source.Summary.Size);
        Assert.Equal(stamp.Sha256, source.Summary.Sha256);
        Assert.Equal([1, 2, 3], source.Bytes.ToArray());
        Assert.Equal("opaque-canonical-identity", source.AcceptedIdentity);
    }

    /// <summary>Canonical identity policy owns case rules, dedupe order, and virtual exclusion.</summary>
    [Fact]
    public void PlannerDeduplicatesSameIdentityAndStampInBindingOrder()
    {
        byte[] accepted = [1, 2, 3];
        FileStamp stamp = FileStamp.FromBytes(accepted);
        CompositionOutputBundleSourceCandidate[] candidates =
        [
            new("dp", "dp-slot", "C:/input/same.bin", stamp, accepted),
            new("dp-alias", "alias-slot", "c:/INPUT/SAME.bin", stamp, accepted),
            new(
                "virtual",
                "virtual-slot",
                VirtualArtifactLocator.CreateGeneralReplacePatch("generated"),
                stamp,
                accepted),
        ];

        IReadOnlyList<CompositionExecutionBundleSource> actual =
            CompositionOutputBundleSourcePlanner.Canonicalize(
                candidates,
                new CaseInsensitiveIdentityPolicy());

        CompositionExecutionBundleSource source = Assert.Single(actual);
        Assert.Equal("dp", source.Summary.BindingId);
        Assert.Equal("same.bin", source.Summary.OriginalFileName);
    }

    /// <summary>The same canonical identity with a different accepted stamp fails closed.</summary>
    [Fact]
    public void PlannerRejectsSameIdentityWithDifferentStamp()
    {
        byte[] first = [1];
        byte[] second = [2];
        CompositionOutputBundleSourceCandidate[] candidates =
        [
            new("dp", "dp-slot", "C:/input/same.bin", FileStamp.FromBytes(first), first),
            new("tp", "tp-slot", "c:/INPUT/SAME.bin", FileStamp.FromBytes(second), second),
        ];

        _ = Assert.Throws<InvalidOperationException>(() =>
            CompositionOutputBundleSourcePlanner.Canonicalize(
                candidates,
                new CaseInsensitiveIdentityPolicy()));
    }

    private sealed class CaseInsensitiveIdentityPolicy : ICompositionArtifactIdentityPolicy
    {
        public CompositionAcceptedArtifactIdentity Resolve(string artifactLocator)
        {
            string normalized = artifactLocator.Replace('\\', '/').ToUpperInvariant();
            string originalFileName = artifactLocator[(artifactLocator.LastIndexOf('/') + 1)..];
            return new CompositionAcceptedArtifactIdentity(normalized, originalFileName);
        }
    }
}
