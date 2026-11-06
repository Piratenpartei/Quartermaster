using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Members;
using Quartermaster.Data.Users;
using Quartermaster.Server.Authentication;

namespace Quartermaster.Server.TestData;

/// <summary>
/// Dev-only: links the signed-in account to a test member of the root chapter, so a local account without membership can use the
/// member pages such as the motion portal. Elided from Release builds like <see cref="TestDataSeedEndpoint"/>, hence no test coverage.
/// </summary>
#if !DEBUG
[DontRegister]
#endif
public class TestDataLinkMemberEndpoint : EndpointWithoutRequest {
    private readonly DbContext _context;
    private readonly ChapterRepository _chapterRepo;
    private readonly MemberRepository _memberRepo;
    private readonly UserRepository _userRepo;
    private readonly PermissionContext _perms;

    public TestDataLinkMemberEndpoint(DbContext context, ChapterRepository chapterRepo, MemberRepository memberRepo, UserRepository userRepo,
        PermissionContext perms) {
        _context = context;
        _chapterRepo = chapterRepo;
        _memberRepo = memberRepo;
        _userRepo = userRepo;
        _perms = perms;
    }

    public override void Configure() {
        Post("/api/testdata/link-member");
    }

    public override async Task HandleAsync(CancellationToken ct) {
        var user = _perms.UserId == null ? null : _userRepo.Get(_perms.UserId.Value);
        if (user == null) {
            await SendUnauthorizedAsync(ct);
            return;
        }
        if (_context.Members.Any(m => m.UserId == user.Id)) {
            await SendOkAsync(ct);
            return;
        }

        var highestNumber = _context.Members.Max(m => (int?)m.MemberNumber) ?? 0;
        var member = new Member {
            MemberNumber = highestNumber + 1,
            FirstName = string.IsNullOrEmpty(user.FirstName) ? "Test" : user.FirstName,
            LastName = string.IsNullOrEmpty(user.LastName) ? "Mitglied" : user.LastName,
            Email = user.Email,
            ChapterId = _chapterRepo.GetRootChapter()?.Id,
            EntryDate = DateTime.UtcNow.AddYears(-1),
            HasVotingRights = true
        };
        _memberRepo.Insert(member);
        _memberRepo.SetUserId(member.Id, user.Id);
        await SendOkAsync(ct);
    }
}
