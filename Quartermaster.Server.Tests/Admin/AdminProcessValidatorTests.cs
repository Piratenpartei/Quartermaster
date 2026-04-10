using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.I18n;
using Quartermaster.Server.Admin;

namespace Quartermaster.Server.Tests.Admin;

public class DueSelectionProcessRequestValidatorTests {
    private readonly DueSelectionProcessRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new DueSelectionProcessRequest {
            Id = Guid.NewGuid(),
            Status = 1
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyId_ShouldHaveError() {
        var request = new DueSelectionProcessRequest {
            Id = Guid.Empty,
            Status = 1
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.IdRequired);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public void StatusInRange_ShouldHaveNoError(int status) {
        var request = new DueSelectionProcessRequest {
            Id = Guid.NewGuid(),
            Status = status
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Test]
    [Arguments(0)]
    [Arguments(3)]
    [Arguments(-1)]
    public void StatusOutOfRange_ShouldHaveError(int status) {
        var request = new DueSelectionProcessRequest {
            Id = Guid.NewGuid(),
            Status = status
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage(I18nKey.Error.Admin.DueSelection.StatusInvalid);
    }
}

public class MembershipApplicationProcessRequestValidatorTests {
    private readonly MembershipApplicationProcessRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new MembershipApplicationProcessRequest {
            Id = Guid.NewGuid(),
            Status = 1
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyId_ShouldHaveError() {
        var request = new MembershipApplicationProcessRequest {
            Id = Guid.Empty,
            Status = 1
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Id)
            .WithErrorMessage(I18nKey.Error.Admin.Application.IdRequired);
    }

    [Test]
    [Arguments(1)]
    [Arguments(2)]
    public void StatusInRange_ShouldHaveNoError(int status) {
        var request = new MembershipApplicationProcessRequest {
            Id = Guid.NewGuid(),
            Status = status
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Status);
    }

    [Test]
    [Arguments(0)]
    [Arguments(3)]
    [Arguments(-1)]
    public void StatusOutOfRange_ShouldHaveError(int status) {
        var request = new MembershipApplicationProcessRequest {
            Id = Guid.NewGuid(),
            Status = status
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Status)
            .WithErrorMessage(I18nKey.Error.Admin.Application.StatusInvalid);
    }
}
