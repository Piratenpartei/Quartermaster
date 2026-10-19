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
