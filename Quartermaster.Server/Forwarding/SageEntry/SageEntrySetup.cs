using System;
using System.Collections.Generic;
using System.Globalization;
using Quartermaster.Api.Forwarding;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Forwarding;
using Quartermaster.Data.Options;
using Quartermaster.Server.Forwarding.Redmine;

namespace Quartermaster.Server.Forwarding.SageEntry;

/// <summary>Whether the federal Redmine setup carries everything the SAGE entry check needs.</summary>
public class SageEntrySetup {
    private readonly OptionRepository _options;
    private readonly ChapterRepository _chapters;
    private readonly ForwardingSettingsRepository _settings;
    private readonly ForwardingSettingsResolver _resolver;

    public SageEntrySetup(OptionRepository options, ChapterRepository chapters, ForwardingSettingsRepository settings, ForwardingSettingsResolver resolver) {
        _options = options;
        _chapters = chapters;
        _settings = settings;
        _resolver = resolver;
    }

    public List<SageSetupGap> Missing() {
        var missing = new List<SageSetupGap>();
        var federal = _chapters.GetRootChapter();
        var target = FederalApplicationTarget();

        if (!_options.IsGlobalEnabled(ForwardingOptionKeys.MembershipApplicationEnabled))
            missing.Add(SageSetupGap.ForwardingSwitch);
        if (federal == null || _resolver.ConnectionFor(federal.Id) == null)
            missing.Add(SageSetupGap.FederalConnection);
        if (target == null)
            missing.Add(SageSetupGap.FederalTarget);
        if (target?.SageAssigneeId == null)
            missing.Add(SageSetupGap.SageGroup);
        if (!HasDoneSignal(target))
            missing.Add(SageSetupGap.DoneSignal);

        return missing;
    }

    public bool IsComplete() {
        return Missing().Count == 0;
    }

    /// <summary>
    /// The target a SAGE note or check works with; missing SAGE fields fail the step at once and keep the approval mail held. The forwarding switch
    /// is not required here: it only gates new applications, those already approved must still be finished.
    /// </summary>
    public ForwardingTarget RequireTarget(ForwardingTarget? target) {
        if (target?.SageAssigneeId == null || !HasDoneSignal(target)) {
            throw new RedmineException("Die SAGE-Eintragung ist nicht vollständig eingerichtet: Am Zielprojekt für Mitgliedsanträge der Bundesebene fehlen "
                + "die SAGE-Gruppe oder das Signal für die erledigte Eintragung.", RedmineException.NotConfigured);
        }
        return target;
    }

    public ForwardingTarget? FederalApplicationTarget() {
        var federal = _chapters.GetRootChapter();
        return federal == null ? null : _settings.GetTarget(federal.Id, ForwardingPurpose.Application);
    }

    public TimeSpan CheckInterval() {
        var value = _options.GetGlobalValue(ForwardingOptionKeys.SageCheckMinutes)?.Value?.Trim();
        var minutes = int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : ForwardingOptionKeys.DefaultSageCheckMinutes;
        return TimeSpan.FromMinutes(Math.Max(minutes, ForwardingOptionKeys.MinimumSageCheckMinutes));
    }

    private static bool HasDoneSignal(ForwardingTarget? target) {
        if (target == null)
            return false;
        if (target.SageDoneStatusId != null)
            return true;
        return target.SageAwaitReassignment;
    }
}
