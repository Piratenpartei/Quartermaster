using System;

namespace Quartermaster.Api.SupportSignatures;

public class SignatureCampaignSaveRequest {
    /// <summary>The campaign to update; bound from the route, unused on create.</summary>
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string Name { get; set; } = "";
    public int Target { get; set; }
    public int MarginPercent { get; set; } = 20;
    public DateTimeOffset Deadline { get; set; }
    public int OverdueDays { get; set; } = 21;
    public bool VisibleToMembers { get; set; }
    public string? MemberNotice { get; set; }
}

public class SignatureCampaignListRequest {
    public bool Archived { get; set; }
}

public class SignatureCampaignIdRequest {
    public Guid Id { get; set; }
}

public class SignatureCampaignDTO {
    public Guid Id { get; set; }
    public Guid ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string Name { get; set; } = "";
    public int Target { get; set; }
    public int MarginPercent { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public int OverdueDays { get; set; }
    public bool VisibleToMembers { get; set; }
    public string? MemberNotice { get; set; }
    public bool HasForm { get; set; }
    public string? FormFileName { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public SignatureTotalsDTO Totals { get; set; } = new();
    public int OverdueShipmentCount { get; set; }
}

/// <summary>The numbers of a campaign; <see cref="Goal"/> is the target plus the safety margin.</summary>
public class SignatureTotalsDTO {
    public int Received { get; set; }
    public int AlreadyCertified { get; set; }
    public int Waiting { get; set; }
    public int AtOffices { get; set; }
    public int Certified { get; set; }
    public int Rejected { get; set; }
    public int Target { get; set; }
    public int Goal { get; set; }
}
