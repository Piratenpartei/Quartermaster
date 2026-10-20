using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.Forwarding;

/// <summary>The forwarding cases the user may handle: application cases with the global ProcessApplications permission, due selection cases with ProcessDueSelections.</summary>
public partial class ForwardingCases {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private bool Loading = true;
    private bool Busy;
    private bool Forbidden;
    private const int PageSize = 25;

    private string Filter = ((int)ForwardingCaseStatus.Failed).ToString();
    private List<ForwardingCaseListItemDTO> Cases = [];
    private int CurrentPage = 1;
    private int TotalCount;
    private ConfirmDialog ResolveDialog = default!;

    protected override async Task OnInitializedAsync() {
        await LoadAsync();
    }

    private int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    private async Task OnFilterChanged(string value) {
        Filter = value;
        CurrentPage = 1;
        await LoadAsync();
    }

    private async Task GoToPage(int page) {
        CurrentPage = page;
        await LoadAsync();
    }

    private async Task LoadAsync() {
        Loading = true;
        try {
            var status = Filter == "" ? "" : $"&Status={Enum.GetName((ForwardingCaseStatus)int.Parse(Filter))}";
            using var response = await Http.GetAsync($"/api/forwarding/cases?Page={CurrentPage}&PageSize={PageSize}{status}");
            Forbidden = response.StatusCode == HttpStatusCode.Forbidden;
            if (response.IsSuccessStatusCode) {
                var page = await response.Content.ReadFromJsonAsync<ForwardingCaseListResponse>();
                Cases = page?.Items ?? [];
                TotalCount = page?.TotalCount ?? 0;
            } else if (!Forbidden) {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Loading = false;
        }
    }

    private Task RetryAsync(Guid id) {
        return RunAsync(() => Http.PostAsJsonAsync($"/api/forwarding/cases/{id}/retry", new { }));
    }

    private async Task ResolveAsync(Guid id) {
        if (!await ResolveDialog.ShowAsync(I18n[I18nKey.Ui.ForwardingCases.ResolveConfirm]))
            return;

        await RunAsync(() => Http.PostAsJsonAsync($"/api/forwarding/cases/{id}/resolve", new { }));
    }

    private async Task RunAsync(Func<Task<HttpResponseMessage>> send) {
        Busy = true;
        try {
            using var response = await send();
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
            await LoadAsync();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private string KindLabel(ForwardingCaseKind kind) {
        return kind == ForwardingCaseKind.Application ? I18n[I18nKey.Ui.ForwardingCases.KindApplication] : I18n[I18nKey.Ui.ForwardingCases.KindDueSelection];
    }

    private string StatusLabel(ForwardingCaseStatus status) {
        return status switch {
            ForwardingCaseStatus.Done => I18n[I18nKey.Ui.ForwardingCases.StatusDone],
            ForwardingCaseStatus.Failed => I18n[I18nKey.Ui.ForwardingCases.StatusFailed],
            ForwardingCaseStatus.ManuallyResolved => I18n[I18nKey.Ui.ForwardingCases.StatusResolved],
            _ => I18n[I18nKey.Ui.ForwardingCases.StatusOpen]
        };
    }

    private static string StatusClass(ForwardingCaseStatus status) {
        return status switch {
            ForwardingCaseStatus.Done => "border-success text-success-emphasis",
            ForwardingCaseStatus.Failed => "border-danger text-danger-emphasis",
            ForwardingCaseStatus.ManuallyResolved => "border-secondary text-secondary-emphasis",
            _ => "border-info text-info-emphasis"
        };
    }

    private string StepLabel(ForwardingStepState state) {
        return state switch {
            ForwardingStepState.Waiting => I18n[I18nKey.Ui.ForwardingCases.StepWaiting],
            ForwardingStepState.InFlight => I18n[I18nKey.Ui.ForwardingCases.StepInFlight],
            ForwardingStepState.Done => I18n[I18nKey.Ui.ForwardingCases.StepDone],
            ForwardingStepState.Failed => I18n[I18nKey.Ui.ForwardingCases.StepFailed],
            ForwardingStepState.Skipped => I18n[I18nKey.Ui.ForwardingCases.StepSkipped],
            _ => I18n[I18nKey.Ui.ForwardingCases.StepPending]
        };
    }

    private static string StepClass(ForwardingStepState state) {
        return state switch {
            ForwardingStepState.Done => "border-success text-success-emphasis",
            ForwardingStepState.Failed => "border-danger text-danger-emphasis",
            ForwardingStepState.Skipped => "border-secondary text-secondary-emphasis",
            _ => "border-info text-info-emphasis"
        };
    }

    private string StepTitle(ForwardingStepDTO step) {
        if (step.Kind == ForwardingStepKind.AddOutcomeNote)
            return I18n[I18nKey.Ui.ForwardingCases.NoteStep];

        return step.Purpose switch {
            ForwardingPurpose.Application => I18n[I18nKey.Ui.ForwardingCases.PurposeApplication],
            ForwardingPurpose.Reduction => I18n[I18nKey.Ui.ForwardingCases.PurposeReduction],
            _ => I18n[I18nKey.Ui.ForwardingCases.PurposeRegular]
        };
    }
}
