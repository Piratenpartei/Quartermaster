using System;
using System.Linq;
using System.Text;

namespace Quartermaster.Api.Accreditation;

// Shared by the server (account creation) and the Blazor page (live suggestion while typing); both must
// produce the names MemberDataImport's UsernameGenerator produces so accounts created here look alike.
/// <summary>Base username normalization and validity rule for Keycloak usernames.</summary>
public static class UsernameRule {
    // mirrors Keycloak's username-prohibited-characters validator (whitespace and control chars are handled separately)
    private const string ProhibitedCharacters = "<>&\"'$%!#?§,;:*~/\\|^=[]{}()`";

    public static string BuildBaseName(string firstName, string lastName, string separator) {
        var raw = $"{firstName.Trim()}{separator}{lastName.Trim()}";
        var kept = new StringBuilder(raw.Length);
        foreach (var character in raw) {
            if (character == '.' || char.IsWhiteSpace(character)) {
                kept.Append(' ');
            } else if (!ProhibitedCharacters.Contains(character) && !char.IsControl(character)) {
                kept.Append(character);
            }
        }
        var parts = kept.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(separator, parts);
    }

    public static bool IsValid(string username) {
        if (username.Length == 0)
            return false;
        return !username.Any(c => char.IsWhiteSpace(c) || ProhibitedCharacters.Contains(c) || char.IsControl(c));
    }
}
