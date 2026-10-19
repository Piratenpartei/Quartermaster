using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;

namespace Quartermaster.Blazor.Pages.Forms.TravelExpenses;

public partial class TravelExpenseNoteList {
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public List<TravelExpenseNoteDTO> Notes { get; set; } = default!;

    [Parameter]
    public bool Compact { get; set; }

    /// <summary>Names the module a note is about, for lists shown away from the module itself.</summary>
    [Parameter]
    public IReadOnlyDictionary<Guid, string>? ModuleLabels { get; set; }

    private string Text(TravelExpenseNoteDTO note) {
        var message = I18n[note.MessageKey];
        if (note.ModuleId == null || ModuleLabels == null || !ModuleLabels.TryGetValue(note.ModuleId.Value, out var label))
            return message;

        return $"{label}: {message}";
    }

    private static string Icon(TravelExpenseNoteSeverity severity) {
        return severity switch {
            TravelExpenseNoteSeverity.Problem => "bi-x-octagon",
            TravelExpenseNoteSeverity.Warning => "bi-exclamation-triangle",
            _ => "bi-info-circle"
        };
    }

    private static string TextClass(TravelExpenseNoteSeverity severity) {
        return severity switch {
            TravelExpenseNoteSeverity.Problem => "text-danger",
            TravelExpenseNoteSeverity.Warning => "text-warning-emphasis",
            _ => "text-secondary"
        };
    }
}
