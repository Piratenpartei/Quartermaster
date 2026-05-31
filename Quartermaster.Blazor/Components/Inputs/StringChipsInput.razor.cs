using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class StringChipsInput {
    [Parameter]
    public List<string> Value { get; set; } = [];

    [Parameter]
    public EventCallback<List<string>> ValueChanged { get; set; }

    [Parameter]
    public string Placeholder { get; set; } = "";

    private string CurrentInput = "";

    private void OnInput(ChangeEventArgs e) {
        CurrentInput = e.Value?.ToString() ?? "";
    }

    private async Task OnKeyDown(KeyboardEventArgs e) {
        if (e.Key == "Enter" || e.Key == ",") {
            await CommitCurrent();
        } else if (e.Key == "Backspace" && CurrentInput.Length == 0 && Value.Count > 0) {
            var next = Value.Take(Value.Count - 1).ToList();
            await ValueChanged.InvokeAsync(next);
        }
    }

    private async Task CommitCurrent() {
        var trimmed = CurrentInput.Trim().TrimEnd(',').Trim();
        CurrentInput = "";
        if (trimmed.Length == 0 || Value.Contains(trimmed))
            return;
        var next = Value.Append(trimmed).ToList();
        await ValueChanged.InvokeAsync(next);
    }

    private async Task Remove(string chip) {
        var next = Value.Where(c => c != chip).ToList();
        await ValueChanged.InvokeAsync(next);
    }
}
