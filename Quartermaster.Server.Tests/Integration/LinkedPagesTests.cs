using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Json;
using System.Threading.Tasks;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Api.DueSelector;
using Quartermaster.Api.MembershipApplications;
using Quartermaster.Api.Members;
using Quartermaster.Api.Motions;
using Quartermaster.Data.Chapters;
using Quartermaster.Data.DueSelector;
using Quartermaster.Data.MembershipApplications;
using Quartermaster.Data.Motions;
using Quartermaster.Server.Tests.Infrastructure;
using Quartermaster.Server.Users;

namespace Quartermaster.Server.Tests.Integration;

/// <summary>Detail pages offer buttons to their linked pages, but only to pages the viewer may open.</summary>
public class LinkedPagesTests : IntegrationTestBase {
    private static readonly string[] _processorPermissions = [
        PermissionIdentifier.ViewMotions, PermissionIdentifier.ViewApplications, PermissionIdentifier.ViewDueSelections
    ];

    [Test]
    public async Task An_application_motion_offers_its_application_and_reduction() {
        var (chapter, application, reduction) = SeedApplicationWithDues(SelectedValuation.Reduced);
        var motion = SeedMotion(chapter.Id, applicationId: application.Id);

        var dto = await GetMotionAsync(motion.Id, ProcessorOf(chapter.Id));

        await Assert.That(dto.ViewableApplicationId).IsEqualTo(application.Id);
        await Assert.That(dto.ViewableReductionId).IsEqualTo(reduction.Id);
    }

    [Test]
    public async Task A_regular_due_selection_is_not_offered_as_a_reduction() {
        var (chapter, application, _) = SeedApplicationWithDues(SelectedValuation.MonthlyPayGroup);
        var motion = SeedMotion(chapter.Id, applicationId: application.Id);

        var dto = await GetMotionAsync(motion.Id, ProcessorOf(chapter.Id));

        await Assert.That(dto.ViewableApplicationId).IsEqualTo(application.Id);
        await Assert.That(dto.ViewableReductionId).IsNull();
    }

    [Test]
    public async Task A_reduction_motion_offers_its_reduction_and_application() {
        var (chapter, application, reduction) = SeedApplicationWithDues(SelectedValuation.Reduced);
        var motion = SeedMotion(chapter.Id, dueSelectionId: reduction.Id);

        var dto = await GetMotionAsync(motion.Id, ProcessorOf(chapter.Id));

        await Assert.That(dto.ViewableReductionId).IsEqualTo(reduction.Id);
        await Assert.That(dto.ViewableApplicationId).IsEqualTo(application.Id);
    }

