using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public partial class BerlinDateTimeInput {
    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public string CssClass { get; set; } = "";

    [Parameter]
    public bool Disabled { get; set; }

    private async Task OnChange(ChangeEventArgs e) {
        await ValueChanged.InvokeAsync(e.Value?.ToString() ?? "");
    }
}
