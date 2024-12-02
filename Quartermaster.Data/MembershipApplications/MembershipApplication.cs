using System;

namespace Quartermaster.Data.MembershipApplications;

public class MembershipApplication {
    public Guid Id { get; set; }
    public Guid UserId { get; set; }

    public bool HasPriorDeclinedApplication { get; set; }
    public bool IsMemberOfAnotherParty { get; set; }

    public string ApplicationText { get; set; } = "";
}