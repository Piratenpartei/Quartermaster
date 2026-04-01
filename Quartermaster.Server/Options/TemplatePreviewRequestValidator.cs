using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.Options;

namespace Quartermaster.Server.Options;

public class TemplatePreviewRequestValidator : Validator<TemplatePreviewRequest> {
    public TemplatePreviewRequestValidator() {
        RuleFor(x => x.TemplateText)
            .NotEmpty()
            .WithMessage("Vorlagentext darf nicht leer sein.");
    }
}
