using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Quartermaster.Api.Banking;
using Quartermaster.Api.Forms;
using Quartermaster.Api.I18n;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Components.Forms;
using Quartermaster.Blazor.Pages.Forms.TravelExpenses;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Forms;

/// <summary>
/// The travel expense wizard. Logged-in users work on a server draft, anonymous users on a draft in browser storage or
/// only in memory; amounts are calculated live with the rules of the chapter travelled for.
/// </summary>
public partial class TravelExpense : IAsyncDisposable {
    private const string FormKey = "travel-expense";
    private const string PdfFileName = "reisekosten.pdf";
    private static readonly TimeSpan AutosaveDelay = TimeSpan.FromSeconds(1.5);
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required IJSRuntime JS { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }
    [Inject]
    public required NavigationManager Navigation { get; set; }
    [Inject]
    public required AuthService AuthService { get; set; }

    [Parameter]
    public Guid? Id { get; set; }

    [SupplyParameterFromQuery(Name = "localId")]
    public Guid? LocalId { get; set; }

    private AnonymousFormGate Gate = default!;
    private ConfirmDialog DeleteConfirm = default!;
    private TravelExpenseInputDTO Input = new();
    private TravelExpenseRulesDTO? Rules;
    private TravelExpenseCalculationDTO Calculation = new();
    private List<WizardStep> Steps = [];
    private bool Loading = true;
    private bool GateBlocked;
    private bool Busy;
    private bool IsSubmitted;
    private DateTimeOffset? SubmittedAt;
    private bool _needsAnonymousGate;
    private FormBackingMode Mode;
    private LocalDraftEnvelope<TravelExpenseInputDTO>? _localEnvelope;
    private (Guid? ChapterId, DateOnly Date)? _rulesFor;
    private int _autosaveVersion;
    private bool _autosavePending;
    private DateTime? LastSavedAt;

    private bool HasPersistentBacking => Mode == FormBackingMode.Server || Mode == FormBackingMode.Local;

    private enum FormBackingMode {
        Ephemeral,
        Server,
        Local
    }

    protected override async Task OnParametersSetAsync() {
        Loading = true;
        IsSubmitted = false;
        SubmittedAt = null;
        _needsAnonymousGate = false;

        if (Id.HasValue) {
            if (!AuthService.IsAuthenticated) {
                Navigation.NavigateTo("/Login", replace: true);
                return;
            }
            Mode = FormBackingMode.Server;
            await LoadServerDraft();
            await ReadyAsync();
            return;
        }
        if (LocalId.HasValue) {
            Mode = FormBackingMode.Local;
            await LoadLocalDraft(LocalId.Value);
            await ReadyAsync();
            return;
        }
        if (AuthService.IsAuthenticated) {
            Mode = FormBackingMode.Server;
            await CreateServerDraft();
            return;
        }

        _needsAnonymousGate = true;
        GateBlocked = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender) {
        if (!firstRender || !_needsAnonymousGate)
            return;

        _needsAnonymousGate = false;
        var choice = await Gate.ShowAsync();
        GateBlocked = false;
        switch (choice) {
            case GateChoice.Cancel:
                Navigation.NavigateTo("/");
                return;
            case GateChoice.AcceptWithStorage:
                await CreateLocalDraftAndRedirect();
                return;
            case GateChoice.AcceptInMemory:
                Mode = FormBackingMode.Ephemeral;
                await ReadyAsync();
                StateHasChanged();
                break;
        }
    }

    private async Task ReadyAsync() {
        await RefreshRulesAsync();
        BuildSteps();
        Loading = false;
    }

