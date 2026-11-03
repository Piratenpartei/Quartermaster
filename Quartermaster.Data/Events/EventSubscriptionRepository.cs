using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using MySqlConnector;
using Quartermaster.Api.Events;

namespace Quartermaster.Data.Events;

public class EventSubscriptionRepository {
    private readonly DbContext _context;

    public EventSubscriptionRepository(DbContext context) {
        _context = context;
    }

    /// <summary>Subscribes the user; an existing subscription is returned unchanged.</summary>
    public EventSubscription Subscribe(Guid eventId, Guid userId, DateTime nowUtc) {
        var existing = Get(eventId, userId);
        if (existing != null)
            return existing;

        var subscription = new EventSubscription { EventId = eventId, UserId = userId, CreatedAt = nowUtc };
        try {
            _context.Insert(subscription);
        } catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry) {
            return Get(eventId, userId) ?? throw new InvalidOperationException($"Subscription of {userId} to {eventId} vanished after a duplicate key.", ex);
        }
        return subscription;
    }

    public void Unsubscribe(Guid eventId, Guid userId) {
        var existing = Get(eventId, userId);
        if (existing == null)
            return;

        using var tx = _context.BeginTransaction();
        _context.EventReminderDeliveries.Where(d => d.SubscriptionId == existing.Id).Delete();
        _context.EventSubscriptions.Where(s => s.Id == existing.Id).Delete();
        tx.Commit();
    }

    public bool IsSubscribed(Guid eventId, Guid userId) {
        return _context.EventSubscriptions.Any(s => s.EventId == eventId && s.UserId == userId);
    }

    public List<EventSubscription> ListForUser(Guid userId) {
        return _context.EventSubscriptions.Where(s => s.UserId == userId).ToList();
    }

    public List<EventSubscription> ListForEvent(Guid eventId) {
        return _context.EventSubscriptions.Where(s => s.EventId == eventId).ToList();
    }

    /// <summary>Subscriptions whose event is not deleted and neither completed nor archived.</summary>
    public List<EventSubscription> ListActive() {
        return _context.EventSubscriptions
            .InnerJoin(_context.Events, (s, e) => s.EventId == e.Id, (s, e) => new { s, e })
            .Where(x => x.e.DeletedAt == null
                && x.e.Status != EventStatus.Completed
                && x.e.Status != EventStatus.Archived)
            .Select(x => x.s)
            .ToList();
    }

    public HashSet<(Guid SubscriptionId, EventReminderKind Kind, DateTime TargetAt)> ListDeliveries(IReadOnlyList<Guid> subscriptionIds) {
        if (subscriptionIds.Count == 0)
            return [];

        return _context.EventReminderDeliveries
            .Where(d => subscriptionIds.Contains(d.SubscriptionId))
            .Select(d => new { d.SubscriptionId, d.Kind, d.TargetAt })
            .ToList()
            .Select(d => (d.SubscriptionId, d.Kind, d.TargetAt))
            .ToHashSet();
    }

    /// <summary>Records that the reminder went out; false when it was recorded before.</summary>
    public bool TryRecordDelivery(Guid subscriptionId, EventReminderKind kind, DateTime targetAt, DateTime nowUtc) {
        var exists = _context.EventReminderDeliveries
            .Any(d => d.SubscriptionId == subscriptionId && d.Kind == kind && d.TargetAt == targetAt);
        if (exists)
            return false;

        try {
            _context.Insert(new EventReminderDelivery { SubscriptionId = subscriptionId, Kind = kind, TargetAt = targetAt, SentAt = nowUtc });
        } catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry) {
            return false;
        }
        return true;
    }

    private EventSubscription? Get(Guid eventId, Guid userId) {
        return _context.EventSubscriptions.FirstOrDefault(s => s.EventId == eventId && s.UserId == userId);
    }
}
