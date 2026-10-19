using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Blazor.Pages.Administration.TravelExpenseRules;

public partial class TravelExpenseRuleForm {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public TravelExpenseRulesDTO Rules { get; set; } = default!;
}
