using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Blazor.Pages.Forms.TravelExpenses;

/// <summary>The space between two timeline entries; opens an add bar that inserts right there.</summary>
public partial class TripGap {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public bool Open { get; set; }

    [Parameter]
    public EventCallback OnOpen { get; set; }

    [Parameter]
    public EventCallback<TripModuleKind> OnAdd { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }
}
