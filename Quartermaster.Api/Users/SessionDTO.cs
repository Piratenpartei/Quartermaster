using System;

namespace Quartermaster.Api.Users;

/// <summary>
/// One entry in the /api/users/sessions list — a single valid login token attributed
/// to the calling user, with the audit columns populated at login time.
/// </summary>
public class SessionDTO {
    public Guid TokenId { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string? IssuedIp { get; set; }
    public string? IssuedUserAgent { get; set; }

    /// <summary>True when this row is the token the current request authenticated with.</summary>
    public bool IsCurrent { get; set; }
}
