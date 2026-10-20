using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.Forwarding;

/// <summary>
/// The forwarding settings of one chapter. The federal chapter always shows its three targets, because federal tickets are created whenever forwarding is on;
/// other chapters show a target only for a purpose they handle in Redmine.
/// </summary>
public partial class ForwardingChapterEditor {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public required ForwardingChapterSettingDTO Setting { get; set; }

    [Parameter]
    public EventCallback OnSaved { get; set; }

    [Parameter]
    public EventCallback OnCancel { get; set; }

    [Parameter]
    public string IdPrefix { get; set; } = "chapter";

    private string ApplicationMode = "";
    private string ReductionMode = "";
    private string FailureEmail = "";
    private readonly Dictionary<ForwardingPurpose, ForwardingTargetDTO> Targets = [];
    private RedmineLookupDTO? Lookups;
    private bool LookupFailed;
    private bool Busy;
    private ForwardingChapterSettingDTO? LoadedSetting;

    private bool DecidesOnReductions => Setting.IsFederal || Setting.IsStateChapter;

    // The parent passes the same object on every render; only a different one may reset unsaved input.
    protected override async Task OnParametersSetAsync() {
        if (ReferenceEquals(LoadedSetting, Setting))
            return;
        LoadedSetting = Setting;

        ApplicationMode = Setting.ApplicationMode == null ? "" : ((int)Setting.ApplicationMode).ToString();
        ReductionMode = Setting.ReductionMode == null ? "" : ((int)Setting.ReductionMode).ToString();
        FailureEmail = Setting.FailureEmail ?? "";
        Targets.Clear();
        foreach (var purpose in new[] { ForwardingPurpose.Regular, ForwardingPurpose.Reduction, ForwardingPurpose.Application }) {
            Targets[purpose] = Setting.Targets.FirstOrDefault(t => t.Purpose == purpose) ?? new ForwardingTargetDTO { Purpose = purpose };
        }

        if (Setting.HasConnection)
            await LoadLookupsAsync();
    }

    private async Task LoadLookupsAsync() {
        try {
            using var response = await Http.GetAsync($"/api/forwarding/redmine-lookups/{Setting.ChapterId}");
            if (response.IsSuccessStatusCode) {
                Lookups = await response.Content.ReadFromJsonAsync<RedmineLookupDTO>();
                LookupFailed = false;
            } else {
                Lookups = null;
                LookupFailed = true;
            }
        } catch (HttpRequestException) {
            Lookups = null;
            LookupFailed = true;
        }
    }

    private List<ForwardingPurpose> VisiblePurposes() {
        if (Setting.IsFederal)
            return [ForwardingPurpose.Regular, ForwardingPurpose.Reduction, ForwardingPurpose.Application];

        var purposes = new List<ForwardingPurpose>();
        if (ApplicationMode == ((int)ForwardingMode.Redmine).ToString())
            purposes.Add(ForwardingPurpose.Application);
        if (DecidesOnReductions && ReductionMode == ((int)ForwardingMode.Redmine).ToString())
            purposes.Add(ForwardingPurpose.Reduction);
        return purposes;
    }

    private string TargetTitle(ForwardingPurpose purpose) {
        return purpose switch {
            ForwardingPurpose.Regular => I18n[I18nKey.Ui.ForwardingSetup.TargetRegular],
            ForwardingPurpose.Reduction => I18n[I18nKey.Ui.ForwardingSetup.TargetReduction],
            _ => I18n[I18nKey.Ui.ForwardingSetup.TargetApplication]
        };
    }

    private static ForwardingMode? ParseMode(string value) {
        return int.TryParse(value, out var mode) ? (ForwardingMode)mode : null;
    }

    private async Task SaveAsync() {
        if (VisiblePurposes().Select(p => Targets[p]).Any(t => (t.ProjectId > 0) != (t.TrackerId > 0))) {
            ToastService.ToastKey(I18nKey.Error.Forwarding.TargetIncomplete, "danger");
            return;
        }

        var targets = VisiblePurposes()
            .Select(p => Targets[p])
            .Where(t => t.ProjectId > 0 && t.TrackerId > 0)
            .Select(t => new ForwardingTargetDTO {
                Purpose = t.Purpose,
                ProjectId = t.ProjectId,
                TrackerId = t.TrackerId,
                PriorityId = t.PriorityId,
                AssigneeId = t.AssigneeId,
                NoteStatusId = t.NoteStatusId,
                NoteAssigneeId = t.NoteAssigneeId,
                CustomFields = t.CustomFields.Where(f => f.FieldId > 0 && !string.IsNullOrWhiteSpace(f.Template)).ToList()
            })
            .ToList();

        Busy = true;
        try {
            using var response = await Http.PutAsJsonAsync($"/api/forwarding/chapters/{Setting.ChapterId}", new ForwardingChapterSaveRequest {
                ChapterId = Setting.ChapterId,
                ApplicationMode = ParseMode(ApplicationMode),
                ReductionMode = DecidesOnReductions ? ParseMode(ReductionMode) : null,
                FailureEmail = string.IsNullOrWhiteSpace(FailureEmail) ? null : FailureEmail.Trim(),
                Targets = targets
            });
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
            await OnSaved.InvokeAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }
}
