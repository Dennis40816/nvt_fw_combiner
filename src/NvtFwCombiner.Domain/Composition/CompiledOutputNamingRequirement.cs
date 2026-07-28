namespace NvtFwCombiner.Domain.Composition;

/// <summary>Closed invalid-character policy for an unrendered v2 output-name template.</summary>
public enum CompiledOutputInvalidCharacterPolicy
{
    /// <inheritdoc/>
    Reject,
    /// <inheritdoc/>
    ReplaceUnderscore,
}

/// <summary>Closed renderer selected from the profile-owned output-name contract.</summary>
public enum CompiledOutputNameRendererKind
{
    /// <summary>A literal, token-free profile filename.</summary>
    Static,
    /// <summary>A token template retained for a future renderer and not runtime executable.</summary>
    DeferredTokenTemplate,
    /// <summary>The fixed, evidence-backed AB Code filename contract.</summary>
    AbCodeV1,
    /// <summary>The canonical normal FlashCode filename contract.</summary>
    NormalFlashCodeV1,
    /// <summary>The canonical TP-firmware filename contract.</summary>
    TpFirmwareV1,
}

/// <summary>Closed behavior when one compiled output-name token has no accepted value.</summary>
public enum CompiledOutputTokenMissingPolicy
{
    /// <summary>The output name cannot be resolved without this token.</summary>
    Block,
    /// <summary>The compiled contract supplies one exact literal placeholder.</summary>
    UsePlaceholder,
}

/// <summary>One compiled token reference and its explicit missing-value behavior.</summary>
public sealed record CompiledOutputTokenRequirement
{
    internal CompiledOutputTokenRequirement(
        string tokenId,
        CompiledOutputTokenMissingPolicy missingPolicy,
        string? placeholder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);
        if (!Enum.IsDefined(missingPolicy))
        {
            throw new ArgumentOutOfRangeException(
                nameof(missingPolicy),
                missingPolicy,
                "Unknown output token missing policy.");
        }

        if (missingPolicy == CompiledOutputTokenMissingPolicy.UsePlaceholder)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(placeholder);
        }
        else if (placeholder is not null)
        {
            throw new ArgumentException(
                "Blocking output-name tokens cannot declare a placeholder.",
                nameof(placeholder));
        }

        TokenId = tokenId;
        MissingPolicy = missingPolicy;
        Placeholder = placeholder;
    }

    /// <summary>Canonical token identifier from the compiled template.</summary>
    public string TokenId { get; }

    /// <summary>Explicit behavior when accepted inspection has no value.</summary>
    public CompiledOutputTokenMissingPolicy MissingPolicy { get; }

    /// <summary>Exact literal used only by <see cref="CompiledOutputTokenMissingPolicy.UsePlaceholder"/>.</summary>
    public string? Placeholder { get; }
}

/// <summary>Profile-owned output naming requirements retained for runtime rendering.</summary>
public sealed class CompiledOutputNamingRequirement
{
    /// <summary>Canonical template for the AB Code v1 execution-path renderer.</summary>
    public const string AbCodeV1Template = "NT{ic}_FlashCode_A_{dp-a}{tp-a}_B_{dp-b}{tp-b}_{date}.bin";

    /// <summary>Canonical normal FlashCode name rendered from DPCMI and FirmwareConfig facts.</summary>
    public const string NormalFlashCodeV1Template =
        "{ic}_FlashCode_D{dp-version}T{tp-version}_{date}.bin";

    /// <summary>Canonical TP-firmware name rendered from FirmwareConfig facts.</summary>
    public const string TpFirmwareV1Template =
        "{ic}_TPFW_T{tp-version}_{date}.bin";

    private static readonly string[] s_abCodeV1TokenIds = ["date", "dp-a", "dp-b", "ic", "tp-a", "tp-b"];
    private static readonly string[] s_normalFlashCodeV1TokenIds = ["date", "dp-version", "ic", "tp-version"];
    private static readonly string[] s_tpFirmwareV1TokenIds = ["date", "ic", "tp-version"];
    private static readonly System.Buffers.SearchValues<char> s_windowsInvalidFileNameCharacters = System.Buffers.SearchValues.Create("<>\"|?*");
    private static readonly char[] WindowsInvalidFileNameCharacters = ['<', '>', '"', '|', '?', '*'];

    private readonly string[] _requiredTokenIds;
    private readonly CompiledOutputTokenRequirement[] _tokenRequirements;

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

