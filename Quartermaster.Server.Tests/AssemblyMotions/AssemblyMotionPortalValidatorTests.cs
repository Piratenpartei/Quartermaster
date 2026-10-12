using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.AssemblyMotions;
using Quartermaster.Api.I18n;
using Quartermaster.Server.AssemblyMotions.Endpoints.Portal;

namespace Quartermaster.Server.Tests.AssemblyMotions;

public class AssemblyMotionPortalUpdateRequestValidatorTests {
    private static readonly DateTimeOffset Opens = new(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Closes = new(2026, 12, 1, 8, 0, 0, TimeSpan.Zero);

    private readonly AssemblyMotionPortalUpdateRequestValidator _validator = new();

    private static AssemblyMotionPortalUpdateRequest ValidRequest() => new() {
        Id = Guid.NewGuid(),
        OpensAt = Opens,
        ClosesAt = Closes
    };

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var result = _validator.TestValidate(ValidRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void ClosesAtNotAfterOpensAt_ShouldHaveError() {
        var request = ValidRequest();
        request.ClosesAt = request.OpensAt;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ClosesAt)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.Portal.ClosesBeforeOpens);
    }

    [Test]
    public void PrioritizationEnabledWithoutWindow_ShouldHaveErrors() {
        var request = ValidRequest();
        request.PrioritizationEnabled = true;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PrioritizationStartsAt)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.Portal.PrioritizationWindowRequired);
        result.ShouldHaveValidationErrorFor(x => x.PrioritizationEndsAt)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.Portal.PrioritizationWindowRequired);
    }

    [Test]
    public void PrioritizationDisabledWithoutWindow_ShouldHaveNoErrors() {
        var request = ValidRequest();
        request.PrioritizationEnabled = false;

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void PrioritizationEndNotAfterStart_ShouldHaveError() {
        var request = ValidRequest();
        request.PrioritizationEnabled = true;
        request.PrioritizationStartsAt = Opens.AddDays(10);
        request.PrioritizationEndsAt = Opens.AddDays(10);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PrioritizationEndsAt)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.Portal.PrioritizationWindowInvalid);
    }

    [Test]
    public void PrioritizationEndAfterClose_ShouldHaveError() {
        var request = ValidRequest();
        request.PrioritizationEnabled = true;
        request.PrioritizationStartsAt = Closes.AddDays(-1);
        request.PrioritizationEndsAt = Closes.AddDays(1);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.PrioritizationEndsAt)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.Portal.PrioritizationEndAfterClose);
    }

    [Test]
    public void PrioritizationEndEqualToClose_ShouldHaveNoErrors() {
        var request = ValidRequest();
        request.PrioritizationEnabled = true;
        request.PrioritizationStartsAt = Closes.AddDays(-1);
        request.PrioritizationEndsAt = Closes;

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void WikiMirrorEnabledWithoutPrefixAndLabel_ShouldHaveErrors() {
        var request = ValidRequest();
        request.WikiMirrorEnabled = true;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.WikiPagePrefix)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.Portal.WikiPagePrefixRequired);
        result.ShouldHaveValidationErrorFor(x => x.WikiAssemblyLabel)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.Portal.WikiAssemblyLabelRequired);
    }

    [Test]
    public void WikiMirrorEnabledWithPrefixAndLabel_ShouldHaveNoErrors() {
        var request = ValidRequest();
        request.WikiMirrorEnabled = true;
        request.WikiPagePrefix = "Antragsportal/BPT";
        request.WikiAssemblyLabel = "BPT 2026.2";

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    [Arguments("/Antrag")]
    [Arguments("Antrag/")]
    [Arguments(" Antrag")]
    [Arguments("Antrag ")]
    [Arguments("Antrag//BPT")]
    [Arguments("Antrag#BPT")]
    [Arguments("Antrag<BPT")]
    [Arguments("Antrag>BPT")]
    [Arguments("Antrag[BPT")]
    [Arguments("Antrag]BPT")]
    [Arguments("Antrag|BPT")]
    [Arguments("Antrag{BPT")]
    [Arguments("Antrag}BPT")]
    public void InvalidWikiPagePrefix_ShouldHaveError(string prefix) {
        var request = ValidRequest();
        request.WikiPagePrefix = prefix;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.WikiPagePrefix)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.WikiPrefixInvalid);
    }

    [Test]
    public void WikiPagePrefixLongerThanColumn_ShouldHaveError() {
        var request = ValidRequest();
        request.WikiPagePrefix = new string('a', 301);

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.WikiPagePrefix)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.WikiPrefixInvalid);
    }

    [Test]
    public void WikiPagePrefixWithNamespaceAndSlashes_ShouldHaveNoErrors() {
        var request = ValidRequest();
        request.WikiPagePrefix = "Antrag:Bundesparteitag 2027.1/Antragsportal";

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class AssemblyMotionPortalEnableRequestValidatorTests {
    private readonly AssemblyMotionPortalEnableRequestValidator _validator = new();

    [Test]
    public void ClosesAtNotAfterOpensAt_ShouldHaveError() {
        var now = DateTimeOffset.UtcNow;
        var request = new AssemblyMotionPortalEnableRequest {
            EventId = Guid.NewGuid(),
            OpensAt = now,
            ClosesAt = now.AddMinutes(-1)
        };

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.ClosesAt)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.Portal.ClosesBeforeOpens);
    }

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var now = DateTimeOffset.UtcNow;
        var request = new AssemblyMotionPortalEnableRequest {
            EventId = Guid.NewGuid(),
            OpensAt = now,
            ClosesAt = now.AddDays(7)
        };

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}

