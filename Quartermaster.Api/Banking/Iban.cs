using System.Collections.Generic;
using System.Text;

namespace Quartermaster.Api.Banking;

/// <summary>
/// Offline IBAN check after ISO 13616: two letters, two check digits, an alphanumeric account part, the fixed length of
/// SEPA countries and the mod-97 checksum. It catches typos and swapped digits, not whether the account exists.
/// </summary>
public static class Iban {
    public const int MinLength = 15;
    private const int MaxLength = 34;

    private static readonly Dictionary<string, int> SepaLengths = new() {
        ["AD"] = 24, ["AT"] = 20, ["BE"] = 16, ["BG"] = 22, ["CH"] = 21, ["CY"] = 28, ["CZ"] = 24, ["DE"] = 22,
        ["DK"] = 18, ["EE"] = 20, ["ES"] = 24, ["FI"] = 18, ["FR"] = 27, ["GB"] = 22, ["GI"] = 23, ["GR"] = 27,
        ["HR"] = 21, ["HU"] = 28, ["IE"] = 22, ["IS"] = 26, ["IT"] = 27, ["LI"] = 21, ["LT"] = 20, ["LU"] = 20,
        ["LV"] = 21, ["MC"] = 27, ["MT"] = 31, ["NL"] = 18, ["NO"] = 15, ["PL"] = 28, ["PT"] = 25, ["RO"] = 24,
        ["SE"] = 24, ["SI"] = 19, ["SK"] = 24, ["SM"] = 27, ["VA"] = 22
    };

    /// <summary>Uppercase without spaces, the form the checksum works on.</summary>
    public static string Normalize(string? value) {
        if (string.IsNullOrEmpty(value))
            return "";

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value) {
            if (!char.IsWhiteSpace(ch))
                builder.Append(char.ToUpperInvariant(ch));
        }
        return builder.ToString();
    }

    public static bool IsValid(string? value) {
        var iban = Normalize(value);
        if (!HasValidShape(iban))
            return false;
        if (SepaLengths.TryGetValue(iban[..2], out var length) && iban.Length != length)
            return false;

        return Mod97(iban) == 1;
    }

    private static bool HasValidShape(string iban) {
        if (iban.Length < MinLength || iban.Length > MaxLength)
            return false;
        if (!IsAsciiLetter(iban[0]) || !IsAsciiLetter(iban[1]))
            return false;
        if (!char.IsAsciiDigit(iban[2]) || !char.IsAsciiDigit(iban[3]))
            return false;

        for (var i = 4; i < iban.Length; i++) {
            if (!IsAsciiLetter(iban[i]) && !char.IsAsciiDigit(iban[i]))
                return false;
        }
        return true;
    }

    // The country code and check digits move to the end, letters count as 10 (A) to 35 (Z), and the remainder is
    // taken digit by digit so the number never has to fit into a machine integer.
    private static int Mod97(string iban) {
        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var ch in rearranged) {
            var value = char.IsAsciiDigit(ch) ? ch - '0' : ch - 'A' + 10;
            remainder = value >= 10
                ? (remainder * 100 + value) % 97
                : (remainder * 10 + value) % 97;
        }
        return remainder;
    }

    private static bool IsAsciiLetter(char ch) {
        return ch >= 'A' && ch <= 'Z';
    }
}
