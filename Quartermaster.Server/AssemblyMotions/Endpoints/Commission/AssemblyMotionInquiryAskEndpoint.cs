using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Server.AssemblyMotions.Endpoints.Members;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Commission;

public class AssemblyMotionInquiryAskRequestValidator : Validator<AssemblyMotionInquiryAskRequest> {
    public AssemblyMotionInquiryAskRequestValidator() {
        RuleFor(x => x.QuestionMarkdown)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(I18nKey.Error.AssemblyMotion.InquiryQuestionRequired)
            .MaximumLength(20000)
            .WithMessage(I18nKey.Error.AssemblyMotion.InquiryQuestionMaxLength)
            .RestrictedMarkdown();
    }
}

public class AssemblyMotionInquiryAskEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionInquiryAskRequest> {
    private readonly AssemblyMotionCommissionActions _actions;
    private readonly AssemblyMotionInquiryService _service;

    public AssemblyMotionInquiryAskEndpoint(AssemblyMotionCommissionActions actions, AssemblyMotionInquiryService service) {
        _actions = actions;
        _service = service;
    }

    public override void Configure() {
        Post("/api/assemblymotions/motions/{MotionId}/inquiries");
    }

    public override async Task HandleAsync(AssemblyMotionInquiryAskRequest req, CancellationToken ct) {
        var result = _actions.Run(req.MotionId, (userId, nowUtc)
            => _service.Ask(req.MotionId, req.QuestionMarkdown.Trim(), req.ResponseDueAt.UtcDateTime, userId, nowUtc));
        await SendResultAsync(result, ct);
    }
}
