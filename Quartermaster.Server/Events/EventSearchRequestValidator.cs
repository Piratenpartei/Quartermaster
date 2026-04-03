using FastEndpoints;
using Quartermaster.Api.Events;
using Quartermaster.Server.Validation;

namespace Quartermaster.Server.Events;

public class EventSearchRequestValidator : Validator<EventSearchRequest> {
    public EventSearchRequestValidator() {
        this.AddPaginationRules();
    }
}
