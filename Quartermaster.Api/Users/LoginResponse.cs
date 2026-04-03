using System;
using System.Collections.Generic;

namespace Quartermaster.Api.Users;

public class LoginResponse {
    public string Token { get; set; } = "";
    public DateTime? Expires { get; set; }
    public LoginUserInfo User { get; set; } = new();
    public LoginPermissions Permissions { get; set; } = new();
}

public class LoginUserInfo {
    public Guid Id { get; set; }
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string EMail { get; set; } = "";
}

public class LoginPermissions {
    public List<string> Global { get; set; } = new();
    public Dictionary<string, List<string>> Chapters { get; set; } = new();
}
