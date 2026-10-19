using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Blazor.Pages.Forms.TravelExpenses;

public partial class TripAddBar {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public EventCallback<TripModuleKind> OnAdd { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }
}
