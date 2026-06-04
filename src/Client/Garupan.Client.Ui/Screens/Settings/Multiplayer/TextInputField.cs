using System;

namespace Garupan.Client.Ui.Screens.Settings.Multiplayer;

/// <summary>
/// Immutable single-line text input state — value + cursor position, with character-class
/// filtering. The screen owns one instance per visible field and replaces it on every
/// edit (the value type makes the model trivially testable and side-effect free).
/// </summary>
/// <remarks>
/// Cursor moves between [0, Value.Length] inclusive — Length is the "append" position to
/// the right of the last character. Characters that <see cref="CharFilter"/> rejects (and
/// the printable-null char from <see cref="KeyCharMap"/>) are ignored — the field never
/// throws on bad input.
/// </remarks>
public readonly record struct TextInputField
{
    /// <summary>Builds an empty field of <paramref name="maxLength"/> capacity using
    /// <paramref name="charFilter"/> as the accept-character predicate.</summary>
    public static TextInputField Empty(int maxLength, Func<char, bool> charFilter) =>
        new(string.Empty, cursor: 0, maxLength, charFilter);

    /// <summary>Builds a field pre-populated with <paramref name="value"/>; the cursor
    /// parks at the end. Characters outside the filter are silently dropped so the
    /// constructor never carries an unrepresentable value.</summary>
    public static TextInputField From(string value, int maxLength, Func<char, bool> charFilter)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(charFilter);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxLength);

        var sanitized = Sanitize(value, maxLength, charFilter);
        return new TextInputField(sanitized, sanitized.Length, maxLength, charFilter);
    }

    private TextInputField(string value, int cursor, int maxLength, Func<char, bool> charFilter)
    {
        Value = value;
        Cursor = cursor;
        MaxLength = maxLength;
        CharFilter = charFilter;
    }

    public string Value { get; }

    /// <summary>Cursor position in [0, <see cref="Value"/>.Length]. A cursor at Length
    /// means the next typed character appends to the end.</summary>
    public int Cursor { get; }

    public int MaxLength { get; }

    public Func<char, bool> CharFilter { get; }

    public bool IsEmpty => Value.Length == 0;

    /// <summary>Returns a field with <paramref name="ch"/> inserted at the cursor. A
    /// character rejected by the filter, a null char, or a full buffer leave the field
    /// unchanged.</summary>
    public TextInputField WithTyped(char ch)
    {
        if (ch == '\0' || !CharFilter(ch) || Value.Length >= MaxLength)
        {
            return this;
        }

        var next = string.Concat(Value.AsSpan(0, Cursor), ch.ToString(), Value.AsSpan(Cursor));
        return new TextInputField(next, Cursor + 1, MaxLength, CharFilter);
    }

    /// <summary>Returns a field with the character immediately left of the cursor removed.
    /// A no-op when the cursor is at the start.</summary>
    public TextInputField WithBackspace()
    {
        if (Cursor == 0)
        {
            return this;
        }

        var next = string.Concat(Value.AsSpan(0, Cursor - 1), Value.AsSpan(Cursor));
        return new TextInputField(next, Cursor - 1, MaxLength, CharFilter);
    }

    /// <summary>Moves the cursor by <paramref name="direction"/> (-1 / +1), clamped to
    /// the field's [0, Length] bounds.</summary>
    public TextInputField WithCursor(int direction)
    {
        var moved = Math.Clamp(Cursor + Math.Sign(direction), 0, Value.Length);
        if (moved == Cursor)
        {
            return this;
        }

        return new TextInputField(Value, moved, MaxLength, CharFilter);
    }

    /// <summary>Returns a field with the cursor at the value's end — used when focus
    /// transfers in from another field and the player expects to append.</summary>
    public TextInputField WithCursorAtEnd() =>
        Cursor == Value.Length
            ? this
            : new TextInputField(Value, Value.Length, MaxLength, CharFilter);

    /// <summary>Returns a field with its content replaced by <paramref name="value"/> —
    /// used by Reset / Cancel paths that need to roll back to a known snapshot.</summary>
    public TextInputField WithValue(string value) => From(value, MaxLength, CharFilter);

    private static string Sanitize(string value, int maxLength, Func<char, bool> filter)
    {
        if (value.Length == 0)
        {
            return string.Empty;
        }

        Span<char> buffer = stackalloc char[Math.Min(value.Length, maxLength)];
        var written = 0;
        foreach (var ch in value)
        {
            if (!filter(ch) || written == buffer.Length)
            {
                continue;
            }

            buffer[written++] = ch;
        }

        return written == 0 ? string.Empty : new string(buffer[..written]);
    }
}

/// <summary>Static accept predicates used by <see cref="TextInputField"/>. Pulled out so
/// the model + tests both reference the same constants.</summary>
public static class TextInputFilters
{
    /// <summary>Hostname / IPv4-literal accept set: digits, lowercase ASCII letters,
    /// dot, hyphen. The screen's renderer expects the lowercased form (see
    /// <see cref="KeyCharMap"/>).</summary>
    public static bool Host(char ch) =>
        (ch >= '0' && ch <= '9') ||
        (ch >= 'a' && ch <= 'z') ||
        ch == '.' || ch == '-';

    /// <summary>Port digits only — TCP/UDP ports are pure unsigned integers. The
    /// model clamps the parsed value to <c>[MinPort, MaxPort]</c> on commit.</summary>
    public static bool Port(char ch) => ch >= '0' && ch <= '9';
}
