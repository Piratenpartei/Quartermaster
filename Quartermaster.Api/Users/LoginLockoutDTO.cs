using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Users;

public class LoginLockoutDTO {
    public string IpAddress { get; set; } = "";
    public string UsernameOrEmail { get; set; } = "";
    public int FailedAttempts { get; set; }
    public DateTime LastAttemptAt { get; set; }
    public DateTime LockedUntil { get; set; }
}

public class LoginLockoutListResponse {
    public List<LoginLockoutDTO> Items { get; set; } = new();
}

public class LoginLockoutUnlockRequest {
    public string IpAddress { get; set; } = "";
    public string UsernameOrEmail { get; set; } = "";
}
