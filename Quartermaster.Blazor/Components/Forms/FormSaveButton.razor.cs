using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.Forms;

/// <summary>The save button of a <see cref="DirtyForm"/>: an <see cref="AsyncButton"/> that is disabled while the form has no changes.</summary>
public partial class FormSaveButton {
    [CascadingParameter]
    public DirtyForm? Form { get; set; }

    /// <summary>Saves; returns whether it succeeded, after showing the error toast on failure.</summary>
    [Parameter, EditorRequired]
    public Func<Task<bool>> Action { get; set; } = default!;

    [Parameter]
    public bool Enabled { get; set; } = true;

    [Parameter]
    public string? Text { get; set; }

    [Parameter]
    public ButtonKind Kind { get; set; } = ButtonKind.Primary;

    private bool IsDisabled => !Enabled || (Form != null && !Form.IsDirty);

    private string ResolvedText => Text ?? I18n[I18nKey.Ui.FormSaveButton.DefaultText];
}
