using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Options;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.Administration.Settings;

/// <summary>
/// The public form switches. The motion form resolves per chapter up the ancestor chain, so a chapter override applies to
/// every sub-chapter without one of its own; removing an override makes the chapter inherit again.
/// </summary>
public partial class PublicFormsSetup {
    private const string MembershipKey = "public_forms.membership_application.enabled";
    private const string DueSelectionKey = "public_forms.due_selection.enabled";
    private const string MotionKey = "public_forms.motion.enabled";
    private const string TrueValue = "true";
    private const string FalseValue = "false";

    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    [Inject]
    public required ClientConfigService Config { get; set; }

    private bool Loading = true;
    private bool Busy;
    private bool MembershipEnabled;
    private bool DueSelectionEnabled;
    private bool MotionDefault;
    private List<OptionOverrideDTO> Overrides = [];
    private string NewChapterId = "";
    private string NewValue = TrueValue;

    protected override async Task OnInitializedAsync() {
        try {
            await ReloadAsync();
        } finally {
            Loading = false;
        }
    }

    private async Task ReloadAsync() {
        try {
            var options = await Http.GetFromJsonAsync<List<OptionDefinitionDTO>>("/api/options") ?? [];
            var byId = options.ToDictionary(o => o.Identifier);
            MembershipEnabled = IsTrue(byId.GetValueOrDefault(MembershipKey)?.GlobalValue);
            DueSelectionEnabled = IsTrue(byId.GetValueOrDefault(DueSelectionKey)?.GlobalValue);
            var motion = byId.GetValueOrDefault(MotionKey);
            MotionDefault = IsTrue(motion?.GlobalValue);
            Overrides = (motion?.Overrides ?? [])
                .OrderBy(o => o.ChapterName, StringComparer.CurrentCulture)
                .ToList();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private List<Guid> OverriddenChapterIds() {
        return Overrides
            .Select(o => o.ChapterId)
            .ToList();
    }

    private Task SetGlobal(string key, bool value) {
        return WriteAsync(key, null, value);
    }

    private Task SetOverride(Guid chapterId, bool value) {
        return WriteAsync(MotionKey, chapterId, value);
    }

    private async Task AddOverride() {
        if (!Guid.TryParse(NewChapterId, out var chapterId))
            return;

        if (await WriteAsync(MotionKey, chapterId, NewValue == TrueValue)) {
            NewChapterId = "";
            NewValue = TrueValue;
        }
    }

    private async Task RemoveOverride(Guid chapterId) {
        await RunAsync(() => Http.DeleteAsync($"/api/options/{Uri.EscapeDataString(MotionKey)}/overrides/{chapterId}"));
    }

    private Task<bool> WriteAsync(string key, Guid? chapterId, bool value) {
        return RunAsync(() => Http.PostAsJsonAsync("/api/options", new OptionUpdateRequest {
            Identifier = key,
            ChapterId = chapterId,
            Value = value ? TrueValue : FalseValue
        }));
    }

    // Every change saves at once and refreshes the client config, so the navigation and the open count follow immediately.
    private async Task<bool> RunAsync(Func<Task<HttpResponseMessage>> send) {
        Busy = true;
        try {
            using var response = await send();
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                await ReloadAsync();
                return false;
            }

            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
            await Config.LoadAsync(forceRefresh: true);
            await ReloadAsync();
            return true;
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
            return false;
        } finally {
            Busy = false;
        }
    }

    private static bool IsTrue(string? value) {
        return string.Equals(value, TrueValue, StringComparison.OrdinalIgnoreCase);
    }
}
