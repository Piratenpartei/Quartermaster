using System;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api.AssemblyAssistant;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Options;
using Quartermaster.Server.AssemblyAssistant.Mumble;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.AssemblyAssistant.Endpoints;

public class AssemblySetupSaveChannelsEndpoint : Endpoint<AssemblySetupSaveChannelsRequest> {
    private readonly PermissionContext _perms;
    private readonly OptionRepository _options;

    public AssemblySetupSaveChannelsEndpoint(PermissionContext perms, OptionRepository options) {
        _perms = perms;
        _options = options;
    }

    public override void Configure() {
        Post("/api/assembly-assistant/setup/channels");
    }

    public override async Task HandleAsync(AssemblySetupSaveChannelsRequest req, CancellationToken ct) {
        var status = AssemblySetupGuard.Check(_perms);
        if (status != null) {
            await SendErrorsAsync(status.Value, ct);
            return;
        }

        foreach (var role in Enum.GetValues<ChannelRole>()) {
            if (!req.Channels.ContainsKey(role))
                ThrowError(I18nKey.Ui.AssemblySetup.Error.AllRolesRequired);
        }

        foreach (var (role, optionId) in MumbleSettings.ChannelOptionIds)
            _options.SetValue(optionId, null, req.Channels[role].ToString());

        await SendOkAsync(ct);
    }
}
