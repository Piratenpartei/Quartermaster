using FastEndpoints;
using FluentValidation;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Server.SupportSignatures.Endpoints.Shipments;

public class SingleShipmentRequestValidator : Validator<SingleShipmentRequest> {
    public SingleShipmentRequestValidator() {
        RuleForEach(x => x.Lines)
            .Must(line => line.Count >= 0)
            .WithMessage(I18nKey.Error.SupportSignatures.Shipment.NegativeCount);
    }
}
