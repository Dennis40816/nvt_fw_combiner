using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private const string GeneralReplaceInputAddressSpaceId = "replacement-input";
    private const string GeneralReplaceOperationId = "replace-general";
    private const int GeneralReplaceOperationSequence = 100;

    private static bool TryCompileProfile(
        CompositionProfileDefinition profile,
        ParsedOptions options,
        TextWriter error,
        out ProfileCompileResult compile)
    {
        if (profile.ExperienceId != IcWorkflowIds.GeneralReplace)
        {
            compile = CompositionProfileCompiler.Compile(profile, []);
            return true;
        }

        if (!TryCreateGeneralMapping(options, error, out ExplicitMapping? mapping, out AddressSpace? requestSpace))
        {
            compile = ProfileCompileResult.Failed([]);
            return false;
        }

        compile = CompositionProfileCompiler.Compile(profile, [mapping], [requestSpace]);
        return true;
    }

    private static bool TryCreateGeneralMapping(
        ParsedOptions options,
        TextWriter error,
        [NotNullWhen(true)] out ExplicitMapping? mapping,
        [NotNullWhen(true)] out AddressSpace? requestSpace)
    {
        mapping = null;
        requestSpace = null;
        if (!RequireOption(options, "--input", error, out string? inputPath) ||
            !RequireLong(options, "--source-start", error, out long sourceStart) ||
            !RequireLong(options, "--target-start", error, out long targetStart) ||
            !RequireLong(options, "--length", error, out long length))
        {
            return false;
        }

        if (length <= 0)
        {
            error.WriteLine("error: --length must be positive");
            return false;
        }

        string fullPath = Path.GetFullPath(inputPath);
        long declaredLength = File.Exists(fullPath)
            ? new FileInfo(fullPath).Length
            : checked(sourceStart + length);
        requestSpace = new AddressSpace(GeneralReplaceInputAddressSpaceId, declaredLength, AddressSpaceMutability.Immutable);
        mapping = new ExplicitMapping(
            GeneralReplaceOperationId,
            GeneralReplaceOperationSequence,
            ExplicitMappingOperationKind.ReplaceRange,
            GeneralReplaceInputAddressSpaceId,
            new ByteRange(sourceStart, length),
            CompositionAddressSpaceIds.OutputImage,
            new ByteRange(targetStart, length),
            OverlapPolicy.Reject,
            alignment: 1,
            "Replace synthetic explicit range.",
            targetRegionId: null);
        return true;
    }
}