    private void BuildSteps() {
        var canUseReceipts = Mode == FormBackingMode.Server;
        Steps = [
            Step(I18nKey.Ui.TravelExpense.StepPerson, builder => {
                builder.OpenComponent<TravelExpensePersonStep>(0);
                builder.AddComponentParameter(1, nameof(TravelExpensePersonStep.Input), Input);
                builder.CloseComponent();
            }, IsPersonComplete, I18nKey.Ui.TravelExpense.PersonRequired),
            Step(I18nKey.Ui.TravelExpense.StepChapter, builder => {
                builder.OpenComponent<TravelExpenseChapterStep>(0);
                builder.AddComponentParameter(1, nameof(TravelExpenseChapterStep.Input), Input);
                builder.AddComponentParameter(2, nameof(TravelExpenseChapterStep.Rules), CurrentRules());
                builder.AddComponentParameter(3, nameof(TravelExpenseChapterStep.OnChanged), EventCallback.Factory.Create(this, OnInputChangedAsync));
                builder.CloseComponent();
            }, () => true, null),
            Step(I18nKey.Ui.TravelExpense.StepTrip, builder => {
                builder.OpenComponent<TravelExpenseTripStep>(0);
                builder.AddComponentParameter(1, nameof(TravelExpenseTripStep.Input), Input);
                builder.CloseComponent();
            }, () => true, null),
            Step(I18nKey.Ui.TravelExpense.StepBuilder, builder => {
                builder.OpenComponent<TripBuilder>(0);
                builder.AddComponentParameter(1, nameof(TripBuilder.Input), Input);
                builder.AddComponentParameter(2, nameof(TripBuilder.Rules), CurrentRules());
                builder.AddComponentParameter(3, nameof(TripBuilder.Calculation), Calculation);
                builder.AddComponentParameter(4, nameof(TripBuilder.CanUseReceipts), canUseReceipts);
                builder.AddComponentParameter(5, nameof(TripBuilder.OnChanged), EventCallback.Factory.Create(this, OnInputChangedAsync));
                builder.CloseComponent();
            }, AreTimesValid, I18nKey.Ui.TravelExpense.TimesRequired),
            Step(I18nKey.Ui.TravelExpense.StepPayout, builder => {
                builder.OpenComponent<TravelExpensePayoutStep>(0);
                builder.AddComponentParameter(1, nameof(TravelExpensePayoutStep.Input), Input);
                builder.AddComponentParameter(2, nameof(TravelExpensePayoutStep.Calculation), Calculation);
                builder.AddComponentParameter(3, nameof(TravelExpensePayoutStep.CanUseReceipts), canUseReceipts);
                builder.AddComponentParameter(4, nameof(TravelExpensePayoutStep.OnChanged), EventCallback.Factory.Create(this, OnInputChangedAsync));
                builder.CloseComponent();
            }, IsIbanAcceptable, I18nKey.Ui.TravelExpense.Note.IbanInvalid),
            Step(I18nKey.Ui.TravelExpense.StepOverview, builder => {
                builder.OpenComponent<TravelExpenseOverviewStep>(0);
                builder.AddComponentParameter(1, nameof(TravelExpenseOverviewStep.Input), Input);
                builder.AddComponentParameter(2, nameof(TravelExpenseOverviewStep.Rules), CurrentRules());
                builder.AddComponentParameter(3, nameof(TravelExpenseOverviewStep.Calculation), Calculation);
                builder.CloseComponent();
            }, () => true, null)
        ];
    }

    private WizardStep Step(string titleKey, RenderFragment content, Func<bool> isComplete, string? incompleteKey) {
        return new WizardStep {
            Title = I18n[titleKey],
            Content = content,
            IsValid = isComplete,
            Validate = () => {
                if (isComplete())
                    return Task.FromResult(true);
                if (incompleteKey != null)
                    ToastService.ErrorKey(incompleteKey);

                return Task.FromResult(false);
            }
        };
    }

    private bool IsPersonComplete() {
        return !string.IsNullOrWhiteSpace(Input.FirstName) && !string.IsNullOrWhiteSpace(Input.LastName);
    }

    private bool AreTimesValid() {
        return Input.Start.At != null && Input.End.At != null && Input.End.At > Input.Start.At;
    }

    // An IBAN still empty is a note in the overview; a wrong one is caught here before the PDF is made.
    private bool IsIbanAcceptable() {
        return string.IsNullOrWhiteSpace(Input.Iban) || Iban.IsValid(Input.Iban);
    }

    public TravelExpenseRulesDTO CurrentRules() {
        return Rules ?? TravelExpenseRulePresets.Federal();
    }

    private async Task OnInputChangedAsync() {
        await RefreshRulesAsync();
        Recalculate();
        ScheduleAutosave();
    }

    /// <summary>Saves a moment after the last change, so a burst of edits costs one request.</summary>
    private void ScheduleAutosave() {
        if (!HasPersistentBacking || IsSubmitted)
            return;

        var version = ++_autosaveVersion;
        _autosavePending = true;
        _ = AutosaveAfterDelayAsync(version);
    }

    private async Task AutosaveAfterDelayAsync(int version) {
        await Task.Delay(AutosaveDelay);
        if (version != _autosaveVersion || !_autosavePending)
            return;

        await SaveDraft();
        await InvokeAsync(StateHasChanged);
    }

    // Leaving the page right after an edit still keeps it.
    public async ValueTask DisposeAsync() {
        if (_autosavePending)
            await SaveDraft();
    }

    // The step fragments read Input, Rules and Calculation when they render, so the steps stay built once.
    private void Recalculate() {
        Calculation = TravelExpenseCalculator.Calculate(Input, CurrentRules(), DateOnly.FromDateTime(DateTime.Today));
    }

