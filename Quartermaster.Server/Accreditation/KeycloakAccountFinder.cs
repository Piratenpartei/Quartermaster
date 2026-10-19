using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quartermaster.Api.Accreditation;
using Quartermaster.Api.I18n;
using Quartermaster.Server.Members;

namespace Quartermaster.Server.Accreditation;

/// <summary>The account search of the accreditation page and the user import: an exact member number, a name, or both.</summary>
public static class KeycloakAccountFinder {
    public const int MinQueryLength = 2;

    // Keycloak can only search for one string, so it searches the longest word and the other words filter its answer.
    private const int CandidateLimit = 100;

    /// <summary>The i18n key of what is wrong with the query; null when it can run.</summary>
    public static string? Problem(KeycloakAccountQuery query) {
        var hasNumber = !string.IsNullOrWhiteSpace(query.MemberNumber);
        var words = Words(query.Query);
        if (!hasNumber && words.Count == 0)
            return I18nKey.Error.KeycloakAccount.QueryRequired;
        if (hasNumber && !MemberNumberParser.TryParse(query.MemberNumber, out _))
            return I18nKey.Error.KeycloakAccount.InvalidMemberNumber;
        if (!hasNumber && words.Max(w => w.Length) < MinQueryLength)
            return I18nKey.Error.KeycloakAccount.QueryTooShort;
        return null;
    }

    public static async Task<List<KeycloakUserRepresentation>> FindAsync(IKeycloakAdminClient keycloak, KeycloakAccountQuery query, CancellationToken ct) {
        var words = Words(query.Query);
        var candidates = string.IsNullOrWhiteSpace(query.MemberNumber)
            ? await keycloak.SearchAsync(words.OrderByDescending(w => w.Length).First(), CandidateLimit, ct)
            : await keycloak.FindByMemberNumberAsync(query.MemberNumber, ct);

        return candidates
            .Where(u => words.All(w => Matches(u, w)))
            .OrderBy(u => u.LastName)
            .ThenBy(u => u.FirstName)
            .Take(KeycloakAccountQuery.MaxResults)
            .ToList();
    }

    private static List<string> Words(string? text) {
        return (text ?? "")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
    }

    private static bool Matches(KeycloakUserRepresentation user, string word) {
        return Contains(user.Username, word) || Contains(user.FirstName, word) || Contains(user.LastName, word);
    }

    private static bool Contains(string? value, string word) {
        return value != null && value.Contains(word, StringComparison.OrdinalIgnoreCase);
    }
}
