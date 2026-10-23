using System;
using System.Linq;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Campaigns;

public class SignatureCampaignSaveRequestValidator : Validator<SignatureCampaignSaveRequest> {
    public const int MaxMarginPercent = 100;
    public const int MaxOverdueDays = 365;

    public SignatureCampaignSaveRequestValidator() {
        RuleFor(x => x.ChapterId)
            .NotEqual(Guid.Empty)
            .WithMessage(I18nKey.Error.SupportSignatures.Campaign.ChapterRequired);

        RuleFor(x => x.Deadline)
            .NotEqual(default(DateTimeOffset))
            .WithMessage(I18nKey.Error.SupportSignatures.Campaign.DeadlineRequired);

        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage(I18nKey.Error.SupportSignatures.Campaign.NameRequired);

        RuleFor(x => x.Name)
            .Must(name => name == null || name.Trim().Length <= SignatureCampaign.NameMaxLength)
            .WithMessage(I18nKey.Error.SupportSignatures.Campaign.NameTooLong);

        RuleFor(x => x.Target)
            .GreaterThan(0)
            .WithMessage(I18nKey.Error.SupportSignatures.Campaign.TargetInvalid);

        RuleFor(x => x.MarginPercent)
            .InclusiveBetween(0, MaxMarginPercent)
            .WithMessage(I18nKey.Error.SupportSignatures.Campaign.MarginInvalid);

        RuleFor(x => x.OverdueDays)
            .InclusiveBetween(1, MaxOverdueDays)
            .WithMessage(I18nKey.Error.SupportSignatures.Campaign.OverdueDaysInvalid);

        RuleFor(x => x.MemberNotice)
            .Must(notice => notice == null || notice.Length <= SignatureCampaign.MemberNoticeMaxLength)
            .WithMessage(I18nKey.Error.SupportSignatures.Campaign.NoticeTooLong);

        RuleFor(x => x.Visibility)
            .IsInEnum()
            .WithMessage(I18nKey.Error.SupportSignatures.Campaign.VisibilityInvalid);

        RuleFor(x => x.PostalAddress)
            .Must(address => address == null || address.Trim().Length <= SignatureCampaign.PostalAddressMaxLength)
            .WithMessage(I18nKey.Error.SupportSignatures.Campaign.PostalAddressTooLong);

        RuleFor(x => x.ContactEmail)
            .Must(email => email == null || email.Trim().Length <= SignatureCampaign.ContactEmailMaxLength)
            .WithMessage(I18nKey.Error.SupportSignatures.Campaign.ContactEmailTooLong);

        RuleFor(x => x.ContactEmail)
            .Must(IsValidEmail)
            .WithMessage(I18nKey.Error.SupportSignatures.Campaign.ContactEmailInvalid);
    }

    private static bool IsValidEmail(string? email) {
        if (string.IsNullOrWhiteSpace(email))
            return true;
        var trimmed = email.Trim();
        return trimmed.Contains('@') && !trimmed.Any(char.IsWhiteSpace);
    }
}
