using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Data.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Offices;

public class CertificationOfficeSaveRequestValidator : Validator<CertificationOfficeSaveRequest> {
    public CertificationOfficeSaveRequestValidator() {
        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name))
            .WithMessage(I18nKey.Error.SupportSignatures.Office.NameRequired);

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Street) && !string.IsNullOrWhiteSpace(x.PostCode) && !string.IsNullOrWhiteSpace(x.City))
            .WithMessage(I18nKey.Error.SupportSignatures.Office.AddressRequired);

        RuleFor(x => x)
            .Must(FieldsFit)
            .WithMessage(I18nKey.Error.SupportSignatures.Office.FieldTooLong);

        RuleFor(x => x.Email)
            .Must(email => string.IsNullOrWhiteSpace(email) || email.Trim().Contains('@'))
            .WithMessage(I18nKey.Error.SupportSignatures.Office.EmailInvalid);

        RuleFor(x => x.MunicipalityIds)
            .NotNull()
            .WithMessage(I18nKey.Error.SupportSignatures.Office.NotAMunicipality);
    }

    private static bool FieldsFit(CertificationOfficeSaveRequest request) {
        if (request.Name?.Length > CertificationOffice.NameMaxLength || request.AddressAddition?.Length > CertificationOffice.AddressFieldMaxLength)
            return false;
        if (request.Street?.Length > CertificationOffice.AddressFieldMaxLength || request.PostCode?.Length > CertificationOffice.AddressFieldMaxLength)
            return false;
        if (request.City?.Length > CertificationOffice.AddressFieldMaxLength || request.Email?.Length > CertificationOffice.EmailMaxLength)
            return false;
        if (request.Phone?.Length > CertificationOffice.PhoneMaxLength)
            return false;
        return !(request.Notes?.Length > CertificationOffice.NotesMaxLength);
    }
}
