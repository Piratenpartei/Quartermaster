using System.Collections.Generic;

namespace Quartermaster.Api.Accreditation;

public class AccreditationAccountDTO {
    public string KeycloakUserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string? Email { get; set; }
    public string MemberNumber { get; set; } = "";

    /// <summary>The <c>ekklesia_department</c> attribute: the state group MemberDataImport placed the account in.</summary>
    public string? Department { get; set; }
    public bool Enabled { get; set; }
    public bool Closed { get; set; }
    public bool Eligible { get; set; }
    public bool Verified { get; set; }
}

/// <summary>A Keycloak account search: an exact member number, a name matched word by word against username, first and last name, or both.</summary>
public class KeycloakAccountQuery {
    public const int MaxResults = 25;

    public string? MemberNumber { get; set; }
    public string? Query { get; set; }
}

public class AccreditationConfigDTO {
    public string UsernameSeparator { get; set; } = ".";
    public bool KeycloakConfigured { get; set; }
}

public class TemporaryAccountProblemDTO {
    /// <summary>Form field the problem belongs to: firstName, lastName, memberNumber, email or username.</summary>
    public string Field { get; set; } = "";
    public string Message { get; set; } = "";
}

public class TemporaryAccountCheckDTO {
    public bool Ok { get; set; }
    public List<TemporaryAccountProblemDTO> Problems { get; set; } = [];

    /// <summary>A free username following the MemberDataImport rule, offered when the entered one is taken.</summary>
    public string? SuggestedUsername { get; set; }
    public List<AccreditationAccountDTO> ExistingAccounts { get; set; } = [];
}

public class TemporaryAccountRequest {
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string MemberNumber { get; set; } = "";
    public string? Username { get; set; }
    public string? Email { get; set; }
    public bool Eligible { get; set; } = true;
}

public class TemporaryAccountCreatedDTO {
    public string KeycloakUserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Warning { get; set; }
}

public class TemporaryAccountConflictDTO {
    public string Message { get; set; } = "";
    public List<AccreditationAccountDTO> ExistingAccounts { get; set; } = [];
}
