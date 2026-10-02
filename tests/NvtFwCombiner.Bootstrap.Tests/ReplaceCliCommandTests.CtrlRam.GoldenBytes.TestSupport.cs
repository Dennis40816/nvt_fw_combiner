using System.Text.Json;
using NvtFwCombiner.Application.Authoring;
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
        CliGoldenByteEvidence.BuildEvidence control, CliGoldenByteEvidence.BuildEvidence edited, int versionPairCount)
    {
        Assert.Equal(control.Bytes.Length, edited.Bytes.Length);
        JsonElement[] operations = [.. edited.Report.GetProperty("Operations").EnumerateArray()];
        JsonElement[] patches = [.. operations.Where(static operation =>
            operation.GetProperty("OperationId").GetString()!.Contains("patch-fw-", StringComparison.Ordinal))];
        Assert.Equal(versionPairCount * 2, patches.Length);
        JsonElement[] publications = [.. operations.Where(static operation =>
            operation.GetProperty("OperationId").GetString()!.EndsWith("/publish", StringComparison.Ordinal))];
        string outputSpace = publications.Length == 0
            ? patches[0].GetProperty("TargetSpaceId").GetString()!
            : publications[0].GetProperty("TargetSpaceId").GetString()!;
        var allowedWrites = new List<ByteRange>();
        var versionWrites = new List<ByteRange>();
        foreach (JsonElement operation in operations)
        {
            string id = operation.GetProperty("OperationId").GetString()!;
            string space = operation.GetProperty("TargetSpaceId").GetString()!;
            ByteRange[] writes = id.Contains("patch-fw-", StringComparison.Ordinal)
                ? [CliGoldenByteEvidence.Range(operation.GetProperty("TargetRange"))]
                : [.. operation.GetProperty("ProcessorAllowedWriteRanges").EnumerateArray().Select(CliGoldenByteEvidence.Range)];
            foreach (ByteRange range in writes)
            {
                Assert.Equal("Succeeded", operation.GetProperty("Status").GetString());
                ByteRange outputRange = space == outputSpace ? range : ProjectPublishedRange(
                    Assert.Single(publications, publication => publication.GetProperty("SourceSpaceId").GetString() == space), range);
                Assert.InRange(outputRange.EndExclusive, 1, edited.Bytes.LongLength);
                allowedWrites.Add(outputRange);
                if (id.Contains("patch-fw-", StringComparison.Ordinal))
                {
                    versionWrites.Add(outputRange);
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
                Assert.Fail($"Version option changed output-image offset 0x{offset:X} outside report-declared writes.");
            }

            differences.Add(offset);
        }

        Assert.NotEmpty(differences);
        Assert.All(versionWrites, range => Assert.Contains(differences, range.Contains));
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
        string basePath, string sourcePath, CtrlRamAuthoringDraftState draft, JsonElement report)
    {
        CompositionHostServices host = BootstrapTestHost.ProductServices;
        var paths = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = basePath,
            ["replace-ctrlram-nf"] = sourcePath,
        };
        var bytes = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [CompositionSlotIds.ReplaceBase] = File.ReadAllBytes(basePath),
            ["replace-ctrlram-nf"] = File.ReadAllBytes(sourcePath),
        };
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
