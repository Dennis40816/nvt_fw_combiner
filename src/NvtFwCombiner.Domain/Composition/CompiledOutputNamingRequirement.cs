namespace NvtFwCombiner.Domain.Composition;

/// <summary>Closed invalid-character policy for an unrendered v2 output-name template.</summary>
public enum CompiledOutputInvalidCharacterPolicy
{
    /// <inheritdoc/>
    Reject,
    /// <inheritdoc/>
    ReplaceUnderscore,
}

/// <summary>Profile-owned output naming requirements retained before runtime token rendering exists.</summary>
public sealed class CompiledOutputNamingRequirement
{
    private static readonly System.Buffers.SearchValues<char> s_windowsInvalidFileNameCharacters = System.Buffers.SearchValues.Create("<>\"|?*");
    private static readonly char[] WindowsInvalidFileNameCharacters = ['<', '>', '"', '|', '?', '*'];

    private readonly string[] _requiredTokenIds;

    internal CompiledOutputNamingRequirement(
        string fileNameTemplate,
        bool allowOverride,
        CompiledOutputInvalidCharacterPolicy invalidCharacterPolicy,
        IEnumerable<string> requiredTokenIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileNameTemplate);
        if (fileNameTemplate.IndexOfAny(['/', '\\', ':']) >= 0 ||
            fileNameTemplate is "." or ".." ||
            fileNameTemplate.Any(char.IsControl))
        {
            throw new ArgumentException("Output file-name templates must not contain path or control syntax.", nameof(fileNameTemplate));
        }

        if (!Enum.IsDefined(invalidCharacterPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(invalidCharacterPolicy),
                invalidCharacterPolicy,
                "Unknown output invalid-character policy.");
        }

        _requiredTokenIds = CompiledProfilePromotionBlocker.SnapshotIds(
            requiredTokenIds,
            nameof(requiredTokenIds),
            requireValue: false);
        foreach (string tokenId in _requiredTokenIds)
        {
            ValidateTokenId(tokenId, nameof(requiredTokenIds));
        }

        string[] templateTokenIds = ExtractTokenIds(fileNameTemplate);
        if (!templateTokenIds.SequenceEqual(_requiredTokenIds, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Output template tokens must exactly match required token ids.",
                nameof(requiredTokenIds));
        }

        ValidateWindowsTemplateSafety(fileNameTemplate, invalidCharacterPolicy, nameof(fileNameTemplate));

