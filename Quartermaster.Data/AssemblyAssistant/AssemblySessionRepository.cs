using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api.AssemblyAssistant;

namespace Quartermaster.Data.AssemblyAssistant;

public class AssemblySessionRepository {
    private readonly DbContext _context;

    public AssemblySessionRepository(DbContext context) {
        _context = context;
    }

    public AssemblySession Create(Guid eventId, int? meetingId, Guid userId, DateTime nowUtc) {
        var session = new AssemblySession {
            EventId = eventId,
            OpenSlidesMeetingId = meetingId,
            StartedAt = nowUtc,
            StartedByUserId = userId
        };
        _context.Insert(session);
        return session;
    }

    public AssemblySession? Get(Guid id) {
        return _context.AssemblySessions.FirstOrDefault(s => s.Id == id);
    }

    public List<AssemblySession> ListByEvent(Guid eventId) {
        return _context.AssemblySessions
            .Where(s => s.EventId == eventId)
            .OrderByDescending(s => s.StartedAt)
            .ToList();
    }

    public Dictionary<Guid, int> CountEntries(IReadOnlyCollection<Guid> sessionIds) {
        return _context.AssemblyLogEntries
            .Where(e => sessionIds.Contains(e.SessionId))
            .GroupBy(e => e.SessionId)
            .Select(g => new { SessionId = g.Key, Count = g.Count() })
            .ToDictionary(x => x.SessionId, x => x.Count);
    }

    public AssemblySession? GetOpen() {
        return _context.AssemblySessions
            .Where(s => s.EndedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefault();
    }

    public void End(Guid sessionId, DateTime nowUtc) {
        _context.AssemblySessions
            .Where(s => s.Id == sessionId)
            .Set(s => s.EndedAt, nowUtc)
            .Update();
    }

    /// <summary>Upserts by id: a speech is written when it starts and again with its end.</summary>
    /// <summary>Cuts free text to the column sizes, so a long item title or Mumble name cannot fail the whole batch.</summary>
    public void SaveLog(IEnumerable<AssemblyLogEntry> entries) {
        foreach (var entry in entries) {
            entry.ItemTitle = Truncate(entry.ItemTitle, AssemblyLogEntry.ItemTitleMaxLength);
            entry.MumbleName = Truncate(entry.MumbleName, AssemblyLogEntry.MumbleNameMaxLength);
            _context.InsertOrReplace(entry);
        }
    }

    private static string? Truncate(string? value, int maxLength) {
        return value == null || value.Length <= maxLength ? value : value[..maxLength];
    }

    public AssemblyLogEntry? GetOpenSpeech(Guid sessionId) {
        return _context.AssemblyLogEntries
            .Where(e => e.SessionId == sessionId && e.Kind == AssemblyLogKind.Speech && e.EndedAt == null && e.StartedAt != null)
            .OrderByDescending(e => e.StartedAt)
            .FirstOrDefault();
    }

    public List<AssemblyLogEntry> GetLog(Guid sessionId) {
        return _context.AssemblyLogEntries
            .Where(e => e.SessionId == sessionId)
            .OrderBy(e => e.At)
            .ToList();
    }

    public void DeleteLog(Guid sessionId, DateTime nowUtc) {
        using var tx = _context.BeginTransaction();
        _context.AssemblyLogEntries
            .Where(e => e.SessionId == sessionId)
            .Delete();
        _context.AssemblySessions
            .Where(s => s.Id == sessionId)
            .Set(s => s.LogDeletedAt, nowUtc)
            .Update();
        tx.Commit();
    }
}
