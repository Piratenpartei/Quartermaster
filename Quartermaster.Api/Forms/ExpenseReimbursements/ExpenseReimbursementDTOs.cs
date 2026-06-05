using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Forms.ExpenseReimbursements;

public enum ExpenseReimbursementStatusDTO {
    Draft = 0,
    Submitted = 1
}

public enum ExpensePayoutMethodDTO {
    None = 0,
    Cash = 1,
    BankTransfer = 2
}

public class ExpenseReimbursementLineItemDTO {
    public string Description { get; set; } = "";
    public decimal Value { get; set; }
}

/// <summary>One row in the "Meine Formulare" listing.</summary>
public class ExpenseReimbursementListItemDTO {
    public Guid Id { get; set; }
    public ExpenseReimbursementStatusDTO Status { get; set; }
    public string ChapterName { get; set; } = "";
    public decimal Total { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
}

/// <summary>Response shape for GET /api/forms/expense-reimbursement/{id} and the create/copy endpoints.</summary>
public class ExpenseReimbursementResponseDTO {
    public Guid Id { get; set; }
    public ExpenseReimbursementStatusDTO Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; set; }
    public ExpenseReimbursementInputDTO Input { get; set; } = new();

    public ExpenseReimbursementInputDTO ToInput() => Input;
}

/// <summary>Full form payload used by both the anonymous render endpoint and (later) the authenticated draft update endpoint.</summary>
public class ExpenseReimbursementInputDTO {
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int? MemberNumber { get; set; }
    public string Street { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";

    public Guid? ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public string Function { get; set; } = "";
    public DateTime MotionDate { get; set; }

    public decimal DonatedAmount { get; set; }
    public decimal PayoutAmount { get; set; }
    public ExpensePayoutMethodDTO PayoutMethod { get; set; } = ExpensePayoutMethodDTO.None;
    public bool RequestsDonationReceipt { get; set; }
    public string? Iban { get; set; }
    public string SignatureLocation { get; set; } = "";
    public DateTime FormDate { get; set; }

    public List<ExpenseReimbursementLineItemDTO> LineItems { get; set; } = new();
}
