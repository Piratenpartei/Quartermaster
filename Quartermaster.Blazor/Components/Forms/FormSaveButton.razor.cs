using Microsoft.AspNetCore.Components;

namespace Quartermaster.Blazor.Components.Forms;

public partial class FormSaveButton {
    [CascadingParameter]
    public DirtyForm? Form { get; set; }

    [Parameter]
    public EventCallback OnClick { get; set; }

    [Parameter]
    public bool Enabled { get; set; } = true;

    [Parameter]
    public bool Saving { get; set; }

    [Parameter]
    public string Text { get; set; } = "Speichern";

    [Parameter]
    public string SavingText { get; set; } = "Wird gespeichert...";

    [Parameter]
    public string ButtonClass { get; set; } = "btn-primary";

    private bool IsDisabled => Saving || !Enabled || (Form != null && !Form.IsDirty);
}
