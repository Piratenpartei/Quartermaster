using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Components.Forms;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration;

public partial class OptionDetail {
    [Inject]
    public required HttpClient Http { get; set; }
    [Inject]
    public required ToastService ToastService { get; set; }
    [Inject]
    public required I18nService I18n { get; set; }

    [Parameter]
    public Guid Id { get; set; }

    private OptionDefinitionDTO? Option;
    private List<ChapterDTO>? Chapters;
    private bool Loading = true;
    private string NewOverrideChapterId { get; set; } = "";
    private DirtyForm _globalForm = default!;

    protected override async Task OnInitializedAsync() {
        try {
            Chapters = await Http.GetFromJsonAsync<List<ChapterDTO>>("/api/chapters");

            var options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options");
            Option = options?.FirstOrDefault(o => o.Id == Id);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }

        Loading = false;
    }

    private async Task SaveGlobal() {
        if (Option == null)
            return;

        try {
            await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
                Identifier = Option.Identifier,
                ChapterId = null,
                Value = Option.GlobalValue
            });
            _globalForm.Reset();
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task SaveOverride(Guid chapterId, string value) {
        if (Option == null)
            return;

        try {
            await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
                Identifier = Option.Identifier,
                ChapterId = chapterId,
                Value = value
            });
            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task AddOverride() {
        if (Option == null || !Guid.TryParse(NewOverrideChapterId, out var chapterId))
            return;

        try {
            await Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
                Identifier = Option.Identifier,
                ChapterId = chapterId,
                Value = Option.GlobalValue
            });

            NewOverrideChapterId = "";
            await ReloadOption();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private async Task ReloadOption() {
        try {
            var options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options");
            Option = options?.FirstOrDefault(o => o.Id == Id);
            StateHasChanged();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private string DataTypeLabel(OptionDataType dt) => dt switch {
        OptionDataType.String => I18n[I18nKey.Ui.OptionList.DataTypeString],
        OptionDataType.Number => I18n[I18nKey.Ui.OptionList.DataTypeNumber],
        _ => I18n[I18nKey.Ui.OptionList.DataTypeUnknown]
    };

    private static string DataTypeBadge(OptionDataType dt) => dt switch {
        OptionDataType.String => "border-info text-info-emphasis",
        OptionDataType.Number => "border-primary text-primary-emphasis",
        _ => "border-secondary text-secondary-emphasis"
    };
}
