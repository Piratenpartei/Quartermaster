using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.Admin;

public class DueSelectionProcessRequestValidator : Validator<DueSelectionProcessRequest> {
    public DueSelectionProcessRequestValidator() {
        RuleFor(x => x.Id)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Admin.DueSelection.IdRequired);

        RuleFor(x => x.Status)
            .IsInEnum()
            .WithMessage(I18nKey.Error.Admin.DueSelection.StatusInvalid);
    }
}
