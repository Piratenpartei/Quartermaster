using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Pages.Forms;

public partial class FormsCatalog {
    [Inject]
    public required I18nService I18n { get; set; }
}
