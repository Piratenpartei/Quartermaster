using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public partial class MembershipEmailCodeStep : IDisposable {
    private const int CodeLength = 6;
    private const int ResendSeconds = 60;

    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public MembershipApplicationEntryState State { get; set; } = default!;

    private bool EditingAddress;
    private bool Sending;
    private string AddressInput = "";
    private Timer? ResendTimer;
    private string? RawCodeInput;

    private bool NotRequired => State.IsEmailCodeComplete() && State.EmailCode.Length == 0;

    private string SentToMarkup => I18n[$"{I18nKey.Ui.EmailCodeStep.SentTo}?email={Uri.EscapeDataString(WebUtility.HtmlEncode(State.Email.Trim()))}"];

    private string ResendLabel => SecondsLeft > 0
        ? I18n[$"{I18nKey.Ui.EmailCodeStep.ResendWait}?seconds={SecondsLeft}"]
        : I18n[I18nKey.Ui.EmailCodeStep.Resend];

    private int SecondsLeft {
        get {
            if (State.EmailCodeSentAt == null)
                return 0;

            var elapsed = DateTime.UtcNow - State.EmailCodeSentAt.Value;
            return Math.Max(0, (int)Math.Ceiling(ResendSeconds - elapsed.TotalSeconds));
        }
    }

    private bool WasSentToCurrentAddress => State.EmailCodeSentFor == MembershipApplicationEntryState.NormalizeEmail(State.Email);

    protected override async Task OnInitializedAsync() {
        if (WasSentRecently())
            StartTimer();
        if (State.HasUnexpiredCode(DateTime.UtcNow) || State.IsEmailCodeComplete())
            return;

        await SendAsync(State.Email.Trim());
    }

    public void Dispose() {
        ResendTimer?.Dispose();
    }

    public async Task<bool> ValidateAsync() {
        if (State.IsEmailCodeComplete())
            return true;
        if (!IsCodeComplete()) {
            ToastService.ErrorKey(I18nKey.Error.EmailCode.Required);
            return false;
        }

        try {
            var response = await Http.PostAsJsonAsync("/api/membershipapplications/email-code/check", new EmailCodeCheckRequest {
                Email = State.Email,
                Code = State.EmailCode
            });
            if (!response.IsSuccessStatusCode) {
                await ShowErrorAsync(response);
                return false;
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            return false;
        }

        State.EmailCodeVerifiedFor = MembershipApplicationEntryState.NormalizeEmail(State.Email);
        return true;
    }

    private bool WasSentRecently() {
        if (!WasSentToCurrentAddress)
            return false;

        return SecondsLeft > 0;
    }

    private bool IsCodeComplete() {
        return State.EmailCode.Length == CodeLength && State.EmailCode.All(char.IsAsciiDigit);
    }

    private async Task OnCodeInput(ChangeEventArgs args) {
        var raw = args.Value?.ToString() ?? "";
        var digits = new string(raw.Where(char.IsAsciiDigit).Take(CodeLength).ToArray());
        if (digits != State.EmailCode) {
            State.EmailCode = digits;
            State.EmailCodeVerifiedFor = null;
        }

        if (raw == digits)
            return;

        RawCodeInput = raw;
        StateHasChanged();
        await Task.Yield();
        RawCodeInput = null;
    }

    private void StartEditing() {
        AddressInput = State.Email.Trim();
        EditingAddress = true;
    }

    private void CancelEditing() {
        EditingAddress = false;
    }

    private async Task SaveAddress() {
        if (await SendAsync(AddressInput.Trim()))
            EditingAddress = false;
    }

    private async Task Resend() {
        await SendAsync(State.Email.Trim());
    }

    private async Task<bool> SendAsync(string email) {
        Sending = true;
        StateHasChanged();
        try {
            var response = await Http.PostAsJsonAsync("/api/membershipapplications/email-code", new EmailCodeSendRequest {
                Email = email,
                ChapterId = State.ChapterId
            });
            if (!response.IsSuccessStatusCode) {
                await ShowErrorAsync(response);
                return false;
            }

            var body = await response.Content.ReadFromJsonAsync<EmailCodeSendResponse>();
            var normalized = MembershipApplicationEntryState.NormalizeEmail(email);
            State.Email = email;
            State.EmailCode = "";
            State.RecordCodeSent(email, DateTime.UtcNow, body?.LifetimeMinutes ?? 0);
            State.EmailCodeVerifiedFor = body?.CodeRequired == false ? normalized : null;
            StartTimer();
            return true;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            return false;
        } finally {
            Sending = false;
            StateHasChanged();
        }
    }

    private Task ShowErrorAsync(HttpResponseMessage response) {
        var fallback = response.StatusCode == HttpStatusCode.TooManyRequests ? I18n[I18nKey.Error.EmailCode.TooManySends] : null;
        return ToastService.ErrorAsync(response, fallback);
    }

    private void StartTimer() {
        ResendTimer?.Dispose();
        ResendTimer = new Timer(_ => _ = InvokeAsync(Tick), null, 1000, 1000);
    }

    private void Tick() {
        if (SecondsLeft == 0)
            ResendTimer?.Dispose();

        StateHasChanged();
    }
}