    [Test]
    public async Task A_motion_does_not_offer_pages_the_viewer_may_not_open() {
        var (chapter, application, _) = SeedApplicationWithDues(SelectedValuation.Reduced);
        var motion = SeedMotion(chapter.Id, applicationId: application.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewMotions } });

        var dto = await GetMotionAsync(motion.Id, token);

        await Assert.That(dto.ViewableApplicationId).IsNull();
        await Assert.That(dto.ViewableReductionId).IsNull();
    }

    [Test]
    public async Task An_application_offers_its_motion_and_reduction() {
        var (chapter, application, reduction) = SeedApplicationWithDues(SelectedValuation.Reduced);
        var motion = SeedMotion(chapter.Id, applicationId: application.Id);

        using var client = AuthenticatedClient(ProcessorOf(chapter.Id));
        var dto = await client.GetFromJsonAsync<MembershipApplicationDetailDTO>($"/api/admin/membershipapplications/{application.Id}");

        await Assert.That(dto!.ViewableMotionId).IsEqualTo(motion.Id);
        await Assert.That(dto.ViewableReductionId).IsEqualTo(reduction.Id);
    }

    [Test]
    public async Task An_application_does_not_offer_a_motion_the_viewer_may_not_open() {
        var (chapter, application, _) = SeedApplicationWithDues(SelectedValuation.Reduced);
        SeedMotion(chapter.Id, applicationId: application.Id);
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewApplications } });

        using var client = AuthenticatedClient(token);
        var dto = await client.GetFromJsonAsync<MembershipApplicationDetailDTO>($"/api/admin/membershipapplications/{application.Id}");

        await Assert.That(dto!.ViewableMotionId).IsNull();
        await Assert.That(dto.ViewableReductionId).IsNull();
    }

    [Test]
    public async Task A_due_selection_offers_its_motion_and_application() {
        var (chapter, application, reduction) = SeedApplicationWithDues(SelectedValuation.Reduced);
        var motion = SeedMotion(chapter.Id, dueSelectionId: reduction.Id);

        using var client = AuthenticatedClient(ProcessorOf(chapter.Id));
        var dto = await client.GetFromJsonAsync<DueSelectionDetailDTO>($"/api/admin/dueselections/{reduction.Id}");

        await Assert.That(dto!.ViewableMotionId).IsEqualTo(motion.Id);
        await Assert.That(dto.ViewableApplicationId).IsEqualTo(application.Id);
    }

    [Test]
    public async Task A_member_offers_their_user_account_to_user_viewers_only() {
        var chapter = Builder.SeedChapter("C");
        var user = Builder.SeedUser();
        var member = Builder.SeedMember(chapter.Id, memberNumber: 4711, userId: user.Id);
        var (_, userViewer) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers, PermissionIdentifier.ViewAllMembers });
        var (_, memberViewer) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewMembers } });

        using var userClient = AuthenticatedClient(userViewer);
        using var memberClient = AuthenticatedClient(memberViewer);
        var forUserViewer = await userClient.GetFromJsonAsync<MemberDetailDTO>($"/api/members/{member.Id}");
        var forMemberViewer = await memberClient.GetFromJsonAsync<MemberDetailDTO>($"/api/members/{member.Id}");

        await Assert.That(forUserViewer!.ViewableUserId).IsEqualTo(user.Id);
        await Assert.That(forMemberViewer!.ViewableUserId).IsNull();
    }

    [Test]
    public async Task A_user_account_offers_its_member_to_member_viewers_only() {
        var chapter = Builder.SeedChapter("C");
        var user = Builder.SeedUser();
        var member = Builder.SeedMember(chapter.Id, memberNumber: 4711, userId: user.Id);
        var (_, memberViewer) = Builder.SeedAuthenticatedUser(
            globalPermissions: new[] { PermissionIdentifier.ViewUsers },
            chapterPermissions: new() { [chapter.Id] = new[] { PermissionIdentifier.ViewMembers } });
        var (_, userViewer) = Builder.SeedAuthenticatedUser(globalPermissions: new[] { PermissionIdentifier.ViewUsers });

        using var memberClient = AuthenticatedClient(memberViewer);
        using var userClient = AuthenticatedClient(userViewer);
        var forMemberViewer = await memberClient.GetFromJsonAsync<UserDetailResponse>($"/api/users/{user.Id}");
        var forUserViewer = await userClient.GetFromJsonAsync<UserDetailResponse>($"/api/users/{user.Id}");

        await Assert.That(forMemberViewer!.ViewableMemberId).IsEqualTo(member.Id);
        await Assert.That(forUserViewer!.ViewableMemberId).IsNull();
    }

    private string ProcessorOf(Guid chapterId) {
        var (_, token) = Builder.SeedAuthenticatedUser(
            chapterPermissions: new Dictionary<Guid, string[]> { [chapterId] = _processorPermissions });
        return token;
    }

    private (Chapter Chapter, MembershipApplication Application, DueSelection DueSelection) SeedApplicationWithDues(SelectedValuation valuation) {
        var chapter = Builder.SeedChapter("C");
        var dueSelection = Builder.SeedDueSelection();
        Db.DueSelections.Where(d => d.Id == dueSelection.Id)
            .Set(d => d.SelectedValuation, valuation)
            .Set(d => d.ChapterId, (Guid?)chapter.Id)
            .Update();
        var application = Builder.SeedMembershipApplication(chapter.Id);
        Db.MembershipApplications.Where(a => a.Id == application.Id)
            .Set(a => a.DueSelectionId, (Guid?)dueSelection.Id)
            .Update();
        return (chapter, application, dueSelection);
    }

    private Motion SeedMotion(Guid chapterId, Guid? applicationId = null, Guid? dueSelectionId = null) {
        var motion = Builder.SeedMotion(chapterId, isPublic: false);
        Db.Motions.Where(m => m.Id == motion.Id)
            .Set(m => m.LinkedMembershipApplicationId, applicationId)
            .Set(m => m.LinkedDueSelectionId, dueSelectionId)
            .Update();
        return motion;
    }

    private async Task<MotionDetailDTO> GetMotionAsync(Guid motionId, string token) {
        using var client = AuthenticatedClient(token);
        return (await client.GetFromJsonAsync<MotionDetailDTO>($"/api/motions/{motionId}"))!;
    }
}
