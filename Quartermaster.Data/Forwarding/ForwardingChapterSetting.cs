using System;
using LinqToDB.Mapping;
using Quartermaster.Api.Forwarding;

namespace Quartermaster.Data.Forwarding;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ForwardingChapterSetting {
    public const string TableName = "ForwardingChapterSettings";
    public const int FailureEmailMaxLength = 320;

    [PrimaryKey]
    public Guid ChapterId { get; set; }

    public ForwardingMode? ApplicationMode { get; set; }
    public ForwardingMode? ReductionMode { get; set; }
    public string? FailureEmail { get; set; }
}
