using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.MembershipApplications;

[Table(TableName, IsColumnAttributeRequired = false)]
public class MembershipApplication {
    public const string TableName = "MembershipApplications";

    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public bool HasPriorDeclinedApplication { get; set; }
    public bool IsMemberOfAnotherParty { get; set; }

    public string ApplicationText { get; set; } = "";
}