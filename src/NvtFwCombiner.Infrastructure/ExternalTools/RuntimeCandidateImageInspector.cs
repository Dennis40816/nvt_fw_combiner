using System.Collections.Immutable;
using System.Reflection.PortableExecutable;

namespace NvtFwCombiner.Infrastructure.ExternalTools;

internal sealed record RuntimeCandidateImageFacts(Machine Machine, bool IsDll, bool HasClrHeader, PEMagic Magic);

internal sealed record RuntimeCandidateImageInspection(RuntimeCandidateImageFacts? Facts)
{
    internal bool IsMalformed => Facts is null;
}

/// <summary>Observes immutable PE headers without loading code or granting runtime trust.</summary>
internal static class RuntimeCandidateImageInspector
{
    internal static RuntimeCandidateImageInspection Inspect(ImmutableArray<byte> snapshot)
    {
        if (snapshot.IsDefaultOrEmpty) { return new(null); }
        try
        {
            using PEReader reader = new(snapshot);
            PEHeaders headers = reader.PEHeaders;
            return headers.PEHeader is not { } optional ? new(null) : new(new(
                headers.CoffHeader.Machine,
                (headers.CoffHeader.Characteristics & Characteristics.Dll) != 0,
                headers.CorHeader is not null,
                optional.Magic));
        }
        catch (BadImageFormatException)
        {
            return new(null);
        }
    }
}
