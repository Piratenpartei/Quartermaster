using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Blazor.Components;
using Quartermaster.Blazor.Services;

namespace Quartermaster.Blazor.Pages.AssemblyAssistant;

public partial class AssemblySetup {
    [Inject]
    public required HttpClient Http { get; set; }

    [Inject]
    public required ToastService ToastService { get; set; }

    [Inject]
    public required I18nService I18n { get; set; }

    private static readonly string[] ConnectionKeys = [
        "mumble.host",
        "mumble.port",
        "mumble.username",
        "mumble.server_certificate_sha256"
    ];

    private static readonly ChannelRole[] Roles = Enum.GetValues<ChannelRole>();

    private AssemblySetupStatusDTO? Status;
    private AssemblySetupCheckDTO? CheckResult;
    private Dictionary<ChannelRole, uint> Mapping = new();
    private List<ChannelOption> ChannelOptions = [];
    private ConfirmDialog? ReplaceDialog;
    private bool Busy;

    protected override async Task OnInitializedAsync() {
        await LoadStatus();
    }

    private async Task LoadStatus() {
        try {
            Status = await Http.GetFromJsonAsync<AssemblySetupStatusDTO>("/api/assembly-assistant/setup");
            Mapping = Status == null ? new() : new(Status.Channels);
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        }
    }

    private Task GenerateCertificate() {
        return RequestCertificate(false);
    }

    private async Task ReplaceCertificate() {
        if (ReplaceDialog == null || !await ReplaceDialog.ShowAsync())
            return;

        await RequestCertificate(true);
    }

    private async Task RequestCertificate(bool replace) {
        await RunBusy(async () => {
            var response = await Http.PostAsJsonAsync("/api/assembly-assistant/setup/certificate",
                new AssemblySetupGenerateCertificateRequest { Replace = replace });
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            ToastService.ToastKey(I18nKey.Ui.AssemblySetup.CertificateGenerated);
            await LoadStatus();
        });
    }

    private async Task Check() {
        await RunBusy(async () => {
            var response = await Http.PostAsync("/api/assembly-assistant/setup/check", null);
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            CheckResult = await response.Content.ReadFromJsonAsync<AssemblySetupCheckDTO>();
            ChannelOptions = CheckResult == null ? [] : BuildChannelOptions(CheckResult.Channels);
        });
    }

    private async Task Register() {
        await RunBusy(async () => {
            var response = await Http.PostAsync("/api/assembly-assistant/setup/register", null);
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            var result = await response.Content.ReadFromJsonAsync<AssemblySetupRegisterDTO>();
            if (result?.Registered == true) {
                ToastService.ToastKey(I18nKey.Ui.AssemblySetup.RegisterDone);
                if (CheckResult != null)
                    CheckResult.Registered = true;
            } else {
                ToastService.ErrorKey(result?.ErrorKey ?? I18nKey.Ui.AssemblySetup.Error.RegisterFailed);
            }
        });
    }

    private async Task SaveChannels() {
        if (Roles.Any(r => !Mapping.ContainsKey(r))) {
            ToastService.ErrorKey(I18nKey.Ui.AssemblySetup.Error.AllRolesRequired);
            return;
        }

        await RunBusy(async () => {
            var response = await Http.PostAsJsonAsync("/api/assembly-assistant/setup/channels",
                new AssemblySetupSaveChannelsRequest { Channels = new(Mapping) });
            if (!response.IsSuccessStatusCode) {
                await ToastService.ErrorAsync(response);
                return;
            }

            ToastService.ToastKey(I18nKey.Ui.Toast.Saved);
        });
        await Check();
    }

    private async Task RunBusy(Func<Task> action) {
        Busy = true;
        StateHasChanged();
        try {
            await action();
        } catch (HttpRequestException ex) {
            ToastService.Error(ex);
        } finally {
            Busy = false;
            StateHasChanged();
        }
    }

    private void SelectChannel(ChannelRole role, string? value) {
        if (uint.TryParse(value, out var channelId)) {
            Mapping[role] = channelId;
        } else {
            Mapping.Remove(role);
        }
    }

    private string SelectedValue(ChannelRole role) {
        return Mapping.TryGetValue(role, out var channelId) ? channelId.ToString() : "";
    }

    private IEnumerable<AssemblySetupProblemDTO> ProblemsFor(ChannelRole role) {
        return CheckResult?.Problems.Where(p => p.Role == role) ?? [];
    }

    private static string RoleKey(ChannelRole role) {
        return role switch {
            ChannelRole.Chair => I18nKey.Ui.AssemblySetup.Role.Chair,
            ChannelRole.Speaker => I18nKey.Ui.AssemblySetup.Role.Speaker,
            ChannelRole.MotionAuthor => I18nKey.Ui.AssemblySetup.Role.MotionAuthor,
            ChannelRole.Pro => I18nKey.Ui.AssemblySetup.Role.Pro,
            ChannelRole.Contra => I18nKey.Ui.AssemblySetup.Role.Contra,
            ChannelRole.Question => I18nKey.Ui.AssemblySetup.Role.Question,
            ChannelRole.Point => I18nKey.Ui.AssemblySetup.Role.Point,
            _ => I18nKey.Ui.AssemblySetup.Role.Listener
        };
    }

    private static List<ChannelOption> BuildChannelOptions(List<AssemblySetupChannelDTO> channels) {
        var childrenByParent = channels
            .Where(c => c.ParentId != null)
            .GroupBy(c => c.ParentId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Name).ToList());
        var ids = channels.Select(c => c.Id).ToHashSet();
        var roots = channels
            .Where(c => c.ParentId == null || !ids.Contains(c.ParentId.Value))
            .OrderBy(c => c.Id)
            .ToList();

        var result = new List<ChannelOption>();
        foreach (var root in roots)
            AddWithChildren(root, 0, childrenByParent, result);

        return result;
    }

    private static void AddWithChildren(AssemblySetupChannelDTO channel, int depth, Dictionary<uint, List<AssemblySetupChannelDTO>> childrenByParent, List<ChannelOption> result) {
        var indent = new string(' ', depth * 4);
        var users = channel.UserCount > 0 ? $" ({channel.UserCount})" : "";
        result.Add(new ChannelOption(channel.Id, $"{indent}{channel.Name}{users}"));

        if (!childrenByParent.TryGetValue(channel.Id, out var children))
            return;

        foreach (var child in children)
            AddWithChildren(child, depth + 1, childrenByParent, result);
    }

    private record ChannelOption(uint Id, string Label);
}
