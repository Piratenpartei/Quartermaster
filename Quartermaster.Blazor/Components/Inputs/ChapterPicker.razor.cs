using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.I18n;

namespace Quartermaster.Blazor.Components.Inputs;

public partial class ChapterPicker {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public string Value { get; set; } = "";

    [Parameter]
    public EventCallback<string> ValueChanged { get; set; }

    [Parameter]
    public bool AllowEmpty { get; set; }

    [Parameter]
    public string? EmptyLabel { get; set; }

    private string ResolvedEmptyLabel => EmptyLabel ?? I18n[I18nKey.Ui.ChapterPicker.EmptyLabel];

    [Parameter]
    public bool Inline { get; set; }

    [Parameter]
    public string SizeClass { get; set; } = "";

    /// <summary>Chapter ids to hide from the selectable list (e.g. the chapter being edited to prevent self-as-parent).</summary>
    [Parameter]
    public List<Guid>? ExcludeIds { get; set; }

    private List<ChapterDTO>? Chapters;
    private string SearchText = "";
    private bool ShowDropdown;

    private List<ChapterDTO> VisibleChapters {
        get {
            if (Chapters == null) {
                return new();
            }
            if (ExcludeIds == null || ExcludeIds.Count == 0) {
                return Chapters;
            }
            var excluded = new HashSet<Guid>(ExcludeIds);
            return Chapters.Where(c => !excluded.Contains(c.Id)).ToList();
        }
    }

    private List<ChapterDTO> FilteredChapters {
        get {
            var source = VisibleChapters;
            if (string.IsNullOrWhiteSpace(SearchText)) {
                return source;
            }
            return source.Where(c =>
                c.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || (c.ShortCode != null && c.ShortCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                || (c.ExternalCode != null && c.ExternalCode.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }
    }

    protected override async Task OnInitializedAsync() {
        Chapters = await Http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters") ?? new();

        // If a value is already set, show its name in the search box
        if (!string.IsNullOrEmpty(Value) && Guid.TryParse(Value, out var id) && Chapters != null) {
            var selected = Chapters.FirstOrDefault(c => c.Id == id);
            if (selected != null)
                SearchText = selected.Name;
        }
    }

    private async Task OnDropdownChanged(ChangeEventArgs e) {
        Value = e.Value?.ToString() ?? "";
        await ValueChanged.InvokeAsync(Value);
    }

    private void OnSearchInput(ChangeEventArgs e) {
        SearchText = e.Value?.ToString() ?? "";
        ShowDropdown = true;
        StateHasChanged();
    }

    private async Task SelectChapter(ChapterDTO chapter) {
        Value = chapter.Id.ToString();
        SearchText = chapter.Name;
        ShowDropdown = false;
        await ValueChanged.InvokeAsync(Value);
    }

    private async Task Clear() {
        Value = "";
        SearchText = "";
        ShowDropdown = false;
        await ValueChanged.InvokeAsync(Value);
    }

    private async Task ScheduleClose() {
        await Task.Delay(200);
        ShowDropdown = false;
        StateHasChanged();
    }
}
