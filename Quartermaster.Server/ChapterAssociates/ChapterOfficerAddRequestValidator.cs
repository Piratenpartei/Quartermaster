using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.ChapterAssociates;

namespace Quartermaster.Server.ChapterAssociates;

public class ChapterOfficerAddRequestValidator : Validator<ChapterOfficerAddRequest> {
    public ChapterOfficerAddRequestValidator() {
        RuleFor(x => x.MemberId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Mitglied muss ausgewählt werden.");

        RuleFor(x => x.ChapterId)
            .NotEqual(System.Guid.Empty)
            .WithMessage("Gliederung muss ausgewählt werden.");

        RuleFor(x => x.AssociateType)
            .InclusiveBetween(0, 6)
            .WithMessage("Ungültiger Vorstandstyp.");
    }
}
