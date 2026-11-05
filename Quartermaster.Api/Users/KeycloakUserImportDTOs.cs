using System;
using Quartermaster.Api.Accreditation;

namespace Quartermaster.Api.Users;

/// <summary>A Keycloak account as the user import shows it: whether it already has a user, and the chapter its department maps to.</summary>
public class KeycloakUserCandidateDTO {
    public AccreditationAccountDTO Account { get; set; } = new();
    public Guid? ExistingUserId { get; set; }
    public string? ChapterName { get; set; }

    /// <summary>Why the account can't be added, as an i18n key; null when it can.</summary>
    public string? Problem { get; set; }
}

public class KeycloakUserImportRequest {
    public string KeycloakUserId { get; set; } = "";
}

public class KeycloakUserImportedDTO {
    public Guid UserId { get; set; }
}

/// <summary>What refreshing the users from Keycloak changed.</summary>
public class KeycloakUserRefreshDTO {
    public int AccountsRead { get; set; }
    public int KeycloakIdsStored { get; set; }
    public int ChaptersAssigned { get; set; }
    public int VotingRightsChanged { get; set; }
    public int UsernamesSet { get; set; }

    /// <summary>Accounts whose Keycloak username another account here still holds; they keep their old one.</summary>
    public int UsernamesSkipped { get; set; }

    /// <summary>Open accounts with a member number whose member has no Quartermaster user.</summary>
    public int AccountsWithoutUser { get; set; }
}
