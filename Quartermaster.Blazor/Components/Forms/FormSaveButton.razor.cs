using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

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
    public string? Text { get; set; }

    [Parameter]
    public string? SavingText { get; set; }

    [Parameter]
    public string ButtonClass { get; set; } = "btn-primary";

    private bool IsDisabled => Saving || !Enabled || (Form != null && !Form.IsDirty);

    private string ResolvedText => Text ?? I18n[I18nKey.Ui.FormSaveButton.DefaultText];
    private string ResolvedSavingText => SavingText ?? I18n[I18nKey.Ui.FormSaveButton.DefaultSavingText];
}
