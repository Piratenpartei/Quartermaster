using System;

namespace Quartermaster.Api.SupportSignatures;

/// <summary>What members see of a campaign: only totals, never regions or offices.</summary>
public class MemberSignatureCampaignDTO {
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string ChapterName { get; set; } = "";
    public int Target { get; set; }
    public int Goal { get; set; }
    public int CertifiedTotal { get; set; }

    /// <summary>Everything received so far except what the offices rejected; certified forms are part of it.</summary>
    public int CollectedTotal { get; set; }
    public DateTimeOffset Deadline { get; set; }
    public bool HasForm { get; set; }
    public string? MemberNotice { get; set; }
    public string? PostalAddress { get; set; }
    public string? ContactEmail { get; set; }

    /// <summary>Whether the caller may manage the campaign in the UU-Assistent; only set on the detail endpoint.</summary>
    public bool CanManage { get; set; }
}
