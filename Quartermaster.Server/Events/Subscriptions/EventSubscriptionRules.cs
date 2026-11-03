using Quartermaster.Api.Events;
using Quartermaster.Data.Events;

namespace Quartermaster.Server.Events.Subscriptions;

public static class EventSubscriptionRules {
    public static bool CanSubscribe(Event ev) {
        return ev.DeletedAt == null
            && ev.Status != EventStatus.Completed
            && ev.Status != EventStatus.Archived;
    }
}
