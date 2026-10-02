using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
using NvtFwCombiner.Application.FlashMaps;
using NvtFwCombiner.Domain.Composition;

namespace NvtFwCombiner.Bootstrap.Tests;

public sealed partial class ReplaceCliCommandTests
{
    private static void AssertPublishedReplacement(JsonElement report, string bankId, byte[] source, byte[] output)
    {
        JsonElement[] operations = [.. report.GetProperty("Operations").EnumerateArray()];
        JsonElement publish = Assert.Single(operations,
            operation => operation.GetProperty("OperationId").GetString() == bankId + "/publish");
        JsonElement replacement = Assert.Single(operations, operation =>
            operation.GetProperty("SourceSpaceId").GetString() == "replace-ctrlram-normal" &&
            operation.GetProperty("TargetSpaceId").GetString() == publish.GetProperty("SourceSpaceId").GetString());
        ByteRange sourceRange = CliGoldenByteEvidence.Range(replacement.GetProperty("SourceRange"));
        ByteRange outputRange = ProjectPublishedRange(publish,
            CliGoldenByteEvidence.Range(replacement.GetProperty("TargetRange")));
        Assert.Equal(sourceRange.Length, outputRange.Length);
        Assert.Equal(source.AsSpan(checked((int)sourceRange.Start), checked((int)sourceRange.Length)).ToArray(),
            output.AsSpan(checked((int)outputRange.Start), checked((int)outputRange.Length)).ToArray());
    }

    private static void AssertVersionDifferencesAreDeclared(
        CliGoldenByteEvidence.BuildEvidence control, CliGoldenByteEvidence.BuildEvidence edited, CtrlRamAuthoringDraftState draft)
    {
        Assert.Equal(control.Bytes.Length, edited.Bytes.Length);
        JsonElement[] operations = [.. edited.Report.GetProperty("Operations").EnumerateArray()];
        JsonElement[] patches = [.. operations.Where(static operation =>
            operation.GetProperty("OperationId").GetString()!.Contains("patch-fw-", StringComparison.Ordinal))];
        int versionPairCount = draft is AbCtrlRamDraftState ab
            ? (ab.AVersion is null ? 0 : 1) + (ab.BVersion is null ? 0 : 1)
            : 1;
        Assert.Equal(versionPairCount * 2, patches.Length);
        JsonElement[] publications = [.. operations.Where(static operation =>
            operation.GetProperty("OperationId").GetString()!.EndsWith("/publish", StringComparison.Ordinal))];
        string outputSpace = publications.Length == 0
            ? patches[0].GetProperty("TargetSpaceId").GetString()!
            : publications[0].GetProperty("TargetSpaceId").GetString()!;
        JsonElement[] replacements = [.. operations.Where(static operation =>
            operation.GetProperty("SourceSpaceId").GetString()?.StartsWith(
                CompositionAddressSpaceIds.DynamicCtrlRamReplacementPrefix, StringComparison.Ordinal) == true)];
        ByteRange[] payloadRanges = [.. replacements.Select(operation => ProjectOperationRange(
            operation, CliGoldenByteEvidence.Range(operation.GetProperty("TargetRange")), publications, outputSpace))];
        Assert.Equal(PostbuildCtrlRamSources.Length * Math.Max(1, publications.Length), payloadRanges.Length);
        foreach (JsonElement replacement in replacements)
        {
            string id = replacement.GetProperty("OperationId").GetString()!;
            JsonElement controlReplacement = Assert.Single(control.Report.GetProperty("Operations").EnumerateArray(),
                operation => operation.GetProperty("OperationId").GetString() == id);
            Assert.Equal(controlReplacement.GetProperty("TargetSpaceId").GetString(), replacement.GetProperty("TargetSpaceId").GetString());
            Assert.Equal(CliGoldenByteEvidence.Range(controlReplacement.GetProperty("TargetRange")),
                CliGoldenByteEvidence.Range(replacement.GetProperty("TargetRange")));
            Assert.Equal("Succeeded", replacement.GetProperty("Status").GetString());
        }

        Assert.All(payloadRanges, range => CliGoldenByteEvidence.EqualRange(control.Bytes, edited.Bytes, range));
        var allowedWrites = new List<ByteRange>();
        var versionWrites = new List<ByteRange>();
        foreach (JsonElement operation in operations)
        {
            string id = operation.GetProperty("OperationId").GetString()!;
            ByteRange[] writes = id.Contains("patch-fw-", StringComparison.Ordinal)
                ? [CliGoldenByteEvidence.Range(operation.GetProperty("TargetRange"))]
                : [.. operation.GetProperty("ProcessorAllowedWriteRanges").EnumerateArray().Select(CliGoldenByteEvidence.Range)];
            foreach (ByteRange range in writes)
            {
                Assert.Equal("Succeeded", operation.GetProperty("Status").GetString());
                ByteRange outputRange = ProjectOperationRange(operation, range, publications, outputSpace);
                Assert.InRange(outputRange.EndExclusive, 1, edited.Bytes.LongLength);
                if (id.Contains("patch-fw-", StringComparison.Ordinal))
                {
                    Assert.DoesNotContain(payloadRanges, payload => payload.Overlaps(outputRange));
                    AssertRequestedVersionPatch(edited.Bytes, id, outputRange, draft);
                    allowedWrites.Add(outputRange);
                    versionWrites.Add(outputRange);
                }
                else
                {
                    // Processor authority can include payload publications; version edits must leave them unchanged.
                    allowedWrites.AddRange(outputRange.Subtract(payloadRanges));
                }
            }
        }

        Assert.NotEmpty(allowedWrites);
        var differences = new List<long>();
        for (int offset = 0; offset < edited.Bytes.Length; offset++)
        {
            if (control.Bytes[offset] == edited.Bytes[offset])
            {
                continue;
            }

            if (!allowedWrites.Any(range => range.Contains(offset)))
            {
                Assert.Fail($"Version option changed output-image offset 0x{offset:X} outside version patches and non-payload processor writes.");
            }

            differences.Add(offset);
        }

        Assert.NotEmpty(differences);
        Assert.All(versionWrites, range => Assert.Contains(differences, range.Contains));
    }

