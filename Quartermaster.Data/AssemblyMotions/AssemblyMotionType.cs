using System;
using LinqToDB.Mapping;
using Quartermaster.Api;
using Quartermaster.Api.AssemblyMotions;

namespace Quartermaster.Data.AssemblyMotions;

[Table(TableName, IsColumnAttributeRequired = false)]
public class AssemblyMotionType {
    public const string TableName = "AssemblyMotionTypes";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid PortalId { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int SortOrder { get; set; }
    public int MinSubmitters { get; set; }
    public DateTime? Deadline { get; set; }
    public DateTime? CompetingDeadline { get; set; }
    public RequiredMajority RequiredMajority { get; set; }
    public string WikiTemplate { get; set; } = "";
    public int? OpenSlidesCategoryId { get; set; }

    public void ApplyFrom(AssemblyMotionTypeFields fields) {
        Code = fields.Code;
        Name = fields.Name;
        SortOrder = fields.SortOrder;
        MinSubmitters = fields.MinSubmitters;
        Deadline = fields.Deadline?.UtcDateTime;
        CompetingDeadline = fields.CompetingDeadline?.UtcDateTime;
        RequiredMajority = fields.RequiredMajority;
        WikiTemplate = fields.WikiTemplate;
    }

    public AssemblyMotionTypeDTO ToDto() {
        return new() {
            Id = Id,
            Code = Code,
            Name = Name,
            SortOrder = SortOrder,
            MinSubmitters = MinSubmitters,
            Deadline = Deadline.ToDtoUtc(),
            CompetingDeadline = CompetingDeadline.ToDtoUtc(),
            RequiredMajority = RequiredMajority
        };
    }

    public AssemblyMotionTypeSettingsDTO ToSettingsDto(bool hasMotions) {
        return new() {
            Id = Id,
            Code = Code,
            Name = Name,
            SortOrder = SortOrder,
            MinSubmitters = MinSubmitters,
            Deadline = Deadline.ToDtoUtc(),
            CompetingDeadline = CompetingDeadline.ToDtoUtc(),
            RequiredMajority = RequiredMajority,
            WikiTemplate = WikiTemplate,
            HasMotions = hasMotions
        };
    }
}
