using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components;

public partial class MotionOnBehalfText {
    [Parameter, EditorRequired]
    public ChapterMark Reason { get; set; }

    [Parameter, EditorRequired]
    public string ChapterName { get; set; } = "";

    [Inject]
    public required I18nService I18n { get; set; }

    private string Text => Reason switch {
        ChapterMark.UnableToAct => I18n[$"{I18nKey.Ui.ChapterMarks.NoticeUnableToAct}?name={Uri.EscapeDataString(ChapterName)}"],
        _ => I18n[$"{I18nKey.Ui.ChapterMarks.NoticeNoMemberAdministration}?name={Uri.EscapeDataString(ChapterName)}"]
    };
}
