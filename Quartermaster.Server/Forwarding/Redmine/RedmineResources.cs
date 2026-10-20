using System.Collections.Generic;

namespace Quartermaster.Server.Forwarding.Redmine;

public record RedmineIdName(int Id, string Name);

public record RedmineCustomFieldValue(int Id, string Value);

public record RedmineIssueCreate(int ProjectId, int TrackerId, string Subject, string Description, int? PriorityId, int? AssigneeId,
    IReadOnlyList<RedmineCustomFieldValue> CustomFields);

public record RedmineIssueUpdate(string Notes, int? StatusId, int? AssigneeId);
