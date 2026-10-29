using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Chapters;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Motions;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Motions;

public class MotionHandoverFieldsTests : IntegrationTestBase {
    private Chapter _root = null!;
    private Chapter _state = null!;
    private Chapter _county = null!;

    public MotionHandoverFieldsTests() {
        DbContext.SupplementDefaults(Factory.Services);
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        (_root, _state, _county) = (chain[0], chain[1], chain[2]);
        Db.Chapters.Where(c => c.Id == _state.Id).Set(c => c.Level, ChapterLevel.State).Update();
    }

    private Motion SeedApplicationMotion(bool isPublic = false) {
        var application = Builder.SeedMembershipApplication(_county.Id);
        var motion = Builder.SeedMotion(_county.Id, isPublic: isPublic);
        Db.Motions.Where(m => m.Id == motion.Id).Set(m => m.LinkedMembershipApplicationId, (Guid?)application.Id).Update();
        return Db.Motions.Single(m => m.Id == motion.Id);
    }

    private string TokenWith(Guid chapterId, params string[] permissions) {
        var (_, token) = Builder.SeedAuthenticatedUser(email: $"{Guid.NewGuid()}@test.local",
            chapterPermissions: new Dictionary<Guid, string[]> { [chapterId] = permissions });
        return token;
    }

    private async Task<MotionDetailDTO> DetailAsync(Guid motionId, string? token = null) {
        using var client = token == null ? AnonymousClient() : AuthenticatedClient(token);
        var response = await client.GetAsync($"/api/motions/{motionId}");
        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<MotionDetailDTO>())!;
    }

    [Test]
    public async Task Detail_shows_next_take_over() {
        var motion = SeedApplicationMotion();

        var dto = await DetailAsync(motion.Id, TokenWith(_county.Id, PermissionIdentifier.ViewMotions));

        await Assert.That(dto.NextTakeOverChapterName).IsEqualTo("Land");
        await Assert.That(dto.NextTakeOverAt).IsNotNull();
        await Assert.That(dto.NextTakeOverAt!.Value).IsGreaterThan(DateTimeOffset.UtcNow.AddDays(26));
        await Assert.That(dto.NextTakeOverAt!.Value).IsLessThan(DateTimeOffset.UtcNow.AddDays(29));
    }

    [Test]
    public async Task Detail_of_replacement_names_predecessor() {
        var motion = SeedApplicationMotion();
        var replacement = Builder.SeedMotion(_state.Id, isPublic: false);
        Db.Motions.Where(m => m.Id == replacement.Id)
            .Set(m => m.LinkedMembershipApplicationId, motion.LinkedMembershipApplicationId)
            .Update();
        Db.Motions.Where(m => m.Id == motion.Id)
            .Set(m => m.ApprovalStatus, MotionApprovalStatus.ClosedWithoutAction)
            .Set(m => m.HandedOverToMotionId, (Guid?)replacement.Id)
            .Set(m => m.HandoverReason, (MotionHandoverReason?)MotionHandoverReason.Deadline)
            .Update();
        var token = TokenWith(_root.Id, PermissionIdentifier.ViewMotions);

        var detail = await DetailAsync(replacement.Id, token);
        var closed = await DetailAsync(motion.Id, token);

        await Assert.That(detail.HandedOverFromChapterName).IsEqualTo("Kreis");
        await Assert.That(detail.HandedOverFromReason).IsEqualTo(MotionHandoverReason.Deadline);
        await Assert.That(detail.ApplicationSubmittedAt).IsNotNull();
        await Assert.That(closed.HandoverReason).IsEqualTo(MotionHandoverReason.Deadline);
        await Assert.That(closed.HandedOverToChapterName).IsEqualTo("Land");
    }

    [Test]
    public async Task Detail_can_take_over_only_for_federal() {
        var selection = Builder.SeedDueSelection();
        var motion = Builder.SeedMotion(_county.Id, isPublic: false);
        Db.Motions.Where(m => m.Id == motion.Id).Set(m => m.LinkedDueSelectionId, (Guid?)selection.Id).Update();
        var viewer = new[] { PermissionIdentifier.ViewMotions, PermissionIdentifier.ProcessDueSelections };

        var federal = await DetailAsync(motion.Id, TokenWith(_root.Id, viewer));
        var state = await DetailAsync(motion.Id, TokenWith(_state.Id, viewer));

        await Assert.That(federal.CanTakeOver).IsTrue();
        await Assert.That(state.CanTakeOver).IsFalse();
    }

    [Test]
    public async Task Public_motion_hides_handover_fields_from_anonymous() {
        var motion = SeedApplicationMotion(isPublic: true);

        var dto = await DetailAsync(motion.Id);

        await Assert.That(dto.NextTakeOverAt).IsNull();
        await Assert.That(dto.NextTakeOverChapterName).IsNull();
        await Assert.That(dto.CanTakeOver).IsFalse();
        await Assert.That(dto.HandoverReason).IsNull();
        await Assert.That(dto.HandedOverFromChapterName).IsNull();
        await Assert.That(dto.ApplicationSubmittedAt).IsNull();
    }

    [Test]
    public async Task Application_detail_shows_next_take_over() {
        var motion = SeedApplicationMotion();
        var token = TokenWith(_county.Id, PermissionIdentifier.ViewApplications);
        using var client = AuthenticatedClient(token);

        var response = await client.GetAsync($"/api/admin/membershipapplications/{motion.LinkedMembershipApplicationId}");

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var dto = await response.Content.ReadFromJsonAsync<MembershipApplicationDetailDTO>();
        await Assert.That(dto!.NextTakeOverChapterName).IsEqualTo("Land");
        await Assert.That(dto.NextTakeOverAt).IsNotNull();
    }
}
