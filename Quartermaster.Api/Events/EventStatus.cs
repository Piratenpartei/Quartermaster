namespace Quartermaster.Api.Events;

public enum EventStatus {
    Draft = 0,
    Active = 1,
    Completed = 2,
    Archived = 3
}

public enum EventVisibility {
    Public = 0,
    MembersOnly = 1,
    Private = 2
}
