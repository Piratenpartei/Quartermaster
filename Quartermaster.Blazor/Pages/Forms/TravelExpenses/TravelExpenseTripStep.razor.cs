using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Blazor.Pages.Forms.TravelExpenses;

public partial class TravelExpenseTripStep {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public TravelExpenseInputDTO Input { get; set; } = default!;
}
