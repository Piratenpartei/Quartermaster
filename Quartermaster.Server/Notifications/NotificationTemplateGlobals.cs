using System;
using System.Collections.Generic;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Notifications;

public class NotificationTemplateGlobals {
    public const string BaseUrlOptionKey = "system.public_base_url";
    public const string AppNameOptionKey = "system.app_name";

    private readonly OptionRepository _optionRepo;

    public NotificationTemplateGlobals(OptionRepository optionRepo) {
        _optionRepo = optionRepo;
    }

    public IDictionary<string, object?> Build() {
        var baseUrl = _optionRepo.GetGlobalValue(BaseUrlOptionKey)?.Value?.TrimEnd('/') ?? "";
        var appName = _optionRepo.GetGlobalValue(AppNameOptionKey)?.Value;
        if (string.IsNullOrWhiteSpace(appName)) {
            appName = "Quartermaster";
        }
        return new Dictionary<string, object?> {
            ["BaseUrl"] = baseUrl,
            ["AppName"] = appName,
            ["Now"] = DateTime.UtcNow
        };
    }
}
