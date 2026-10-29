using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.Motions;
using Quartermaster.Data;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Tests.Infrastructure;

namespace Quartermaster.Server.Tests.Motions;

public class MotionTakeOverEndpointTests : IntegrationTestBase {
    private Chapter _root = null!;
    private Chapter _state = null!;
    private Chapter _county = null!;

    public MotionTakeOverEndpointTests() {
        DbContext.SupplementDefaults(Factory.Services);
        var chain = Builder.SeedChapterHierarchy("Bund", "Land", "Kreis");
        (_root, _state, _county) = (chain[0], chain[1], chain[2]);
    }

    private Motion SeedReductionMotion(Guid chapterId, MotionApprovalStatus status = MotionApprovalStatus.Pending) {
        var selection = Builder.SeedDueSelection();
        Db.DueSelections.Where(d => d.Id == selection.Id).Set(d => d.ChapterId, (Guid?)chapterId).Update();
        var motion = Builder.SeedMotion(chapterId, status: status, isPublic: false);
        Db.Motions.Where(m => m.Id == motion.Id).Set(m => m.LinkedDueSelectionId, (Guid?)selection.Id).Update();
        return Db.Motions.Single(m => m.Id == motion.Id);
    }

    private async Task<HttpResponseMessage> TakeOverAsync(string token, Guid motionId) {
        using var client = await AuthenticatedClientWithCsrfAsync(token);
        return await client.PostAsync($"/api/motions/{motionId}/take-over", null);
    }

    private string FederalOfficerToken() {
        var (_, token) = Builder.SeedAuthenticatedUser(email: "federal@test.local",
            chapterPermissions: new Dictionary<Guid, string[]> { [_root.Id] = [PermissionIdentifier.ProcessDueSelections] });
        return token;
    }

    [Test]
    public async Task Take_over_moves_reduction_motion_to_root() {
        var motion = SeedReductionMotion(_county.Id);

        var response = await TakeOverAsync(FederalOfficerToken(), motion.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<MotionTakeOverResponse>();
        var closed = Db.Motions.Single(m => m.Id == motion.Id);
        await Assert.That(closed.ApprovalStatus).IsEqualTo(MotionApprovalStatus.ClosedWithoutAction);
        await Assert.That(closed.HandoverReason).IsEqualTo(MotionHandoverReason.TakenOver);
        await Assert.That(closed.HandedOverToMotionId).IsEqualTo(body!.MotionId);
        var replacement = Db.Motions.Single(m => m.Id == body.MotionId);
        await Assert.That(replacement.ChapterId).IsEqualTo(_root.Id);
        await Assert.That(replacement.ApprovalStatus).IsEqualTo(MotionApprovalStatus.Pending);
        await Assert.That(Db.DueSelections.Single(d => d.Id == motion.LinkedDueSelectionId).ChapterId).IsEqualTo(_root.Id);
    }

    [Test]
    public async Task Take_over_refuses_application_motion() {
        var application = Builder.SeedMembershipApplication(_county.Id);
        var motion = Builder.SeedMotion(_county.Id, isPublic: false);
        Db.Motions.Where(m => m.Id == motion.Id).Set(m => m.LinkedMembershipApplicationId, (Guid?)application.Id).Update();

        var response = await TakeOverAsync(FederalOfficerToken(), motion.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Db.Motions.Single(m => m.Id == motion.Id).HandedOverToMotionId).IsNull();
    }

    [Test]
    public async Task Take_over_refuses_motion_at_root() {
        var motion = SeedReductionMotion(_root.Id);

        var response = await TakeOverAsync(FederalOfficerToken(), motion.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Db.Motions.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Take_over_refuses_decided_motion() {
        var motion = SeedReductionMotion(_county.Id, MotionApprovalStatus.Approved);

        var response = await TakeOverAsync(FederalOfficerToken(), motion.Id);

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(Db.Motions.Count()).IsEqualTo(1);
    }

    [Test]
    public async Task Take_over_refuses_handed_over_motion() {
        var motion = SeedReductionMotion(_county.Id);
        var first = await TakeOverAsync(FederalOfficerToken(), motion.Id);
        await Assert.That(first.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var second = await TakeOverAsync(FederalOfficerToken(), motion.Id);

        await Assert.That(second.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
    }

    [Test]
    public async Task Take_over_needs_permission_on_root() {
        var motion = SeedReductionMotion(_county.Id);
        var (_, stateToken) = Builder.SeedAuthenticatedUser(email: "state@test.local",
            chapterPermissions: new Dictionary<Guid, string[]> { [_state.Id] = [PermissionIdentifier.ProcessDueSelections] });

        var forbidden = await TakeOverAsync(stateToken, motion.Id);

        await Assert.That(forbidden.StatusCode).IsEqualTo(HttpStatusCode.Forbidden);
        await Assert.That(Db.Motions.Single(m => m.Id == motion.Id).HandedOverToMotionId).IsNull();

        var federal = await TakeOverAsync(FederalOfficerToken(), motion.Id);
        await Assert.That(federal.StatusCode).IsEqualTo(HttpStatusCode.OK);

        var other = SeedReductionMotion(_county.Id);
        var (_, adminToken) = Builder.SeedAuthenticatedUser(email: "admin@test.local", globalPermissions: [PermissionIdentifier.ProcessDueSelections]);
        var global = await TakeOverAsync(adminToken, other.Id);
        await Assert.That(global.StatusCode).IsEqualTo(HttpStatusCode.OK);
    }

    [Test]
    public async Task Take_over_unknown_motion_is_404() {
        var response = await TakeOverAsync(FederalOfficerToken(), Guid.NewGuid());

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Vote_on_handed_over_motion_is_refused() {
        var motion = SeedReductionMotion(_county.Id);
        var officer = Builder.SeedMember(_county.Id);
        Builder.SeedChapterOfficer(officer.Id, _county.Id);
        var take = await TakeOverAsync(FederalOfficerToken(), motion.Id);
        await Assert.That(take.StatusCode).IsEqualTo(HttpStatusCode.OK);
        var (_, voterToken) = Builder.SeedAuthenticatedUser(email: "voter@test.local", globalPermissions: [PermissionIdentifier.SystemVote]);
        using var client = await AuthenticatedClientWithCsrfAsync(voterToken);

        var response = await client.PostAsJsonAsync("/api/motions/vote", new MotionVoteRequest { MotionId = motion.Id, MemberId = officer.Id, Vote = VoteType.Approve });

        await Assert.That(response.StatusCode).IsEqualTo(HttpStatusCode.BadRequest);
        await Assert.That(await response.Content.ReadAsStringAsync()).Contains("error.motion.handed_over");
        await Assert.That(Db.MotionVotes.Count(v => v.MotionId == motion.Id)).IsEqualTo(0);
    }
}
