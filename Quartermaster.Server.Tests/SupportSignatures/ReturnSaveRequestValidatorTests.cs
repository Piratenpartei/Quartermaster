using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.I18n;
using Quartermaster.Api.SupportSignatures;
using Quartermaster.Server.SupportSignatures.Endpoints.Shipments;

namespace Quartermaster.Server.Tests.SupportSignatures;

public class ReturnSaveRequestValidatorTests {
    private readonly ReturnSaveRequestValidator _validator = new();

    private static ReturnSaveRequest With(ReturnLineInput line) {
        return new ReturnSaveRequest { ReturnedOn = new DateOnly(2027, 3, 1), Lines = [line] };
    }

    [Test]
    public void Line_with_counts_and_known_reason_has_no_errors() {
        var request = With(new ReturnLineInput { MunicipalityId = Guid.NewGuid(), Certified = 3, Rejected = 1, RejectionReason = SignatureRejectionReason.SignedTwice });

        _validator.TestValidate(request).ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Unknown_rejection_reason_is_refused() {
        var request = With(new ReturnLineInput { MunicipalityId = Guid.NewGuid(), Rejected = 1, RejectionReason = (SignatureRejectionReason)99 });

        _validator.TestValidate(request).ShouldHaveAnyValidationError()
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Return.ReasonInvalid);
    }

    [Test]
    public void Line_without_any_form_is_refused() {
        var request = With(new ReturnLineInput { MunicipalityId = Guid.NewGuid() });

        _validator.TestValidate(request).ShouldHaveAnyValidationError()
            .WithErrorMessage(I18nKey.Error.SupportSignatures.Return.EmptyLine);
    }
}
