namespace Quartermaster.Api.DueSelector;

/// <summary>Person data for the due selection form of the logged-in user. <see cref="Locked"/> is true when it comes from a linked member record and may not be changed.</summary>
public class DueSelectionPrefillDTO {
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Email { get; set; } = "";
    public int? MemberNumber { get; set; }
    public bool Locked { get; set; }
}
