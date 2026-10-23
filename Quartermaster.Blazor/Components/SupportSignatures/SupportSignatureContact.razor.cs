using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.SupportSignatures;

public partial class SupportSignatureContact {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public string? PostalAddress { get; set; }

    [Parameter]
    public string? ContactEmail { get; set; }
}