    /// <summary>Loads the rules again only when the chapter travelled for or the trip's start day changed.</summary>
    private async Task RefreshRulesAsync() {
        var date = Input.Start.At == null ? DateOnly.FromDateTime(DateTime.Today) : DateOnly.FromDateTime(Input.Start.At.Value);
        var key = (Input.TravelChapterId, date);
        if (_rulesFor == key && Rules != null) {
            Recalculate();
            return;
        }

        _rulesFor = key;
        var query = $"date={date:yyyy-MM-dd}" + (Input.TravelChapterId == null ? "" : $"&chapterId={Input.TravelChapterId}");
        try {
            Rules = await Http.GetFromJsonAsync<TravelExpenseRulesDTO>($"/api/forms/travel-expense/rules?{query}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
        Recalculate();
    }

    private async Task CreateServerDraft() {
        try {
            var response = await Http.PostAsJsonAsync("/api/forms/travel-expense", new TravelExpenseInputDTO());
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }
            var created = await response.Content.ReadFromJsonAsync<TravelExpenseResponseDTO>();
            if (created != null)
                Navigation.NavigateTo($"/Forms/TravelExpense/{created.Id}", replace: true);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task CreateLocalDraftAndRedirect() {
        var now = DateTimeOffset.UtcNow;
        _localEnvelope = new LocalDraftEnvelope<TravelExpenseInputDTO> {
            Id = Guid.NewGuid(),
            Status = LocalDraftStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
            Data = Input
        };
        await SaveLocalEnvelope();
        Navigation.NavigateTo($"/Forms/TravelExpense?localId={_localEnvelope.Id}", replace: true);
    }

    private async Task LoadServerDraft() {
        try {
            var response = await Http.GetFromJsonAsync<TravelExpenseResponseDTO>($"/api/forms/travel-expense/{Id}");
            if (response == null)
                return;

            Input = response.Input;
            IsSubmitted = response.Status == TravelExpenseStatusDTO.Submitted;
            SubmittedAt = response.SubmittedAt;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task LoadLocalDraft(Guid id) {
        try {
            var raw = await JS.InvokeAsync<string?>("qmStorage.load", LocalDraftKeys.For(FormKey, id));
            if (string.IsNullOrEmpty(raw)) {
                ToastService.Error(I18n[I18nKey.Ui.MyForms.EmptyStateLocal]);
                Navigation.NavigateTo("/Forms/Mine");
                return;
            }
            _localEnvelope = JsonSerializer.Deserialize<LocalDraftEnvelope<TravelExpenseInputDTO>>(raw);
            if (_localEnvelope?.Data == null)
                return;

            Input = _localEnvelope.Data;
            IsSubmitted = _localEnvelope.Status == LocalDraftStatus.Submitted;
            SubmittedAt = _localEnvelope.SubmittedAt;
        } catch (Exception ex) when (ex is JsonException || ex is JSException) {
            ToastService.Error(ex.Message);
        }
    }

    private async Task SaveLocalEnvelope() {
        if (_localEnvelope == null)
            return;

        _localEnvelope.UpdatedAt = DateTimeOffset.UtcNow;
        _localEnvelope.Summary = new LocalDraftSummary { Chapter = Input.TravelChapterName, Total = Calculation.Total };
        _localEnvelope.Data = Input;
        try {
            await JS.InvokeVoidAsync("qmStorage.save", LocalDraftKeys.For(FormKey, _localEnvelope.Id), JsonSerializer.Serialize(_localEnvelope));
            LastSavedAt = DateTime.Now;
        } catch (JSException ex) {
            ToastService.Error(ex.Message);
        }
    }

    private Task OnAdvance(int stepIndex) {
        Input.ResumeStep = Math.Max(Input.ResumeStep, stepIndex + 1);
        return SaveDraft();
    }

    private async Task SaveDraft() {
        _autosavePending = false;
        if (Mode == FormBackingMode.Local) {
            await SaveLocalEnvelope();
            return;
        }
        if (Mode != FormBackingMode.Server || Id == null)
            return;

        try {
            var response = await Http.PutAsJsonAsync($"/api/forms/travel-expense/{Id}", Input);
            if (response.IsSuccessStatusCode) {
                LastSavedAt = DateTime.Now;
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task Submit() {
        Busy = true;
        try {
            var bytes = await SubmitAndRenderAsync();
            if (bytes.Length > 0)
                await JS.InvokeVoidAsync("qmDownload.blob", bytes, PdfFileName, "application/pdf");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task<byte[]> SubmitAndRenderAsync() {
        if (Mode == FormBackingMode.Server) {
            await SaveDraft();
            var response = await Http.PostAsJsonAsync($"/api/forms/travel-expense/{Id}/submit", new { });
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return [];
            }
            IsSubmitted = true;
            SubmittedAt = DateTimeOffset.UtcNow;
            return await response.Content.ReadAsByteArrayAsync();
        }

        var bytes = await RenderAnonymousAsync();
        if (bytes.Length == 0 || Mode != FormBackingMode.Local || _localEnvelope == null)
            return bytes;

        _localEnvelope.Status = LocalDraftStatus.Submitted;
        _localEnvelope.SubmittedAt = DateTimeOffset.UtcNow;
        await SaveLocalEnvelope();
        IsSubmitted = true;
        SubmittedAt = _localEnvelope.SubmittedAt;
        return bytes;
    }

    private async Task<byte[]> RenderAnonymousAsync() {
        var response = await Http.PostAsJsonAsync("/api/forms/travel-expense/render", Input);
        if (response.StatusCode == HttpStatusCode.TooManyRequests) {
            ToastService.ErrorKey(I18nKey.Error.PublicForm.PdfRateLimited);
            return [];
        }
        if (!response.IsSuccessStatusCode) {
            await ToastService.ErrorAsync(response);
            return [];
        }
        return await response.Content.ReadAsByteArrayAsync();
    }

    private async Task DownloadAgain() {
        Busy = true;
        try {
            byte[] bytes;
            if (Mode == FormBackingMode.Server && Id.HasValue) {
                var response = await Http.GetAsync($"/api/forms/travel-expense/{Id}/pdf");
                if (!response.IsSuccessStatusCode) {
                    await ToastService.ErrorAsync(response);
                    return;
                }
                bytes = await response.Content.ReadAsByteArrayAsync();
            } else {
                bytes = await RenderAnonymousAsync();
            }
            if (bytes.Length > 0)
                await JS.InvokeVoidAsync("qmDownload.blob", bytes, PdfFileName, "application/pdf");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task CopyToDraft() {
        Busy = true;
        try {
            if (Mode == FormBackingMode.Server && Id.HasValue) {
                var response = await Http.PostAsJsonAsync($"/api/forms/travel-expense/{Id}/copy", new { });
                if (!response.IsSuccessStatusCode) {
                    await ToastService.ErrorAsync(response);
                    return;
                }
                var created = await response.Content.ReadFromJsonAsync<TravelExpenseResponseDTO>();
                if (created != null)
                    Navigation.NavigateTo($"/Forms/TravelExpense/{created.Id}");
                return;
            }
            if (Mode != FormBackingMode.Local)
                return;

            var now = DateTimeOffset.UtcNow;
            Input.ResumeStep = 0;
            foreach (var module in Input.Modules)
                module.Id = Guid.NewGuid();
            var clone = new LocalDraftEnvelope<TravelExpenseInputDTO> {
                Id = Guid.NewGuid(),
                Status = LocalDraftStatus.Draft,
                CreatedAt = now,
                UpdatedAt = now,
                Summary = new LocalDraftSummary { Chapter = Input.TravelChapterName, Total = Calculation.Total },
                Data = Input
            };
            await JS.InvokeVoidAsync("qmStorage.save", LocalDraftKeys.For(FormKey, clone.Id), JsonSerializer.Serialize(clone));
            Navigation.NavigateTo($"/Forms/TravelExpense?localId={clone.Id}");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private async Task DeleteDraft() {
        if (!await DeleteConfirm.ShowAsync())
            return;

        Busy = true;
        try {
            if (Mode == FormBackingMode.Server && Id.HasValue) {
                var response = await Http.DeleteAsync($"/api/forms/travel-expense/{Id}");
                if (!response.IsSuccessStatusCode) {
                    await ToastService.ErrorAsync(response);
                    return;
                }
            } else if (Mode == FormBackingMode.Local && LocalId.HasValue) {
                await JS.InvokeVoidAsync("qmStorage.clear", LocalDraftKeys.For(FormKey, LocalId.Value));
            }
            Navigation.NavigateTo("/Forms/Mine");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
    }

    private string SavedText() {
        return LastSavedAt == null ? "" : I18n[I18nParams.With(I18nKey.Ui.TravelExpense.SavedAt, ("time", LastSavedAt.Value.ToString("HH:mm", German)))];
    }

    private string SubmittedText() {
        return SubmittedAt?.ToLocalTime().ToString("dd.MM.yyyy HH:mm", German) ?? "";
    }
}
