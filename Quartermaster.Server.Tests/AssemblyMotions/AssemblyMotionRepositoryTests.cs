using LinqToDB;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Data;
using Quartermaster.Data.AssemblyMotions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionRepositoryTests : RepositoryTestBase {
    private DbContext _context = default!;
    private TestDataBuilder _builder = default!;
    private AssemblyMotionPortalRepository _portalRepo = default!;
    private AssemblyMotionRepository _motionRepo = default!;

    [Before(Test)]
    public void Setup() {
        _context = Db;
        _builder = new TestDataBuilder(_context);
        _portalRepo = new AssemblyMotionPortalRepository(_context, AuditLog);
        _motionRepo = new AssemblyMotionRepository(_context, AuditLog);
    }

    private AssemblyMotionPortal SeedPortal() {
        var chapter = _builder.SeedChapter();
        var evt = _builder.SeedEvent(chapter.Id);
        return _portalRepo.Create(evt.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(7), DateTime.UtcNow);
    }

    [Test]
    public async Task Create_seeds_wiki_template_per_type_code() {
        var chapter = _builder.SeedChapter();
        var evt = _builder.SeedEvent(chapter.Id);

        var portal = _portalRepo.Create(evt.Id, DateTime.UtcNow, DateTime.UtcNow.AddDays(7), DateTime.UtcNow, code => "T-" + code);

        var templates = _context.AssemblyMotionTypes
            .Where(t => t.PortalId == portal.Id)
            .ToList()
            .ToDictionary(t => t.Code, t => t.WikiTemplate);
        await Assert.That(templates.Count).IsEqualTo(6);
        await Assert.That(templates.All(pair => pair.Value == "T-" + pair.Key)).IsTrue();
    }

    private AssemblyMotion InsertMotion(Guid portalId, Guid typeId, Guid memberId, int? number,
        AssemblyMotionStatus status = AssemblyMotionStatus.Submitted, DateTime? deletedAt = null) {
        var motion = new AssemblyMotion {
            PortalId = portalId,
            TypeId = typeId,
            Number = number,
            Title = "Testantrag",
            Status = status,
            CreatedByMemberId = memberId,
            CreatedAt = DateTime.UtcNow,
            DeletedAt = deletedAt
        };
        _context.Insert(motion);
        return motion;
    }

    [Test]
    public async Task Create_seeds_six_default_types() {
        var portal = SeedPortal();

        var types = _portalRepo.GetTypes(portal.Id);

        var codes = string.Join(",", types.Select(t => t.Code));
        var minSubmitters = string.Join(",", types.Select(t => t.MinSubmitters));

        await Assert.That(codes).IsEqualTo("GP,WP,SÄA,SBA,PP,SO");
        await Assert.That(minSubmitters).IsEqualTo("5,5,5,5,1,1");
    }

    [Test]
    public async Task NextNumber_counts_withdrawn_and_deleted() {
        var portal = SeedPortal();
        var member = _builder.SeedMember();
        var types = _portalRepo.GetTypes(portal.Id);
        var typeA = types[0];
        var typeB = types[1];

        InsertMotion(portal.Id, typeA.Id, member.Id, number: 1, status: AssemblyMotionStatus.Withdrawn);
        InsertMotion(portal.Id, typeA.Id, member.Id, number: 2, deletedAt: DateTime.UtcNow);

        await Assert.That(_motionRepo.NextNumber(portal.Id, typeA.Id)).IsEqualTo(3);
        await Assert.That(_motionRepo.NextNumber(portal.Id, typeB.Id)).IsEqualTo(1);
    }

    [Test]
    public async Task RemoveCoSubmitters_keeps_author() {
        var portal = SeedPortal();
        var author = _builder.SeedMember();
        var coSubmitter1 = _builder.SeedMember();
        var coSubmitter2 = _builder.SeedMember();
        var type = _portalRepo.GetTypes(portal.Id)[0];
        var motion = InsertMotion(portal.Id, type.Id, author.Id, number: null, status: AssemblyMotionStatus.CollectingCoSubmitters);

        _motionRepo.AddSubmitter(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = author.Id, IsAuthor = true, JoinedAt = DateTime.UtcNow, JoinedRevision = 1 });
        _motionRepo.AddSubmitter(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = coSubmitter1.Id, IsAuthor = false, JoinedAt = DateTime.UtcNow, JoinedRevision = 1 });
        _motionRepo.AddSubmitter(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = coSubmitter2.Id, IsAuthor = false, JoinedAt = DateTime.UtcNow, JoinedRevision = 1 });

        var removedMemberIds = _motionRepo.RemoveCoSubmitters(motion.Id);

        await Assert.That(removedMemberIds).IsEquivalentTo([coSubmitter1.Id, coSubmitter2.Id]);

        var remaining = _motionRepo.GetSubmitters(motion.Id);
        await Assert.That(remaining.Count).IsEqualTo(1);
        await Assert.That(remaining[0].MemberId).IsEqualTo(author.Id);
    }

    [Test]
    public async Task GetSubmitters_orders_author_first() {
        var portal = SeedPortal();
        var author = _builder.SeedMember();
        var coSubmitter = _builder.SeedMember();
        var type = _portalRepo.GetTypes(portal.Id)[0];
        var motion = InsertMotion(portal.Id, type.Id, author.Id, number: null, status: AssemblyMotionStatus.CollectingCoSubmitters);

        var now = DateTime.UtcNow;
        _motionRepo.AddSubmitter(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = coSubmitter.Id, IsAuthor = false, JoinedAt = now.AddMinutes(-5), JoinedRevision = 1 });
        _motionRepo.AddSubmitter(new AssemblyMotionSubmitter { MotionId = motion.Id, MemberId = author.Id, IsAuthor = true, JoinedAt = now, JoinedRevision = 1 });

        var submitters = _motionRepo.GetSubmitters(motion.Id);

        await Assert.That(submitters[0].MemberId).IsEqualTo(author.Id);
        await Assert.That(submitters[1].MemberId).IsEqualTo(coSubmitter.Id);
    }

    [Test]
    public async Task TypeHasMotions_counts_soft_deleted_drafts_because_they_still_reference_the_type() {
        var portal = SeedPortal();
        var member = _builder.SeedMember();
        var types = _portalRepo.GetTypes(portal.Id);
        var typeWithDeletedDraft = types[0];
        var typeWithLiveDraft = types[1];

        InsertMotion(portal.Id, typeWithDeletedDraft.Id, member.Id, number: null, status: AssemblyMotionStatus.Draft, deletedAt: DateTime.UtcNow);
        InsertMotion(portal.Id, typeWithLiveDraft.Id, member.Id, number: null, status: AssemblyMotionStatus.Draft);

        await Assert.That(_portalRepo.TypeHasMotions(typeWithDeletedDraft.Id)).IsTrue();
        await Assert.That(_portalRepo.TypeHasMotions(typeWithLiveDraft.Id)).IsTrue();
        await Assert.That(_portalRepo.TypeHasMotions(types[2].Id)).IsFalse();
    }

    [Test]
    public async Task ListSummariesByPortal_excludes_drafts_and_deleted_rows_in_sql_and_leaves_texts_unread() {
        var portal = SeedPortal();
        var member = _builder.SeedMember();
        var type = _portalRepo.GetTypes(portal.Id)[0];
        var submitted = InsertMotion(portal.Id, type.Id, member.Id, number: 1);
        InsertMotion(portal.Id, type.Id, member.Id, number: null, status: AssemblyMotionStatus.Draft);
        InsertMotion(portal.Id, type.Id, member.Id, number: 2, deletedAt: DateTime.UtcNow);
        var collecting = InsertMotion(portal.Id, type.Id, member.Id, number: null, status: AssemblyMotionStatus.CollectingCoSubmitters);
        _context.AssemblyMotions
            .Where(m => m.Id == submitted.Id)
            .Set(m => m.TextMarkdown, "Volltext")
            .Set(m => m.TextHtml, "<p>Volltext</p>")
            .Set(m => m.ReasonMarkdown, "Begruendung")
            .Set(m => m.CheckNote, "Pruefnotiz")
            .Set(m => m.WikiLastError, "Fehlertext")
            .Update();

        var all = _motionRepo.ListSummariesByPortal(portal.Id);
        var onlyCollecting = _motionRepo.ListSummariesByPortal(portal.Id, AssemblyMotionStatus.CollectingCoSubmitters);
        var summary = all.Single(m => m.Id == submitted.Id);

        await Assert.That(all.Select(m => m.Id).ToArray()).IsEquivalentTo(new[] { submitted.Id, collecting.Id });
        await Assert.That(onlyCollecting.Select(m => m.Id).ToArray()).IsEquivalentTo(new[] { collecting.Id });
        await Assert.That(summary.Title).IsEqualTo("Testantrag");
        await Assert.That(summary.Number).IsEqualTo(1);
        await Assert.That(summary.TextMarkdown).IsEqualTo("");
        await Assert.That(summary.TextHtml).IsEqualTo("");
        await Assert.That(summary.ReasonMarkdown).IsEqualTo("");
        await Assert.That(summary.CheckNote).IsNull();
        await Assert.That(summary.WikiLastError).IsNull();
    }

    [Test]
    public async Task ListNumberedByPortal_returns_full_rows_of_numbered_motions_only() {
        var portal = SeedPortal();
        var member = _builder.SeedMember();
        var type = _portalRepo.GetTypes(portal.Id)[0];
        var numbered = InsertMotion(portal.Id, type.Id, member.Id, number: 1);
        InsertMotion(portal.Id, type.Id, member.Id, number: null, status: AssemblyMotionStatus.Draft);
        InsertMotion(portal.Id, type.Id, member.Id, number: 2, deletedAt: DateTime.UtcNow);
        _context.AssemblyMotions
            .Where(m => m.Id == numbered.Id)
            .Set(m => m.TextMarkdown, "Volltext")
            .Update();

        var rows = _motionRepo.ListNumberedByPortal(portal.Id);

        await Assert.That(rows.Select(m => m.Id).ToArray()).IsEquivalentTo(new[] { numbered.Id });
        await Assert.That(rows[0].TextMarkdown).IsEqualTo("Volltext");
        await Assert.That(_motionRepo.ListNumberedIds(portal.Id).ToArray()).IsEquivalentTo(new[] { numbered.Id });
    }
    private void SetMirrors(Guid portalId, bool wiki, bool forum) {
        _context.AssemblyMotionPortals
            .Where(p => p.Id == portalId)
            .Set(p => p.WikiMirrorEnabled, wiki)
            .Set(p => p.ForumMirrorEnabled, forum)
            .Update();
    }

    private void SetForumState(Guid motionId, ForumSyncState state, DateTime? nextAttemptAt = null) {
        _context.AssemblyMotions
            .Where(m => m.Id == motionId)
            .Set(m => m.ForumSyncState, state)
            .Set(m => m.ForumNextAttemptAt, nextAttemptAt)
            .Update();
    }

    [Test]
    public async Task MarkMirrorsPending_queues_only_enabled_mirrors() {
        var portal = SeedPortal();
        var type = _portalRepo.GetTypes(portal.Id).First();
        var member = _builder.SeedMember();
        var motion = InsertMotion(portal.Id, type.Id, member.Id, number: 1);
        var now = DateTime.UtcNow;

        SetMirrors(portal.Id, wiki: true, forum: false);
        _motionRepo.MarkMirrorsPending(motion.Id, now);
        var wikiOnly = _motionRepo.Get(motion.Id)!;

        SetMirrors(portal.Id, wiki: true, forum: true);
        _motionRepo.MarkMirrorsPending(motion.Id, now);
        var both = _motionRepo.Get(motion.Id)!;

        await Assert.That(wikiOnly.WikiSyncState).IsEqualTo(WikiSyncState.Pending);
        await Assert.That(wikiOnly.ForumSyncState).IsEqualTo(ForumSyncState.NotMirrored);
        await Assert.That(both.ForumSyncState).IsEqualTo(ForumSyncState.Pending);
        await Assert.That(both.ForumSyncRequestedAt).IsNotNull();
    }

    [Test]
    public async Task MarkForumPending_leaves_deleted_motions_alone() {
        var portal = SeedPortal();
        SetMirrors(portal.Id, wiki: false, forum: true);
        var type = _portalRepo.GetTypes(portal.Id).First();
        var motion = InsertMotion(portal.Id, type.Id, _builder.SeedMember().Id, number: 1);
        SetForumState(motion.Id, ForumSyncState.Deleted);

        _motionRepo.MarkForumPending(motion.Id, DateTime.UtcNow);

        var reloaded = _motionRepo.Get(motion.Id)!;
        await Assert.That(reloaded.ForumSyncState).IsEqualTo(ForumSyncState.Deleted);
        await Assert.That(reloaded.ForumSyncRequestedAt).IsNull();
    }

    [Test]
    public async Task ListForumDue_includes_not_mirrored_and_due_failures() {
        var portal = SeedPortal();
        SetMirrors(portal.Id, wiki: false, forum: true);
        var type = _portalRepo.GetTypes(portal.Id).First();
        var member = _builder.SeedMember();
        var now = DateTime.UtcNow;
        var pending = InsertMotion(portal.Id, type.Id, member.Id, number: 1);
        SetForumState(pending.Id, ForumSyncState.Pending);
        var notMirrored = InsertMotion(portal.Id, type.Id, member.Id, number: 2);
        var dueFailure = InsertMotion(portal.Id, type.Id, member.Id, number: 3);
        SetForumState(dueFailure.Id, ForumSyncState.Failed, now.AddMinutes(-1));
        var laterFailure = InsertMotion(portal.Id, type.Id, member.Id, number: 4);
        SetForumState(laterFailure.Id, ForumSyncState.Failed, now.AddMinutes(5));
        var synced = InsertMotion(portal.Id, type.Id, member.Id, number: 5);
        SetForumState(synced.Id, ForumSyncState.Synced);
        var deleted = InsertMotion(portal.Id, type.Id, member.Id, number: 6);
        SetForumState(deleted.Id, ForumSyncState.Deleted);
        InsertMotion(portal.Id, type.Id, member.Id, number: null, status: AssemblyMotionStatus.Draft);
        InsertMotion(portal.Id, type.Id, member.Id, number: 7, deletedAt: now);

        var due = _motionRepo.ListForumDue(now, 20);

        await Assert.That(due.Select(m => m.Id).ToArray()).IsEquivalentTo(new[] { pending.Id, notMirrored.Id, dueFailure.Id });
    }

    [Test]
    public async Task CompleteForumSync_keeps_newer_request_pending() {
        var portal = SeedPortal();
        SetMirrors(portal.Id, wiki: false, forum: true);
        var type = _portalRepo.GetTypes(portal.Id).First();
        var motion = InsertMotion(portal.Id, type.Id, _builder.SeedMember().Id, number: 1);
        var now = DateTime.UtcNow;
        _motionRepo.MarkForumPending(motion.Id, now);
        var loadedRequestedAt = _motionRepo.Get(motion.Id)!.ForumSyncRequestedAt;
        _motionRepo.MarkForumPending(motion.Id, now.AddSeconds(5));

        var applied = _motionRepo.CompleteForumSync(motion.Id, loadedRequestedAt, 1, "hash", now);

        var reloaded = _motionRepo.Get(motion.Id)!;
        await Assert.That(applied).IsFalse();
        await Assert.That(reloaded.ForumSyncState).IsEqualTo(ForumSyncState.Pending);
        await Assert.That(reloaded.ForumSyncedHash).IsEqualTo("hash");
    }
}
