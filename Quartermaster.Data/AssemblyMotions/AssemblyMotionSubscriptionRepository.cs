using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using MySqlConnector;

namespace Quartermaster.Data.AssemblyMotions;

public class AssemblyMotionSubscriptionRepository {
    private readonly DbContext _context;

    public AssemblyMotionSubscriptionRepository(DbContext context) {
        _context = context;
    }

    /// <summary>Subscribes the user; an existing subscription is returned unchanged.</summary>
    public AssemblyMotionSubscription Subscribe(Guid motionId, Guid userId, DateTime nowUtc) {
        var existing = Get(motionId, userId);
        if (existing != null)
            return existing;

        var subscription = new AssemblyMotionSubscription { MotionId = motionId, UserId = userId, CreatedAt = nowUtc };
        try {
            _context.Insert(subscription);
        } catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry) {
            return Get(motionId, userId) ?? throw new InvalidOperationException($"Subscription of {userId} to motion {motionId} vanished after a duplicate key.", ex);
        }
        return subscription;
    }

    public void Unsubscribe(Guid motionId, Guid userId) {
        _context.AssemblyMotionSubscriptions
            .Where(s => s.MotionId == motionId && s.UserId == userId)
            .Delete();
    }

    public bool IsSubscribed(Guid motionId, Guid userId) {
        return _context.AssemblyMotionSubscriptions.Any(s => s.MotionId == motionId && s.UserId == userId);
    }

    public List<AssemblyMotionSubscription> ListForUser(Guid userId) {
        return _context.AssemblyMotionSubscriptions.Where(s => s.UserId == userId).ToList();
    }

    public List<Guid> ListUserIdsForMotion(Guid motionId) {
        return _context.AssemblyMotionSubscriptions
            .Where(s => s.MotionId == motionId)
            .Select(s => s.UserId)
            .ToList();
    }

    private AssemblyMotionSubscription? Get(Guid motionId, Guid userId) {
        return _context.AssemblyMotionSubscriptions.FirstOrDefault(s => s.MotionId == motionId && s.UserId == userId);
    }
}
