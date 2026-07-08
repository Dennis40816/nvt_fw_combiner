using System.Diagnostics.CodeAnalysis;
using NvtFwCombiner.Domain.Composition;
using NvtFwCombiner.Profiles;

namespace NvtFwCombiner.Bootstrap;

internal static partial class ReplaceCliCommandHandler
{
    private static bool TryCompileProfile(
        CompositionProfileDefinition profile,
        ParsedOptions options,
        TextWriter error,
        out ProfileCompileResult compile)
    {
        if (profile.ExperienceId != "general-replace")
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
        requestSpace = new AddressSpace("replacement-input", declaredLength, AddressSpaceMutability.Immutable);
        mapping = new ExplicitMapping(
            "replace-general",
            100,
            ExplicitMappingOperationKind.ReplaceRange,
            "replacement-input",
            new ByteRange(sourceStart, length),
            "output-image",
            new ByteRange(targetStart, length),
            OverlapPolicy.Reject,
            alignment: 1,
            "Replace synthetic explicit range.",
            targetRegionId: null);
        return true;
    }
}
