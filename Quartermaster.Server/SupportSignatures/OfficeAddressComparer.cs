using System.Linq;

namespace Quartermaster.Server.SupportSignatures;

/// <summary>Compares office details the way a letter carrier reads them: case, spaces, dots and the spellings of "Straße" do not matter.</summary>
public static class OfficeAddressComparer {
    public static string Normalize(string? value) {
        var text = (value ?? "")
            .ToLowerInvariant()
            .Replace("ß", "ss")
            .Replace("strasse", "str");
        return new string(text.Where(c => !char.IsWhiteSpace(c) && c != '.').ToArray());
    }

    public static bool SameAddress(string streetA, string postCodeA, string cityA, string streetB, string postCodeB, string cityB) {
        if (Normalize(streetA) != Normalize(streetB))
            return false;
        if (Normalize(postCodeA) != Normalize(postCodeB))
            return false;

        return Normalize(cityA) == Normalize(cityB);
    }

    /// <summary>A key that is equal for two offices with the same name, addition and address.</summary>
    public static string OfficeKey(string name, string? addition, string street, string postCode, string city) {
        return string.Join("|", Normalize(name), Normalize(addition), Normalize(street), Normalize(postCode), Normalize(city));
    }
}
