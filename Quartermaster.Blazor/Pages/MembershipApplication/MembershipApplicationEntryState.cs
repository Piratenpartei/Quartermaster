using System;
using Quartermaster.Blazor.Abstract;
using Quartermaster.Blazor.Pages.DueSelector;

namespace Quartermaster.Blazor.Pages.MembershipApplication;

public class MembershipApplicationEntryState : EntryStateBase {
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public DateTime? DateOfBirth { get; set; }
    public string Citizenship { get; set; } = "Deutsch";
    public string Email { get; set; } = "";
    public string PhoneNumber { get; set; } = "";

    /// <summary>False until the applicant picked Germany or abroad on the address step.</summary>
    public bool CountrySelected { get; set; }
    public bool IsGermany { get; set; } = true;
    /// <summary>Germany without a municipality from the search: the post code and city are typed in.</summary>
    public bool ManualAddressEntry { get; set; }
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

    public bool IsPersonalDataComplete() {
        if (string.IsNullOrWhiteSpace(FirstName))
            return false;
        if (string.IsNullOrWhiteSpace(LastName))
            return false;
        if (DateOfBirth == null)
            return false;
        if (string.IsNullOrWhiteSpace(Citizenship))
            return false;

        return !string.IsNullOrWhiteSpace(Email);
    }

    public bool IsAddressComplete() {
        if (!CountrySelected)
            return false;
        if (!IsGermany && string.IsNullOrWhiteSpace(AddressCountry))
            return false;
        if (string.IsNullOrWhiteSpace(AddressPostCode))
            return false;
        if (string.IsNullOrWhiteSpace(AddressCity))
            return false;
        if (string.IsNullOrWhiteSpace(AddressStreet))
            return false;

        return !string.IsNullOrWhiteSpace(AddressHouseNbr);
    }

    public bool AreDeclarationsComplete() {
        return ConformityDeclarationAccepted;
    }

    /// <summary>Switching between Germany and abroad starts the address over, so no matched municipality or chapter is carried along.</summary>
    public void SelectCountry(bool isGermany, string germanyName) {
        CountrySelected = true;
        IsGermany = isGermany;
        AddressCountry = isGermany ? germanyName : "";
        ManualAddressEntry = false;
        ClearMunicipality();
        AddressStreet = "";
        AddressHouseNbr = "";
    }

    public void ClearMunicipality() {
        AddressPostCode = "";
        AddressCity = "";
        AddressAdministrativeDivisionId = null;
        ChapterId = null;
        ChapterName = null;
    }
}
