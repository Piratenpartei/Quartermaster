using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.MembershipApplications;

namespace Quartermaster.Server.MembershipApplications;

public class MembershipApplicationDTOValidator : Validator<MembershipApplicationDTO> {
    public MembershipApplicationDTOValidator() {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Admin.Application.FirstNameRequired)
            .MaximumLength(256)
            .WithMessage(I18nKey.Error.Admin.Application.FirstNameMaxLength);

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Admin.Application.LastNameRequired)
            .MaximumLength(256)
            .WithMessage(I18nKey.Error.Admin.Application.LastNameMaxLength);

        RuleFor(x => x.EMail)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Admin.Application.EmailRequired)
            .Must(e => e != null && e.Contains('@'))
            .WithMessage(I18nKey.Error.Admin.Application.EmailInvalid)
            .MaximumLength(256)
            .WithMessage(I18nKey.Error.Admin.Application.EmailMaxLength);

        RuleFor(x => x.Citizenship)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Admin.Application.NationalityRequired)
            .MaximumLength(256)
            .WithMessage(I18nKey.Error.Admin.Application.NationalityMaxLength);

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(64)
            .WithMessage(I18nKey.Error.Admin.Application.PhoneMaxLength);

        RuleFor(x => x.AddressStreet)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Admin.Application.StreetRequired)
            .MaximumLength(256)
            .WithMessage(I18nKey.Error.Admin.Application.StreetMaxLength);

        RuleFor(x => x.AddressHouseNbr)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Admin.Application.HouseNumberRequired)
            .MaximumLength(32)
            .WithMessage(I18nKey.Error.Admin.Application.HouseNumberMaxLength);

        RuleFor(x => x.AddressPostCode)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Admin.Application.PostalCodeRequired)
            .MaximumLength(16)
            .WithMessage(I18nKey.Error.Admin.Application.PostalCodeMaxLength);

        RuleFor(x => x.AddressCity)
            .NotEmpty()
            .WithMessage(I18nKey.Error.Admin.Application.CityRequired)
            .MaximumLength(256)
            .WithMessage(I18nKey.Error.Admin.Application.CityMaxLength);

        RuleFor(x => x.ApplicationText)
            .MaximumLength(2048)
            .WithMessage(I18nKey.Error.Admin.Application.BodyMaxLength);

        RuleFor(x => x.ConformityDeclarationAccepted)
            .Equal(true)
            .WithMessage(I18nKey.Error.Admin.Application.DeclarationRequired);
    }
}