public class AssemblyMotionTypeRequestValidatorTests {
    private readonly AssemblyMotionTypeCreateRequestValidator _validator = new();

    private static AssemblyMotionTypeCreateRequest ValidRequest() => new() {
        PortalId = Guid.NewGuid(),
        Code = "SÄA",
        Name = "Satzungsänderungsantrag",
        MinSubmitters = 5
    };

    [Test]
    public void ValidRequest_ShouldHaveNoErrors() {
        var result = _validator.TestValidate(ValidRequest());

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    [Arguments("")]
    [Arguments("ABCDEFGHIJKLMNOPQ")]
    [Arguments("G1")]
    [Arguments("G P")]
    public void InvalidCode_ShouldHaveError(string code) {
        var request = ValidRequest();
        request.Code = code;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Code)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.Type.CodeInvalid);
    }

    [Test]
    public void SixteenLetterCode_ShouldHaveNoErrors() {
        var request = ValidRequest();
        request.Code = "ABCDEFGHIJKLMNOP";

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveValidationErrorFor(x => x.Code);
    }

    [Test]
    public void EmptyName_ShouldHaveError() {
        var request = ValidRequest();
        request.Name = "";

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.Name)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.Type.NameRequired);
    }

    [Test]
    public void ZeroMinSubmitters_ShouldHaveError() {
        var request = ValidRequest();
        request.MinSubmitters = 0;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.MinSubmitters)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.Type.MinSubmittersInvalid);
    }

    [Test]
    public void CompetingDeadlineNotAfterDeadline_ShouldHaveError() {
        var deadline = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        var request = ValidRequest();
        request.Deadline = deadline;
        request.CompetingDeadline = deadline;

        var result = _validator.TestValidate(request);

        result.ShouldHaveValidationErrorFor(x => x.CompetingDeadline)
            .WithErrorMessage(I18nKey.Error.AssemblyMotion.Type.CompetingDeadlineInvalid);
    }

    [Test]
    public void CompetingDeadlineAfterDeadline_ShouldHaveNoErrors() {
        var deadline = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero);
        var request = ValidRequest();
        request.Deadline = deadline;
        request.CompetingDeadline = deadline.AddDays(1);

        var result = _validator.TestValidate(request);

        result.ShouldNotHaveAnyValidationErrors();
    }
}
