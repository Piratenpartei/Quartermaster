using System;
using System.Collections.Generic;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Notifications;

/// <summary>
/// Builds the <c>globals</c> sub-model that the dispatcher injects into every notification
/// template alongside the per-trigger model. Templates can reference <c>{{ globals.base_url }}</c>,
/// <c>{{ globals.app_name }}</c>, <c>{{ globals.now }}</c> regardless of which trigger fired.
/// </summary>
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
            ["base_url"] = baseUrl,
            ["app_name"] = appName,
            ["now"] = DateTime.UtcNow
        };
    }
}
