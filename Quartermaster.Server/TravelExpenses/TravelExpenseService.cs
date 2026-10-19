using System;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.TravelExpenses;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.TravelExpenses;
using Quartermaster.Data.Users;

namespace Quartermaster.Server.TravelExpenses;

/// <summary>Finds the rules for an application and runs the calculator, the same way for drafts, PDFs and the browser.</summary>
public class TravelExpenseService {
    private readonly TravelExpenseRuleSetRepository _rules;
    private readonly ChapterRepository _chapters;
    private readonly UserRepository _users;
    private readonly MemberRepository _members;

    public TravelExpenseService(TravelExpenseRuleSetRepository rules, ChapterRepository chapters, UserRepository users, MemberRepository members) {
        _rules = rules;
        _chapters = chapters;
        _users = users;
        _members = members;
    }

    public TravelExpenseRulesDTO RulesFor(Guid? chapterId, DateOnly date) {
        return _rules.Resolve(chapterId, date, _chapters)?.ToDto() ?? TravelExpenseRulePresets.Federal();
    }

    /// <summary>The rules of the chapter travelled for on the trip's start day; without a start, today's.</summary>
    public TravelExpenseRulesDTO RulesFor(TravelExpenseInputDTO input) {
        var date = input.Start.At == null ? Today() : DateOnly.FromDateTime(input.Start.At.Value);
        return RulesFor(input.TravelChapterId, date);
    }

    public TravelExpenseCalculationDTO Calculate(TravelExpenseInputDTO input) {
        return TravelExpenseCalculator.Calculate(input, RulesFor(input), Today());
    }

    /// <summary>Fills an empty application with the user's account and member data; the home town becomes the start and end.</summary>
    public void Prefill(TravelExpenseInputDTO input, Guid userId) {
        if (!string.IsNullOrWhiteSpace(input.FirstName) || !string.IsNullOrWhiteSpace(input.LastName))
            return;

        var user = _users.Get(userId);
        if (user == null)
            return;

        var member = _members.GetByUserId(userId);
        input.FirstName = member?.FirstName ?? user.FirstName;
        input.LastName = member?.LastName ?? user.LastName;
        input.Email = member?.Email ?? user.Email;
        input.MemberNumber = member?.MemberNumber;
        input.Street = member?.Street ?? "";
        input.PostCode = member?.PostCode ?? "";
        input.City = member?.City ?? "";

        var chapterId = member?.ChapterId ?? user.ChapterId;
        var chapter = chapterId == null ? null : _chapters.Get(chapterId.Value);
        if (chapter != null) {
            input.MembershipChapterId = chapter.Id;
            input.MembershipChapterName = chapter.Name;
        }
        if (string.IsNullOrWhiteSpace(input.Start.Place))
            input.Start.Place = input.City;
        if (string.IsNullOrWhiteSpace(input.End.Place))
            input.End.Place = input.City;
    }

    public static DateOnly Today() {
        return DateOnly.FromDateTime(BerlinTime.FromUtc(DateTime.UtcNow));
    }
}
