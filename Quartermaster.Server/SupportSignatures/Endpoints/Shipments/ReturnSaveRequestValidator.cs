using System;
using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Shipments;

public class ReturnSaveRequestValidator : Validator<ReturnSaveRequest> {
    public ReturnSaveRequestValidator() {
        RuleForEach(x => x.Lines)
            .Must(line => line.Certified >= 0 && line.Rejected >= 0)
            .WithMessage(I18nKey.Error.SupportSignatures.Return.NegativeCount);

        RuleForEach(x => x.Lines)
            .Must(line => line.Certified + line.Rejected > 0)
            .WithMessage(I18nKey.Error.SupportSignatures.Return.EmptyLine);

        RuleForEach(x => x.Lines)
            .Must(line => line.RejectionReason == null || Enum.IsDefined(line.RejectionReason.Value))
            .WithMessage(I18nKey.Error.SupportSignatures.Return.ReasonInvalid);
    }
}
