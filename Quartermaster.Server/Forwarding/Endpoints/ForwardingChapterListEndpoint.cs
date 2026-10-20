using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forwarding.Endpoints;

/// <summary>The forwarding settings of the federal chapter (always listed, also before it has a row) and every chapter with its own settings.</summary>
public class ForwardingChapterListEndpoint : EndpointWithoutRequest<List<ForwardingChapterSettingDTO>> {
    private readonly ForwardingSettingsRepository _settings;
    private readonly ForwardingSettingsResolver _resolver;
    private readonly ChapterRepository _chapters;
    private readonly PermissionContext _perms;

    public ForwardingChapterListEndpoint(ForwardingSettingsRepository settings, ForwardingSettingsResolver resolver, ChapterRepository chapters, PermissionContext perms) {
        _settings = settings;
        _resolver = resolver;
        _chapters = chapters;
        _perms = perms;
    }

    public override void Configure() {
        Get("/api/forwarding/chapters");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var rows = _settings.ListChapters().ToDictionary(s => s.ChapterId);
        var chapterIds = rows.Keys.ToList();
        var root = _chapters.GetRootChapter();
        if (root != null && !rows.ContainsKey(root.Id))
            chapterIds.Insert(0, root.Id);

        var list = new List<ForwardingChapterSettingDTO>();
        foreach (var chapterId in chapterIds) {
            var chapter = _chapters.Get(chapterId);
            if (chapter == null)
                continue;

            rows.TryGetValue(chapterId, out var row);
            list.Add(new ForwardingChapterSettingDTO {
                ChapterId = chapterId,
                ChapterName = chapter.Name,
                IsFederal = chapter.ParentChapterId == null,
                IsStateChapter = IsStateChapter(chapter),
                ApplicationMode = row?.ApplicationMode,
                ReductionMode = row?.ReductionMode,
                FailureEmail = row?.FailureEmail,
                Targets = _settings.ListTargets(chapterId).Select(t => t.ToDto()).ToList(),
                HasConnection = _resolver.ConnectionFor(chapterId) != null
            });
        }

        await SendAsync(list
            .OrderByDescending(c => c.IsFederal)
            .ThenByDescending(c => c.IsStateChapter)
            .ThenBy(c => c.ChapterName, StringComparer.CurrentCulture)
            .ToList(), cancellation: ct);
    }

    private bool IsStateChapter(Chapter chapter) {
        if (chapter.ParentChapterId == null)
            return false;
        return _chapters.Get(chapter.ParentChapterId.Value)?.ParentChapterId == null;
    }
}
