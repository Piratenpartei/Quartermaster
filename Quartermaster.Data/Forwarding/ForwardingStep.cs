using System;
using LinqToDB.Mapping;
using Quartermaster.Api.Forwarding;

namespace Quartermaster.Data.Forwarding;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ForwardingStep {
    public const string TableName = "ForwardingSteps";
    public const int TicketUrlMaxLength = 600;

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CaseId { get; set; }
    public int Order { get; set; }
    public ForwardingStepKind Kind { get; set; }
    public Guid TargetChapterId { get; set; }
    public ForwardingPurpose Purpose { get; set; }
    public ForwardingStepState State { get; set; }
    public int? TicketId { get; set; }
    public string? TicketUrl { get; set; }
    public int Attempts { get; set; }
    public DateTime? NextAttemptAt { get; set; }
    public string? LastError { get; set; }
    public DateTime UpdatedAt { get; set; }
}
