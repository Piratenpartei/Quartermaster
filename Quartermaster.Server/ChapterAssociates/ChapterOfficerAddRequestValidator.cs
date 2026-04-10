using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.I18n;

namespace Quartermaster.Server.ChapterAssociates;

public class ChapterOfficerAddRequestValidator : Validator<ChapterOfficerAddRequest> {
    public ChapterOfficerAddRequestValidator() {
        RuleFor(x => x.MemberId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Chapter.Officer.MemberRequired);

        RuleFor(x => x.ChapterId)
            .NotEqual(System.Guid.Empty)
            .WithMessage(I18nKey.Error.Chapter.Officer.ChapterRequired);

        RuleFor(x => x.AssociateType)
            .InclusiveBetween(0, 6)
            .WithMessage(I18nKey.Error.Chapter.Officer.InvalidOfficerType);
    }
}
