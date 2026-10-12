using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public partial class AssemblyMotionRevisionHistory {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required AuthService AuthService { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public Guid MotionId { get; set; }

    [Parameter]
    public int CurrentRevision { get; set; }

    private List<AssemblyMotionRevisionDTO> Revisions = [];
    private int SelectedRevision;
    private Guid loadedMotionId;
    private int loadedRevision = -1;

    private AssemblyMotionRevisionDTO? Current => Revisions.FirstOrDefault(r => r.Revision == SelectedRevision);

    private AssemblyMotionRevisionDTO? Previous => Revisions
        .Where(r => r.Revision < SelectedRevision)
        .OrderByDescending(r => r.Revision)
        .FirstOrDefault();

    private List<TextDiffLine> TextDiffLines = [];
    private List<TextDiffLine> ReasonDiffLines = [];

    private string DiffTitle => Current == null || Previous == null
        ? ""
        : I18n[I18nParams.With(I18nKey.Ui.AssemblyMotion.DiffTitle,
            ("from", Previous.Revision.ToString()),
            ("to", Current.Revision.ToString()))];

    protected override async Task OnParametersSetAsync() {
        if (loadedMotionId == MotionId && loadedRevision == CurrentRevision)
            return;

        loadedMotionId = MotionId;
        loadedRevision = CurrentRevision;
        await LoadAsync();
    }

    private async Task LoadAsync() {
        Revisions = [];
        try {
            await AuthService.WaitForInitializationAsync();
            if (!AuthService.IsAuthenticated)
                return;

            var response = await Http.GetAsync($"/api/assemblymotions/motions/{MotionId}/revisions");
            if (response.IsSuccessStatusCode) {
                var loaded = await response.Content.ReadFromJsonAsync<List<AssemblyMotionRevisionDTO>>() ?? [];
                Revisions = loaded
                    .OrderByDescending(r => r.Revision)
                    .ToList();
                Select(Revisions.Count > 0 ? Revisions[0].Revision : 0);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private void Select(int revision) {
        SelectedRevision = revision;
        var current = Current;
        var previous = Previous;
        if (current == null || previous == null) {
            TextDiffLines = [];
            ReasonDiffLines = [];
        } else {
            TextDiffLines = TextDiff.Lines(previous.TextMarkdown, current.TextMarkdown);
            ReasonDiffLines = TextDiff.Lines(previous.ReasonMarkdown, current.ReasonMarkdown);
        }
    }
}