        FileNameTemplate = fileNameTemplate;
        AllowOverride = allowOverride;
        InvalidCharacterPolicy = invalidCharacterPolicy;
        RequiredTokenIds = Array.AsReadOnly(_requiredTokenIds);
    }

    /// <summary>Unrendered profile filename template.</summary>
    public string FileNameTemplate { get; }

    /// <summary>Whether a later runtime may accept an owner-approved filename override.</summary>
    public bool AllowOverride { get; }

    /// <summary>Closed policy for invalid rendered filename characters.</summary>
    public CompiledOutputInvalidCharacterPolicy InvalidCharacterPolicy { get; }

    /// <summary>Profile tokens required before a future runtime may render this template.</summary>
    public IReadOnlyList<string> RequiredTokenIds { get; }

    /// <summary>Validates a literal runtime output filename under the closed Windows-safe V2 policy.</summary>
    public static void ValidateRuntimeLiteralFileName(string fileName, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName, parameterName);
        if (fileName.IndexOfAny(['/', '\\', ':']) >= 0 ||
            fileName is "." or ".." ||
            fileName.Any(char.IsControl))
        {
            throw new ArgumentException(
                "Runtime output file names must not contain path or control syntax.",
                parameterName);
        }

        ValidateWindowsTemplateSafety(
            fileName,
            CompiledOutputInvalidCharacterPolicy.Reject,
            parameterName);
    }

    private static string[] ExtractTokenIds(string template)
    {
        var tokenIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < template.Length; index++)
        {
            char current = template[index];
            if (current == '}')
            {
                throw new ArgumentException("Output templates cannot contain an unmatched closing token brace.", nameof(template));
            }

            if (current != '{')
            {
                continue;
            }

            int close = template.IndexOf('}', index + 1);
            if (close < 0 || close == index + 1)
            {
                throw new ArgumentException("Output templates require non-empty closed token braces.", nameof(template));
            }

            string tokenId = template[(index + 1)..close];
            if (tokenId.Any(char.IsWhiteSpace) || tokenId.IndexOfAny(['{', '}']) >= 0)
            {
                throw new ArgumentException("Output template token ids cannot contain whitespace or braces.", nameof(template));
            }

            ValidateTokenId(tokenId, nameof(template));
            _ = tokenIds.Add(tokenId);
            index = close;
        }

        return [.. tokenIds.Order(StringComparer.Ordinal)];
    }

    private static void ValidateTokenId(string tokenId, string parameterName)
    {
        if (tokenId.Length == 0 || tokenId[0] is < 'a' or > 'z' || tokenId[^1] == '-')
        {
            throw new ArgumentException("Output template token ids must use canonical profile ids.", parameterName);
        }

        bool previousHyphen = false;
        foreach (char character in tokenId[1..])
        {
            bool isLowercaseLetter = character is >= 'a' and <= 'z';
            bool isDigit = character is >= '0' and <= '9';
            if (!isLowercaseLetter && !isDigit && character != '-')
            {
                throw new ArgumentException("Output template token ids must use canonical profile ids.", parameterName);
            }

            if (character == '-' && previousHyphen)
            {
                throw new ArgumentException("Output template token ids must use canonical profile ids.", parameterName);
            }

            previousHyphen = character == '-';
        }
    }

    private static void ValidateWindowsTemplateSafety(
        string template,
        CompiledOutputInvalidCharacterPolicy invalidCharacterPolicy,
        string parameterName)
    {
        string candidate = ReplaceTokenOccurrences(template);
        if (invalidCharacterPolicy == CompiledOutputInvalidCharacterPolicy.ReplaceUnderscore)
        {
            candidate = string.Create(candidate.Length, candidate, static (destination, source) =>
            {
                for (int index = 0; index < source.Length; index++)
                {
                    destination[index] = Array.IndexOf(WindowsInvalidFileNameCharacters, source[index]) >= 0
                        ? '_'
                        : source[index];
                }
            });
        }
        else if (candidate.AsSpan().IndexOfAny(s_windowsInvalidFileNameCharacters) >= 0)
        {
            throw new ArgumentException(
                "Reject output policy cannot declare invalid filename characters.",
                parameterName);
        }

        if (candidate.Length == 0 || candidate is "." or ".." ||
            candidate.EndsWith(' ') || candidate.EndsWith('.') || IsWindowsReservedDeviceName(candidate))
        {
            throw new ArgumentException(
                "Output templates must be safe Windows file names after token rendering.",
                parameterName);
        }
    }

    private static string ReplaceTokenOccurrences(string template)
    {
        var builder = new System.Text.StringBuilder(template.Length);
        for (int index = 0; index < template.Length; index++)
        {
            if (template[index] != '{')
            {
                _ = builder.Append(template[index]);
                continue;
            }

            int close = template.IndexOf('}', index + 1);
            _ = builder.Append('x');
            index = close;
        }

        return builder.ToString();
    }

    private static bool IsWindowsReservedDeviceName(string fileName)
    {
        string stem = fileName.Split('.', 2, StringSplitOptions.None)[0];
        return stem.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            (stem.Length == 4 &&
             (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
              stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
             IsWindowsDeviceNumber(stem[3]));
    }

    private static bool IsWindowsDeviceNumber(char value)
    {
        return value is (>= '1' and <= '9') or '\u00B9' or '\u00B2' or '\u00B3';
    }
}
