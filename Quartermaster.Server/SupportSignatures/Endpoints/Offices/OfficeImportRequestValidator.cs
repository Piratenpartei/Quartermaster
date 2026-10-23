using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Offices;

public class OfficeImportRequestValidator : Validator<OfficeImportRequest> {
    public OfficeImportRequestValidator() {
        RuleFor(x => x.Content)
            .Must(content => content != null && content.Length > 0)
            .WithMessage(I18nKey.Error.SupportSignatures.OfficeImport.Empty);

        RuleFor(x => x.Content)
            .Must(content => content == null || content.Length <= OfficeImportRequest.MaxContentBytes)
            .WithMessage(I18nKey.Error.SupportSignatures.OfficeImport.FileTooLarge);
    }
}
