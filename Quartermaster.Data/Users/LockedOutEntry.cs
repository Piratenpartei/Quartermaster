using System;

namespace Quartermaster.Data.Users;

public class LockedOutEntry {
    public string IpAddress { get; set; } = "";
    public string UsernameOrEmail { get; set; } = "";
    public int FailedAttempts { get; set; }
    public DateTime LastAttemptAt { get; set; }
    public bool HasRecentSuccess { get; set; }
}
