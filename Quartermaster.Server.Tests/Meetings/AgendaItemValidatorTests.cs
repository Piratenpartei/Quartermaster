using System;
using FluentValidation.TestHelper;
using Quartermaster.Api.I18n;
using Quartermaster.Api.Meetings;
using Quartermaster.Api.Motions;
using Quartermaster.Server.Meetings.Validators;

namespace Quartermaster.Server.Tests.Meetings;

public class AgendaItemCreateRequestValidatorTests {
    private readonly AgendaItemCreateRequestValidator _validator = new();

    private static AgendaItemCreateRequest Valid() => new() {
        MeetingId = Guid.NewGuid(),
        Title = "TOP 1",
        ItemType = AgendaItemType.Discussion
    };

    [Test]
    public void Valid_request_has_no_errors() {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Empty_meeting_id_errors() {
        var req = Valid();
        req.MeetingId = Guid.Empty;
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.MeetingId)
            .WithErrorMessage(I18nKey.Error.Meeting.Agenda.MeetingRequired);
    }

    [Test]
    public void Empty_title_errors() {
        var req = Valid();
        req.Title = "";
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(I18nKey.Error.Meeting.Agenda.TitleRequired);
    }

    [Test]
    public void Title_at_201_chars_errors() {
        var req = Valid();
        req.Title = new string('a', 201);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Title)
            .WithErrorMessage(I18nKey.Error.Meeting.Agenda.TitleMaxLength);
    }

    [Test]
    public void Invalid_item_type_errors() {
        var req = Valid();
        req.ItemType = (AgendaItemType)999;
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.ItemType)
            .WithErrorMessage(I18nKey.Error.Meeting.Agenda.ItemTypeInvalid);
    }
}

public class AgendaItemUpdateRequestValidatorTests {
    private readonly AgendaItemUpdateRequestValidator _validator = new();

    private static AgendaItemUpdateRequest Valid() => new() {
        MeetingId = Guid.NewGuid(),
        ItemId = Guid.NewGuid(),
        Title = "TOP",
        ItemType = AgendaItemType.Discussion
    };

    [Test]
    public void Valid_request_has_no_errors() {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Notes_max_length_enforced() {
        var req = Valid();
        req.Notes = new string('n', 20001);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Notes)
            .WithErrorMessage(I18nKey.Error.Meeting.Agenda.NotesMaxLength);
    }

    [Test]
    public void Resolution_max_length_enforced() {
        var req = Valid();
        req.Resolution = new string('r', 5001);
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Resolution)
            .WithErrorMessage(I18nKey.Error.Meeting.Agenda.ResolutionMaxLength);
    }

    [Test]
    public void Empty_item_id_errors() {
        var req = Valid();
        req.ItemId = Guid.Empty;
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.ItemId)
            .WithErrorMessage(I18nKey.Error.Meeting.Agenda.ItemRequired);
    }
}

public class AgendaItemMoveRequestValidatorTests {
    private readonly AgendaItemMoveRequestValidator _validator = new();

    [Test]
    public void Valid_request_has_no_errors() {
        var result = _validator.TestValidate(new AgendaItemMoveRequest {
            MeetingId = Guid.NewGuid(), ItemId = Guid.NewGuid(), NewParentId = null });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Empty_meeting_id_errors() {
        var result = _validator.TestValidate(new AgendaItemMoveRequest {
            MeetingId = Guid.Empty, ItemId = Guid.NewGuid() });
        result.ShouldHaveValidationErrorFor(x => x.MeetingId);
    }

    [Test]
    public void Empty_item_id_errors() {
        var result = _validator.TestValidate(new AgendaItemMoveRequest {
            MeetingId = Guid.NewGuid(), ItemId = Guid.Empty });
        result.ShouldHaveValidationErrorFor(x => x.ItemId);
    }
}

public class AgendaItemNotesRequestValidatorTests {
    private readonly AgendaItemNotesRequestValidator _validator = new();

    [Test]
    public void Valid_request_has_no_errors() {
        var result = _validator.TestValidate(new AgendaItemNotesRequest {
            MeetingId = Guid.NewGuid(), ItemId = Guid.NewGuid(), Notes = "x" });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Long_notes_errors() {
        var result = _validator.TestValidate(new AgendaItemNotesRequest {
            MeetingId = Guid.NewGuid(), ItemId = Guid.NewGuid(),
            Notes = new string('n', 20001) });
        result.ShouldHaveValidationErrorFor(x => x.Notes)
            .WithErrorMessage(I18nKey.Error.Meeting.Agenda.NotesMaxLength);
    }
}

public class AgendaItemReorderRequestValidatorTests {
    private readonly AgendaItemReorderRequestValidator _validator = new();

    [Test]
    public void Direction_up_passes() {
        var result = _validator.TestValidate(new AgendaItemReorderRequest {
            MeetingId = Guid.NewGuid(), ItemId = Guid.NewGuid(), Direction = -1 });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Direction_down_passes() {
        var result = _validator.TestValidate(new AgendaItemReorderRequest {
            MeetingId = Guid.NewGuid(), ItemId = Guid.NewGuid(), Direction = 1 });
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Direction_zero_errors() {
        var result = _validator.TestValidate(new AgendaItemReorderRequest {
            MeetingId = Guid.NewGuid(), ItemId = Guid.NewGuid(), Direction = 0 });
        result.ShouldHaveValidationErrorFor(x => x.Direction)
            .WithErrorMessage(I18nKey.Error.Meeting.Agenda.ReorderDirectionInvalid);
    }

    [Test]
    public void Direction_other_value_errors() {
        var result = _validator.TestValidate(new AgendaItemReorderRequest {
            MeetingId = Guid.NewGuid(), ItemId = Guid.NewGuid(), Direction = 5 });
        result.ShouldHaveValidationErrorFor(x => x.Direction);
    }
}

public class AgendaItemVoteRequestValidatorTests {
    private readonly AgendaItemVoteRequestValidator _validator = new();

    private static AgendaItemVoteRequest Valid() => new() {
        MeetingId = Guid.NewGuid(),
        ItemId = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Vote = VoteType.Approve
    };

    [Test]
    public void Valid_request_has_no_errors() {
        var result = _validator.TestValidate(Valid());
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Test]
    public void Empty_user_id_errors() {
        var req = Valid();
        req.UserId = Guid.Empty;
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Test]
    public void Invalid_vote_errors() {
        var req = Valid();
        req.Vote = (VoteType)999;
        var result = _validator.TestValidate(req);
        result.ShouldHaveValidationErrorFor(x => x.Vote)
            .WithErrorMessage(I18nKey.Error.Meeting.Agenda.VoteValueInvalid);
    }
}
