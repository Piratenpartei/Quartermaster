using System;
using System.Collections.Generic;
using Quartermaster.Api.ApplicantRegister;
using Quartermaster.Api.DueSelector;

namespace Quartermaster.Api.MembershipApplications;

public class MembershipApplicationDetailDTO {
    public Guid Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateOnly DateOfBirth { get; set; }
    public string Citizenship { get; set; } = "";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string AddressStreet { get; set; } = "";
    public string AddressHouseNbr { get; set; } = "";
    public string AddressPostCode { get; set; } = "";
    public string AddressCity { get; set; } = "";
    public string AddressCountry { get; set; } = "";
    public Guid? ChapterId { get; set; }
    public string ChapterName { get; set; } = "";
    public DueSelectionAdminDTO? DueSelection { get; set; }
    public bool ConformityDeclarationAccepted { get; set; }
    public bool HasPriorDeclinedApplication { get; set; }
    public bool IsMemberOfAnotherParty { get; set; }
    public string OtherParties { get; set; } = "";
    public string ApplicationText { get; set; } = "";
    public DateOnly EntryDate { get; set; }
    public DateTimeOffset SubmittedAt { get; set; }
    public ApplicationStatus Status { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    /// <summary>The motion deciding this application; set only when the viewer may open it.</summary>
    public Guid? ViewableMotionId { get; set; }

    /// <summary>The application's due selection when it is a reduction; set only when the viewer may open it.</summary>
    public Guid? ViewableReductionId { get; set; }

    public int? MemberNumber { get; set; }
    public DateTimeOffset? WelcomeSentAt { get; set; }
    public bool HasReducedDueSelection { get; set; }
    public ApprovalMailState ApprovalMailState { get; set; }
    public DateTimeOffset? ApprovalMailSentAt { get; set; }
    public string? FederalTicketUrl { get; set; }
    public bool RequiresFederalApproval { get; set; }
    public Guid? FederalApprovalMotionId { get; set; }

    /// <summary>When and to which chapter the waiting application motion moves next if nobody decides before.</summary>
    public DateTimeOffset? NextTakeOverAt { get; set; }
    public string? NextTakeOverChapterName { get; set; }

    /// <summary>Only filled for holders of the register permission; null for everyone else.</summary>
    public List<ApplicationRegisterHitDTO>? RegisterHits { get; set; }
}
