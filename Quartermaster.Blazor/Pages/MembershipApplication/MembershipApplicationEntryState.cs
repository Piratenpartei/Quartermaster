using System;
using Quartermaster.Blazor.Abstract;
using Quartermaster.Blazor.Pages.DueSelector;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public class MembershipApplicationEntryState : EntryStateBase {
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }
    public string Citizenship { get; set; } = "Deutsch";
    public string EMail { get; set; } = "";
    public string PhoneNumber { get; set; } = "";

    public bool IsGermany { get; set; } = true;
    public string AddressCountry { get; set; } = "Deutschland";
    public string AddressStreet { get; set; } = "";
    public string AddressHouseNbr { get; set; } = "";
    public string AddressPostCode { get; set; } = "";
    public string AddressCity { get; set; } = "";
    public Guid? AddressAdministrativeDivisionId { get; set; }

    public Guid? ChapterId { get; set; }
    public string? ChapterName { get; set; }

    public bool ConformityDeclarationAccepted { get; set; }
    public bool HasPriorDeclinedApplication { get; set; }
    public bool IsMemberOfAnotherParty { get; set; }
    public string ApplicationText { get; set; } = "";

    public DateTime EntryDate { get; set; } = DateTime.Today;
}
