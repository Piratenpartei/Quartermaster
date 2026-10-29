using System;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Motions;

namespace Quartermaster.Blazor.Components;

public partial class MotionHandoverText {
    [Parameter]
    public MotionHandoverReason? Reason { get; set; }

    [Parameter, EditorRequired]
    public string ChapterName { get; set; } = "";

    [Inject]
    public required I18nService I18n { get; set; }

    private string Text => Reason switch {
        MotionHandoverReason.Deadline => I18n[I18nParams.With(I18nKey.Ui.HandUp.HandedOverDeadline, ("name", ChapterName))],
        MotionHandoverReason.TakenOver => I18n[I18nKey.Ui.HandUp.HandedOverTakenOver],
        _ => I18n[I18nParams.With(I18nKey.Ui.ChapterMarks.HandedOverTo, ("name", ChapterName))]
    };
}
