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
            AssociateType = ChapterOfficerType.Treasurer
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void EmptyMemberId_ShouldHaveError() {
        var request = new ChapterOfficerAddRequest {
            MemberId = Guid.Empty,
            ChapterId = Guid.NewGuid(),
            AssociateType = ChapterOfficerType.Captain
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
            AssociateType = ChapterOfficerType.Captain
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ChapterId)
            .WithErrorMessage(I18nKey.Error.Chapter.Officer.ChapterRequired);
    }

    [Test]
    [Arguments(ChapterOfficerType.Captain)]
    [Arguments(ChapterOfficerType.Treasurer)]
    [Arguments(ChapterOfficerType.Member)]
    public void AssociateTypeInRange_ShouldHaveNoError(ChapterOfficerType associateType) {
        var request = new ChapterOfficerAddRequest {
            MemberId = Guid.NewGuid(),
            ChapterId = Guid.NewGuid(),
            AssociateType = associateType
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.AssociateType);
    }

    [Test]
    [Arguments((ChapterOfficerType)(-1))]
    [Arguments((ChapterOfficerType)7)]
    [Arguments((ChapterOfficerType)100)]
    public void AssociateTypeOutOfRange_ShouldHaveError(ChapterOfficerType associateType) {
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
