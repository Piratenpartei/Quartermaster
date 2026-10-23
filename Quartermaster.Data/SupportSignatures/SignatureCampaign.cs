using System;
using LinqToDB.Mapping;
using Quartermaster.Api;
using Quartermaster.Api.SupportSignatures;

namespace Quartermaster.Data.SupportSignatures;

/// <summary>One nomination that needs support signatures, e.g. the state list or one direct candidate.</summary>
[Table(TableName, IsColumnAttributeRequired = false)]
public class SignatureCampaign {
    public const string TableName = "SignatureCampaigns";
    public const int NameMaxLength = 200;
    public const int MemberNoticeMaxLength = 2000;
    public const int PostalAddressMaxLength = 500;
    public const int ContactEmailMaxLength = 254;
    public const int FormFileNameMaxLength = 255;
    public const int DefaultMarginPercent = 20;
    public const int DefaultOverdueDays = 21;

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ChapterId { get; set; }
    public string Name { get; set; } = "";
    public int Target { get; set; }
    public int MarginPercent { get; set; } = DefaultMarginPercent;
    public DateTime Deadline { get; set; }
    public int OverdueDays { get; set; } = DefaultOverdueDays;
    public SignatureCampaignVisibility Visibility { get; set; } = SignatureCampaignVisibility.Private;
    public string? MemberNotice { get; set; }
    public string? PostalAddress { get; set; }
    public string? ContactEmail { get; set; }
    public bool HasForm { get; set; }
    public string? FormFileName { get; set; }
    public DateTime? ArchivedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public SignatureCampaignDTO ToDto(string chapterName, SignatureTotalsDTO totals, int overdueShipmentCount) {
        return new() {
            Id = Id,
            ChapterId = ChapterId,
            ChapterName = chapterName,
            Name = Name,
            Target = Target,
            MarginPercent = MarginPercent,
            Deadline = Deadline.ToDtoUtc(),
            OverdueDays = OverdueDays,
            Visibility = Visibility,
            MemberNotice = MemberNotice,
            PostalAddress = PostalAddress,
            ContactEmail = ContactEmail,
            HasForm = HasForm,
            FormFileName = FormFileName,
            ArchivedAt = ArchivedAt.ToDtoUtc(),
            Totals = totals,
            OverdueShipmentCount = overdueShipmentCount
        };
    }
}
