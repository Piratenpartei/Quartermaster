using System.Linq;

namespace Quartermaster.Server.Validation;

public static class EmailAddressFormat {
    /// <summary>A loose check only: an @ and no whitespace. Whether the address exists shows when mail is sent to it.</summary>
    public static bool IsPlausible(string email) {
        var trimmed = email.Trim();
        return trimmed.Contains('@') && !trimmed.Any(char.IsWhiteSpace);
    }
}
