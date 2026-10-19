using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.AssemblyMotions.Forum;

public class AssemblyMotionForumConnectionCheck {
    public const string NotConfiguredMessage = "Die Forum-Zugangsdaten sind nicht hinterlegt.";

    private readonly IDiscourseClient _forum;
    private readonly OptionRepository _options;
    private readonly ILogger<AssemblyMotionForumConnectionCheck> _logger;

    public AssemblyMotionForumConnectionCheck(IDiscourseClient forum, OptionRepository options, ILogger<AssemblyMotionForumConnectionCheck> logger) {
        _forum = forum;
        _options = options;
        _logger = logger;
    }

    /// <summary>Checks the account and, with <paramref name="categoryId"/>, whether the account may create topics there. Never creates or edits anything.</summary>
    public async Task<ForumConnectionCheckResult> CheckAsync(int? categoryId, CancellationToken ct) {
        var settings = DiscourseSettings.ReadFrom(_options);
        if (!settings.IsConfigured())
            return new ForumConnectionCheckResult(false, false, NotConfiguredMessage, []);

        var warnings = new List<string>();
        DiscourseCurrentUser user;
        try {
            user = await _forum.GetCurrentUserAsync(settings, ct);
        } catch (Exception ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("Forum connection check failed: {Exception}", ex);
            return new ForumConnectionCheckResult(false, true, Reason(ex), []);
        }

        if (!user.IsStaff)
            warnings.Add($"Das Konto {user.Username} ist kein Moderator; Discourse begrenzt dann Themen, Bearbeitungen und Links.");

        var message = $"Verbindung erfolgreich: angemeldet als {user.Username}.";
        if (categoryId == null)
            return new ForumConnectionCheckResult(true, true, message, warnings);

        DiscourseCategory category;
        try {
            category = await _forum.GetCategoryAsync(settings, categoryId.Value, ct);
        } catch (DiscourseException ex) when (ex.Code == DiscourseException.NotFound) {
            return new ForumConnectionCheckResult(false, true, $"Die Forum-Kategorie {categoryId} existiert nicht.", warnings);
        } catch (Exception ex) when (!ct.IsCancellationRequested) {
            _logger.LogWarning("Forum category check failed: {Exception}", ex);
            return new ForumConnectionCheckResult(false, true, Reason(ex), warnings);
        }

        if (!category.CanCreateTopic)
            return new ForumConnectionCheckResult(false, true, "Das Konto darf in dieser Kategorie keine Themen anlegen.", warnings);
        if (!category.ReadRestricted)
            warnings.Add("Die Kategorie ist öffentlich lesbar; die Beiträge enthalten die Namen der Antragsteller.");

        return new ForumConnectionCheckResult(true, true, $"{message} Kategorie: {category.Name}.", warnings);
    }

    // Only fixed texts pass through: exception messages may carry details from the forum's response.
    private static string Reason(Exception exception) {
        if (exception is not DiscourseException forumException)
            return "Unerwarteter Fehler, Details stehen im Serverprotokoll.";

        switch (forumException.Code) {
            case DiscourseException.Unauthorized:
                return "Das Forum hat den API-Schlüssel oder das Bot-Konto abgelehnt.";
            case DiscourseException.InsecureUrl:
                return "Die Forum-URL muss https verwenden.";
            case DiscourseException.Timeout:
                return "Zeitüberschreitung bei der Anfrage an das Forum.";
            case DiscourseException.RateLimited:
                return "Das Forum begrenzt die Anfragen gerade, bitte später erneut prüfen.";
            case DiscourseException.NotFound:
                return "Das Forum kennt die Adresse nicht. Bitte die Basis-URL prüfen.";
            case DiscourseException.HttpError:
                return "Das Forum ist nicht erreichbar oder hat die Anfrage abgelehnt.";
            default:
                return "Das Forum hat unerwartet geantwortet.";
        }
    }
}
