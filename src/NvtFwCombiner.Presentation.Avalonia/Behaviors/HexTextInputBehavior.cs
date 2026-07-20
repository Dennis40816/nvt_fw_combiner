using Avalonia;
using Avalonia.Controls;

namespace NvtFwCombiner.Presentation.Avalonia.Behaviors;

/// <summary>Canonical hexadecimal text formats used by technical editor inputs.</summary>
public enum HexTextInputMode
{
    /// <summary>No hexadecimal normalization is applied.</summary>
    None,

    /// <summary>Canonical address with a lowercase 0x prefix and uppercase digits.</summary>
    Address,

    /// <summary>One uppercase two-digit byte without a prefix.</summary>
    Byte,

    /// <summary>Uppercase byte text that retains Excel-friendly whitespace and comma separators.</summary>
    ByteSequence,
}

/// <summary>
/// Normalizes hexadecimal TextBox input at one shared presentation boundary. Firmware parsing and
/// byte execution remain owned by the application layer.
/// </summary>
public sealed class HexTextInputBehavior : AvaloniaObject
{
    /// <summary>Identifies the canonical hexadecimal format applied to a TextBox.</summary>
    public static readonly AttachedProperty<HexTextInputMode> ModeProperty =
        AvaloniaProperty.RegisterAttached<HexTextInputBehavior, TextBox, HexTextInputMode>("Mode");

    private static readonly AttachedProperty<bool> IsNormalizingProperty =
        AvaloniaProperty.RegisterAttached<HexTextInputBehavior, TextBox, bool>("IsNormalizing");

    static HexTextInputBehavior()
    {
        _ = ModeProperty.Changed.AddClassHandler<TextBox>(OnModeChanged);
    }

    private HexTextInputBehavior()
    {
    }

    /// <summary>Gets the hexadecimal input mode attached to a TextBox.</summary>
    public static HexTextInputMode GetMode(AvaloniaObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return element.GetValue(ModeProperty);
    }

    /// <summary>Sets the hexadecimal input mode attached to a TextBox.</summary>
    public static void SetMode(AvaloniaObject element, HexTextInputMode value)
    {
        ArgumentNullException.ThrowIfNull(element);
        _ = element.SetValue(ModeProperty, value);
    }

    private static void OnModeChanged(TextBox textBox, AvaloniaPropertyChangedEventArgs e)
    {
        textBox.TextChanged -= TextBox_OnTextChanged;
        if (e.NewValue is HexTextInputMode mode && mode != HexTextInputMode.None)
        {
            textBox.TextChanged += TextBox_OnTextChanged;
            Normalize(textBox, mode);
        }
    }

    private static void TextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && !textBox.GetValue(IsNormalizingProperty))
        {
            Normalize(textBox, GetMode(textBox));
        }
    }

    private static void Normalize(TextBox textBox, HexTextInputMode mode)
    {
        string source = textBox.Text ?? string.Empty;
        int sourceCaret = Math.Clamp(textBox.CaretIndex, 0, source.Length);
        (string text, int caret) normalized = (source, sourceCaret);
        if (mode == HexTextInputMode.Address)
        {
            normalized = NormalizeAddress(source, sourceCaret);
        }
        else if (mode == HexTextInputMode.Byte)
        {
            normalized = NormalizeByteText(source, sourceCaret, maximumDigits: 2);
        }
        else if (mode == HexTextInputMode.ByteSequence)
        {
            normalized = NormalizeByteSequence(source, sourceCaret);
        }

        (string text, int caret) = normalized;
        if (string.Equals(source, text, StringComparison.Ordinal) && sourceCaret == caret)
        {
            return;
        }

        _ = textBox.SetValue(IsNormalizingProperty, true);
        try
        {
            textBox.Text = text;
            textBox.CaretIndex = Math.Clamp(caret, 0, text.Length);
        }
        finally
        {
            _ = textBox.SetValue(IsNormalizingProperty, false);
        }
    }

    private static (string Text, int Caret) NormalizeAddress(string source, int caret)
    {
        int bodyStart = source.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? 2 : 0;
        string body = NormalizeHexDigits(source.AsSpan(bodyStart), maximumDigits: 16);
        int bodyCaret = Math.Max(0, caret - bodyStart);
        int digitsBeforeCaret = CountHexDigits(source.AsSpan(bodyStart, Math.Min(bodyCaret, source.Length - bodyStart)));
        return ($"0x{body}", 2 + Math.Min(digitsBeforeCaret, body.Length));
    }

    private static (string Text, int Caret) NormalizeByteText(string source, int caret, int maximumDigits)
    {
        string text = NormalizeHexDigits(source.AsSpan(), maximumDigits);
        int digitsBeforeCaret = CountHexDigits(source.AsSpan(0, caret));
        return (text, Math.Min(digitsBeforeCaret, text.Length));
    }

    private static (string Text, int Caret) NormalizeByteSequence(string source, int caret)
    {
        string text = NormalizeSequence(source.AsSpan());
        int normalizedCaret = NormalizeSequence(source.AsSpan(0, caret)).Length;
        return (text, Math.Min(normalizedCaret, text.Length));
    }

    private static string NormalizeHexDigits(ReadOnlySpan<char> source, int maximumDigits)
    {
        return new string([
            .. source
                .ToArray()
                .Where(Uri.IsHexDigit)
                .Take(maximumDigits)
                .Select(char.ToUpperInvariant),
        ]);
    }

    private static string NormalizeSequence(ReadOnlySpan<char> source)
    {
        return new string([
            .. source
                .ToArray()
                .Where(character => Uri.IsHexDigit(character) || char.IsWhiteSpace(character) || character == ',')
                .Select(character => Uri.IsHexDigit(character) ? char.ToUpperInvariant(character) : character),
        ]);
    }

    private static int CountHexDigits(ReadOnlySpan<char> source)
    {
        int count = 0;
        foreach (char character in source)
        {
            if (Uri.IsHexDigit(character))
            {
                count++;
            }
        }

        return count;
    }
}
