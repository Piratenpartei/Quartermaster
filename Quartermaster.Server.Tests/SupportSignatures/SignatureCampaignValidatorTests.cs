using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Server.SupportSignatures.Endpoints.Campaigns;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class SignatureCampaignValidatorTests {
    private readonly SignatureCampaignSaveRequestValidator _validator = new();

    private static SignatureCampaignSaveRequest Valid() {
        return new SignatureCampaignSaveRequest {
            ChapterId = Guid.NewGuid(),
            Name = "Landesliste Landtagswahl 2027",
            Target = 2000,
            MarginPercent = 20,
            Deadline = new DateTimeOffset(2027, 6, 1, 0, 0, 0, TimeSpan.Zero),
            OverdueDays = 21
        };
    }

    [Test]
    public void Valid_request_has_no_errors() {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Target_zero_is_refused() {
        var request = Valid();
        request.Target = 0;

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Target)
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Campaign.TargetInvalid);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(101)]
    public void Margin_outside_0_to_100_is_refused(int margin) {
        var request = Valid();
        request.MarginPercent = margin;

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.MarginPercent)
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Campaign.MarginInvalid);
    }

    [Test]
    [Arguments(0)]
    [Arguments(366)]
    public void Overdue_days_outside_1_to_365_are_refused(int days) {
        var request = Valid();
        request.OverdueDays = days;

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.OverdueDays)
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Campaign.OverdueDaysInvalid);
    }

    [Test]
    public void Name_empty_or_too_long_is_refused() {
        var empty = Valid();
        empty.Name = " ";
        var tooLong = Valid();
        tooLong.Name = new string('x', 201);

        _validator.TestValidate(empty).ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Campaign.NameRequired);
        _validator.TestValidate(tooLong).ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Campaign.NameTooLong);
    }

    [Test]
    public void Notice_too_long_is_refused() {
        var request = Valid();
        request.MemberNotice = new string('x', 2001);

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.MemberNotice)
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Campaign.NoticeTooLong);
    }

    [Test]
    public void Missing_chapter_is_refused() {
        var request = Valid();
        request.ChapterId = Guid.Empty;

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.ChapterId)
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Campaign.ChapterRequired);
    }

    [Test]
    public void Missing_deadline_is_refused() {
        var request = Valid();
        request.Deadline = default;

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Deadline)
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Campaign.DeadlineRequired);
    }

    [Test]
    public void Unknown_visibility_is_refused() {
        var request = Valid();
        request.Visibility = (SignatureCampaignVisibility)7;

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.Visibility)
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Campaign.VisibilityInvalid);
    }

    [Test]
    public void Postal_address_too_long_is_refused() {
        var request = Valid();
        request.PostalAddress = new string('x', 501);

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.PostalAddress)
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Campaign.PostalAddressTooLong);
    }

    [Test]
    [Arguments("keine-adresse")]
    [Arguments("sammeln @piraten-nds.de")]
    public void Invalid_contact_email_is_refused(string email) {
        var request = Valid();
        request.ContactEmail = email;

        _validator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.ContactEmail)
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Campaign.ContactEmailInvalid);
    }

    [Test]
    public void Contact_email_and_address_are_optional() {
        var request = Valid();
        request.ContactEmail = " ";
        request.PostalAddress = "";

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }
}