        _requiredTokenIds = ImmutableStringSnapshot.Create(
            requiredTokenIds,
            nameof(requiredTokenIds),
            null,
            "Identifiers must be non-empty values.",
            "Identifiers must be ordinally unique.");
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
        RendererKind = ResolveRendererKind(
            fileNameTemplate,
            invalidCharacterPolicy,
            _requiredTokenIds);
        _tokenRequirements = CreateTokenRequirements(
            RendererKind,
            _requiredTokenIds);
        TokenRequirements = Array.AsReadOnly(_tokenRequirements);
    }

    /// <summary>Unrendered profile filename template.</summary>
    public string FileNameTemplate { get; }

    /// <summary>Whether a later runtime may accept an owner-approved filename override.</summary>
    public bool AllowOverride { get; }

    /// <summary>Closed policy for invalid rendered filename characters.</summary>
    public CompiledOutputInvalidCharacterPolicy InvalidCharacterPolicy { get; }

    /// <summary>Profile tokens required before a future runtime may render this template.</summary>
    public IReadOnlyList<string> RequiredTokenIds { get; }

    /// <summary>Typed rendering behavior admitted by the compiled output contract.</summary>
    public CompiledOutputNameRendererKind RendererKind { get; }

    /// <summary>Token references and explicit missing-value behavior in canonical token-id order.</summary>
    public IReadOnlyList<CompiledOutputTokenRequirement> TokenRequirements { get; }

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

    /// <summary>Validates the canonical IC identity required by compiled dynamic renderers.</summary>
    public static void ValidateCanonicalIcIdentity(
        string icId,
        string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icId, parameterName);
        const string Prefix = "NT";
        if (icId.Length != Prefix.Length + 5 ||
            !icId.StartsWith(Prefix, StringComparison.Ordinal) ||
            icId.AsSpan(Prefix.Length).IndexOfAnyExceptInRange('0', '9') >= 0)
        {
            throw new ArgumentException(
                "Compiled output naming requires a canonical NTxxxxx IC identity.",
                parameterName);
        }
    }

    private static CompiledOutputNameRendererKind ResolveRendererKind(
        string fileNameTemplate,
        CompiledOutputInvalidCharacterPolicy invalidCharacterPolicy,
        string[] requiredTokenIds)
    {
        return invalidCharacterPolicy switch
        {
            CompiledOutputInvalidCharacterPolicy.Reject
                when IsContract(fileNameTemplate, requiredTokenIds, AbCodeV1Template, s_abCodeV1TokenIds) =>
                    CompiledOutputNameRendererKind.AbCodeV1,
            CompiledOutputInvalidCharacterPolicy.Reject
                when IsContract(
                    fileNameTemplate,
                    requiredTokenIds,
                    NormalFlashCodeV1Template,
                    s_normalFlashCodeV1TokenIds) =>
                    CompiledOutputNameRendererKind.NormalFlashCodeV1,
            CompiledOutputInvalidCharacterPolicy.Reject
                when IsContract(
                    fileNameTemplate,
                    requiredTokenIds,
                    TpFirmwareV1Template,
                    s_tpFirmwareV1TokenIds) =>
                    CompiledOutputNameRendererKind.TpFirmwareV1,
            CompiledOutputInvalidCharacterPolicy.Reject or
                CompiledOutputInvalidCharacterPolicy.ReplaceUnderscore =>
                    requiredTokenIds.Length == 0
                        ? CompiledOutputNameRendererKind.Static
                        : CompiledOutputNameRendererKind.DeferredTokenTemplate,
            _ => throw new ArgumentOutOfRangeException(
                nameof(invalidCharacterPolicy),
                invalidCharacterPolicy,
                "Unknown output invalid-character policy."),
        };
    }

    private static bool IsContract(
        string actualTemplate,
        string[] actualTokenIds,
        string expectedTemplate,
        string[] expectedTokenIds)
    {
        return string.Equals(actualTemplate, expectedTemplate, StringComparison.Ordinal) &&
               actualTokenIds.SequenceEqual(expectedTokenIds, StringComparer.Ordinal);
    }

    private static CompiledOutputTokenRequirement[] CreateTokenRequirements(
        CompiledOutputNameRendererKind rendererKind,
        IEnumerable<string> requiredTokenIds)
    {
        return
        [
            .. requiredTokenIds.Select(tokenId =>
            {
                string? placeholder = rendererKind switch
                {
                    CompiledOutputNameRendererKind.AbCodeV1 => tokenId switch
                    {
                        "dp-a" or "dp-b" => "Dxxxx",
                        "tp-a" or "tp-b" => "Txxxx",
                        _ => null,
                    },
                    CompiledOutputNameRendererKind.NormalFlashCodeV1 =>
                        tokenId is "dp-version" or "tp-version" ? "xxxx" : null,
                    CompiledOutputNameRendererKind.TpFirmwareV1 =>
                        tokenId == "tp-version" ? "xxxx" : null,
                    CompiledOutputNameRendererKind.Static or
                        CompiledOutputNameRendererKind.DeferredTokenTemplate => null,
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(rendererKind),
                        rendererKind,
                        "Unknown output renderer kind."),
                };
                return new CompiledOutputTokenRequirement(
                    tokenId,
                    placeholder is null
                        ? CompiledOutputTokenMissingPolicy.Block
                        : CompiledOutputTokenMissingPolicy.UsePlaceholder,
                    placeholder);
            }),
        ];
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
