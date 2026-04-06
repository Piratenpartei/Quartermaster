using FastEndpoints;
using Quartermaster.Api.Meetings;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.Meetings.Validators;

public class MeetingListRequestValidator : Validator<MeetingListRequest> {
    public MeetingListRequestValidator() {
        this.AddPaginationRules();
    }
}
