using System;
using System.Collections.Generic;
using System.Linq;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;

namespace Quartermaster.Server.Forwarding;

/// <summary>
/// Looks settings up the chapter tree. Lower-chapter modes stop below the root, because the federal chapter's own modes must never act as a default for them;
/// connections and failure addresses do fall back to the root.
/// </summary>
public class ForwardingSettingsResolver {
    private readonly ChapterRepository _chapters;
    private readonly ForwardingSettingsRepository _settings;
    private readonly RedmineConnectionRepository _connections;

    public ForwardingSettingsResolver(ChapterRepository chapters, ForwardingSettingsRepository settings, RedmineConnectionRepository connections) {
        _chapters = chapters;
        _settings = settings;
        _connections = connections;
    }

    /// <summary>The chapter directly below the root in the ancestry of <paramref name="chapterId"/>; null for the root itself or no chapter.</summary>
    public Guid? StateChapterOf(Guid? chapterId) {
        if (chapterId == null)
            return null;

        var chain = _chapters.GetAncestorChainIds(chapterId.Value);
        return chain.Count < 2 ? null : chain[^2];
    }

    public Guid? FederalChapterOf(Guid? chapterId) {
        if (chapterId == null)
            return _chapters.GetRootChapter()?.Id;

        var chain = _chapters.GetAncestorChainIds(chapterId.Value);
        return chain.Count == 0 ? _chapters.GetRootChapter()?.Id : chain[^1];
    }

    public (Guid? ChapterId, ForwardingMode? Mode) HandlingChapterFor(Guid? chapterId) {
        if (chapterId == null)
            return (null, null);

        var chain = _chapters.GetAncestorChainIds(chapterId.Value);
        foreach (var id in chain.Take(Math.Max(chain.Count - 1, 0))) {
            var mode = _settings.GetChapter(id)?.ApplicationMode;
            if (mode != null)
                return (id, mode);
        }

        return (null, null);
    }

    public RedmineConnection? ConnectionFor(Guid chapterId) {
        foreach (var id in SettingsChain(chapterId)) {
            var connection = _connections.Get(id);
            if (connection != null)
                return connection;
        }

        return null;
    }

    public string? FailureEmailFor(Guid chapterId) {
        foreach (var id in SettingsChain(chapterId)) {
            var email = _settings.GetChapter(id)?.FailureEmail;
            if (!string.IsNullOrWhiteSpace(email))
                return email.Trim();
        }

        return null;
    }

    /// <summary>
    /// The chain connections and failure addresses are looked up along. A queued step can target a chapter deleted since; its tickets and failure mails
    /// still go where that chapter's settings, or its ancestors', point.
    /// </summary>
    private List<Guid> SettingsChain(Guid chapterId) {
        var chain = _chapters.GetAncestorChainIds(chapterId);
        return chain.Count > 0 ? chain : _chapters.GetAncestorChainIdsIncludingDeleted(chapterId);
    }

    /// <summary>The routing context for a member's or an application's chapter; null resolves to the root chapter without a state chapter.</summary>
    public ForwardingChapterContext Build(Guid? chapterId) {
        var federalId = FederalChapterOf(chapterId);
        var stateId = StateChapterOf(chapterId);
        var federal = federalId == null ? null : _settings.GetChapter(federalId.Value);
        var state = stateId == null ? null : _settings.GetChapter(stateId.Value);
        var handling = HandlingChapterFor(chapterId);

        return new ForwardingChapterContext(
            federalId,
            stateId,
            federal?.ApplicationMode,
            federal?.ReductionMode,
            state?.ReductionMode,
            handling.ChapterId,
            handling.Mode);
    }
}
