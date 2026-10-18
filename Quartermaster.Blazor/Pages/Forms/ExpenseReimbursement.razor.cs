using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Quartermaster.Api.Forms;
using Quartermaster.Api.Forms.ExpenseReimbursements;
using Quartermaster.Api.Banking;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components.Forms;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Forms;

public partial class ExpenseReimbursement {
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

    private static readonly CultureInfo De = CultureInfo.GetCultureInfo("de-DE");
    private const string FormKey = "expense-reimbursement";

    private AnonymousFormGate Gate = default!;
    private ExpenseReimbursementInputDTO Input = NewEmptyInput();
    private List<WizardStep> Steps = new();
    private bool Loading = true;
    private bool GateBlocked;
    private bool Busy;
    private bool IsSubmitted;
    private DateTimeOffset? SubmittedAt;
    private bool _needsAnonymousGate;
    private FormBackingMode Mode;
    private LocalDraftEnvelope<ExpenseReimbursementInputDTO>? _localEnvelope;

    private bool HasPersistentBacking => Mode is FormBackingMode.Server or FormBackingMode.Local;
    private string ServerOrLocalKey => $"{LocalDraftKeys.PrefixFor(FormKey)}{LocalId}";

    private static readonly string[] FunctionSuggestions = new[] {
        "Vorsitz", "Stellvertretender Vorsitz",
        "Schatzmeister", "Stellvertretender Schatzmeister",
        "Politischer Geschäftsführer", "Generalsekretär",
        "Beauftragter"
    };

    private decimal LineItemTotal => Input.LineItems.Sum(i => i.Value);
    private decimal SplitSum => Input.DonatedAmount + Input.PayoutAmount;
    private string SplitClass => SplitSum == LineItemTotal ? "text-secondary" : "text-danger";

    private bool PayoutValid =>
        SplitSum == LineItemTotal
        && (Input.PayoutAmount == 0m
            || (Input.PayoutMethod == ExpensePayoutMethodDTO.Cash)
            || (Input.PayoutMethod == ExpensePayoutMethodDTO.BankTransfer && Iban.IsValid(Input.Iban)));

    private string ChapterIdString {
        get => Input.ChapterId?.ToString() ?? "";
        set { /* assigned via OnChapterPicked */ }
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
            BuildSteps();
            Loading = false;
            return;
        }

        if (LocalId.HasValue) {
            Mode = FormBackingMode.Local;
            await LoadLocalDraft(LocalId.Value);
            BuildSteps();
            Loading = false;
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
        if (!firstRender || !_needsAnonymousGate) {
            return;
        }
        _needsAnonymousGate = false;
        await RunAnonymousGate();
    }

