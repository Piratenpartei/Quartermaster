using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Forms.TravelExpenses;

public partial class TravelExpensePersonStep {
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required ChapterNames ChapterNames { get; set; }

    [Parameter, EditorRequired]
    public TravelExpenseInputDTO Input { get; set; } = default!;

    private async Task OnChapterPicked(string raw) {
        Input.MembershipChapterId = Guid.TryParse(raw, out var id) ? id : null;
        Input.MembershipChapterName = Input.MembershipChapterId == null ? "" : await ChapterNames.NameOfAsync(Input.MembershipChapterId.Value);
    }
}
