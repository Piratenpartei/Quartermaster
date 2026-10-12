using System;
using System.Collections.Generic;
using System.Linq;
using LinqToDB;
using Quartermaster.Api;
using Quartermaster.Data;
using Quartermaster.Server.Notifications;

namespace Quartermaster.Server.AssemblyMotions.Notifications;

/// <summary>
/// Resolves the members of an <see cref="AssemblyMotionNotificationPayload"/> to their linked users and, when
/// requested, adds the users holding <see cref="PermissionIdentifier.ManageAssemblyMotions"/> on exactly the
/// event's chapter (the permission does not inherit to sub-chapters).
/// </summary>
public class AssemblyMotionRecipientResolver : IRecipientResolver {
    public static IReadOnlyList<string> TriggerIds { get; } = [
        NotificationTriggers.AssemblyMotionCoSubmitterReset,
        NotificationTriggers.AssemblyMotionCollectingReminder,
        NotificationTriggers.AssemblyMotionSubmitted,
        NotificationTriggers.AssemblyMotionLapsed,
        NotificationTriggers.AssemblyMotionChecked,
        NotificationTriggers.AssemblyMotionInquiryAsked,
        NotificationTriggers.AssemblyMotionInquiryAnswered,
        NotificationTriggers.AssemblyMotionEditedByCommission,
        NotificationTriggers.AssemblyMotionCompetitionDeclared,
        NotificationTriggers.AssemblyMotionWithdrawn
    ];

    private readonly DbContext _db;

    public AssemblyMotionRecipientResolver(string triggerId, DbContext db) {
        TriggerId = triggerId;
        _db = db;
    }

    public string TriggerId { get; }

    public IReadOnlyList<NotificationRecipient> Resolve(object payload) {
        if (payload is not AssemblyMotionNotificationPayload typed)
            return Array.Empty<NotificationRecipient>();

        var recipients = ResolveMembers(typed.MemberIds);
        if (typed.IncludeCommission)
            recipients.AddRange(ResolveCommission(typed.EventChapterId));

        return recipients
            .GroupBy(r => r.UserId)
            .Select(g => g.First())
            .ToList();
    }

    private List<NotificationRecipient> ResolveMembers(IReadOnlyList<Guid> memberIds) {
        if (memberIds.Count == 0)
            return [];

        var userIds = _db.Members
            .Where(m => memberIds.Contains(m.Id) && m.UserId != null)
            .Select(m => m.UserId!.Value)
            .Distinct()
            .ToList();
        if (userIds.Count == 0)
            return [];

        return _db.Users
            .Where(u => userIds.Contains(u.Id) && u.DeletedAt == null)
            .Select(u => new { u.Id, u.Email })
            .ToList()
            .Where(u => !string.IsNullOrEmpty(u.Email))
            .Select(u => new NotificationRecipient(u.Id, u.Email!))
            .ToList();
    }

    private List<NotificationRecipient> ResolveCommission(Guid chapterId) {
        var permissionId = _db.Permissions
            .Where(perm => perm.Identifier == PermissionIdentifier.ManageAssemblyMotions)
            .Select(perm => (Guid?)perm.Id)
            .FirstOrDefault();

        var globalGrantedUserIds = permissionId.HasValue
            ? _db.UserGlobalPermissions
                .Where(ugp => ugp.PermissionId == permissionId.Value)
                .Select(ugp => ugp.UserId)
                .Distinct()
                .ToList()
            : new List<Guid>();

        var directGrantedUserIds = permissionId.HasValue
            ? _db.UserChapterPermissions
                .Where(ucp => ucp.PermissionId == permissionId.Value && ucp.ChapterId == chapterId)
                .Select(ucp => ucp.UserId)
                .Distinct()
                .ToList()
            : new List<Guid>();

        var roleGrantedUserIds = _db.UserRoleAssignments
            .Join(_db.RolePermissions, a => a.RoleId, rp => rp.RoleId, (a, rp) => new { a, rp })
            .Where(x => x.rp.PermissionIdentifier == PermissionIdentifier.ManageAssemblyMotions
                && (x.a.ChapterId == null || x.a.ChapterId == chapterId))
            .Select(x => x.a.UserId)
            .Distinct()
            .ToList();

        var userIds = globalGrantedUserIds
            .Union(directGrantedUserIds)
            .Union(roleGrantedUserIds)
            .ToList();
        if (userIds.Count == 0)
            return [];

        return _db.Users
            .Where(u => userIds.Contains(u.Id) && u.DeletedAt == null)
            .Select(u => new { u.Id, u.Email })
            .ToList()
            .Select(u => new NotificationRecipient(u.Id, u.Email ?? ""))
            .ToList();
    }
}
