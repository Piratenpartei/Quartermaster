using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Microsoft.Extensions.Logging;
using Quartermaster.Data;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.Members;
using Quartermaster.Data.MembershipApplications;

namespace Quartermaster.Server.Members;

/// <summary>10-year contractual retention sweep. Applications/Selections defer while a name+DOB-matching Member is still in retention.</summary>
public class RetentionAnonymizationService {
    private readonly MemberRepository _memberRepo;
    private readonly MembershipApplicationRepository _applicationRepo;
    private readonly DueSelectionRepository _selectionRepo;
    private readonly DbContext _context;
    private readonly ILogger<RetentionAnonymizationService> _logger;

    public RetentionAnonymizationService(
        MemberRepository memberRepo,
        MembershipApplicationRepository applicationRepo,
        DueSelectionRepository selectionRepo,
        DbContext context,
        ILogger<RetentionAnonymizationService> logger) {
        _memberRepo = memberRepo;
        _applicationRepo = applicationRepo;
        _selectionRepo = selectionRepo;
        _context = context;
        _logger = logger;
    }

    public AnonymizationRunSummary RunOnce(DateTime now) {
        var members = _memberRepo.GetEligibleForAnonymization(now);
        foreach (var m in members)
            _memberRepo.Anonymize(m.Id);

        var applications = _applicationRepo.GetEligibleForAnonymization(now);
        var applicationIdsAnonymized = new List<Guid>();
        foreach (var app in applications) {
            if (LinkedMemberStillInRetention(app, now))
                continue;
            _applicationRepo.Anonymize(app.Id);
            applicationIdsAnonymized.Add(app.Id);
        }

        var selections = _selectionRepo.GetEligibleForAnonymization(now);
        var liveApplicationDueSelectionIds = _context.MembershipApplications
            .Where(a => a.DueSelectionId != null && a.AnonymizedAt == null)
            .Select(a => a.DueSelectionId!.Value)
            .ToHashSet();
        foreach (var sel in selections) {
            if (liveApplicationDueSelectionIds.Contains(sel.Id))
                continue;
            _selectionRepo.Anonymize(sel.Id);
        }

        var summary = new AnonymizationRunSummary(
            MembersAnonymized: members.Count,
            ApplicationsAnonymized: applicationIdsAnonymized.Count,
            SelectionsAnonymized: selections.Count - liveApplicationDueSelectionIds.Intersect(selections.Select(s => s.Id)).Count()
        );
        _logger.LogInformation("Retention sweep complete: {Members} members, {Apps} applications, {Sels} selections anonymized",
            summary.MembersAnonymized, summary.ApplicationsAnonymized, summary.SelectionsAnonymized);
        return summary;
    }

    private bool LinkedMemberStillInRetention(MembershipApplication app, DateTime now) {
        var match = _context.Members.FirstOrDefault(m =>
            m.AnonymizedAt == null
            && m.FirstName == app.FirstName
            && m.LastName == app.LastName
            && m.DateOfBirth != null
            && m.DateOfBirth == app.DateOfBirth);
        if (match == null)
            return false;
        if (match.ExitDate == null)
            return true;
        var thresholdYear = now.Year - 11;
        return match.ExitDate.Value.Year > thresholdYear;
    }
}

public record AnonymizationRunSummary(int MembersAnonymized, int ApplicationsAnonymized, int SelectionsAnonymized);
