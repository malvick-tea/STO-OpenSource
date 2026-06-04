using Opus.Engine.Input;

namespace Garupan.Client.Ui.Screens.Settings.Multiplayer;

/// <summary>
/// Pure read-only mapping of <see cref="Key"/> → printable character for text-entry
/// fields. Returns <c>'\0'</c> for keys that have no printable form (Esc, Enter,
/// arrows, function keys). Hostnames are case-insensitive per RFC 952 / 1123, so
/// letters resolve lowercase — there is no SHIFT-state to consult.
/// </summary>
/// <remarks>
/// The set is intentionally minimal — the characters required to type any of: an IPv4
/// dotted literal, an IPv6 literal, or a DNS hostname. Specifically: digits,
/// lowercase ASCII letters, dot, hyphen. Colons (IPv6 separator) and brackets (IPv6
/// literal delimiter) are NOT in the engine <see cref="Key"/> enum yet; the resolver
/// already accepts the unbracketed form a tester would type if they pasted an IPv6
/// address into a field — but a real bracketed-IPv6 text-entry path lands when the
/// engine input layer carries the underlying keys.
/// </remarks>
internal static class KeyCharMap
{
    /// <summary>Translates <paramref name="key"/> to its lowercase printable character
    /// for hostname / port entry. Returns <c>'\0'</c> when the key has no printable
    /// form — callers treat the null char as "ignore".</summary>
    public static char ToPrintable(Key key) => key switch
    {
        Key.A => 'a', Key.B => 'b', Key.C => 'c', Key.D => 'd', Key.E => 'e',
        Key.F => 'f', Key.G => 'g', Key.H => 'h', Key.I => 'i', Key.J => 'j',
        Key.K => 'k', Key.L => 'l', Key.M => 'm', Key.N => 'n', Key.O => 'o',
        Key.P => 'p', Key.Q => 'q', Key.R => 'r', Key.S => 's', Key.T => 't',
        Key.U => 'u', Key.V => 'v', Key.W => 'w', Key.X => 'x', Key.Y => 'y',
        Key.Z => 'z',
        Key.D0 => '0', Key.D1 => '1', Key.D2 => '2', Key.D3 => '3', Key.D4 => '4',
        Key.D5 => '5', Key.D6 => '6', Key.D7 => '7', Key.D8 => '8', Key.D9 => '9',
        Key.Period => '.',
        Key.Hyphen => '-',
        _ => '\0',
    };
}
