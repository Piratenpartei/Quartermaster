using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Data.ApplicantRegister;
using Quartermaster.Server.AdministrativeDivisions;

namespace Quartermaster.Server.ApplicantRegister;

public static class ApplicantRegisterMatcher {
    public const int Threshold = 3;

    private const int LongWordLength = 8;
    private const int StreetMaxDistance = 2;
    private const int CityMaxDistance = 1;

    private static readonly RegisterMatchFields[] AllFields = Enum.GetValues<RegisterMatchFields>()
        .Where(field => field != RegisterMatchFields.None)
        .ToArray();

    public static List<ApplicationRegisterHit> Match(RegisterPerson candidate, IReadOnlyList<ApplicantRegisterEntry> entries, DateTime nowUtc) {
        var hits = new List<ApplicationRegisterHit>();
        foreach (var entry in entries) {
            var fields = Compare(candidate, RegisterPerson.FromEntry(entry));
            if (CountFields(fields) < Threshold)
                continue;

            hits.Add(new ApplicationRegisterHit {
                ApplicationId = Guid.Empty,
                EntryId = entry.Id,
                MatchedFields = fields,
                CreatedAt = nowUtc
            });
        }

        return hits;
    }

    public static RegisterMatchFields Compare(RegisterPerson candidate, RegisterPerson entry) {
        var fields = RegisterMatchFields.None;
        if (NamesMatch(candidate.FirstName, entry.FirstName))
            fields |= RegisterMatchFields.FirstName;
        if (NamesMatch(candidate.LastName, entry.LastName))
            fields |= RegisterMatchFields.LastName;
        if (DatesOfBirthMatch(candidate, entry))
            fields |= RegisterMatchFields.DateOfBirth;
        if (StreetsMatch(candidate, entry))
            fields |= RegisterMatchFields.Street;
        if (PostCodesAndCitiesMatch(candidate, entry))
            fields |= RegisterMatchFields.PostCodeCity;
        if (EmailsMatch(candidate.Email, entry.Email))
            fields |= RegisterMatchFields.Email;
        return fields;
    }

    public static int CountFields(RegisterMatchFields fields) {
        var count = 0;
        foreach (var field in AllFields) {
            if (fields.HasFlag(field))
                count++;
        }

        return count;
    }

    public static bool NamesMatch(string a, string b) {
        var wordsA = DivisionNameTokenizer.Words(a);
        var wordsB = DivisionNameTokenizer.Words(b);
        if (wordsA.Count == 0 || wordsB.Count == 0)
            return false;

        var fewer = wordsA.Count <= wordsB.Count ? wordsA : wordsB;
        var other = wordsA.Count <= wordsB.Count ? wordsB : wordsA;
        return fewer.All(word => other.Any(candidate => WordsMatch(word, candidate)));
    }

    private static bool WordsMatch(string a, string b) {
        var max = Math.Max(a.Length, b.Length) >= LongWordLength ? 2 : 1;
        return EditDistance.Bounded(a, b, max) <= max;
    }

    private static bool DatesOfBirthMatch(RegisterPerson candidate, RegisterPerson entry) {
        if (!candidate.DateOfBirth.HasValue)
            return false;
        if (!entry.DateOfBirth.HasValue)
            return false;
        return candidate.DateOfBirth.Value == entry.DateOfBirth.Value;
    }

    private static bool StreetsMatch(RegisterPerson candidate, RegisterPerson entry) {
        var streetA = FoldStreet(candidate.Street);
        var streetB = FoldStreet(entry.Street);
        if (streetA.Length == 0 || streetB.Length == 0)
            return false;

        var houseNumberA = FoldHouseNumber(candidate.HouseNumber);
        var houseNumberB = FoldHouseNumber(entry.HouseNumber);
        if (houseNumberA.Length == 0 || houseNumberA != houseNumberB)
            return false;

        return EditDistance.Bounded(streetA, streetB, StreetMaxDistance) <= StreetMaxDistance;
    }

    private static bool PostCodesAndCitiesMatch(RegisterPerson candidate, RegisterPerson entry) {
        var postCode = FoldPostCode(candidate.PostCode);
        if (postCode.Length == 0 || postCode != FoldPostCode(entry.PostCode))
            return false;

        var cityA = FoldCity(candidate.City);
        var cityB = FoldCity(entry.City);
        if (cityA.Length == 0 || cityB.Length == 0)
            return false;

        return EditDistance.Bounded(cityA, cityB, CityMaxDistance) <= CityMaxDistance;
    }

    private static bool EmailsMatch(string? a, string? b) {
        var emailA = a?.Trim() ?? "";
        var emailB = b?.Trim() ?? "";
        if (emailA.Length == 0 || emailB.Length == 0)
            return false;

        return string.Equals(emailA, emailB, StringComparison.OrdinalIgnoreCase);
    }

    private static string FoldStreet(string? street) {
        return string.Concat(DivisionNameTokenizer.Words(street ?? "")).Replace("strasse", "str");
    }

    private static string FoldCity(string? city) {
        return string.Concat(DivisionNameTokenizer.Words(city ?? ""));
    }

    private static string FoldHouseNumber(string? houseNumber) {
        return RemoveWhitespace(houseNumber).ToLowerInvariant();
    }

    private static string FoldPostCode(string? postCode) {
        return RemoveWhitespace(postCode);
    }

    private static string RemoveWhitespace(string? text) {
        return string.Concat((text ?? "").Where(c => !char.IsWhiteSpace(c)));
    }
}
