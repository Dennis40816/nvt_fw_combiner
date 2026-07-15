using NvtFwCombiner.Contracts.Profiles;

namespace NvtFwCombiner.Profiles.V2;

internal static partial class CompositionProfileNormalizer
{
    internal static CompositionProfileOutput NormalizeOutput(
        CompositionProfileOutputDocument document,
        string path = "output")
    {
        ArgumentNullException.ThrowIfNull(document);
        return Wrap(path, () => new CompositionProfileOutput(
            document.FileNameTemplate,
            document.AllowOverride,
            NormalizeInvalidCharacterPolicy(
                document.InvalidCharacterPolicy,
                $"{path}.invalidCharacterPolicy"),
            RequireList(document.RequiredTokenIds, $"{path}.requiredTokenIds")));
    }

    private static CompositionProfileInvalidCharacterPolicy NormalizeInvalidCharacterPolicy(
        string value,
        string path)
    {
        return value switch
        {
            "reject" => CompositionProfileInvalidCharacterPolicy.Reject,
            "replace-underscore" => CompositionProfileInvalidCharacterPolicy.ReplaceUnderscore,
            _ => throw Error(path, "Unknown output invalid-character policy."),
        };
    }
}
