using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Forms;

public partial class DirtyForm {
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    public bool IsDirty { get; private set; }

    public void MarkDirty() {
        if (IsDirty)
            return;

        IsDirty = true;
        StateHasChanged();
    }

    public void Reset() {
        IsDirty = false;
        StateHasChanged();
    }
}
