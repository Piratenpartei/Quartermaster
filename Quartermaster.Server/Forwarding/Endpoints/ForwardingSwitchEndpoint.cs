using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Api;
using Quartermaster.Api.Forwarding;
using Quartermaster.Api.I18n;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.Options;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.Forwarding.Endpoints;

/// <summary>
/// Turns a forwarding master switch on or off. Turning one on needs the federal Redmine connection and the federal targets the switch uses,
/// because every submission then creates a federal ticket; without them each one would fail and keep its data waiting.
/// </summary>
public class ForwardingSwitchEndpoint : Endpoint<ForwardingSwitchRequest> {
    private readonly OptionRepository _options;
    private readonly ForwardingSettingsRepository _settings;
    private readonly ForwardingSettingsResolver _resolver;
    private readonly ChapterRepository _chapters;
    private readonly PermissionContext _perms;

    public ForwardingSwitchEndpoint(OptionRepository options, ForwardingSettingsRepository settings, ForwardingSettingsResolver resolver,
        ChapterRepository chapters, PermissionContext perms) {
        _options = options;
        _settings = settings;
        _resolver = resolver;
        _chapters = chapters;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/forwarding/switches");
    }

    public override async Task HandleAsync(ForwardingSwitchRequest req, CancellationToken ct) {
        if (!_perms.HasGlobal(PermissionIdentifier.EditOptions)) {
            await SendForbiddenAsync(ct);
            return;
        }

        var purposes = RequiredPurposes(req.Key);
        if (purposes == null) {
            await SendErrorsAsync(400, ct);
            return;
        }
        if (req.Enabled && !FederalSetupComplete(purposes)) {
            AddError(I18nKey.Error.Forwarding.FederalSetupIncomplete);
            await SendErrorsAsync(400, ct);
            return;
        }

        _options.SetValue(req.Key, null, req.Enabled ? "true" : "false");
        await SendOkAsync(ct);
    }

    private static ForwardingPurpose[]? RequiredPurposes(string key) {
        if (key == ForwardingOptionKeys.MembershipApplicationEnabled)
            return [ForwardingPurpose.Application];
        if (key == ForwardingOptionKeys.DueSelectionEnabled)
            return [ForwardingPurpose.Regular, ForwardingPurpose.Reduction];
        return null;
    }

    private bool FederalSetupComplete(ForwardingPurpose[] purposes) {
        var federal = _chapters.GetRootChapter();
        if (federal == null || _resolver.ConnectionFor(federal.Id) == null)
            return false;

        var targets = _settings.ListTargets(federal.Id).Select(t => t.Purpose).ToHashSet();
        return purposes.All(targets.Contains);
    }
}
