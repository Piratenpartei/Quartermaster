using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.ChapterAssociates;
using Quartermaster.Api.I18n;
using Quartermaster.Server.ChapterAssociates;

namespace Quartermaster.Server.Tests.ChapterAssociates;

public class ChapterOfficerAddRequestValidatorTests {
    private readonly ChapterOfficerAddRequestValidator _validator = new();

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var request = new ChapterOfficerAddRequest {
            MemberId = Guid.NewGuid(),
            ChapterId = Guid.NewGuid(),
            AssociateType = 3
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyMemberId_ShouldHaveError() {
        var request = new ChapterOfficerAddRequest {
            MemberId = Guid.Empty,
            ChapterId = Guid.NewGuid(),
            AssociateType = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.MemberId)
            .WithErrorMessage(I18nKey.Error.Chapter.Officer.MemberRequired);
    }

    [Test]
    public void EmptyChapterId_ShouldHaveError() {
        var request = new ChapterOfficerAddRequest {
            MemberId = Guid.NewGuid(),
            ChapterId = Guid.Empty,
            AssociateType = 0
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ChapterId)
            .WithErrorMessage(I18nKey.Error.Chapter.Officer.ChapterRequired);
    }

    [Test]
    [Arguments(0)]
    [Arguments(3)]
    [Arguments(6)]
    public void AssociateTypeInRange_ShouldHaveNoError(int associateType) {
        var request = new ChapterOfficerAddRequest {
            MemberId = Guid.NewGuid(),
            ChapterId = Guid.NewGuid(),
            AssociateType = associateType
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.AssociateType);
    }

    [Test]
    [Arguments(-1)]
    [Arguments(7)]
    [Arguments(100)]
    public void AssociateTypeOutOfRange_ShouldHaveError(int associateType) {
        var request = new ChapterOfficerAddRequest {
            MemberId = Guid.NewGuid(),
            ChapterId = Guid.NewGuid(),
            AssociateType = associateType
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.AssociateType)
            .WithErrorMessage(I18nKey.Error.Chapter.Officer.InvalidOfficerType);
    }
}