    private async Task RunAnonymousGate() {
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
                BuildSteps();
                Loading = false;
                StateHasChanged();
                break;
        }
    }

    private async Task CreateServerDraft() {
        try {
            var resp = await Http.PostAsJsonAsync("/api/forms/expense-reimbursement", new { });
            if (!resp.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(resp);
                return;
            }
            var created = await resp.Content.ReadFromJsonAsync<ExpenseReimbursementResponseDTO>();
            if (created != null) {
                Navigation.NavigateTo($"/Formulare/auslagenerstattung/{created.Id}", replace: true);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task CreateLocalDraftAndRedirect() {
        var newId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        _localEnvelope = new LocalDraftEnvelope<ExpenseReimbursementInputDTO> {
            Id = newId,
            Status = LocalDraftStatus.Draft,
            CreatedAt = now,
            UpdatedAt = now,
            Summary = new LocalDraftSummary(),
            Data = Input
        };
        await SaveLocalEnvelope();
        Navigation.NavigateTo($"/Formulare/auslagenerstattung?localId={newId}", replace: true);
    }

    private async Task LoadServerDraft() {
        try {
            var resp = await Http.GetFromJsonAsync<ExpenseReimbursementResponseDTO>($"/api/forms/expense-reimbursement/{Id}");
            if (resp != null) {
                Input = resp.Input;
                IsSubmitted = resp.Status == ExpenseReimbursementStatusDTO.Submitted;
                SubmittedAt = resp.SubmittedAt;
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task LoadLocalDraft(Guid id) {
        try {
            var raw = await JS.InvokeAsync<string?>("qmStorage.load", LocalDraftKeys.For(FormKey, id));
            if (string.IsNullOrEmpty(raw)) {
                ToastService.Error(I18n[I18nKey.Ui.MyForms.EmptyStateLocal]);
                Navigation.NavigateTo("/Formulare/meine");
                return;
            }
            _localEnvelope = JsonSerializer.Deserialize<LocalDraftEnvelope<ExpenseReimbursementInputDTO>>(raw);
            if (_localEnvelope?.Data != null) {
                Input = _localEnvelope.Data;
                IsSubmitted = _localEnvelope.Status == LocalDraftStatus.Submitted;
                SubmittedAt = _localEnvelope.SubmittedAt;
            }
        } catch (Exception ex) {
            ToastService.Error(ex.Message);
        }
    }

    private async Task SaveLocalEnvelope() {
        if (_localEnvelope == null) {
            return;
        }
        _localEnvelope.UpdatedAt = DateTimeOffset.UtcNow;
        _localEnvelope.Summary = new LocalDraftSummary {
            Chapter = Input.ChapterName,
            Total = LineItemTotal
        };
        _localEnvelope.Data = Input;
        try {
            var json = JsonSerializer.Serialize(_localEnvelope);
            await JS.InvokeVoidAsync("qmStorage.save", LocalDraftKeys.For(FormKey, _localEnvelope.Id), json);
        } catch (Exception ex) {
            ToastService.Error(ex.Message);
        }
    }

    private void BuildSteps() {
        Steps = new List<WizardStep> {
            new() {
                Title = I18n[I18nKey.Ui.ExpenseReimbursement.Step1Title],
                Content = Step1Personal,
                Validate = () => Task.FromResult(ValidatePersonal(surfaceErrors: true)),
                IsValid = () => ValidatePersonal(surfaceErrors: false)
            },
            new() {
                Title = I18n[I18nKey.Ui.ExpenseReimbursement.Step2Title],
                Content = Step2Chapter,
                Validate = () => Task.FromResult(ValidateChapter(surfaceErrors: true)),
                IsValid = () => ValidateChapter(surfaceErrors: false)
            },
            new() {
                Title = I18n[I18nKey.Ui.ExpenseReimbursement.Step3Title],
                Content = Step3LineItems,
                Validate = () => Task.FromResult(ValidateLineItems(surfaceErrors: true)),
                IsValid = () => ValidateLineItems(surfaceErrors: false)
            },
            new() {
                Title = I18n[I18nKey.Ui.ExpenseReimbursement.Step4Title],
                Content = Step4Payout,
                Validate = () => Task.FromResult(ValidatePayout(surfaceErrors: true)),
                IsValid = () => ValidatePayout(surfaceErrors: false)
            },
            new() {
                Title = I18n[I18nKey.Ui.ExpenseReimbursement.Step5Title],
                Content = Step5Overview,
                IsValid = () => true
            }
        };
    }

    private bool ValidatePersonal(bool surfaceErrors) {
        var ok = !string.IsNullOrWhiteSpace(Input.FirstName)
            && !string.IsNullOrWhiteSpace(Input.LastName)
            && !string.IsNullOrWhiteSpace(Input.Street)
            && !string.IsNullOrWhiteSpace(Input.City);
        if (!ok && surfaceErrors) {
            ToastService.Error(I18n[I18nKey.Ui.ExpenseReimbursement.ValidationPersonalRequired]);
        }
        return ok;
    }

    private bool ValidateChapter(bool surfaceErrors) {
        var ok = !string.IsNullOrWhiteSpace(Input.ChapterName)
            && !string.IsNullOrWhiteSpace(Input.Function)
            && Input.MotionDate != default;
        if (!ok && surfaceErrors) {
            ToastService.Error(I18n[I18nKey.Ui.ExpenseReimbursement.ValidationChapterRequired]);
        }
        return ok;
    }

    private bool ValidateLineItems(bool surfaceErrors) {
        var ok = Input.LineItems.Any(i => i.Value > 0m && !string.IsNullOrWhiteSpace(i.Description));
        if (!ok && surfaceErrors) {
            ToastService.Error(I18n[I18nKey.Ui.ExpenseReimbursement.ValidationLineItemsRequired]);
        }
        return ok;
    }

    private bool ValidatePayout(bool surfaceErrors) {
        if (SplitSum != LineItemTotal) {
            if (surfaceErrors) {
                ToastService.Error(I18n[I18nKey.Ui.ExpenseReimbursement.ValidationSplitMismatch]);
            }
            return false;
        }
        if (Input.PayoutMethod != ExpensePayoutMethodDTO.BankTransfer) {
            return true;
        }
        if (string.IsNullOrWhiteSpace(Input.Iban)) {
            if (surfaceErrors) {
                ToastService.Error(I18n[I18nKey.Ui.ExpenseReimbursement.ValidationIbanRequired]);
            }
            return false;
        }
        if (!Iban.IsValid(Input.Iban)) {
            if (surfaceErrors) {
                ToastService.Error(I18n[I18nKey.Ui.PaymentOptionSelection.IbanInvalid]);
            }
            return false;
        }
        return true;
    }

    private async Task OnAdvance(int stepIndex) {
        await SaveDraft();
    }

    private async Task SaveDraft() {
        switch (Mode) {
            case FormBackingMode.Server:
                if (Id == null) {
                    return;
                }
                try {
                    var resp = await Http.PutAsJsonAsync($"/api/forms/expense-reimbursement/{Id}", Input);
                    if (!resp.IsSuccessStatusCode) {
                        await ToastService.ErrorAsync(resp);
                    }
                } catch (HttpRequestException ex) {
                    ToastService.Error(ex);
                }
                break;
            case FormBackingMode.Local:
                await SaveLocalEnvelope();
                break;
            case FormBackingMode.Ephemeral:
                break;
        }
    }

    private async Task Submit() {
        Busy = true;
        StateHasChanged();
        try {
            byte[] bytes;
            switch (Mode) {
                case FormBackingMode.Server:
                    await SaveDraft();
                    var serverResp = await Http.PostAsJsonAsync($"/api/forms/expense-reimbursement/{Id}/submit", new { });
                    if (!serverResp.IsSuccessStatusCode) {
                        await ToastService.ErrorAsync(serverResp);
                        return;
                    }
                    bytes = await serverResp.Content.ReadAsByteArrayAsync();
                    IsSubmitted = true;
                    SubmittedAt = DateTimeOffset.UtcNow;
                    break;
                case FormBackingMode.Local:
                    bytes = await RenderAnonymous(Input);
                    if (bytes.Length == 0) {
                        return;
                    }
                    if (_localEnvelope != null) {
                        _localEnvelope.Status = LocalDraftStatus.Submitted;
                        _localEnvelope.SubmittedAt = DateTimeOffset.UtcNow;
                        await SaveLocalEnvelope();
                    }
                    IsSubmitted = true;
                    SubmittedAt = DateTimeOffset.UtcNow;
                    break;
                default:
                    bytes = await RenderAnonymous(Input);
                    if (bytes.Length == 0) {
                        return;
                    }
                    break;
            }
            await JS.InvokeVoidAsync("qmDownload.blob", bytes, "auslagenerstattung.pdf", "application/pdf");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
            StateHasChanged();
        }
    }

    private async Task<byte[]> RenderAnonymous(ExpenseReimbursementInputDTO input) {
        var resp = await Http.PostAsJsonAsync("/api/forms/expense-reimbursement/render", input);
        if (!resp.IsSuccessStatusCode) {
            await ToastService.ErrorAsync(resp);
            return Array.Empty<byte>();
        }
        return await resp.Content.ReadAsByteArrayAsync();
    }

    private async Task DownloadAgain() {
        Busy = true;
        StateHasChanged();
        try {
            byte[] bytes;
            if (Mode == FormBackingMode.Server && Id.HasValue) {
                var resp = await Http.GetAsync($"/api/forms/expense-reimbursement/{Id}/pdf");
                if (!resp.IsSuccessStatusCode) {
                    await ToastService.ErrorAsync(resp);
                    return;
                }
                bytes = await resp.Content.ReadAsByteArrayAsync();
            } else {
                bytes = await RenderAnonymous(Input);
                if (bytes.Length == 0) {
                    return;
                }
            }
            await JS.InvokeVoidAsync("qmDownload.blob", bytes, "auslagenerstattung.pdf", "application/pdf");
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
            StateHasChanged();
        }
    }

    private async Task CopyToDraft() {
        Busy = true;
        StateHasChanged();
        try {
            switch (Mode) {
                case FormBackingMode.Server when Id.HasValue:
                    var resp = await Http.PostAsJsonAsync($"/api/forms/expense-reimbursement/{Id}/copy", new { });
                    if (!resp.IsSuccessStatusCode) {
                        await ToastService.ErrorAsync(resp);
                        return;
                    }
                    var created = await resp.Content.ReadFromJsonAsync<ExpenseReimbursementResponseDTO>();
                    if (created != null) {
                        Navigation.NavigateTo($"/Formulare/auslagenerstattung/{created.Id}");
                    }
                    break;
                case FormBackingMode.Local:
                    var newId = Guid.NewGuid();
                    var now = DateTimeOffset.UtcNow;
                    var clone = new LocalDraftEnvelope<ExpenseReimbursementInputDTO> {
                        Id = newId,
                        Status = LocalDraftStatus.Draft,
                        CreatedAt = now,
                        UpdatedAt = now,
                        Summary = new LocalDraftSummary {
                            Chapter = Input.ChapterName,
                            Total = LineItemTotal
                        },
                        Data = Input
                    };
                    var json = JsonSerializer.Serialize(clone);
                    await JS.InvokeVoidAsync("qmStorage.save", LocalDraftKeys.For(FormKey, newId), json);
                    Navigation.NavigateTo($"/Formulare/auslagenerstattung?localId={newId}");
                    break;
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
            StateHasChanged();
        }
    }

    private async Task DeleteDraft() {
        Busy = true;
        StateHasChanged();
        try {
            switch (Mode) {
                case FormBackingMode.Server when Id.HasValue:
                    var resp = await Http.DeleteAsync($"/api/forms/expense-reimbursement/{Id}");
                    if (!resp.IsSuccessStatusCode) {
                        await ToastService.ErrorAsync(resp);
                        return;
                    }
                    Navigation.NavigateTo("/Formulare/meine");
                    break;
                case FormBackingMode.Local when LocalId.HasValue:
                    await JS.InvokeVoidAsync("qmStorage.clear", LocalDraftKeys.For(FormKey, LocalId.Value));
                    Navigation.NavigateTo("/Formulare/meine");
                    break;
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
            StateHasChanged();
        }
    }

    private void OnChapterPicked(string chapterIdRaw) {
        if (Guid.TryParse(chapterIdRaw, out var parsed)) {
            Input.ChapterId = parsed;
            _ = ResolveChapterName(parsed);
        } else {
            Input.ChapterId = null;
            Input.ChapterName = "";
        }
    }

    private async Task ResolveChapterName(Guid chapterId) {
        try {
            var chapters = await Http.GetFromJsonAsync<List<Quartermaster.Api.Chapters.ChapterDTO>>("/api/chapters");
            var match = chapters?.FirstOrDefault(c => c.Id == chapterId);
            if (match != null) {
                Input.ChapterName = match.Name;
                StateHasChanged();
            }
        } catch (HttpRequestException) {
            // ignore - user can still type
        }
    }

    private void AddLineItem() {
        Input.LineItems.Add(new ExpenseReimbursementLineItemDTO());
    }

    private void RemoveLineItem(int index) {
        if (index >= 0 && index < Input.LineItems.Count) {
            Input.LineItems.RemoveAt(index);
        }
    }

    private void OnDonatedChanged(ChangeEventArgs e) {
        Input.DonatedAmount = ParseDecimal(e.Value?.ToString());
    }

    private void OnPayoutChanged(ChangeEventArgs e) {
        Input.PayoutAmount = ParseDecimal(e.Value?.ToString());
        if (Input.PayoutAmount == 0m) {
            Input.PayoutMethod = ExpensePayoutMethodDTO.None;
        } else if (Input.PayoutMethod == ExpensePayoutMethodDTO.None) {
            Input.PayoutMethod = ExpensePayoutMethodDTO.BankTransfer;
        }
    }

    private static decimal ParseDecimal(string? raw) {
        if (string.IsNullOrWhiteSpace(raw)) {
            return 0m;
        }
        return decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : 0m;
    }

    private static string FormatIso(DateTime value)
        => value == default ? "" : value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static DateTime ParseIso(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return default;
        }
        return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var v) ? v : default;
    }

    private static string FormatTimestamp(DateTimeOffset? value)
        => value?.ToLocalTime().ToString("dd.MM.yyyy HH:mm", De) ?? "";

    private static ExpenseReimbursementInputDTO NewEmptyInput() => new() {
        MotionDate = DateTime.Today,
        FormDate = DateTime.Today,
        LineItems = new() { new ExpenseReimbursementLineItemDTO() }
    };

    private enum FormBackingMode {
        Ephemeral,
        Server,
        Local
    }
}
