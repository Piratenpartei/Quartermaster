using System.Threading;
using System.Threading.Tasks;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Data.AssemblyMotions;

namespace Quartermaster.Server.AssemblyMotions.Endpoints.Members;

public class AssemblyMotionInquiryAnswerRequestValidator : Validator<AssemblyMotionInquiryAnswerRequest> {
    public AssemblyMotionInquiryAnswerRequestValidator() {
        RuleFor(x => x.AnswerMarkdown)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage(I18nKey.Error.AssemblyMotion.InquiryAnswerRequired)
            .MaximumLength(20000)
            .WithMessage(I18nKey.Error.AssemblyMotion.InquiryAnswerMaxLength)
            .RestrictedMarkdown();
    }
}

public class AssemblyMotionInquiryAnswerEndpoint : AssemblyMotionMemberEndpoint<AssemblyMotionInquiryAnswerRequest> {
    private readonly AssemblyMotionMemberActions _actions;
    private readonly AssemblyMotionInquiryRepository _inquiries;
    private readonly AssemblyMotionInquiryService _service;

    public AssemblyMotionInquiryAnswerEndpoint(AssemblyMotionMemberActions actions,
        AssemblyMotionInquiryRepository inquiries, AssemblyMotionInquiryService service) {
        _actions = actions;
        _inquiries = inquiries;
        _service = service;
    }

    public override void Configure() {
        Post("/api/assemblymotions/inquiries/{Id}/answer");
    }

    public override async Task HandleAsync(AssemblyMotionInquiryAnswerRequest req, CancellationToken ct) {
        var inquiry = _inquiries.Get(req.Id);
        if (inquiry == null) {
            await SendNotFoundAsync(ct);
            return;
        }

        var result = _actions.Run(inquiry.MotionId, false, (_, member, _, nowUtc)
            => _service.Answer(req.Id, member, req.AnswerMarkdown, nowUtc));
        await SendResultAsync(result, ct);
    }
}
