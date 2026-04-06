using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api.Meetings;
using Quartermaster.Data.AuditLog;

namespace Quartermaster.Data.Meetings;

public class MeetingRepository {
    private readonly DbContext _context;
    private readonly AuditLogRepository _auditLog;

    public MeetingRepository(DbContext context, AuditLogRepository auditLog) {
        _context = context;
        _auditLog = auditLog;
    }

    public Meeting? Get(Guid id)
        => _context.Meetings.Where(m => m.Id == id && m.DeletedAt == null).FirstOrDefault();

    public List<Meeting> GetAll()
        => _context.Meetings.Where(m => m.DeletedAt == null).OrderByDescending(m => m.MeetingDate).ToList();

    public (List<Meeting> Items, int TotalCount) List(
        Guid? chapterId, MeetingStatus? status, MeetingVisibility? visibility,
        DateTime? dateFrom, DateTime? dateTo, List<Guid>? restrictToChapterIds,
        int page, int pageSize) {

        var q = _context.Meetings.Where(m => m.DeletedAt == null);
        if (chapterId.HasValue)
            q = q.Where(m => m.ChapterId == chapterId.Value);
        if (status.HasValue)
            q = q.Where(m => m.Status == status.Value);
        if (visibility.HasValue)
            q = q.Where(m => m.Visibility == visibility.Value);
        if (dateFrom.HasValue)
            q = q.Where(m => m.MeetingDate >= dateFrom.Value);
        if (dateTo.HasValue)
            q = q.Where(m => m.MeetingDate <= dateTo.Value);
        if (restrictToChapterIds != null)
            q = q.Where(m => restrictToChapterIds.Contains(m.ChapterId));

        var total = q.Count();
        var items = q.OrderByDescending(m => m.MeetingDate).ThenByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return (items, total);
    }

    public Meeting Create(Meeting meeting) {
        if (meeting.Id == Guid.Empty)
            meeting.Id = Guid.NewGuid();
        meeting.CreatedAt = DateTime.UtcNow;
        _context.Insert(meeting);
        _auditLog.LogCreated("Meeting", meeting.Id);
        return meeting;
    }

    public void Update(Meeting updated) {
        var existing = Get(updated.Id);
        if (existing == null)
            return;

        _context.Meetings.Where(m => m.Id == updated.Id)
            .Set(m => m.Title, updated.Title)
            .Set(m => m.MeetingDate, updated.MeetingDate)
            .Set(m => m.Location, updated.Location)
            .Set(m => m.Description, updated.Description)
            .Set(m => m.Visibility, updated.Visibility)
            .Update();

        LogIfChanged("Title", existing.Title, updated.Title, updated.Id);
        LogIfChanged("MeetingDate", existing.MeetingDate?.ToString("o"), updated.MeetingDate?.ToString("o"), updated.Id);
        LogIfChanged("Location", existing.Location, updated.Location, updated.Id);
        LogIfChanged("Visibility", existing.Visibility.ToString(), updated.Visibility.ToString(), updated.Id);
    }

    public void UpdateStatus(Guid meetingId, MeetingStatus newStatus) {
        var existing = Get(meetingId);
        if (existing == null)
            return;
        var now = DateTime.UtcNow;
        var startedAt = newStatus == MeetingStatus.InProgress && existing.StartedAt == null ? now : existing.StartedAt;
        var completedAt = newStatus == MeetingStatus.Completed && existing.CompletedAt == null ? now : existing.CompletedAt;

        _context.Meetings.Where(m => m.Id == meetingId)
            .Set(m => m.Status, newStatus)
            .Set(m => m.StartedAt, startedAt)
            .Set(m => m.CompletedAt, completedAt)
            .Update();

        _auditLog.LogFieldChange("Meeting", meetingId, "Status", existing.Status.ToString(), newStatus.ToString());
    }

    public void SetArchivedPdfPath(Guid meetingId, string path) {
        _context.Meetings.Where(m => m.Id == meetingId)
            .Set(m => m.ArchivedPdfPath, path).Update();
    }

    public void SoftDelete(Guid meetingId) {
        _context.Meetings.Where(m => m.Id == meetingId)
            .Set(m => m.DeletedAt, DateTime.UtcNow).Update();
        _auditLog.LogSoftDeleted("Meeting", meetingId);
    }

    private void LogIfChanged(string field, string? oldValue, string? newValue, Guid id) {
        if (oldValue != newValue)
            _auditLog.LogFieldChange("Meeting", id, field, oldValue, newValue);
    }
}
