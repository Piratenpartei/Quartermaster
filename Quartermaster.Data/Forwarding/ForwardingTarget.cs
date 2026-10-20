using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using LinqToDB.Mapping;
using Quartermaster.Api.Forwarding;

namespace Quartermaster.Data.Forwarding;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ForwardingTarget {
    public const string TableName = "ForwardingTargets";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChapterId { get; set; }
    public ForwardingPurpose Purpose { get; set; }
    public int ProjectId { get; set; }
    public int TrackerId { get; set; }
    public int? PriorityId { get; set; }
    public int? AssigneeId { get; set; }

    /// <summary>JSON list of <c>{"FieldId": 5, "Template": "{{ member.Number }}"}</c>.</summary>
    public string CustomFieldsJson { get; set; } = "[]";

    public int? NoteStatusId { get; set; }
    public int? NoteAssigneeId { get; set; }

    public ForwardingTargetDTO ToDto() {
        List<ForwardingCustomFieldDTO>? fields;
        try {
            fields = JsonSerializer.Deserialize<List<ForwardingCustomFieldDTO>>(string.IsNullOrWhiteSpace(CustomFieldsJson) ? "[]" : CustomFieldsJson);
        } catch (JsonException) {
            fields = [];
        }

        return new ForwardingTargetDTO {
            Purpose = Purpose,
            ProjectId = ProjectId,
            TrackerId = TrackerId,
            PriorityId = PriorityId,
            AssigneeId = AssigneeId,
            CustomFields = fields ?? [],
            NoteStatusId = NoteStatusId,
            NoteAssigneeId = NoteAssigneeId
        };
    }

    public static ForwardingTarget FromDto(Guid chapterId, ForwardingTargetDTO dto) {
        return new ForwardingTarget {
            ChapterId = chapterId,
            Purpose = dto.Purpose,
            ProjectId = dto.ProjectId,
            TrackerId = dto.TrackerId,
            PriorityId = dto.PriorityId,
            AssigneeId = dto.AssigneeId,
            CustomFieldsJson = JsonSerializer.Serialize(dto.CustomFields.Select(f => new ForwardingCustomFieldDTO { FieldId = f.FieldId, Template = f.Template.Trim() }).ToList()),
            NoteStatusId = dto.NoteStatusId,
            NoteAssigneeId = dto.NoteAssigneeId
        };
    }
}
