using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.Events;
using Quartermaster.Api.I18n;
using Quartermaster.Server.Events;

namespace Quartermaster.Server.Tests.Events;

public class EventMeetingUrlTests {
    private readonly EventUpdateRequestValidator _updateValidator = new();
    private readonly EventCreateRequestValidator _createValidator = new();

    private static EventUpdateRequest Update(string? meetingUrl) {
        return new EventUpdateRequest {
            Id = Guid.NewGuid(),
            InternalName = "Kreisparteitag",
            PublicName = "Kreisparteitag",
            MeetingUrl = meetingUrl
        };
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments("https://meet.jit.si/kpt-celle")]
    [Arguments(" https://meet.piratenpartei.de/vorstand?x=1&y=2 ")]
    public void Empty_or_https_link_is_accepted(string? meetingUrl) {
        _updateValidator.TestValidate(Update(meetingUrl)).ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    [Arguments("http://meet.jit.si/kpt-celle")]
    [Arguments("javascript:alert(1)")]
    [Arguments("meet.jit.si/kpt-celle")]
    [Arguments("https://")]
    public void Other_links_are_refused(string meetingUrl) {
        _updateValidator.TestValidate(Update(meetingUrl)).ShouldHaveValidationErrorFor(x => x.MeetingUrl)
            .WithErrorMessage(I18nKey.Error.Event.MeetingUrlInvalid);
    }

    [Test]
    public void Too_long_link_is_refused() {
        var request = Update("https://meet.jit.si/" + new string('x', 490));

        _updateValidator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.MeetingUrl)
            .WithErrorMessage(I18nKey.Error.Event.MeetingUrlMaxLength);
    }

    [Test]
    public void Create_checks_the_link_too() {
        var request = new EventCreateRequest {
            ChapterId = Guid.NewGuid(),
            InternalName = "Kreisparteitag",
            PublicName = "Kreisparteitag",
            MeetingUrl = "http://meet.jit.si/kpt-celle"
        };

        _createValidator.TestValidate(request).ShouldHaveValidationErrorFor(x => x.MeetingUrl)
            .WithErrorMessage(I18nKey.Error.Event.MeetingUrlInvalid);
    }
}
