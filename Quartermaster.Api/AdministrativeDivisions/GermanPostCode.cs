using System.Linq;

namespace Quartermaster.Api.AdministrativeDivisions;

/// <summary>German post codes have five digits and may start with 0.</summary>
public static class GermanPostCode {
    public const int Length = 5;

    /// <summary>Restores the leading zeros of a post code written as a number, so "4289" becomes "04289". Anything other than a short run of digits comes back trimmed only.</summary>
    public static string Normalize(string postCode) {
        var trimmed = postCode.Trim();
        if (trimmed.Length == 0 || trimmed.Length >= Length || !trimmed.All(char.IsAsciiDigit))
            return trimmed;
        return trimmed.PadLeft(Length, '0');
    }
}
