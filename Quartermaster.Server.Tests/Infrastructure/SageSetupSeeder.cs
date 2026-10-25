using System;
using Microsoft.Extensions.DependencyInjection;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.Options;

namespace Quartermaster.Server.Tests.Infrastructure;

/// <summary>Seeds the complete federal SAGE entry setup that membership applications need to be accepted.</summary>
public static class SageSetupSeeder {
    public const int SageAssigneeId = 12;
    public const int SageDoneStatusId = 5;

    /// <summary>
    /// Switches application forwarding on and gives <paramref name="root"/> a connection and an application target. Applications are then handled
    /// in Quartermaster at <paramref name="handlingChapterId"/>, which must be below the root, or at the root itself when it is passed.
    /// </summary>
    public static void Seed(IServiceProvider services, Guid rootChapterId, Guid? handlingChapterId = null) {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        provider.GetRequiredService<OptionRepository>().SetValue(ForwardingOptionKeys.MembershipApplicationEnabled, null, "true");
        provider.GetRequiredService<RedmineConnectionRepository>().Upsert(rootChapterId, "https://redmine.invalid", "key", DateTime.UtcNow);

        var settings = provider.GetRequiredService<ForwardingSettingsRepository>();
        var target = new ForwardingTarget {
            Purpose = ForwardingPurpose.Application,
            ProjectId = 1,
            TrackerId = 1,
            SageAssigneeId = SageAssigneeId,
            SageDoneStatusId = SageDoneStatusId
        };
        var rootMode = handlingChapterId == rootChapterId ? ForwardingMode.Quartermaster : (ForwardingMode?)null;
        settings.SaveChapter(new ForwardingChapterSetting { ChapterId = rootChapterId, ApplicationMode = rootMode }, [target]);

        if (handlingChapterId != null && handlingChapterId != rootChapterId)
            settings.SaveChapter(new ForwardingChapterSetting { ChapterId = handlingChapterId.Value, ApplicationMode = ForwardingMode.Quartermaster }, []);
    }
}
