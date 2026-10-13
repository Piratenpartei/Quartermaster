using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Components.AssemblyMotions;

public partial class AssemblyMotionInquiries {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter, EditorRequired]
    public Guid MotionId { get; set; }

    [Parameter, EditorRequired]
    public required List<AssemblyMotionInquiryDTO> Inquiries { get; set; }

    [Parameter]
    public bool CanAnswer { get; set; }

    [Parameter]
    public bool CanManage { get; set; }

    [Parameter]
    public bool CanAsk { get; set; }

    [Parameter]
    public EventCallback<AssemblyMotionDetailDTO> OnChanged { get; set; }

    private readonly Dictionary<Guid, string> _answers = [];
    private ConfirmDialog CloseDialog = default!;
    private bool Busy;
    private string AskQuestion = "";
    private string AskDue = AssemblyMotionDisplay.ToInput(DateTimeOffset.UtcNow.AddDays(2));

    private string GetAnswer(Guid inquiryId) {
        return _answers.GetValueOrDefault(inquiryId, "");
    }

    private void SetAnswer(Guid inquiryId, string value) {
        _answers[inquiryId] = value;
    }

    private async Task Answer(Guid inquiryId) {
        var request = new AssemblyMotionInquiryAnswerRequest {
            Id = inquiryId,
            AnswerMarkdown = GetAnswer(inquiryId)
        };
        if (await SendAsync($"/api/assemblymotions/inquiries/{inquiryId}/answer", request, I18nKey.Ui.AssemblyMotion.ToastInquiryAnswered))
            _answers.Remove(inquiryId);
    }

    private async Task Ask() {
        var due = AssemblyMotionDisplay.FromInput(AskDue);
        if (due == null) {
            ToastService.ErrorKey(I18nKey.Ui.AssemblyMotion.InquiryDueInvalid);
            return;
        }

        var request = new AssemblyMotionInquiryAskRequest {
            MotionId = MotionId,
            QuestionMarkdown = AskQuestion,
            ResponseDueAt = due.Value
        };
        if (!await SendAsync($"/api/assemblymotions/motions/{MotionId}/inquiries", request, I18nKey.Ui.AssemblyMotion.ToastInquiryAsked))
            return;

        AskQuestion = "";
        AskDue = AssemblyMotionDisplay.ToInput(DateTimeOffset.UtcNow.AddDays(2));
    }

    private async Task Close(Guid inquiryId) {
        if (!await CloseDialog.ShowAsync(I18n[I18nKey.Ui.AssemblyMotion.CloseInquiryMessage]))
            return;

        await SendAsync($"/api/assemblymotions/inquiries/{inquiryId}/close", new { }, I18nKey.Ui.AssemblyMotion.ToastInquiryClosed);
    }

    private async Task<bool> SendAsync<T>(string url, T body, string toastKey) {
        Busy = true;
        var succeeded = false;
        try {
            var response = await Http.PostAsJsonAsync(url, body);
            if (response.IsSuccessStatusCode) {
                var detail = await response.Content.ReadFromJsonAsync<AssemblyMotionDetailDTO>();
                ToastService.ToastKey(toastKey);
                succeeded = true;
                if (detail != null)
                    await OnChanged.InvokeAsync(detail);
            } else {
                await ToastService.ErrorAsync(response);
            }
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
        }
        return succeeded;
    }
}
