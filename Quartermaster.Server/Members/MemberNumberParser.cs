using System.Globalization;

namespace Quartermaster.Server.Members;

/// <summary>Parses member numbers from user input and SSO claims; negative numbers are valid member numbers, zero is not.</summary>
public static class MemberNumberParser {
    public static bool TryParse(string? value, out int memberNumber) {
        if (!int.TryParse(value?.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out memberNumber))
            return false;
        return memberNumber != 0;
    }
}
