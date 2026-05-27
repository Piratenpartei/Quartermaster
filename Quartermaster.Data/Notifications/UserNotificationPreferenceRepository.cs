using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;

namespace Quartermaster.Data.Notifications;

public class UserNotificationPreferenceRepository {
    private readonly DbContext _context;

    public UserNotificationPreferenceRepository(DbContext context) {
        _context = context;
    }

    /// <summary>All explicit overrides for the user. Channels/triggers without rows fall back to channel defaults.</summary>
    public List<UserNotificationPreference> GetForUser(Guid userId) {
        return _context.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToList();
    }

    /// <summary>
    /// Returns the user's preference for the given (trigger, channel) pair, or
    /// <paramref name="defaultIfMissing"/> when no explicit override exists.
    /// </summary>
    public bool IsEnabled(Guid userId, string triggerId, string channelId, bool defaultIfMissing) {
        var row = _context.UserNotificationPreferences
            .Where(p => p.UserId == userId && p.TriggerId == triggerId && p.ChannelId == channelId)
            .FirstOrDefault();
        return row?.Enabled ?? defaultIfMissing;
    }

    /// <summary>
    /// Replace all of the user's overrides with the supplied set. Atomic — wipe + insert in a transaction so
    /// a partial failure doesn't leave the user with a mix of old and new preferences.
    /// </summary>
    public void Replace(Guid userId, IEnumerable<UserNotificationPreference> preferences) {
        using var tx = _context.BeginTransaction();
        _context.UserNotificationPreferences.Where(p => p.UserId == userId).Delete();
        foreach (var pref in preferences) {
            _context.Insert(new UserNotificationPreference {
                UserId = userId,
                TriggerId = pref.TriggerId,
                ChannelId = pref.ChannelId,
                Enabled = pref.Enabled
            });
        }
        tx.Commit();
    }
}