    private static void AssertRequestedVersionPatch(
        byte[] output, string operationId, ByteRange range, CtrlRamAuthoringDraftState draft)
    {
        CtrlRamFirmwareVersionDraftState? expected = draft switch
        {
            CtrlRamFirmwareVersionDraftState standard => standard,
            AbCtrlRamDraftState ab when operationId.StartsWith("a-bank/", StringComparison.Ordinal) => ab.AVersion,
            AbCtrlRamDraftState ab when operationId.StartsWith("b-bank/", StringComparison.Ordinal) => ab.BVersion,
            _ => null,
        };
        Assert.NotNull(expected);
        bool versionAndBar = operationId.EndsWith("patch-fw-version-and-bar", StringComparison.Ordinal);
        Assert.True(versionAndBar || operationId.EndsWith("patch-fw-sub-version", StringComparison.Ordinal));
        Assert.Equal(versionAndBar ? sizeof(ushort) : sizeof(byte), range.Length);
        long structureStart = checked(range.Start - (versionAndBar
            ? FirmwareConfigLayout.FirmwareVersionOffset : FirmwareConfigLayout.FirmwareSubVersionOffset));
        Assert.True(FirmwareConfigMetadataReader.TryReadAtAbsoluteAddress(output, structureStart, out FirmwareConfigMetadata metadata));
        Assert.Equal(expected.FirmwareVersion, metadata.FirmwareVersion);
        Assert.True(metadata.IsFirmwareVersionBarValid);
        Assert.Equal(expected.FirmwareSubVersion, metadata.FirmwareSubVersion);
    }

    private static ByteRange ProjectOperationRange(
        JsonElement operation, ByteRange range, JsonElement[] publications, string outputSpace)
    {
        string space = operation.GetProperty("TargetSpaceId").GetString()!;
        return space == outputSpace ? range : ProjectPublishedRange(
            Assert.Single(publications, publication => publication.GetProperty("SourceSpaceId").GetString() == space), range);
    }

    private static ByteRange ProjectPublishedRange(JsonElement publish, ByteRange workspaceRange)
    {
        ByteRange sourceRange = CliGoldenByteEvidence.Range(publish.GetProperty("SourceRange"));
        ByteRange targetRange = CliGoldenByteEvidence.Range(publish.GetProperty("TargetRange"));
        Assert.Equal(sourceRange.Length, targetRange.Length);
        Assert.True(sourceRange.Contains(workspaceRange));
        return new(checked(targetRange.Start + workspaceRange.Start - sourceRange.Start), workspaceRange.Length);
    }

    private static void AssertAcceptedOutputName(
        string basePath, IReadOnlyDictionary<string, string> sourcePaths, CtrlRamAuthoringDraftState draft, JsonElement report)
    {
        CompositionHostServices host = BootstrapTestHost.ProductServices;
        var paths = new Dictionary<string, string>(sourcePaths, StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = basePath,
        };
        Dictionary<string, byte[]> bytes = paths.ToDictionary(static pair => pair.Key,
            static pair => File.ReadAllBytes(pair.Value), StringComparer.Ordinal);
        var session = new AuthoringSessionState(ExperienceIds.CtrlRamReplace);
        CtrlRamAuthoringSessionPreparation prepared = host.CtrlRamAuthoring.PrepareSession(
            session, "NT51929", "single", paths, bytes);
        Assert.True(prepared.Succeeded, string.Join("; ", prepared.Issues.Select(static issue => issue.Message)));
        CtrlRamAuthoringTransitionResult transitioned = host.CtrlRamAuthoring.TransitionFirmwareVersionCompilation(
            session, "NT51929", "single", paths, draft);
        Assert.True(transitioned.Succeeded, string.Join("; ", transitioned.Issues.Select(static issue => issue.Message)));
        ActiveSessionSnapshot accepted = transitioned.Session!;
        Assert.Equal(report.GetProperty("CompilationFingerprint").GetString(), accepted.CompilationFingerprint);
        JsonElement naming = report.GetProperty("OutputNaming");
        DateTimeOffset resolvedAtUtc = naming.ValueKind == JsonValueKind.Null
            ? report.GetProperty("StartedAtUtc").GetDateTimeOffset()
            : naming.GetProperty("ResolvedAtUtc").GetDateTimeOffset();
        CompositionOutputNamePreview expected = AcceptedSessionOutputNameResolver.Resolve(
            accepted, accepted.ExactCapability!, resolvedAtUtc).OutputName;

        Assert.Equal(expected.FileName, report.GetProperty("Output").GetProperty("FileName").GetString());
        if (expected.OutputNaming is not { } expectedNaming)
        {
            Assert.Equal(JsonValueKind.Null, naming.ValueKind);
            return;
        }

        Assert.Equal(JsonValueKind.Object, naming.ValueKind);
        Assert.Equal(expectedNaming.AutomaticFileName, naming.GetProperty("AutomaticFileName").GetString());
        Assert.Equal(expectedNaming.IsExplicitOverride, naming.GetProperty("IsExplicitOverride").GetBoolean());
        Assert.Equal(expectedNaming.Tokens.Select(static token => (token.TokenId, token.Value)),
            naming.GetProperty("Tokens").EnumerateArray().Select(static token =>
                (token.GetProperty("TokenId").GetString()!, token.GetProperty("Value").GetString()!)));
    }
}
