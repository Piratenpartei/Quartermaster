namespace Quartermaster.Api.Forwarding;

/// <summary>Template variable <c>application</c> of the forwarding templates. Dates and amounts are preformatted German strings.</summary>
public class ForwardingApplicationModel {
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string DateOfBirth { get; set; } = "";
    public string Citizenship { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string Street { get; set; } = "";
    public string HouseNumber { get; set; } = "";
    public string PostCode { get; set; } = "";
    public string City { get; set; } = "";
    public string ChapterName { get; set; } = "";
    public string EntryDate { get; set; } = "";
    public bool IsMemberOfAnotherParty { get; set; }
    public bool HasPriorDeclinedApplication { get; set; }
    public string ApplicationText { get; set; } = "";
}

public class ForwardingDuesModel {
    public string Valuation { get; set; } = "";
    public string YearlyAmount { get; set; } = "";
    public bool Reduced { get; set; }
    public string ReducedAmount { get; set; } = "";
    public string Justification { get; set; } = "";
    public string ReducedTimeSpan { get; set; } = "";
    public string MonthlyIncomeGroup { get; set; } = "";
    public string YearlyIncome { get; set; } = "";
    public string PaymentSchedule { get; set; } = "";
    public bool IsDirectDeposit { get; set; }
    public string AccountHolder { get; set; } = "";
    public string Iban { get; set; } = "";
}

public class ForwardingMemberModel {
    public string Number { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
}

public class ForwardingSubmissionModel {
    public string SubmittedAt { get; set; } = "";
    public string SubmittedBy { get; set; } = "";
    public bool EnteredByHand { get; set; }
}

public class ForwardingChapterModel {
    /// <summary>The chapter the ticket goes to, or the deciding chapter in a note.</summary>
    public string Name { get; set; } = "";
    public string DecidingName { get; set; } = "";
    public string StateName { get; set; } = "";
}

public class ForwardingCaseModel {
    public string Reference { get; set; } = "";
    public string RelatedReference { get; set; } = "";
    public string Kind { get; set; } = "";
}

public class ForwardingTicketModel {
    public string Id { get; set; } = "";
    public string Url { get; set; } = "";
}

public class ForwardingOutcomeModel {
    public string Result { get; set; } = "";
    public string DecidedBy { get; set; } = "";
    public string Date { get; set; } = "";
    public string MotionUrl { get; set; } = "";
}

public class ForwardingFailureModel {
    public string Error { get; set; } = "";
    public string Purpose { get; set; } = "";
    public string TargetChapter { get; set; } = "";
    public string TicketSubject { get; set; } = "";
    public string TicketDescription { get; set; } = "";
    public string CasesUrl { get; set; } = "";
}
