using System;
using LinqToDB.Mapping;

namespace Quartermaster.Data.Forms.ExpenseReimbursements;

[Table(TableName, IsColumnAttributeRequired = false)]
public class ExpenseReimbursement {
    public const string TableName = "ExpenseReimbursements";

    [PrimaryKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Owner of this submission - always set for the authenticated path; anonymous renders never persist a row.</summary>
    public Guid OwnerUserId { get; set; }

    public ExpenseReimbursementStatus Status { get; set; } = ExpenseReimbursementStatus.Draft;

    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int? MemberNumber { get; set; }
    public string Street { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";

    public Guid? ChapterId { get; set; }
    /// <summary>Chapter name captured at submit time so the rendered PDF stays stable even if the chapter is later renamed.</summary>
    public string ChapterNameSnapshot { get; set; } = "";
    /// <summary>Free-text "Funktion" - officer position label or "Beauftragter fuer X".</summary>
    public string Function { get; set; } = "";
    public DateTime MotionDate { get; set; }

    public decimal DonatedAmount { get; set; }
    /// <summary>Combined with <see cref="DonatedAmount"/> must equal the line-item sum.</summary>
    public decimal PayoutAmount { get; set; }
    public ExpensePayoutMethod PayoutMethod { get; set; } = ExpensePayoutMethod.None;
    public bool RequestsDonationReceipt { get; set; }
    /// <summary>IBAN of the receiving account when <see cref="PayoutMethod"/> is <see cref="ExpensePayoutMethod.BankTransfer"/>. BIC is derived from the IBAN at render time and is not stored.</summary>
    public string? Iban { get; set; }
    public string SignatureLocation { get; set; } = "";
    public DateTime FormDate { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
}

public enum ExpenseReimbursementStatus {
    Draft = 0,
    Submitted = 1
}

public enum ExpensePayoutMethod {
    None = 0,
    Cash = 1,
    BankTransfer = 2
}
