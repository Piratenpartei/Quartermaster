using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class MembershipPersonalDataStep {
    [Inject]
    public required AuthService AuthService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public MembershipApplicationEntryState State { get; set; } = default!;
}
